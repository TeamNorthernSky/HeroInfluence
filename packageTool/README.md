# packageTool — Unity manifest 동기화

팀 공용 Unity 패키지 목록(`required-packages.json`)을 `Packages/manifest.json`에 반영하는 도구입니다.

**이미 있는 패키지는 건너뛰고**, 없는 패키지·scoped registry만 추가합니다. 기존 버전은 덮어쓰지 않습니다.


---

## 원숭이도 알수있는 사용법

### 1) .bat 파일을 실행한다
### 2) dist 파일 내부의 exe를 실행한다
### 3) 안되는 경우 제작제에게 문의한다


## 새 패키지 추가법

### 1) required-packages.json  파일을 열어서, json 형식에 맞게 manifast에 새롭게 추가된 코드를 붙혀 넣는다 


---

## 폴더 구조

```
packageTool/
  README.md
  sync_packages.py           # 소스
  required-packages.json     # 팀 공용 패키지 정의
  build_sync_packages.bat    # exe 빌드
  dist/
    sync_packages.exe        # 빌드 결과 (Git 포함 여부는 팀 정책)
  build/                     # PyInstaller 임시 (빌드 시 생성, .gitignore 권장)
```

프로젝트 루트의 `sync_packages.spec`은 PyInstaller 설정 파일입니다.

---

## 사전 요건

- Unity 프로젝트 루트에 `Packages/manifest.json` 존재
- 실행: **Python 3.11+** 또는 `dist/sync_packages.exe`
- exe 빌드: `pip install pyinstaller`

---




## 사용법

### 1) Python으로 실행 (권장)

프로젝트 루트에서:

```powershell
cd C:\HeroInfluencetmp
python packageTool\sync_packages.py --dry-run
python packageTool\sync_packages.py
```

### 2) exe로 실행

```powershell
cd C:\HeroInfluencetmp
packageTool\dist\sync_packages.exe --dry-run
packageTool\dist\sync_packages.exe
```

> Unity 프로젝트 **루트**에서 실행하세요. (`packageTool`의 상위 폴더가 프로젝트 루트로 인식됩니다.)

### 3) 프로젝트 경로 직접 지정

```powershell
python packageTool\sync_packages.py --project-root "D:\MyUnityProject"
```

### 옵션

| 옵션 | 설명 |
|------|------|
| `--dry-run` | 변경 내용만 출력, `manifest.json` 수정 안 함 |
| `--project-root PATH` | Unity 프로젝트 루트 (기본: `packageTool` 상위 폴더) |

---

## required-packages.json

`packageTool/required-packages.json`을 편집해 팀 공용 패키지를 관리합니다.

```json
{
  "dependencies": {
    "com.unity.render-pipelines.universal": "14.0.12",
    "com.example.some-package": "1.0.0"
  },
  "scopedRegistries": [
    {
      "name": "npm",
      "url": "https://registry.npmjs.org",
      "scopes": ["com.kyrylokuzyk"]
    }
  ]
}
```

- `dependencies`: manifest에 **없을 때만** 추가
- `scopedRegistries`: (선택) 동일 name이 없을 때만 추가

---

## 실행 시 변경되는 파일

| 동작 | 결과 |
|------|------|
| `--dry-run` | 파일 변경 없음 |
| 실제 실행 + 변경 있음 | `Packages/manifest.json` 갱신 |
| 백업 | `Packages/manifest.json.YYYYMMDD_HHMMSS.bak` 생성 |

Unity 실행 중이면 Package Manager와 충돌할 수 있으니 **Unity 종료 후** 실행을 권장합니다.

---

## exe 빌드

`packageTool/build_sync_packages.bat` 더블클릭 또는:

```powershell
packageTool\build_sync_packages.bat
```

- 출력: `packageTool\dist\sync_packages.exe`
- 임시: `packageTool\build\` (bat 실행 시 매번 삭제 후 재생성)
- 프로젝트 루트에는 `build/`·`dist/`가 **생성되지 않음**

수동 빌드 (프로젝트 루트에서):

```powershell
python -m PyInstaller --distpath packageTool\dist --workpath packageTool\build sync_packages.spec
```

---

## .gitignore 권장

```
packageTool/build/
# packageTool/dist/   ← exe를 Git에 포함할지 팀에서 결정
Packages/manifest.json.*.bak
```

---

## 문제 해결

| 증상 | 확인 |
|------|------|
| manifest not found | 프로젝트 루트에서 실행했는지 확인 |
| required-packages not found | `packageTool/required-packages.json` 존재 확인 |
| 변경 사항 없음 | 이미 manifest에 패키지가 있음 (정상) |
| exe 빌드 후 exe 없음 | `packageTool\dist\` 확인, bat 로그의 ERROR 확인 |
