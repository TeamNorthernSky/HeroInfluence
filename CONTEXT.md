# HeroInfluence 프로젝트 컨텍스트

> 계정을 변경하거나 새 환경에서 작업을 이어받을 때 이 파일을 먼저 읽으세요.
> 마지막 업데이트: 2026-06-23

---

## 프로젝트 기본 정보

| 항목 | 내용 |
|------|------|
| 프로젝트명 | HeroInfluence |
| 팀명 | TeamNorthernSky |
| GitHub | https://github.com/TeamNorthernSky/HeroInfluence.git |
| 엔진 | Unity (URP) |
| 로컬 경로 | `D:\HeroInfluence` |

---

## 브랜치 구조

| 브랜치 | 담당자 / 역할 |
|--------|--------------|
| `main` | 통합 브랜치 — PR을 통해서만 머지 |
| `KJ` | KJ 작업 브랜치 (현재 체크아웃 중) |
| `JC` | JC 작업 브랜치 |
| `DH` | DH 작업 브랜치 |
| `ASB` | ASB 작업 브랜치 |
| `Sub` | 서브 브랜치 |

---

## 폴더 구조 (Assets/)

```
Assets/
├── KJ_Work/          # KJ 담당 작업
│   ├── Generated/
│   └── Materials/
├── JC_Work/          # JC 담당 작업 (UI, 로비, 모달 등)
├── ASB_Work/         # ASB 담당 작업 (전투 시스템)
├── DH_Work/          # DH 담당 작업 (탐사씬, 맵)
├── _ProtoType_Merge/ # 통합 프로토타입 씬 (TitleScene 등)
├── _SharedAsset/     # 공용 에셋
├── Resources/        # 공용 리소스
└── Scenes/           # 공용 씬
```

---

## 현재 상태 (2026-06-23 기준)

### 현재 브랜치: `KJ`
- `origin/KJ`와 동기화된 상태
- **미커밋 변경사항 있음:**
  - `Assets/_ProtoType_Merge/Scenes/TitleScene.unity` (modified)
  - `ProjectSettings/ProjectSettings.asset` (modified)

### 마지막 머지된 PR
- PR #16 (JC 브랜치) — 2026-06-19

---

## 주요 작업 완료 내역 요약

### 전투 시스템 (ASB)
- 광역 공격 치명타 판정 분리 버그 수정
- 전체 공격 시 material → main target material 처리
- 적 선택 시 최적 경로 표시
- 상호작용 셀 반투명 색 추가

### UI / 로비 (JC)
- 훈련실, 연구소 UI 완료
- Turn Order UI 초상화 및 프레임 적용
- 히어로 정보 모달 프로필 이미지 연결
- 점령 팝업 모달 레이아웃 수정
- 엔딩 시퀀스 시스템 모달 팝업 차단
- 치트키 디버그 모드에서만 작동하도록 수정

### 탐사씬 / 맵 (DH)
- 미니맵 제작
- 탐사 씬 재진입 시 데이터 복원
- 안개 다른 씬 퍼짐 방지
- 엑셀 → ScriptableObject → 데이터 사용 파이프라인 완성

### 씬 연결 / 매니저 (KJ / 통합)
- TitleScene 버튼 스프라이트 적용
- 로비 → 탐사 → 전투 → 종료 씬 루프 구현
- 전역 GameManager로 영속 데이터 이관
- 로비 메뉴 해금, 강화 로직 구현

---

## 데이터 파이프라인

```
엑셀 파일
  → ExcelImporter (Editor 툴)
  → ScriptableObject (.asset)
  → DataManager (런타임)
  → BattleCharacter / UnitData 등
```

- `Assets/JC_Work/Scripts_jc/` — UI 및 로비 스크립트
- `Assets/JC_Work/_ASB_Dup/Scripts/Data/` — 데이터 모델, 로더, 매니저

---

## 앞으로 해야 할 작업

- `KJ` 브랜치의 TitleScene.unity, ProjectSettings.asset 변경사항 커밋 또는 확인
- KJ 브랜치 작업 완료 후 main으로 PR 생성

---

## Claude Code 사용 시 참고

- 작업 일지 (날짜별 커밋 정리)는 Notion 6Q 페이지 안 **"HeroInfluence 작업 일지"** 페이지에 정리되어 있음
  - https://app.notion.com/p/3870622156e7813c8f0ffb71e6724ef0
- 이 파일(`CONTEXT.md`)은 프로젝트 루트에 위치
- 메모리 파일은 `C:\Users\user\.claude\projects\D--HeroInfluence\memory\` 에 저장됨
