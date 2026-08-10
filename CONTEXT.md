# HeroInfluence 프로젝트 컨텍스트

> 새 환경 / 새 세션에서 작업을 이어받을 때 먼저 읽는 문서.
> **이 문서를 읽은 에이전트가 할 일**: §0으로 현재 상태를 확인 → §3 운영 기준 경로에서만 작업 → §5 생성 파일 규칙 준수 → §6 완료 기준으로 검증.
> **주의**: 브랜치·미커밋 변경·로컬 경로 같은 "현재 상태" 값은 이 문서에 고정하지 않는다(금방 낡음). 반드시 §0의 명령으로 확인한다.

---

## 0. 작업 시작 시 확인 (매번 실행)

- `git branch --show-current` — 현재 브랜치
- `git status` — 미커밋 변경. **사용자의 기존 변경은 보존**(덮어쓰지 말 것)
- 로컬 경로는 환경마다 다름(예: 현재 `C:\HeroInfluencetmp`). **경로 하드코딩 금지.**

---

## 1. 프로젝트 기본 정보

| 항목 | 내용 |
|------|------|
| 프로젝트명 | HeroInfluence |
| 팀 | TeamNorthernSky |
| GitHub | https://github.com/TeamNorthernSky/HeroInfluence.git |
| 엔진 | Unity (URP) |

---

## 2. 브랜치 구조 (역할)

| 브랜치 | 역할 |
|--------|------|
| `main` | 통합 — PR로만 머지 |
| `KJ` / `JC` / `DH` / `ASB` | 담당자 작업 브랜치 |
| `Sub` | 서브 |

> 현재 체크아웃 브랜치는 §0의 `git branch --show-current`로 확인.

---

## 3. 코드 / 데이터 기준 경로 ⚑ (중요 — 잘못된 곳 수정 방지)

### 운영 기준 (실제 수정 대상)
- 생성 데이터 테이블 / 로더 / 모델: `Assets/_ProtoType_Merge/ASB/Scripts/Data/` (`Generated/`, `Loader/`, `Model/`)
- 데이터 카탈로그·영속: `Assets/_ProtoType_Merge/DH/Scripts/Manager/Data/`, `.../Manager/Persistence/`
- 전투: `Assets/_ProtoType_Merge/ASB/Scripts/Battle/`, 데이터 테이블 에셋: `Assets/_ProtoType_Merge/ASB/Data/Tables/…`

### 생성 코드 (직접 수정 금지 — §5)
- `Assets/_ProtoType_Merge/ASB/Scripts/Data/Generated/*DataTable.cs` — Excel Importer 산출물 (`// Auto Generated. Do not modify.`)

### 참조 / 중복 경로 (수정 대상 아님 — 혼동 주의)
- `Assets/JC_Work/_ASB_Dup/…` — ASB 중복본(있을 경우)
- `.claude/worktrees/…` — git 워크트리 사본. **메인 트리와 별개, 편집 금지**

### 폴더 개요
```
Assets/
├── _ProtoType_Merge/   # 통합 프로토타입 (운영 기준 코드/씬 대부분)
│   ├── ASB/            # 전투 시스템 + 데이터(Generated/Loader/Model)
│   ├── DH/             # 탐사·맵 + 데이터 카탈로그/영속
│   ├── JC/ · KJ/       # UI/로비 · 씬연결
│   └── Scenes/         # 공용 씬 (예: TmpBattleScene)
├── ASB_Work/ · DH_Work/ · JC_Work/ · KJ_Work/  # 담당자 작업 폴더
├── Resources/ · _SharedAsset/                   # 공용
```

---

## 4. 데이터 파이프라인

```
Excel 파일
  → ExcelImporter (Editor 툴)
  → ScriptableObject (.asset)
  → 캐싱 카탈로그 (DHCsvTemplateCatalog, SO 경로)
  → 런타임 (BattleCharactor / WeaponData / SkillData 등)
```
> 현재 런타임은 **캐싱된 SO 데이터만 사용.** CSV 로더(`CSVDataLoad`/`ReloadFromCSV`) 경로는 미사용이며 제거 예정.

---

## 5. 생성 파일(`*DataTable.cs`) 취급 규칙

- 생성 파일을 **직접 삭제·수정하지 않는다** (Excel 재임포트로 재생성됨).
- 태그(`#Type`) 변경으로 필드 타입이 바뀌면(예: int→string), **소비 코드의 타입 불일치를 검색·수정**한다.
- 스키마 변경 후 **컴파일 완료 + Bake/Export 결과**를 확인한다.
- 생성 파일과 대응 `.asset` 변경을 **함께 검토**한다.

---

## 6. 완료(검증) 기준

- Unity 컴파일 오류 **0건** (에디터 스크립트 포함)
- 관련 씬/기능 **실제 실행** 확인
- 변경 파일 목록 정리 + **사용자 기존 변경 보존**
- 생성 데이터가 **실제 타입/값**으로 반영됐는지 확인

---

## 7. 진행 중 작업

- **무기·스킬 키 int→string 구조 변경** (tmpBattleScene 중심)
  → 지시서: `Docs/무기스킬키_String구조변경_tmpBattleScene_구현지시서.md`

---

## 8. 참고

- 과거 작업 일지(날짜별): Notion "HeroInfluence 작업 일지"
  - https://app.notion.com/p/3870622156e7813c8f0ffb71e6724ef0
- 과거 완료 내역은 git 로그(`git log --oneline`)와 위 Notion 참조.
