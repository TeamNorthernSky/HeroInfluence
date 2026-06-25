import argparse
import json
import shutil
import sys
from datetime import datetime
from pathlib import Path


def get_tool_dir() -> Path:
    """packageTool/ (스크립트) 또는 packageTool/dist/ (PyInstaller exe) 기준."""
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent.parent
    return Path(__file__).resolve().parent


def resolve_project_root(tool_dir: Path, explicit: Path | None) -> Path:
    if explicit is not None:
        return explicit.resolve()
    return tool_dir.parent


def resolve_required_packages_path(tool_dir: Path) -> Path:
    local = tool_dir / "required-packages.json"
    if local.exists():
        return local

    meipass = getattr(sys, "_MEIPASS", None)
    if meipass:
        bundled = Path(meipass) / "required-packages.json"
        if bundled.exists():
            return bundled

    legacy = tool_dir.parent / "Config" / "required-packages.json"
    return legacy


def load_json(path: Path) -> dict:
    if not path.exists():
        raise FileNotFoundError(f"File not found: {path}")

    try:
        with path.open("r", encoding="utf-8-sig") as f:
            return json.load(f)
    except json.JSONDecodeError as e:
        raise ValueError(f"Invalid JSON file: {path}\n{e}") from e


def save_json(path: Path, data: dict) -> None:
    with path.open("w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")


def create_backup_path(project_root: Path) -> Path:
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    return project_root / "Packages" / f"manifest.json.{timestamp}.bak"


def add_missing_scoped_registries(manifest: dict, required: dict) -> tuple[list[str], list[str]]:
    required_registries = required.get("scopedRegistries")

    if required_registries is None:
        return [], []

    if not isinstance(required_registries, list):
        raise ValueError("required-packages.json의 scopedRegistries는 배열이어야 합니다.")

    manifest_registries = manifest.setdefault("scopedRegistries", [])

    if not isinstance(manifest_registries, list):
        raise ValueError("Packages/manifest.json의 scopedRegistries가 올바른 배열이 아닙니다.")

    added_registries = []
    warnings = []
    existing_by_name = {}

    for registry in manifest_registries:
        if not isinstance(registry, dict):
            continue

        name = registry.get("name")
        url = registry.get("url")

        if name:
            existing_by_name[name] = url

    for registry in required_registries:
        if not isinstance(registry, dict):
            raise ValueError("required scoped registry 항목은 객체여야 합니다.")

        name = registry.get("name")
        url = registry.get("url")
        scopes = registry.get("scopes")

        if not name or not url or not isinstance(scopes, list):
            raise ValueError("scoped registry에는 name, url, scopes 배열이 필요합니다.")

        if name in existing_by_name:
            existing_url = existing_by_name[name]

            if existing_url != url:
                warnings.append(
                    f"scoped registry '{name}' already exists with different url. "
                    f"existing='{existing_url}', required='{url}'. Skipped."
                )

            continue

        manifest_registries.append(registry)
        existing_by_name[name] = url
        added_registries.append(f"{name} ({url})")

    return added_registries, warnings


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Sync required Unity packages into Packages/manifest.json"
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Show changes without modifying Packages/manifest.json",
    )
    parser.add_argument(
        "--project-root",
        type=Path,
        default=None,
        help="Unity 프로젝트 루트 (미지정 시 packageTool 상위 폴더)",
    )
    args = parser.parse_args()

    tool_dir = get_tool_dir()
    project_root = resolve_project_root(tool_dir, args.project_root)
    manifest_path = project_root / "Packages" / "manifest.json"
    required_path = resolve_required_packages_path(tool_dir)

    print("Unity가 켜져 있다면 먼저 종료하는 것을 권장합니다.")
    print("Tool dir:", tool_dir)
    print("Project root:", project_root)
    print("Reading manifest:", manifest_path)
    print("Reading required packages:", required_path)

    try:
        manifest = load_json(manifest_path)
        required = load_json(required_path)
    except Exception as e:
        print(f"[ERROR] {e}")
        return 1

    manifest_dependencies = manifest.setdefault("dependencies", {})
    required_dependencies = required.get("dependencies")

    if not isinstance(manifest_dependencies, dict):
        print("[ERROR] Packages/manifest.json의 dependencies가 올바른 객체가 아닙니다.")
        return 1

    if not isinstance(required_dependencies, dict):
        print("[ERROR] required-packages.json에 dependencies 객체가 필요합니다.")
        return 1

    added_packages = []
    skipped_packages = []

    for package_name, required_version in required_dependencies.items():
        if package_name not in manifest_dependencies:
            manifest_dependencies[package_name] = required_version
            added_packages.append((package_name, required_version))
        else:
            skipped_packages.append((package_name, manifest_dependencies[package_name]))

    try:
        added_registries, warnings = add_missing_scoped_registries(manifest, required)
    except Exception as e:
        print(f"[ERROR] {e}")
        return 1

    has_changes = bool(added_packages or added_registries)

    if has_changes:
        manifest["dependencies"] = dict(sorted(manifest_dependencies.items()))

    for warning in warnings:
        print(f"[WARN] {warning}")

    if not has_changes:
        print("변경 사항 없음. 모든 required package와 scoped registry가 이미 manifest에 있습니다.")
        return 0

    print("\n적용 예정 변경사항:" if args.dry_run else "\n적용된 변경사항:")

    if added_packages:
        print("\n추가될 패키지:" if args.dry_run else "\n추가된 패키지:")
        for name, version in added_packages:
            print(f"- {name}: {version}")

    if added_registries:
        print("\n추가될 scoped registry:" if args.dry_run else "\n추가된 scoped registry:")
        for registry in added_registries:
            print(f"- {registry}")

    if skipped_packages:
        print("\n이미 있어서 건너뛴 패키지:")
        for name, version in skipped_packages:
            print(f"- {name}: 현재 manifest 버전 {version}")

    if args.dry_run:
        print("\n--dry-run 모드입니다. manifest.json은 수정하지 않았습니다.")
        return 0

    try:
        backup_path = create_backup_path(project_root)
        shutil.copy2(manifest_path, backup_path)
        save_json(manifest_path, manifest)
    except Exception as e:
        print(f"[ERROR] Failed to write manifest: {e}")
        return 1

    print("\nmanifest.json 업데이트 완료.")
    print("백업 생성:", backup_path)
    print("\nUnity를 다시 열면 Package Manager가 변경된 manifest를 기준으로 패키지를 resolve합니다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
