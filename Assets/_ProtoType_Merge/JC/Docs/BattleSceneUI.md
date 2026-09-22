# 전투씬 UI — 현재 구현 구조

최종 갱신: 2026-09-18
대상: Unity 2022.3.62f3 / `TmpBattleScene`  
2026-09-15까지의 단계:  우상단 네 버튼·좌상단 턴 순서·하단 배경 배치 사용자 검수 완료. 하단 좌측 현재 행동 유닛 정보 이관·비플레이 검증 완료. 코어스킬 사용자 작동 확인 완료. 히어로 4슬롯·매 시전 수동 선택 구현 및 비플레이 검증 완료, 사용자 플레이 검수 대기. 빌런 툴팁 출력은 사용자 확인 완료. 텍스트 자동 크기를 보완했으며 보완분은 사용자 검수 대기다. 하단 우측 고정 설명까지 구현·비플레이 검증 완료했으며, 7단계의 사용자 플레이 검수를 기다린다. 후속 4인 결과창(EXP·랭크 포함)과 시작/매 유닛 턴 팝업도 구현·비플레이 검증 완료했으며 사용자 플레이 검수 대기다.

이 문서는 **현재 실제로 사용하는 구조를 설명하는 유지보수 진입점**이다. 변경 이력이나 전체 구현 계획을 대신하지 않는다. 코드·씬 변경이 적용될 때마다 같은 파일의 해당 설명을 갱신한다. 다른 PC에서도 읽을 수 있도록 본문의 `Assets/...` 표기는 프로젝트 루트 기준이며, 클릭 가능한 링크는 현재 JC/Docs 폴더에서 대상 파일까지의 상대 경로를 사용한다.

기획·데이터 불일치, 미결선, 팀 협의 및 최종 정리 사항은 [재확인 사항 및 후속 처리 보고서](BattleSceneUI_FollowUps.md)에서 계속 갱신한다. 이 문서의 실제 구현 설명과 구분한다.


## 2026-09-16 재개분 — 기획 피드백의 현재 구현

사용자 승인으로 건물 색상 편집 작업과 Unity 편집권을 교대했다. 먼저 JC를 main `3fad785f`까지 동기화한 다음 아래 내용을 적용했다. 이 재개분은 2026-09-16 PR #93으로 통합됐다. 이후 9월 17~18일의 턴 연출·공통 스윕·셀 테두리 후속 구현은 아래 해당 절을 따른다. 아래 설명이 이전 단계 기록과 다르면 아래 현재 구조를 우선한다.

현재 4캐릭터 기본·강화 32개 스킬은 실제 시전 및 VFX 부품에 연결되어 있다. 아래 과거 단계의 ‘현재 연결 외 미결선’·‘강화 결선 보류’는 당시 UI 작업 범위 기록이며 현재 실행 제한이 아니다. 실제 연결과 검사 도구는 [스킬 부품](HeroSkillParts.md), [스킬 작업대](HeroSkillWorkbench.md)를 참조한다.

### 하단 스킬 설명의 단위

`SkillButtonController.RefreshDescription`만 `ClassSkillTooltipText.BuildDesc` / `WeaponTooltipText.BuildWeaponSkillDesc`의 `percentageValues: true`를 사용한다. 해당 옵션은 계수 토큰만 `0.6 → 60%`, `1.2 → 120%`로 치환한다. 원문 토큰 뒤에 `%`가 붙어 있어도 중복하지 않는다. IP, 횟수, 턴 수, 원문 고정 숫자는 바꾸지 않는다. 공용 툴팁·연구소·공방은 기본 옵션(false)으로 종전 표현을 유지한다. 원본의 잘못된 변수명과 툴팁용 문구 정리는 후속 사항이다.

### 셀 표시 — 실제 판정은 기존 전투 코드, 색 표시는 GridManager

- `BattleGridManager.useBattleTilePresentation`은 이 씬에서만 켜져 있다. `flowManager`에서 현재 유닛·행동 진행·종료 상태를 읽고, `inputHandler`에서 기존 직접 선택 방식을 읽는다. 턴 진행이나 피해 계산을 변경하지 않는다.
- `TargetingVisualController`가 기존에 계산한 선택 가능 대상 목록을 `SetSelectableUnits` / `AddSelectableHostages`로 전달한다. `ClearAll`은 이 선택 표시도 해제한다.
- `BattleGridManager`가 현재 턴·직접 선택·호버 미리보기 상태를 따로 보관하고 기존 12개 `GridCell/Plane`의 재질을 갱신한다. 표시 순서는 **빨강 확정 미리보기 → 선택 중 보라/검정 → 노랑 현재 턴 → 투명**이다. 자기 대상도 노랑→보라→빨강→다른 대상 호버 시 보라→취소 시 노랑으로 복원된다. 적 턴도 노란 현재 턴 셀을 사용하며 결과 상태는 모두 지운다.
- `SkillAreaPreviewHelper`는 `TargetAroundRandom` 계열의 랜덤/조건부 추가 후보를 미리보기 목록에서 제외한다. 직접 선택 가능한 대상의 보라 표시는 유지한다. 확정 범위는 전부 빨강이며 전체 공격은 빈 셀까지 해당 진영 6칸을 포함한다. 실행 핸들러·랜덤 추첨·피해 판정은 그대로다.
- 프로젝트에 이미 있는 `Assets/_Ui_Sprites/BattleScene/Tile/UI_tile_{yellow,purple,red,black}.png`를 사용한다. SVN의 같은 파일과 내용이 같아 복사하지 않았다. 기존 셀의 Mesh/Collider/Transform은 유지한다.
- 전투 전용 재질은 `Assets/_ProtoType_Merge/JC/BattleUI/Materials/BattleTile_*.mat` 5개다. URP Unlit 투명 재질이며 그림자를 만들지 않는다. 공용 기존 재질은 수정하지 않았다. 다른 씬은 표시 옵션을 켜지 않는 한 기존 경로를 유지한다.

### 스킬 버튼 효과 — 공통 설정과 씬의 5개 버튼

각 버튼의 `JC/Scripts/UI/Battle/BattleSkillButtonVisual.cs`는 스킬 선택·시전 상태를 소유하지 않고 `SkillButtonController.LateUpdate`가 전달한 사용 가능/선택 상태를 그린다. 2026-09-17부터 시각 설정은 `JC_BattleUI_VFX/SkillBTN_SweepGlow`의 `BattleSkillSweepSettings`에서 함께 조절한다.

- 대상: `BottomPanel/HeroSkillPanel`의 `Button_Skill`, `HeroSkillSlot_2~4`, `BottomPanel/CoreSkillPanel/Button_Skill_2`.
- 각 버튼 아래 `AvailableSkillEffect`와 `SelectedSkillFrame` Image를 씬에 배치했다. RectTransform은 버튼 크기를 따르며 Scale은 1, Raycast Target은 꺼져 있다. 코어는 투명한 클릭판 대신 `SkillIcon`의 코어 모양을 사용한다.
- 사용 가능한 버튼에는 출전 버튼의 `UI/HoverGlowSweep` 셰이더와 기존 `UIGleamSweep.png`를 재사용한다. 선택 테두리는 `UI/HoverOutlineSelected` 셰이더다. 전투 전용 재질 2개를 만들고 실행 중 버튼마다 복제하여 다른 버튼/씬의 설정에 영향을 주지 않는다.
- 아군 행동 대기 상태, 습득/버튼 사용 가능, IP 충족, 행동 허용 조건이 맞을 때만 강조한다. 적 턴·공격 실행·턴 팝업·흐름 잠금·자동 전투·일시정지에는 강조하지 않는다. 선택 테두리는 호버와 무관하게 유지한다.
- 인스펙터의 **SkillBTN_SweepGlow → Battle Skill Sweep Settings**에서 출전 버튼 셰이더의 광선 색·외측/내측 폭·강도·밝기 경계, 스윕 텍스처·색·기울기·폭·이동 범위·지연/지속/반복 시간, 블룸 색·혼합·임계·반경·강도·압축을 조절한다. 선택 테두리 색·폭·강도도 여기서 조절한다. 실제 스윕은 실행 중 시간으로 갱신한다. 자식 Image의 스프라이트·RectTransform도 직접 수정할 수 있다. 공통 설정이 미연결/비활성이면 기존 버튼별 설정을 사용한다.
- 기존 ToggleButton의 선택 스프라이트는 유지한다. 옛 `SelectedSkillHighlight`는 해당 스킬 버튼에서 비활성 보관하고, `ButtonEffectActiveToggle.moduleEnabled`를 꺼서 옛 `BaseHoverOverlay`/`SelectedOverlay`와 새 효과의 중복을 막았다. 코어 버튼의 기존 효과 컨트롤러/오디오 경로는 보존했다. 우상단 자동·배속의 효과는 이번 변경 대상이 아니다.

### 턴 화살표와 카메라

- `BattleUIManager.showTurnArrow=false`, 기존 TurnArrow 오브젝트 비활성. 매 프레임 Follow로 다시 표시되지 않도록 플래그를 읽고 Follow(null)로 해제한다. 파일·오브젝트는 삭제하지 않았다.
- `Main Camera` Transform: Position **(8,10,0)**, Rotation **(55,-90,0)**. Camera의 Field of View는 기존 **60** 유지(기획서 별도 FOV 값 없음). `PlayerPlace.z=-6`, `EnemyPlace.z=6`, 기존 높이·Scale 유지.
- 카메라는 이 씬에 직접 배치되어 있으며 별도 구도 강제 스크립트가 없다. Hierarchy의 **Main Camera → Transform / Camera**에서 위치·각도·FOV·클리핑 등 기본 속성을 직접 조절한다. 카메라 제어 스크립트는 추가하지 않았다.

### 검증 범위와 이전 문서의 경계

Unity 컴파일, 비플레이 상태 전환 검증, 실제 셰이더/스프라이트 렌더 확인을 수행했다. 기존 RectTransform 수치 및 기존 오브젝트는 모두 보존했고 카메라·양 진영 배치의 Transform 3개만 의도대로 변경했다. 플레이모드에는 진입하지 않았다. 실제 캐릭터가 배치된 전투의 구도, 효과 강도/가독성은 사용자 검수 대상이다.

이 작업 전 공유 작업폴더에는 ASB 등의 스킬 실행 관련 수정이 이미 존재했다. 현재 `SkillButtonController`는 `GetCurrentClassSkills`로 4슬롯을 조회하며 습득한 스킬을 선택할 수 있다. 아래 초기 단계의 '기본 1개 외 연결 대기' 설명은 당시 기록으로, 현재 코드의 제한으로 적용하면 안 된다. 새 실행 코드 자체의 기능 검증은 이번 UI 피드백 작업에 포함하지 않았다. 기존 `SkillActivationRules`의 열/진영 클릭 규칙도 그대로 보존하고 셀 표시를 그 규칙에 맞췄다.

## 1. 먼저 확인할 것

- 실제 대상 씬: [TmpBattleScene.unity](../../../../Assets/_ProtoType_Merge/Scenes/TmpBattleScene.unity).
- 씬의 `BattleSceneCanvas`가 전투 UI의 주요 부모다. `TopRightButtons`와 `TurnOrderPanel`은 개편 배치다. `BottomPanel`에는 배경과 좌측 정보 표시가 구현되었다. 코어스킬 영역에는 기존 무기스킬 버튼을 이관했다. 히어로스킬 영역에는 씬에 배치한 4버튼을 사용한다. 설명 영역에는 제목·본문·효과/거리/대상 태그가 연결되어 있다.
- `Assets/Resources/UI_Prefab/Battle_Scene/BattleSceneCanvas.prefab`은 현재 씬 Canvas의 원본이 아니다. 현재 씬을 수정하려고 이 프리팹부터 편집하면 실제 화면과 다른 대상을 수정할 수 있다.
- 우상단 버튼은 사용자가 플레이모드에서 정상 동작 및 요구 조건 충족을 확인했다. 호버 효과와 자동·배속 켜짐 표시의 시각적 품질 향상은 후속 작업이다.
- 이 문서에 있는 향후 계획을 이미 구현된 기능으로 취급하지 않는다. **4슬롯 표시·매 시전 수동 선택·하단 고정 설명은 구현되었다. 미결선 스킬의 추가 실행은 구현 범위 밖이다.**
- 실행 중 만들어지는 오브젝트와 DDOL로 옮겨지는 UI가 있으므로, 편집 모드 Hierarchy만으로 실행 중 전체 구조를 판단하지 않는다.

## 2. 씬의 주요 오브젝트와 컴포넌트

아래는 관련 부분만 추린 실제 구조다. 이름 뒤 대괄호는 주요 컴포넌트이며, 생략된 배경·유닛·전투 관리 오브젝트도 존재한다.

```text
TmpBattleScene
├─ JC_BattleUI_VFX                          ← 전투 UI 효과 설정, 모두 활성
│  ├─ TurnNotify [BattleTurnBanner]          ← 실제 배너는 Canvas에 유지
│  ├─ SkillBTN_SweepGlow [BattleSkillSweepSettings]
│  └─ CellBox [BattleCellVisualSettings]     ← 셀 투명도·파란 테두리 스윕 글로우
├─ BattleSceneManager
│  ├─ [BattleSceneManager / BattleFlowManager / BattleManager]
│  ├─ [AutoBattleController]
│  └─ [SkillButtonController]                 ← 히어로 4슬롯·코어 선택 표시와 우측 고정 설명 제어
├─ InputHandler [InputHandler]
├─ UIManager [BattleUIManager]                ← 선배치 결과창 호출, 턴 화살표는 현재 씬에서 꺼짐
├─ BattleSceneCanvas [BattleEnemyInfoTooltip]
│  ├─ EnemyInfoTooltip [Image / CanvasGroup, 초기 비활성]
│  │  ├─ Name / Rank / Divider
│  │  ├─ PortraitMask / Portrait
│  │  ├─ HpIcon / HpLabel / HpValue / HpBar → Fill
│  │  ├─ AttackIcon / AttackLabel / AttackValue / DefenseIcon / DefenseLabel / DefenseValue
│  │  ├─ SkillHeader / NoSkills
│  │  └─ SkillSlot_1 ~ SkillSlot_5 [Image] → Icon / Name / Description
│  ├─ BottomPanel                            ← 하단 배경·좌측 정보 구현
│  │  ├─ Background [Image]
│  │  ├─ ActorInfoPanel [HeroInfoPanel]       ← 현재 유일한 활성 정보 컨트롤러
│  │  │  ├─ PortraitArea
│  │  │  │  └─ Portrait [Image]              ← 씬 선배치
│  │  │  └─ StatsArea
│  │  │     ├─ ActorName / Rank [TMP]
│  │  │     ├─ HpLabel / HpValue [TMP]
│  │  │     ├─ HpBar [Image]
│  │  │     │  └─ Fill [Image: Filled]
│  │  │     └─ IpIcon [Image] / IpValue [TMP]
│  │  ├─ CoreSkillPanel
│  │  │  ├─ Button_Skill_2 [Button / ToggleButton / SkillButtonTooltip]
│  │  │  │  ├─ SkillIcon [Image / SkillButtonIcon: 꺼짐]
│  │  │  │  ├─ SkillButtonFrame [비활성 보관]
│  │  │  │  ├─ BaseHoverOverlay
│  │  │  │  ├─ SelectedOverlay [비활성 보관]
│  │  │  ├─ AvailableSkillEffect [Image]
│  │  │  └─ SelectedSkillFrame [Image]
│  │  │  └─ Title [TMP: 코어 스킬]
│  │  ├─ HeroSkillPanel
│  │  │  ├─ Button_Skill                   ← 첫 슬롯, 기존 버튼 재사용
│  │  │  └─ HeroSkillSlot_2 ~ 4            ← 씬에 추가한 3버튼
│  │  │     ├─ [Button / ToggleButton / SkillButtonTooltip]
│  │  │     ├─ SkillIcon [Image] / SkillName [TMP]
│  │  │     ├─ LockedOverlay               ← 재사용 잠금 프리팹 인스턴스
│  │  │     └─ UnconnectedLabel [TMP]      ← 습득했지만 미결선이면 표시
│  │  └─ SkillDescriptionPanel
│  │     ├─ Title / Body [TMP]
│  │     └─ EffectTag / RangeTag / TargetTag [Image, 안내 시 비활성]
│  │        └─ Label [TMP]
│  ├─ TopRightButtons                        ← 개편 완료, 직접 배치
│  │  ├─ MenuButton [Button / BattleMenuButton]
│  │  ├─ Button_Auto [Button / ToggleButton / AutoBattleToggleButton]
│  │  │  ├─ BaseHoverOverlay
│  │  │  └─ ActiveState [SelectedSkillHighlight]
│  │  │     └─ ActiveFrame                   ← SelectionFrame 프리팹 인스턴스
│  │  ├─ Button_Speed [Button / ToggleButton / BattleSpeedToggleButton]
│  │  │  ├─ BaseHoverOverlay
│  │  │  └─ ActiveState [SelectedSkillHighlight]
│  │  │     └─ ActiveFrame
│  │  └─ RunButton [Button / RunButton]
│  │     └─ BaseHoverOverlay
│  ├─ TurnOrderPanel [Image / TurnOrderUI]    ← 좌상단 직접 배치
│  │  ├─ SlotParent [HorizontalLayoutGroup: 꺼짐]
│  │     └─ TurnSlot_1 ~ TurnSlot_5 [TurnSlotUI] ← 씬 프리팹 인스턴스 5개
│  │        ├─ Frame [RawImage]              ← 기존 아군/적 프레임
│  │        └─ PortraitMask [RawImage / Mask]
│  │           └─ Portrait [Image]
│  │  ├─ CurrentTurn [Image]                 ← 첫 위치 위에 고정된 표시
│  │  │  └─ Label [TextMeshProUGUI]
│  │  └─ LegacyCurrentTurnMarkers [비활성]   ← 이전 표시 4개 보관
│  ├─ HeroInfoWindow [GameObject: 비활성]    ← 기존 표시 원형 보관
│  │  ├─ Portrait
│  │  │  └─ (이전 초상화 생성 부모, 현재 미사용)
│  │  └─ HeroInfo [HeroInfoPanel: 꺼짐]
│  │     ├─ HeroName
│  │     └─ HPCanvas Variant                 ← 기존 HP·IP 표시
│  ├─ Rank [GameObject: 비활성]             ← 기존 랭크 원형 보관
│  │  └─ RankText
│  ├─ ButtonController                      ← 버튼 이관 후 빈 부모, 정리 후보
│  ├─ ResultPanel [BattleResultPanel / BattleResultView]  ← 초기 비활성, 선배치 결과창
│  │  ├─ Dim
│  │  ├─ ResultContent
│  │  │  ├─ Background / ResultTitle
│  │  │  ├─ HeroSlots / Slot_1 ... Slot_4 [HeroInfoResult]
│  │  │  │  └─ PortraitFrame/Portrait, Rank, Name, ExpLabel/GainedExp,
│  │  │  │     ExpEmpty/ExpFill, ExpProgress, IpLabel/IpBox/IpIcon/IpBeforeAfter
│  │  │  └─ Accept                           ← 이름 기반 공용 Enter 처리 유지
│  │  └─ SkillSelectionHost                  ← 기존 순차 획득창 생성 부모
│  └─ BattleNotices                         ← 표시 오브젝트의 부모
│     ├─ InputBlocker                       ← 투명 화면 입력 차단, 초기 비활성
│     ├─ BattleStartBanner/Label            ← 중앙, 초기 비활성
│     └─ TurnBanner [Image / CanvasGroup]    ← 화면 정중앙, 초기 비활성
│        ├─ MotionBlur [Image]              ← 선배치, 이동 중 표시
│        ├─ Streak_01 ~ Streak_12 [Image]    ← 선배치, 기본 6개 사용
│        └─ Label [TMP]
└─ SkillTooltip                             ← 공용 프리팹 인스턴스
   └─ (실행 시 전역 PersistentTooltipCanvas로 이동)
```

옛 `HeroInfoWindow`는 GameObject 전체가 비활성이다. 그 루트의 기존 HeroInfoPanel 컴포넌트 설정은 켜짐으로 보존되어 있고, 자식 `HeroInfo`의 중복 컴포넌트는 원래부터 꺼져 있다. 루트를 다시 켜면 예전 정보 표시가 함께 갱신되므로 비교 용도 외에 재활성화하지 않는다.

## 3. 우상단 네 버튼 — 현재 완성된 부분

### 배치와 리소스

`TopRightButtons`는 Canvas 우상단 기준이다. 자동 배치 컴포넌트나 런타임 위치 조정 코드는 없다.

| 항목 | 현재 값 |
|---|---|
| 묶음 Anchor Min / Max, Pivot | `(1, 1)` |
| 묶음 Anchored Position | `(-56, -56)` |
| 묶음 크기 | `160 × 299` |
| 버튼 크기 | 각 `160 × 65` |
| 버튼 위에서부터 Y 위치 | `0`, `-78`, `-156`, `-234` |
| 버튼 사이 간격 | `13` |
| 묶음·버튼·자식 RectTransform Scale | `(1, 1, 1)` |

수치는 Canvas 기준 단위다. 화면 배율은 기존 CanvasScaler가 결정한다. 묶음을 이동하려면 `TopRightButtons`, 개별 버튼을 이동하거나 크기를 바꾸려면 각 버튼의 RectTransform을 조절한다. Scale로 크기를 맞추지 않는다.

사용 이미지는 이미 프로젝트에 있는 `Assets/_Ui_Sprites/BattleScene/`의 다음 네 Sprite다. 이미지에 아이콘과 글자가 함께 들어 있으며 별도 TMP 라벨은 없다.

| 버튼 | 이미지 | 기존 오브젝트 여부 |
|---|---|---|
| 메뉴 | `UI_box_button_menu.png` | 신설 |
| 자동 | `UI_box_button_auto.png` | 기존 `Button_Auto` 재사용 |
| 배속 | `UI_box_button_speed.png` | 기존 `Button_Speed` 재사용 |
| 도주 | `UI_box_button_escape.png` | 기존 `RunButton` 재사용 |

세 버튼은 기존 오브젝트를 이동한 것이므로 별도의 비활성 옛 복제본은 없다. `ButtonController`는 현재 스킬 버튼이 사용하므로 유지한다.

### 메뉴 호출과 일시정지

새 [BattleMenuButton.cs](../../../../Assets/_ProtoType_Merge/JC/Scripts/UI/Battle/BattleMenuButton.cs)는 같은 오브젝트의 Button에 클릭 listener를 등록하고, 비활성화 시 해제한다. 클릭하면 공용 `SystemMenuController`를 찾아 캐시하고 `OpenMenu()`를 호출한다. 메뉴 인스턴스가 사라졌으면 다음 클릭에서 다시 찾는다.

```text
MenuButton 클릭
→ BattleMenuButton
→ SystemMenuController.OpenMenu()
→ 공용 메뉴의 Modal 오브젝트 활성화
→ Modal.OnEnable()
→ ModalPauseGate.Refresh()
→ Time.timeScale = 0
```

공용 메뉴는 [CommonUIManager.prefab](../../../../Assets/Resources/CommonUIManager.prefab) 안에 배치되어 있다. `CommonUIManager`는 Resources에서 자동 생성되고 DDOL로 유지된다. 이 인스턴스에서 메뉴의 `Modal.pausesGame`을 **1로 덮어써 사용**한다.

주의: [SystemMenuModal.prefab 원본](../../../../Assets/_UI_Prefabs_Runtime/UI_Common/SystemMenu/SystemMenuModal.prefab)의 `pausesGame` 값만 보면 0이다. 실제 공용 호스트의 override까지 확인해야 메뉴의 정지 동작을 알 수 있다. 메뉴 원본을 별도로 생성하는 것은 현재 버튼의 사용 방식이 아니다.

메뉴 버튼은 **메뉴 열기+일시정지**만 요구한다. ESC의 대화 처리·다른 모달 닫기 등을 대신 호출하지 않는다. 기존 `SystemMenuController`, `Modal`, `ModalPauseGate` 코드는 개편으로 수정하지 않았다. 메뉴 닫기/재개도 공용 메뉴가 담당한다.

### 자동·배속·도주 실행

```text
자동: Button → ToggleButton.OnValueChanged
      → AutoBattleToggleButton
      → BattleRuntimeSettings.SetAutoBattle / AutoBattleController.IsAutoBattle

배속: Button → ToggleButton.OnValueChanged
      → BattleSpeedToggleButton
      → BattleRuntimeSettings.SetBattleSpeed / BattleManager.ChangeBattleSpeed

도주: Button → RunButton
      → BattleFlowManager.RequestFlee()
      → 기존 전투 종료 처리
```

- 자동·배속은 `OnEnable`에서 `BattleRuntimeSettings`의 현재 값을 읽어 토글을 맞춘다. UI를 켤 때 설정을 임의로 초기화하지 않는다.
- 배속 버튼의 현재 설정은 기본 `1`, 빠르게 `2`다. 값은 기존 `BattleSpeedToggleButton`의 Inspector 필드다.
- `BattleRuntimeSettings`는 실행 중 사용하는 정적 상태다. 영구 저장 기능이 있다는 뜻은 아니다.
- 도주 버튼의 기존 플레이어 턴/전투 종료 상태 갱신과 `RequestFlee()`의 허용 검사를 사용한다. 도주는 현재 기존 흐름에서 패배 결과로 종료한다.
- 세 기능 스크립트와 `ToggleButton` 소스는 수정하지 않았다. 각 버튼의 씬 참조를 명시적으로 연결해 둔다.

### 호버와 켜짐 표시

- 기존 세 버튼은 `ButtonEffectController`, `ButtonEffectActiveToggle`, `HoverOverlayTint` 및 `BaseHoverOverlay`를 사용한다. 버튼 자체의 ColorTint도 눌림/비활성 상태를 표시한다.
- 메뉴는 일반 Button의 ColorTint를 사용한다. 세 기존 버튼과 동일한 별도 호버 오버레이를 추가한 상태는 아니다.
- 새 자동·배속 이미지에는 별도의 활성 버전이 없어 `ToggleButton.normalSprite`와 `activeSprite`가 같은 Sprite를 참조한다. **이미지 교체만으로 토글 켜짐 상태를 판단하면 안 된다.**
- 켜짐 상태는 `ActiveState`의 기존 [SelectedSkillHighlight.cs](../../../../Assets/_ProtoType_Merge/JC/Scripts/UI/SelectedSkillHighlight.cs)가 부모의 `ToggleButton.IsOn`을 읽고 `ActiveFrame`을 켜거나 끈다. 스킬 선택 로직을 호출하는 컴포넌트가 아니라 표시용이다.
- `SelectedSkillHighlight`를 클릭 대상 버튼이 아닌 별도 자식에 두어, 부모 버튼 호버가 켜짐 테두리를 숨기지 않게 했다. 자동·배속이 초기 설정을 `notify=false`로 적용해도 이 컴포넌트가 상태를 읽어 표시한다.
- `ActiveFrame`은 [SelectionFrame.prefab](../../../../Assets/_UI_Prefabs_Runtime/UI_ReusableVisuals/SelectionFrame.prefab)의 씬 인스턴스다. 연두색 테두리로 조정했으며 Image의 Raycast Target은 꺼져 있다. 원본 프리팹은 수정하지 않았다.
- 사용자는 현재 요구 조건 충족을 확인했다. **호버 효과·배속 인디케이터의 품질 개선은 이후 별도 작업**이며 현재 완료를 막는 미해결 항목이 아니다.

## 4. 좌상단 턴 순서 — 씬 배치 5슬롯

### 연결과 갱신

[TurnOrderUI.cs](../../../../Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/TurnOrderUI.cs)의 `sceneSlots`에 `TurnSlot_1`부터 `TurnSlot_5`까지 순서대로 연결되어 있다. 각 슬롯은 기존 [TurnSlotUI.prefab](../../../../Assets/_ProtoType_Merge/ASB/Prefabs/UI/TurnSlotUI.prefab)의 **씬 인스턴스**다. 원본 프리팹과 `TurnSlotUI.cs`는 수정하지 않았다. 이 씬에서는 실행 중 슬롯을 생성하지 않는다.

```text
TurnOrderUI.OnEnable / BattleFlowManager.OnTurnStarted
→ BattleFlowManager.GetPredictedTurnOrder()
→ sceneSlots 순서대로 TurnSlotUI.Setup(unit, i == 0, portrait)
→ 진영 프레임·초상화 갱신
```

- 예측 순서 계산은 기존 `BattleFlowManager`가 담당한다. 현재 유닛 → 남은 행동 큐 → 이미 행동한 생존 유닛의 다음 라운드 예측 순서다. UI 개편에서 이 계산을 바꾸지 않았다.
- 유닛 수가 다섯보다 적으면 기존처럼 순서를 반복해서 다섯 칸을 채운다. 표시할 유닛이 없으면 슬롯을 모두 숨긴다.
- `maxDisplayCount` 기본값은 5다. 씬 슬롯 수보다 많이 지정해도 추가 생성하지 않는다. 0 이하는 모두 숨기며, 표시 개수 밖 슬롯도 숨긴다.
- 아군은 `Sprites.Portrait.Hero`, 적은 `Sprites.Portrait.Enemy`로 초상화를 조회한다. 기존 `TurnSlotUI.Setup()`이 진영에 따라 파랑/빨강 프레임을 고른다.
- 현재 행동 유닛은 항상 첫 칸에 있으므로 `CurrentTurn/Label`은 **TurnOrderPanel 직속의 고정 표시**다. 각 슬롯의 `highlightFrame`은 모두 비워 두었다. 슬롯 갱신이 라벨을 켜고 끄거나 이동시키지 않으며, 패널이 켜져 있는 동안 표시한다. 별도 표시 스크립트는 없다.
- 이전 슬롯 2~5의 표시는 패널 아래 비활성 `LegacyCurrentTurnMarkers`에 `CurrentTurn_Legacy_2~5`로 보존했다. 실행 연결은 없으며 최종 정리 시 삭제 후보이다.
- 전투 종료 시 기존 `OnBattleEnded` 처리로 `TurnOrderPanel` 전체를 숨긴다.
- 적 초상화 호버는 `TurnSlotUI.DisplayedUnit`을 통해 전투 전용 `BattleEnemyInfoTooltip`에 연결되어 있다. 아군 초상화 호버는 보류다. 상세 표시·숨김 조건은 빌런 호버 정보창 절을 참조한다.

### 에디터에서 조절하는 위치

| 대상 | 현재 RectTransform 설정 |
|---|---|
| TurnOrderPanel | 좌상단 Anchor/Pivot `(0,1)`, 위치 `(41.2,-32)`, 크기 `600×178.2` |
| SlotParent | 패널 전체 Stretch, 여백 0 |
| TurnSlot_1~5 | 좌상단 Anchor, 중앙 Pivot, 크기 `85×85`, X=`101.6,201.6,301.6,401.6,501.6`, Y=`-94.6` |
| Frame / PortraitMask | 중앙 기준 `85×85` |
| Portrait | 중앙 기준 `74.375×74.375`, 위치 `(0,5.3125)` |
| CurrentTurn | 패널 좌상단 Anchor·중앙 Pivot, `66.7×19.5`, 위치 `(101.6,-43.6)` |
| CurrentTurn/Label | 중앙 기준 `82×24`, 위치 `(0,0)`, 글자 크기 `15` |

관련 RectTransform의 Scale은 모두 `(1,1,1)`이다. 런타임 코드는 위치·크기를 덮어쓰지 않는다. `SlotParent`의 **HorizontalLayoutGroup은 비활성 상태로 보존**했다. 다시 켜면 수동 배치를 덮어쓰므로 켜지 않는다. 최종 정리 때 삭제 후보로 검토한다.

**리소스 용도 주의:** 현재 배경에는 `Assets/_Ui_Sprites/BattleScene/UI_box_turnbox(blue).png`가 임시로 연결되어 있으나, 이 이미지는 본래 **아군 턴 팝업용**이다. `(red)` 역시 적군 턴 팝업용이다. 턴 순서 패널 전용 배경 리소스는 원래 없었다. 이 연결을 정식 용도로 오해하거나 이름을 바꾸지 않는다. 작업 완료 후 사용자가 직접 배경 리소스 교체와 관련 Transform 조정을 진행할 예정이다. 슬롯 프레임은 기존 `Assets/Resources/UI_Sprite/UI_Battle/UI_HUD_playerTurn.png` / `UI_HUD_enemyTurn.png`를 사용한다. 편집 모드에서 배치를 알 수 있도록 초상화에는 `Assets/Resources/Portrait_Hero_Sprite/UI_profile_hero_unselected.png`를 연결했으며, 실행 시 실제 유닛 초상화로 바뀐다. Mask는 클리핑을 유지하고 자체 그림 표시만 껐다. 패널과 슬롯 루트가 UI Raycast를 받고 장식 자식은 받지 않는다.

### 다른 씬과의 호환

`sceneSlots`가 비어 있는 기존 씬은 계속 `slotPrefab`과 `slotParent`로 슬롯을 생성한다. 따라서 기존 두 필드는 삭제하지 않았다. **이 경로는 미개편 씬 호환용이며 TmpBattleScene의 실사용 경로가 아니다.** 미개편 씬을 이관하거나 삭제한 뒤 전체 참조를 확인하고 정리한다. 현재 단계에서는 프리팹·스크립트 파일을 삭제하지 않는다.

## 5. 하단 레이아웃과 현재 행동 유닛 정보

`BattleSceneCanvas/BottomPanel`은 씬에 미리 배치한 하단 고정 레이아웃이다. 배경 Image와 구역별 RectTransform 부모를 사용하며, 현재 `ActorInfoPanel`에 초상화·이름·랭크·HP·IP가 연결되어 있다. 코어스킬 영역에는 기존 버튼을 이관했고, 히어로스킬 영역에는 4버튼이 있다. 설명 영역에는 제목·본문·분류 태그를 선배치했다. 하단 개편에서 새 스크립트 파일이나 프리팹 원본은 만들지 않았다.

### 배경과 조절 방식

- `BottomPanel`: 하단 좌우 Stretch, Anchor `(0,0)~(1,0)`, Pivot `(0.5,0)`, 위치 `(0,0)`, Size Delta `(0,244)`. 기준 Canvas `1920×1080`에서 실제 크기는 `1920×244`다.
- `Background`: 부모 전체 Stretch. 기존 `Assets/_Ui_Sprites/BattleScene/UI_box_bottom(skillPre).png`를 Image/Simple로 표시한다. 원본은 `1920×244`이고 다섯 영역의 테두리가 **한 장에 그려져 있다**.
- 배경은 하나의 Image다. 각 영역의 부모 RectTransform을 바꿔도 그림 속 경계선이 자동으로 바뀌지는 않는다. 배경 전체의 높이·폭과 내용 영역의 배치는 구분해서 조절한다.
- `skillSelect` 이미지는 코어와 히어로 스킬 영역의 구분선이 없는 별도 이미지이며 현재 배경으로 사용하지 않는다. 상태에 따라 두 이미지를 교체하는 기능도 아직 없다.
- 모든 새 RectTransform의 Scale은 1이다. LayoutGroup·ContentSizeFitter·런타임 위치 재설정은 없다.

### 내용 배치용 구역

모두 부모의 좌하단 Anchor/Pivot `(0,0)` 기준이며, Y=0과 높이244를 사용한다. 아래 구역은 편집 가능한 부모다. 정보 구역에는 표시 오브젝트가 있으며 코어스킬 구역에는 버튼과 제목이 있으며, 히어로스킬 구역에는 4버튼이 있으며 설명 구역에는 제목·본문·세 태그가 있다.

| 경로 | X | 너비 | 용도 |
|---|---:|---:|---|
| ActorInfoPanel | 0 | 680 | 현재 행동 유닛의 좌측 정보 묶음 |
| ActorInfoPanel/PortraitArea | 0 | 256 | 초상화 자리 |
| ActorInfoPanel/StatsArea | 256 | 424 | 이름·랭크·HP·IP 자리 |
| CoreSkillPanel | 680 | 224 | 기존 무기스킬 경로를 쓸 코어스킬 자리 |
| HeroSkillPanel | 904 | 600 | 히어로 스킬 4슬롯 자리 |
| SkillDescriptionPanel | 1504 | 416 | 고정 설명 출력 자리 |

`ActorInfoPanel`에는 기존 `HeroInfoPanel` 컴포넌트를 연결한다. 코어스킬 구역은 기존 버튼 컴포넌트가 담당한다. 히어로스킬은 기존 `SkillButtonController`가 제어한다. 설명 구역도 기존 `SkillButtonController`가 관리하며 별도 컨트롤러 파일을 만들지 않았다.

### 현재 화면과의 관계

`BottomPanel`은 Canvas의 첫 번째 자식으로 두어 기존 정보·버튼보다 뒤에 그린다. 좌측 정보는 새 구역으로 이관했고, 옛 `HeroInfoWindow`와 별도 `Rank`는 비활성으로 보존한다. 코어스킬 버튼은 새 구역으로 이동했다. 히어로 스킬 버튼도 새 구역으로 이동했다. 전투 스킬 버튼의 호버는 우측 고정 설명으로 연결되어 있다. 새 배경 Image는 UI Raycast를 받아 하단의 빈 배경 클릭이 전투 필드로 전달되지 않도록 한다.

### 현재 행동 유닛 정보의 제어

기존 [HeroInfoPanel.cs](../../../../Assets/_ProtoType_Merge/KJ/Scripts/UI/HeroInfoPanel.cs)를 `BottomPanel/ActorInfoPanel`에 연결했다. 새 전용 스크립트는 없다.

```text
HeroInfoPanel.OnEnable → BattleFlowManager.CurrentUnit으로 즉시 표시
HeroInfoPanel.LateUpdate → CurrentUnit 변경 시 이전 구독 해제·새 유닛 구독·전체 표시 갱신(턴 팝업 중 포함)
BattleFlowManager.OnTurnStarted → 이전 유닛 이벤트 해제 → 새 현재 유닛 구독 → 전체 표시 갱신
현재 유닛 OnHpChanged / OnInfluenceChanged → 해당 숫자·게이지 갱신
HeroInfoPanel.OnDisable → 전투 흐름 및 유닛 이벤트 구독 해제
```

- `portraitImage`에는 씬의 `PortraitArea/Portrait`를 직접 연결한다. `Awake()`는 이 참조가 있으면 오브젝트를 만들거나 Transform을 변경하지 않는다.
- 이름은 `BattleCharactor.DisplayName`, HP는 `CurrentHp/MaxHp`, IP는 `CurrentInfluence`를 표시한다. HP 숫자는 기존처럼 올림한다. HP 게이지는 최대 HP가 0보다 클 때 비율을 0~1로 제한한다.
- 랭크는 기존 `RankUtil.FromLevel` 계산을 유지한다. `StatsArea/Rank`는 이름 우측의 임시 고정 위치이며, 기획 확인 후 후속 배치·제거 대상이다.
- 아군 초상화는 `Sprites.Portrait.Hero`, 적은 `Sprites.Portrait.Enemy`를 사용한다. 키가 없는 아군은 Unselected, 적은 라이브러리의 기본 적 초상화를 사용한다. 공용 SpriteLibrary 자체는 수정하지 않았다.
- 현재 유닛이 없으면 숫자·이름을 '-'로 비우고 초상화 Image를 숨긴다. 편집 모드의 '이름', '- / -', 물음표 초상화는 배치 확인용 기본 표시다.
- 새 정보 패널의 `showMaxIp=false`, `ipGauge`는 비어 있다. 따라서 IP는 현재 수치만 표시하고 IP 게이지는 없다.
- 미개편 씬의 기존 `portrait` 생성 부모와 `showMaxIp=true` 기본값은 유지한다. 직접 Image가 없는 기존 씬은 이전 방식으로 초상화를 생성한다. 기존 씬도 적 초상화 조회와 활성화 시 현재 유닛 초기 갱신 보완을 공유한다.

### 좌측 정보의 수동 배치

아래 자식들은 각 부모의 좌상단 Anchor/Pivot 기준이고 Scale은 모두 1이다. 자동 LayoutGroup은 없다. 위치·크기는 Inspector에서 직접 조절한다.

| 대상 | 위치 X,Y | 크기 | 표시 |
|---|---|---|---|
| PortraitArea/Portrait | 28,-32 | 200×200 | Image, Preserve Aspect |
| StatsArea/ActorName | 30,-20 | 268×60 | 기본 글자36, 긴 이름22까지 자동 축소 |
| StatsArea/Rank | 310,-26 | 88×48 | 임시 랭크, 글자24 |
| StatsArea/HpLabel | 30,-107 | 52×44 | HP, 글자30 |
| StatsArea/HpBar | 92,-118 | 128×26 | HP 배경/프레임 |
| HpBar/Fill | 2,-2 | 124×22 | Filled/Horizontal/Left |
| StatsArea/HpValue | 232,-107 | 166×44 | 현재/최대 HP, 글자30~18 |
| StatsArea/IpIcon | 28,-173 | 46×46 | IP 아이콘 |
| StatsArea/IpValue | 92,-170 | 306×48 | 현재 IP, 글자34 |

HP는 기존 `Assets/_Ui_Sprites/Icon/Bar/UI_bar_frame.png`와 `UI_bar_fill(HP).png`, IP는 `Assets/_Ui_Sprites/Icon/Stat/UI_icon_IP.png`를 쓴다. 이미 임포트된 리소스를 연결했으며 복사·임포트 설정 변경은 없다. 새 텍스트는 기존 NotoSansKR-Light SDF를 사용한다. 표시용 Image/TMP의 Raycast Target은 끄고 하단 배경이 입력을 받는다.

### 보존한 옛 표시

`HeroInfoWindow`와 옛 `Rank`는 이름·초상화 부모·HP/IP·랭크 및 기존 컴포넌트 참조를 보존한 채 GameObject만 비활성화했다. 새 정보 컨트롤러는 하나만 활성이다. 최종 사용자 검증 후 정리할 후보이며 지금 삭제하지 않는다. 유닛 위 월드 HP/IP는 별도의 기존 `UnitHPBar.cs` 그대로다.

### 코어스킬 버튼 — 기존 무기스킬 연결 유지

`BottomPanel/CoreSkillPanel/Button_Skill_2`는 원래 `ButtonController` 아래에 있던 **같은 오브젝트**다. `SkillButtonController.toggle2`가 동일한 ToggleButton을 참조하며, 이름과 내부 `WeaponSkill` 식별자는 유지했다. 제목 오브젝트도 기존 `Text (TMP)`를 재사용하여 `CoreSkillPanel/Title`로 옮기고 '코어 스킬'로 표시한다.

```text
Button_Skill_2 클릭 → ToggleButton.OnValueChanged
→ SkillButtonController의 toggle2 처리
→ InputHandler.BeginPendingAction(PendingActionType.WeaponSkill)
→ 기존 대상 선택·무기스킬 실행 경로
```

코어 버튼은 기존 무기스킬 실행 경로와 숫자키2 연결을 유지한다. 현재는 히어로 버튼과 함께 매 시전 수동 선택을 사용하며, 시전·취소 후 코어의 선택 표시도 해제된다.

| 대상 | 기준 | 위치 | 크기 |
|---|---|---|---|
| Title | 부모 상단 중앙 Anchor, 중앙 Pivot | `(0,-40)` | `204×52`, 글자36 |
| Button_Skill_2 | 부모 상단 중앙 Anchor, 중앙 Pivot | `(0,-152)` | `156×156` |
| SkillIcon / BaseHoverOverlay / SelectedOverlay | 버튼 중앙 | `(0,0)` | `156×156` |

Scale은 모두1이고 자동 배치는 없다. 버튼의 투명 루트 Image가 입력을 받으며, Button.targetGraphic은 보이는 SkillIcon을 가리킨다. 제목과 장식 Image는 Raycast Target을 끈다.

**아이콘 연결 상태:** 현재 그림은 기획 예시와 같은 `Assets/Resources/UI_Sprite/UI_Icon/Core/UI_icon_core_normal.png`를 직접 연결한 임시 고정 표시다. 실제 장착 코어 종류를 판정해 다른 그림으로 교체하는 기능은 이번에 결선하지 않았다. 이 그림으로 실제 코어 종류가 normal이라고 판단하면 안 된다. 기존 `SkillButtonIcon`은 구 무기스킬 이미지 조회용이므로 이 버튼에서만 비활성으로 보존했다. 공용 SpriteLibrary 및 다른 버튼의 아이콘 코드는 수정하지 않았다.

- `SkillButtonFrame`은 비활성 보존한다. 그 아래 있던 두 효과 오브젝트는 버튼 직속으로 옮겼다.
- 호버의 `HoverOverlayTint.source`를 SkillIcon으로 바꿔 코어 형상을 사용한다. 기존 선택 표시와 호버 우선 동작은 유지하며 효과 품질 개선은 후속 범위다.
- 코어 버튼의 `SkillButtonTooltip.fixedDescription`은 `BattleSceneManager`의 `SkillButtonController`를 참조한다. 현재 `EquippedWeaponData`의 이름·설명을 우측 고정 영역에 표시하며 공용 SkillTooltip을 호출하지 않는다. 실제 표시명은 기존 데이터 그대로다.
- 기능 교체를 위한 별도 버튼 복제본이나 신규 스크립트는 없다. 비활성 SkillButtonFrame/SkillButtonIcon/ToggleSkill은 최종 정리 후보다.

## 6. 히어로 4슬롯과 스킬 선택

### 씬 배치와 책임

기존 [SkillButtonController.cs](../../../../Assets/_ProtoType_Merge/KJ/Scripts/UI/SkillButtonController.cs)는 **BattleSceneManager 오브젝트에 붙어 있다.** Canvas 아래의 `ButtonController`라는 빈 부모와 혼동하지 않는다. `heroSlots` 배열에 네 슬롯의 ToggleButton, Button, 아이콘, 이름, 잠금 묶음, 미결선 안내를 연결한다. 배열 내부 `HeroSkillSlot`은 이 참조들을 묶는 직렬화 항목이며 별도 MonoBehaviour나 새 스크립트 파일이 아니다. 런타임 오브젝트 생성·배치 변경은 없다.

- 첫 슬롯은 기존 `Button_Skill` 오브젝트다. 새 버튼은 `HeroSkillSlot_2`, `HeroSkillSlot_3`, `HeroSkillSlot_4` 세 개다. 모두 `HeroSkillPanel`의 직접 자식이다.
- 첫 슬롯의 옛 `SkillButtonFrame`과 그 아래 효과는 비활성으로 보존한다. 기존 `SkillButtonIcon`, `HoverOverlayTint`, `ButtonEffectController`, `ButtonEffectActiveToggle`, `SelectedSkillHighlight`도 이 버튼에서는 비활성으로 보존한다. 이전 `ToggleSkill` 역시 비활성이다. 다른 버튼·공용 효과 스크립트는 변경하지 않았다.
- 선택 표시는 `ToggleButton`이 `UI_box_button_usingSkill.png`와 `UI_box_button_usingSkill(Selected).png`를 교체한다. 추가 호버 효과는 없다.
- `LockedOverlay`는 기존 `Assets/_UI_Prefabs_Runtime/UI_ReusableVisuals/LockedOverlay.prefab`을 각 버튼에 선배치한 인스턴스다. 회색 반투명 음영·음각 경계·자물쇠를 함께 표시한다. 원본 프리팹은 수정하지 않았다.
- `SkillName`, `SkillIcon`, `LockedOverlay`, `UnconnectedLabel`은 네 버튼에 공통으로 있다. 편집 모드의 '스킬 1~4'와 루미나 그림은 배치 확인용이며, 실행 시 현재 행동 유닛의 데이터로 바뀐다.

아래 수치는 4슬롯 최초 배치 기준이다. 이후 사용자가 씬에서 RectTransform을 조절하므로 현재 배치는 씬의 Inspector를 기준으로 확인한다. 선택 취소 작업은 이 값을 되돌리거나 다시 배치하지 않는다. 자동 LayoutGroup은 없다.

| 슬롯 | 위치 X,Y | 자식 배치 |
|---|---|---|
| Button_Skill | `16,-50` | 아이콘 `10,-10`, `52×52` / 이름 `72,-8`, `190×56` |
| HeroSkillSlot_2 | `310,-50` | 동일 |
| HeroSkillSlot_3 | `16,-146` | 동일 |
| HeroSkillSlot_4 | `310,-146` | 동일 |

이름은 글자24에서18까지 자동 축소한다. 잠금 묶음은 버튼 전체에 Stretch되며 '연결 대기' 안내는 `72,-52`, `185×18`이다. 모두 Inspector에서 직접 배치할 수 있다. 기본 버튼 배경만 Raycast Target을 켜고 이름·아이콘·잠금 장식은 끈다.

### 데이터와 사용 가능 여부

`Start`와 `BattleFlowManager.OnTurnStarted`에서 현재 유닛을 읽어 표시한다. 아군이면 기존 `ResolveSelectedSkill()`로 연결된 스킬을 확인하고, `SourceData.UnitTemplateKey → DHCsvTemplateCatalog.TryGetPlayerUnitTemplate → GetSkillsByClassIndex`로 그 캐릭터의 표시 목록을 얻는다.

- 기본 스킬은 `acquireLevel` 순으로 정렬한다. ID 숫자순과 같다고 가정하지 않는다.
- 강화판은 카탈로그의 `ReplaceSkillKey`가 가리키는 기본 스킬 슬롯에 표시한다. 습득 가능한 강화판이 있으면 그 표시를 사용한다.
- **이미 실행 연결된 스킬은 실제 연결된 기본판/강화판을 그대로 표시한다.** UI가 연결된 기본판을 임의로 강화판으로 교체하지 않는다. 같은 가족의 새 강화판이 열렸어도 실제 장착 갱신이 없다면 기존 연결판 표시·실행을 유지한다.
- 요구 레벨이 현재 유닛 레벨보다 높으면 Button을 비활성화하고 잠금 묶음을 켠다.
- 초기 구현에서는 기본 연결 스킬 외에 연결 대기를 표시했다. 2026-09-16 확인한 현재 코드에서는 습득한 표시 스킬을 선택하며, 이 이전 제한은 더 이상 적용하지 않는다. 실행 로직의 변경은 별도 작업이며 이번 작업은 이를 보존했다.
- 실행 연결된 스킬만 선택·시전 가능하다. 잠금·미결선 버튼도 클릭은 감지하지만 기존 선택 취소만 수행한다. 카탈로그가 없는 기존 테스트 구성은 현재 연결 스킬 한 개를 첫 슬롯에 표시한다. 적 턴·현재 유닛 없음에서는 네 버튼의 이름을 '-'로 비우고 비활성화한다.
- 아이콘은 현재 공용 `Sprites.Icon.ClassSkill(skillIndex, 1)` 조회를 사용한다. 여기의 1은 임시 아이콘 조회 단계이며, 전투 강화값을 설정하거나 변경하는 값이 아니다. 협회 강화 단계별 시각 표현은 이번에 결선하지 않았다.

현재 `DHScene_3`의 카탈로그는 캐릭터 V3.0, 클래스스킬 V3.1 테이블을 참조한다. 예를 들어 루미나의 기본 습득 순서는 `2020 → 2030 → 2040 → 2010`으로 최신 기획과 다르다. UI 개편 중 카탈로그·습득 규칙·장착 데이터를 교체하지 않았으며 이 불일치는 후속 확인 대상이다. 원본 스킬 기획서나 데이터 테이블의 수정·저장은 하지 않았다.

### 매 시전 수동 선택과 상태 동기화

```text
턴 시작 → InputHandler가 선택 해제 → 네 슬롯 표시 갱신
사용 가능한 히어로/코어 버튼 클릭 → ToggleButton.OnValueChanged
→ SkillButtonController → 다른 스킬이면 TryCancelSkillSelection으로 기존 선택 정리
→ InputHandler.BeginPendingAction
→ 기존 검사(IP·튜토리얼 제약·유효 대상) → 선택 성공
→ InputHandler.SelectionChanged → 실제 PendingAction에 맞춰 버튼 선택 표시
→ 대상 유닛 클릭 → 기존 시전/턴 진행 → 선택 초기화 → 버튼 선택 표시 해제
```

[InputHandler.cs](../../../../Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)가 실제 선택 상태를 소유한다. 새 읽기 속성 `PendingAction`과 `SelectionChanged` 이벤트로 UI가 이를 따른다. 버튼을 먼저 켜놓고 실행 가능하다고 가정하지 않는다. 우클릭 취소, IP 부족 초기화, 행동 완료 및 턴 경계의 `ClearSelectionState()`에서도 함께 해제한다. 선택된 버튼을 다시 누르면 기존과 같이 같은 스킬 선택을 요청한다.

- **TmpBattleScene의 `selectSkillOnTurnStart=false`**: 플레이어 턴마다 미선택 상태로 시작한다. 자동전투 AI의 결정 방식과 별개다.
- 다른 미개편 씬의 자동 선택은 기본값 true로 보존한다. `heroSlots`가 비어 있는 씬은 기존 `toggle1/toggle2` 표시를 사용한다. 다른 씬 정리 단계에서 함께 전환한다.
- InputHandler는 자동전투 또는 기존 시전 처리 중 들어오는 수동 재선택을 무시한다. 새 수동 선택 모드에서는 레벨 미달인 연결 스킬의 숫자키 선택도 차단한다.
- 숫자키는 기존대로 `1=연결된 히어로 스킬`, `2=코어(무기)스킬`, `4=턴 스킵`이다. 4버튼의 위치에 숫자키1~4를 배정하지 않았다.
- 선택 가능 표시는 전투 규칙 전체를 미리 계산하는 판정기가 아니다. 클릭 후 IP 부족·튜토리얼 제한 등은 기존 InputHandler가 최종 판단한다.
- `BattleManager.cs`, 실행 스킬 목록, 영속 장착 정보, 타일/대상 판정은 이번에 수정하지 않았다. 필드 클릭 시전은 추가하지 않았다.

### 버튼 전환·우클릭 취소

- 히어로 4버튼과 코어 버튼에 Unity 기본 EventTrigger가 각각 하나씩 있다. Inspector의 **Pointer Click → BattleSceneManager의 SkillButtonController.OnUnavailableSkillClicked(BaseEventData)**로 직렬화되어 있다. 새 프로젝트 스크립트 파일은 없다.
- 활성 버튼은 기존 Button → ToggleButton.OnValueChanged → SkillButtonController.Select 경로로 동작한다. 다른 스킬이면 기존 선택을 먼저 비운 후 새 선택을 요청한다. IP 부족·튜토리얼 제한·실행 데이터 부재 등으로 거절되면 미선택 상태다. 현재 선택한 버튼 재클릭은 기존 재선택 요청을 유지한다.
- EventTrigger는 왼쪽 클릭이며 해당 Button이 IsInteractable()==false일 때만 선택 취소를 요청한다. 활성 버튼 클릭을 중복 처리하거나 우클릭을 별도로 처리하지 않는다. 잠금·미결선 상태를 사용 가능으로 바꾸지 않는다.
- 실제 포인터 이벤트의 pointerClick에서 버튼을 확인한다. 콜백을 연결할 때 스킬 버튼 자체의 EventTrigger에 연결해야 하며, 하단 패널이나 장식 자식에 대신 붙이지 않는다.
- 우클릭은 InputHandler.Update → TryCancelSkillSelection() 경로다. 자동전투가 아니고 대상 선택 대기 상태인 생존 아군에서 오른쪽 버튼을 누르는 순간 처리한다. 커서가 UI 위인지, 메뉴·일시정지 상태인지는 추가 검사하지 않는다. 취소는 선택·대상 강조·범위 미리보기를 비우고 SelectionChanged로 버튼 표시를 동기화한다.
- **사용자 취소 함수 TryCancelSkillSelection()은 자동전투 또는 isProcessingAction 중이면 false를 반환하고 아무 상태도 바꾸지 않는다.** 다른 스킬 선택 요청도 false이면 진행하지 않는다. 현재 선택한 버튼의 재선택은 기존 BeginPendingAction의 실행 중 제한을 사용한다. 버튼의 임시 토글 변화는 실제 입력 상태로 다시 맞춘다.
- 공격 확정 후 입력으로 공격을 중단하지 않는다. 턴 경계용 ClearSelectionState()와 행동 완료의 내부 초기화는 사용자 입력 제한과 별개로 유지한다. IP 소비·장착 스킬·턴 진행·시전 로직은 이 취소 처리에서 변경하지 않는다.
- 하단 패널의 다른 부분을 클릭해도 선택을 취소하지 않는다. 패널에는 취소 이벤트를 연결하지 않았다. 숫자키와 ESC 동작은 변경하지 않았다.
- 스킬 선택이 취소되어도 사용 가능한 버튼에 커서가 남아 있으면 우측 고정 영역의 호버 설명은 유지된다. 선택 취소가 툴팁 강제 닫기를 의미하지 않는다.

### 하단 우측 고정 설명과 공용 DDOL의 구분

`BattleSceneCanvas/BottomPanel/SkillDescriptionPanel`의 Title / Body / EffectTag / RangeTag / TargetTag를 사용한다. 세 태그에는 각각 Label이 있다. 모두 씬에 선배치하며 런타임 생성은 없다. `BattleSceneManager`에 있는 기존 `SkillButtonController`의 descriptionTitle / descriptionBody / descriptionTags에 연결한다. 각 태그 Label의 부모는 해당 태그만의 배경이어야 한다.

```text
전투씬의 다섯 SkillButtonTooltip: 마우스 진입/이탈
→ fixedDescription.SetDescriptionHover(Button, bool)
→ SkillButtonController.RefreshDescription
→ 현재 유닛 + 호버 버튼 + InputHandler.PendingAction으로 표시 결정
→ ClassSkillTooltipText / WeaponTooltipText로 본문 작성
→ 씬에 배치한 Title / Body / 세 태그 갱신

InputHandler.SelectionChanged → SyncSelection → RefreshDescription
SkillButtonController.LateUpdate → 표시 중인 유닛과 CurrentUnit이 다르면 RefreshSkills(턴 팝업 중 포함)
BattleFlowManager.OnTurnStarted → RefreshSkills → SyncSelection → RefreshDescription
BattleFlowManager.OnBattleEnded 또는 컨트롤러 비활성 → 설명 비우기
```

- 아군 턴의 우선순위는 **사용 가능한 버튼 호버 → 현재 선택 스킬 → 선택 안내**다. 호버로 실제 선택이나 장착 데이터를 바꾸지 않는다. 포인터가 떠나면 기존 선택 설명으로 돌아간다. 잠금·미결선 버튼은 호버 설명을 덮어쓰지 않는다.
- 선택 안내는 제목 '스킬 선택', 본문 '사용할 스킬 버튼 또는' / '코어 스킬을 클릭하세요.' 두 줄이다. 안내·적 턴·전투 종료에는 세 태그 배경을 숨긴다.
- 적 턴에는 제목 '적 행동 중'만 표시한다. 실제 적 스킬 시전 내용은 미결선이며 이번 작업에서 BattleManager/BattleFlowManager/InputHandler를 수정하지 않았다.
- 호버한 버튼 자체를 기억한 뒤 갱신할 때 현재 턴의 슬롯 데이터를 읽는다. 커서를 움직이지 않고 턴이 넘어가도 이전 유닛 설명을 보존하지 않는다. 선택 해제 이후에도 사용 가능한 스킬에 호버 중이면 호버 설명이 우선한다.
- 히어로 이름은 해당 슬롯 SkillData.skillName, 본문은 ClassSkillTooltipText.BuildDesc를 사용한다. 강화 조회도 기존 공용 툴팁과 같은 Lab.GetSkillLevel 및 SourceData.SkillLevel 경로다. 코어는 EquippedWeaponData와 WeaponTooltipText.BuildWeaponSkillDesc, 기존 Workshop.GetWeaponLevel을 사용한다. 코어 본문의 IP 표기도 기존 형식을 유지한다. 설명용 새 데이터 저장소나 강화 계산을 만들지 않았다.
- 분류 태그는 현재 SkillData의 classSkillEffect(공격/회복/부활/버프/디버프), classSkillRange(근거리/원거리), classSkillTarget(단수/복수/전체)을 사용한다. 코어는 기존 ToSkillData 변환값이다. 알 수 없는 분류 값은 해당 태그만 숨긴다. 설명 문장에 근거해 실행 데이터의 분류를 임의 보정하지 않는다.
- 위치·크기는 각 RectTransform으로 직접 조절한다. Scale=1이고 LayoutGroup이 없다. 코드가 제목·본문·태그 위치나 크기를 덮어쓰지 않는다. 현재 제목 최대34, 본문25, 태그18의 TMP Auto Size를 사용하고 최소12, 영역 초과 시 Ellipsis다. 본문만 줄바꿈한다. 글자와 태그 배경은 Raycast를 받지 않는다.
- 배경·구분선은 기존 BottomPanel/Background 스프라이트를 재사용한다. 태그는 Unity Image의 단색 배경이다. 이번 단계에서 새 스크립트·프리팹·이미지 파일은 추가하지 않았다. 부모 패널 등 기존 모든 RectTransform 값은 보존했다.

[SkillTooltip.cs](../../../../Assets/_ProtoType_Merge/JC/Scripts/UI/Lobby/SkillTooltip.cs)는 로비·연구소·캐릭터 정보 화면이 사용하는 공용 DDOL UI로 그대로 유지한다. `SkillButtonTooltip.fixedDescription`이 비어 있는 기존 씬은 종전 SkillTooltip.Instance.ShowInfo/Hide 경로를 사용한다. 현재 TmpBattleScene의 다섯 버튼에는 fixedDescription이 연결되어 있어 공용 툴팁을 열거나 닫지 않는다. 이 씬의 SkillTooltip 프리팹 인스턴스도 삭제하지 않았다. 최종 사용처 정리는 후속 범위다.

### 빌런 호버 정보창

[BattleEnemyInfoTooltip.cs](../../../../Assets/_ProtoType_Merge/JC/Scripts/UI/Battle/BattleEnemyInfoTooltip.cs) 하나가 `BattleSceneCanvas`에 부착되어 있다. 비활성 패널 자신에 붙이지 않아 숨긴 이후에도 호버를 감지한다. DDOL 생성이나 런타임 패널/슬롯 생성은 하지 않는다.

- `BattleSceneCanvas/EnemyInfoTooltip`과 다섯 스킬 행을 씬에 배치했다. 에디터에서 패널을 켜서 직접 RectTransform을 조절할 수 있다. 배포 저장 상태는 비활성이다. 실행 시 루트 위치와 높이만 바꾸며, 자식 위치·크기는 덮어쓰지 않는다. 새 계층 Scale은 1이고 LayoutGroup은 없다.
- 패널 폭은 현재 450, 스킬 행 높이는 108, 행 간 시작점 간격은 116 Canvas 단위다. 표시 행의 실제 하단 위치에 아래 여백을 더해 배경 높이를 정한다. 0개면 안내를 표시하고 최소 높이를 사용한다. 최대 5개만 표시한다.
- EventSystem의 최상단 UI가 TurnSlotUI 아래라면 `DisplayedUnit`을 읽는다. TurnSlotUI.Setup이 이 읽기 전용 속성을 갱신하고 빈 슬롯에서는 null로 비운다. 턴 순서 계산·생성 방식은 바꾸지 않았다. 다른 UI 뒤의 필드는 호버하지 않는다.
- UI 위가 아닐 때 기존 입력과 같은 카메라·레이어로 물리 광선을 쏜다. BattleCharactor 또는 점유 셀의 OccupyingUnit을 찾는다. 같은 전투씬의 살아 있는 빌런만 표시하고 아군·빈 셀·사망 유닛은 숨긴다. 툴팁 Graphic과 CanvasGroup은 마우스 광선을 가로채지 않는다.
- 이름은 UnitName, 초상화는 기존 Sprites.Portrait.Enemy, HP는 CurrentHp/MaxHp, 공격력·방어력은 FinalStats.Atk/DEF다. 등급은 공란이다. 표시 중 능력치는 매 프레임 갱신한다.
- 스킬은 현재 DHCsvTemplateCatalog와 SourceEnemyData.UnitTemplateKey로 슬롯별 GetSkillTemplate을 조회한다. 해당 데이터가 없으면 유닛 availableSkills의 같은 슬롯을 사용한다. V4.0 파일을 직접 읽거나 씬 테이블을 교체하지 않는다. 일반 V3.0 카탈로그의 FV20002는 원거리 사격 1개만 반환한다. 이벤트 전투에서는 availableSkills의 명시 스킬을 보충하므로 이 결과가 전체 표시 개수를 뜻하지 않는다. 1구역 이벤트 원본의 3번 스킬 칸에 AI 문구/30이 들어가 소총수·방패병의 추가 카드로 보이는 문제는 후속 보고서 R11에 기록했고, 사용자 결정에 따라 7단계 이후 정리한다.
- 같은 슬롯의 `{EnemySkillNValue}` / `{EnemySkillNSubValue}`는 현재 계수의 백분율로 표시한다. 다른 슬롯을 지칭하는 설명은 **수치 확인 중**으로 남기고 최초 경고를 기록한다. 임의로 원본 오타를 고치지 않는다. 다른 단위·미지원 변수는 후속 보고서 R03 확인 대상이다.
- 숨김 조건: 스킬 선택, 일시정지, 전투 종료, 현재 유닛 없음, 비활성 Flow, Flow 차단, **BattleFlowManager.IsActionInProgress**, 기존 Presentation 연출 진행. 선택 표시가 사라져도 행동 진행 상태가 유지되면 숨긴다.
- `IsActionInProgress`는 아군의 기존 `playerActionClaimed && !playerActionResolved`를 읽는다. 수동·자동 공통이며, 현재 행동 유닛이 아군일 때만 적용한다. 완료 통지 전의 공격 준비와 ExecuteGridSkill 내부의 후속 행동 대기까지 포함한다. 적은 RunEnemyTurn의 AI 처리 구간을 try/finally로 표시하므로 판단 대기부터 공격·반격 반환까지 숨긴다. 새 상태는 UI 조회용이며 공격 취소·턴 제어에 사용하지 않는다.
- 적 상태는 루프 시작/중지·초기화·컴포넌트 비활성화에서 정리한다. 전투 종료 상태에서는 IsActionInProgress가 false이고, 툴팁은 별도 종료 이벤트로 숨긴다. BattleFlowManager 수정은 사용자에게 팀원 허가를 받았다는 확인 후 적용했다. BattleManager와 기존 피해·턴 순서·완료 통지는 수정하지 않았다. 기존 실패 경로의 미완료 점유 상태 관련 후속 확인은 R05에 기록했다.
- 커서 옆에 표시하되 화면을 벗어나면 반대편으로 배치하고 경계에 맞춘다. 5행 기본 크기는 기준 Canvas 안에 들어간다. 사용자가 크게 늘려 화면보다 커지게 만들면 별도 레이아웃 조정이 필요하다.
- 툴팁의 TMP 텍스트 20개 모두 Auto Size를 사용한다. 최소 크기는 10, 최대는 원래 크기(빌런 이름28, 스킬명23, 설명19 등)다. 이름·스킬명·능력치 라벨/숫자·제목은 한 줄 안에서 축소하고, 설명과 스킬 없음 안내는 줄바꿈한다. 내부 여백은 사방 최소2 Canvas 단위이며 최소 글자 크기에서도 넘치면 Ellipsis로 말줄임한다. Inspector의 TMP Auto Size Min/Max와 Margin에서 조절할 수 있다. RectTransform·Scale을 코드로 바꿔 글자를 맞추지 않는다. 툴팁 전용 설명 문구 준비는 R04 후속 사항이다.

재사용 자산: `Assets/_Ui_Sprites/Icon/Stat`의 HP/공격/방어 아이콘, `Icon/Bar`의 HP 프레임·채움, `Hero_Modal/UI_Value_Field.png` 제목 배경. 구분선은 기존과 같은 단색 Image 구성이다. 기존 자산 자체는 수정하지 않았다.

임시 생성 자산: `Assets/_Ui_Sprites/BattleScene/EnemyTooltip/` 아래 TMP_enemyTooltip_panel / skillCard / skillIcon / circleMask PNG 4개. 패널과 카드는 9-slice이며 모든 스킬은 임시 조준점 아이콘을 공유한다. 정식 자산은 씬 Image에서 교체한다. SVN 복사는 수행하지 않았다.

### 결과창 — 전투씬 선배치 4카드

`BattleUIManager.sceneResultPanel`이 연결되어 있어 `ShowBattleResultUI()`는 씬의 ResultPanel을 활성화하고 그 인스턴스의 `Show(result, plan)`을 호출한다. 새 카드 4개는 실행 중 생성/삭제하지 않는다. 기존 ResultPanel의 Image는 비활성으로 남겼고, 명시적 Dim/ResultContent/SkillSelectionHost가 화면과 입력을 나눈다.

- `BattleResultView.sceneHeroSlots`: 좌상·우상·좌하·우하의 4카드. 값은 기존 PartyFormation.PackFrontFirst 순서로 배치하며, context에서 누락된 preview는 뒤에 보충한다. 4인 초과 입력은 경고 후 표시 상한을 지킨다. 공용 PartyFormation의 위치 칸 수는 변경하지 않았다.
- `BattleResultPanel`: Show/OnAccepted/GetSkillResults 외부 계약 유지. 스킬 획득/교체는 기존 순서대로 처리하며 그동안 ResultContent만 숨긴다. 확인 listener는 자신의 callback만 재연결하고 Show마다 확인 1회만 통지한다. 외부 listener를 전체 삭제하지 않는다.
- `HeroInfoResult`: portraitImage가 연결된 카드만 새 표시 경로를 사용한다. 초상화는 기존 Sprites.Portrait.HeroByUnit, 이름은 preview.UnitName, IP는 OldInfluence → NewInfluence. 랭크는 UnitRankLookup.GetRank(NewLevel)와 씬의 rankSprites를 사용하며 불명 랭크는 숨긴다.
- EXP는 `BattleResultPersistenceHandler`가 `PersistentUnitRepository.SimulateExpProgress`를 통해 저장 없이 계산한 값이다. UnitRewardPreview.HasExpPreview / OldExp / OldMaxExp / NewExp / NewMaxExp를 추가했다. 게이지와 숫자는 **보상 적용 후 잔여 EXP / 다음 필요 EXP**, 획득량은 별도 +숫자다. 여러 레벨 상승 계산을 UI에서 복제하지 않는다. 분모가 없거나 0이면 게이지 0, 숫자 -로 표시하며 임의 최대 랭크로 단정하지 않는다.
- 모의 전투 plan=null은 기존 보상 미지급 정책을 유지한다. 카드의 예전 보상 문구를 지우고 가능한 파티 초상화만 보여 준다. 실제 보상 저장은 기존 BattleSceneManager가 확인 후 Commit하는 시점 그대로다.
- 결과/EXP 바/IP/랭크는 프로젝트 기존 Sprite를 사용한다. `UI_box_profilerFrame.png`에는 예시 저스티스 그림까지 포함되어 있으므로 공용 프레임으로 사용하지 않았다. PortraitFrame은 씬의 단색 Image로 둘렀다. 결과 제목·확인 버튼은 글자가 포함된 기존 이미지다.
- 편집: ResultPanel을 켠 뒤 자식 RectTransform을 조절한다. 초기 저장 상태는 ResultPanel 비활성. 새 계층은 Scale=1, LayoutGroup 없음. 기존 172개 Transform의 위치·크기·스케일 값은 보존했다.

**다른 씬의 호환 경로:** BattleUIManager.sceneResultPanel / BattleResultView.sceneHeroSlots가 비어 있으면 기존 승패 프리팹 생성과 카드 생성 경로를 유지한다. HeroInfoResult.portraitImage가 비어 있는 기존 카드는 기존 초상화 생성·EXP 문구·IP 증감량 표시를 유지한다. 공용 승패/카드 프리팹 원본은 변경하지 않았다. DHScene_3의 전투 생략·도주 실패 결과에도 적용되는 공용 코드이므로 해당 경로 플레이 검수를 포함한다. PopupEnterSubmitter가 이름으로 찾는 새 확인 버튼 이름은 **Accept**다.

### 결과 표시 중 HUD 상태

2026-09-15 구현. 기존 BattleUIManager의 ShowBattleResultUI에서 유효한 결과창을 표시하기 직전에 ApplyResultHudState를 실행한다. 씬 선배치 결과창과 기존 프리팹 생성 경로에서 공통으로 호출하며, 참조 배열이 비어 있는 다른 씬은 기존 HUD 상태를 유지한다.

Inspector 연결은 다음 두 배열이다. 기존 오브젝트를 참조하며 런타임 생성이나 레이아웃 변경은 없다.

| 필드 | TmpBattleScene 연결 대상 | 동작 |
|---|---|---|
| hideOnResult | BottomPanel, 자동/배속/도주의 BaseHoverOverlay, 자동/배속의 ActiveState (총 6개) | 결과 표시 시 SetActive(false) |
| disableOnResult | MenuButton, Button_Auto, Button_Speed, RunButton | 오브젝트는 표시한 채 Button.interactable=false |

ResultPanel과 확인 버튼은 숨기는 대상 밖에 둔다. 자동/배속의 ToggleButton.IsOn 및 BattleRuntimeSettings는 변경하지 않는다. ActiveState를 숨겨 SelectedSkillHighlight가 선택 테두리를 다시 켜지 않게 한다. 버튼 회색 표시는 기존 ColorTint/Disabled Color를 그대로 사용한다. 공용 메뉴와 ESC 처리, KJ의 버튼 스크립트는 변경하지 않았다.

전투가 끝난 결과 상태는 씬 복귀까지 유지한다. 저장된 씬의 BottomPanel은 활성, 네 버튼은 interactable=true, ResultPanel은 비활성이므로 다음 씬 로드에서 초기 상태로 시작하며 이후 도주의 기존 턴별 제한을 따른다. 동일 씬 인스턴스를 재사용하여 전투를 재시작하는 별도 기능을 추가할 경우에는 HUD 초기화 경로도 함께 마련해야 한다.

검증: Unity 컴파일 및 격리 PreviewScene 26개 확인 통과(승리/패배 숨김·네 버튼 클릭 차단·확인 콜백·토글값·씬 초기 상태). 실제 플레이 및 씬 복귀/다음 전투 회귀는 미실행이다. EXP 합산 오류는 이 변경에서 수정하지 않았으며 FollowUps R12의 ASB 전달 항목이다.

### 전투 시작·매 유닛 턴 팝업

[BattleTurnBanner.cs](../../../../Assets/_ProtoType_Merge/JC/Scripts/UI/Battle/BattleTurnBanner.cs) 하나가 **JC_BattleUI_VFX/TurnNotify**에 붙어 Canvas/BattleNotices의 배너·문구·입력 차단 영역을 제어한다. 기존 컴포넌트를 이관하고 BattleFlowManager.turnBanner 참조도 갱신했다. DDOL이나 런타임 UI 오브젝트 생성은 하지 않는다.

```text
전투 초기화 / 기존 Flow 잠금 해제
 → 중앙 전투 시작 팝업(실제 1초, 클릭 종료 없음)
 → 순번 유닛 선택·기존 선택 해제
 → 아군 턴 / 적군 턴: 왼쪽 등장 0.5초 → 화면 정중앙 1초 → 오른쪽 퇴장 0.5초
   (좌/우클릭 시 현재 위치·알파에서 최대 0.3초 퇴장, 기존 남은 시간 이내)
 → 닫기 프레임의 입력 처리 완료 대기
 → 기존 턴 시작 상태이상 처리
 → 사망/기절이면 기존 건너뛰기 처리
 → 행동 가능할 때만 기존 OnTurnStarted → 수동/자동/적 행동
```

- 같은 진영 연속 순번도 각각 표시한다. 기절/턴 시작 피해로 건너뛰는 순번도 팝업을 먼저 표시한다. 기존 OnTurnStarted를 상태이상 처리 앞으로 옮기지 않았다.
- `IsTurnPresentationPending`은 안내 중 입력/행동 시작을 차단한다. InputHandler의 Update·스킬 선택·우클릭 취소와 Flow의 행동 허용/점유에서 확인한다. AutoBattleController는 안내 중 자동 모드를 켜도 즉시 코루틴을 시작하지 않고, 이후 OnTurnStarted에서 시작한다.
- 닫기 클릭은 안내에만 소비한다. 투명 InputBlocker를 다음 프레임의 입력 처리까지 유지하여 메뉴/배속 등 기존 버튼이 그 클릭의 PointerDown을 받지 않게 한다. 키보드 스킬 선택/스킵도 대기 중 차단한다.
- 2026-09-17: 하단 정보와 스킬 UI는 팝업이 닫힌 뒤의 OnTurnStarted만 기다리지 않고, LateUpdate에서 CurrentUnit 변경을 감지해 표시를 먼저 갱신한다. 팝업의 진영·하단 이름/초상화/HP/IP·스킬/설명이 같은 유닛을 가리킨다. 행동 시작 이벤트, 상태이상 처리, 자동전투·입력 허용 시점은 변경하지 않았다. 새 스크립트나 씬 참조는 추가하지 않았다. 비플레이로 아군→다른 아군→적의 팝업 중 표시 일치, 입력 차단, 이전 HP 구독 해제 및 현재 HP 갱신을 확인했다.
- 현재 씬은 animateTurn=true이며 **기본 총 2초**다. 이전 duration=3초는 이동 연출을 끄는 경우에만 사용하는 정지 표시 시간이다. 전투 시작 안내는 별도 startDuration=1초를 유지한다. Time.unscaledDeltaTime을 사용하여 배속에 영향받지 않는다. Time.timeScale=0, 공용 모달, 기존 Flow 잠금 동안 타이머와 이동을 멈춘다. 모달 중에는 InputBlocker를 꺼 모달 입력을 보존하고, 모달 종료 클릭은 턴 팝업 종료에 재사용하지 않는다.
- 중단·비활성·전투 종료 시 배너와 입력 차단을 정리한다. 기존 메뉴/일시정지 코드는 수정하지 않았다. turnBanner 미연결 씬은 기존 흐름을 유지한다.
- UI_box_turnbox(blue/red)는 본래 용도인 턴 팝업에서 사용한다. 좌상단 턴 순서 패널의 배경을 교체하지 않았다. 팝업 타이밍은 사용자 지정 임시 사양이며 플레이 체감 후 기획자와 조정한다.

검증: Unity 컴파일, 격리 PreviewScene 63항목(결과 값·EXP 계산 일치·기존 카드 경로·반복 확인·입력 차단·정리)과 정상/기절 코루틴 순서 20항목 통과. 플레이 모드와 실제 보상 저장·귀환은 실행하지 않았으며 클릭/배속/자동전투/튜토리얼 체감 및 탐사 결과창 회귀는 사용자 플레이 검수 대상이다.

### JC_BattleUI_VFX — 2026-09-17 제안용 연출

설정 오브젝트는 씬 루트 아래 세 개이며, 실제 UI Image는 기존 Canvas 아래에 선배치한다. 위치·크기는 기존 RectTransform으로 편집한다. 턴 배너만 요청에 따라 화면 정중앙 앵커/위치로 바꿨다. 재생 시 턴 배너의 X 위치와 효과 자식의 길이/위치를 제어하며 종료 시 원위치로 복원한다.

| 설정 위치 | 현재 제어 내용 |
|---|---|
| TurnNotify | 배너 세로 위치(px), 등장/정지/퇴장 시간, 이동 곡선, 클릭 퇴장 최대 시간·곡선, 화면 밖 여백, 최저 알파, 이동 블러·속도선 |
| SkillBTN_SweepGlow | 히어로 4개+코어 1개 사용 가능 효과 및 선택 테두리 공통 시각 설정. 실제 사용 가능 판정은 기존 SkillButtonController 소유 |
| CellBox | 현재 턴 노랑·선택 가능 보라·확정 범위 빨강·선택 불가 검정 각각의 불투명도(기본 0.65/0.65/0.65/0.35), 파란 테두리 스윕 글로우 |

- **배너 세로 위치 (px)**는 화면 세로 중앙에서 배너 피봇까지의 높이다. 기본 0=중앙, 양수=위, 음수=아래. Canvas 기준 픽셀이므로 Canvas Scaler 배율을 따른다. 턴 배너 출력 시 적용되며 이동 연출 중에도 변경값을 반영한다. 전투 시작 배너의 위치는 변경하지 않는다.
- 등장 이동은 기본 빠르게 시작해 감속하는 곡선, 퇴장은 기본 등가속(t²)이다. 곡선의 키/접선으로 가속 구간을 바꾼다. 페이드는 이동 곡선과 별개로 선형이며 **최저 알파 0.1 → 중앙 1 → 최저 알파 0.1**이다. 퇴장 완료 시 오브젝트를 숨긴다.
- 클릭 퇴장은 현재 위치·알파를 이어받고 `min(0.3초, 원래 남은 시간)` 이내에 오른쪽으로 나간다. 퇴장 중 추가 클릭은 시간을 다시 시작하지 않는다. 퇴장이 끝날 때까지 기존 행동/입력 차단을 유지하며 닫기 클릭 소비 프레임도 유지한다.
- 이동 블러는 배너 배경·외곽의 수평 잔상이며 글자는 선명하게 유지한다. 카메라 포스트프로세스가 아니다. 사용 여부·강도·최대 길이·최대 효과 기준 속도·정지 시 잔상 소거 시간을 조절한다.
- 스트레이크는 뒤쪽 수평 속도선이다. 사용 여부·개수(0~12, 기본 6)·아군/적 색·불투명도·길이/두께 범위·상하 배치 범위를 조절한다. 이동 속도에 따라 길이와 강도가 변하고 정지 구간에서는 사라진다.
- `JC/BattleUI/TurnNotifyMotion.shader`와 `Materials/TurnNotify_Blur.mat`, `TurnNotify_Streak.mat`을 사용한다. 새 비트맵은 없다. 블러 재질만 실행 중 인스턴스로 복제하고 종료/파괴 시 정리한다.
- CellBox는 연결된 **GridCell의 Plane Renderer 12개만** MaterialPropertyBlock으로 조절한다. 실행 중 공유 재질 값과 캐릭터 Renderer는 수정하지 않는다. 비활성 시 기존 속성 블록을 복원한다. 대상 판정/표시 우선순위는 그대로다.
- 이번에 추가한 런타임 파일은 `BattleSkillSweepSettings.cs`, `BattleCellVisualSettings.cs` 두 개다. 턴 연출은 기존 BattleTurnBanner를 확장했고, 기존 BattleSkillButtonVisual/BattleGridManager는 설정을 받아 표시하는 연결만 추가했다. BattleFlowManager의 턴/행동 이벤트 순서는 변경하지 않았다.

검증: 코드·UI 셰이더 컴파일, 격리한 비플레이 검사 63항목(이동/선형 알파/클릭 연속성/남은 시간 제한/씬 참조/재질 보존/설정 적용), 아군·적 포함 5장 렌더 확인. 실제 전투 플레이는 미실행이며 화면 내 연출의 체감 속도와 강도는 사용자 검수 대상이다.

### 셀 파란 테두리 스윕 — 2026-09-18

`JC_BattleUI_VFX/CellBox`의 **파란 테두리 스윕 글로우**에서 켜기, HDR 색/알파, 밝기, 빛띠 폭, 부드러운 번짐 폭, 기울기, 이동 시간, 반복 대기 시간, 반대 방향을 조절한다. 기본은 밝기 2, 폭 0.12, 번짐 0.5, 기울기 18도, 이동 0.8초+대기 1.5초다. 이동 폭은 텍스처 한 변을 1로 하는 UV 단위이며 스윕 시간은 전투 배속과 무관하다.

- 빛띠가 텍스처 왼쪽→오른쪽으로 지나가며 **원본의 파란 테두리에서만** 밝아진다. 사각 둘레를 순환하는 효과가 아니다. 중앙의 상태 색과 금색 외곽선은 그대로다. 현재 네 텍스처의 B>G>R 색상 특성을 이용하므로 별도 마스크 이미지와 SVN 복사는 없다. 향후 테두리 색이 바뀌면 셰이더의 색상 마스크를 다시 검토한다.
- 전투 전용 `BattleTile_CurrentTurn/Selectable/ConfirmedArea/Unavailable.mat` 네 개만 `JC/Battle/CellBorderSweep` 셰이더로 변경했다. `BattleTile_Clear.mat`는 그대로다. 스프라이트 참조·메시·씬 Transform·타깃 판정 변경은 없다.
- 새 파일은 `JC/BattleUI/CellBorderSweep.shader` 하나이며 기존 `BattleCellVisualSettings.cs`를 확장했다. 새 런타임 스크립트/오브젝트는 없다. 기본 재질은 스윕이 꺼져 있고 연결된 CellBox가 12개 Plane에만 설정/시간을 전달한다. 새 C# 필드의 기본값으로 기존 씬에 적용되므로 씬 파일 재저장은 필요하지 않다.
- 글로우는 파란 영역 안에서 부드럽게 번지는 밝기 표현이며 카메라 블룸 추가가 아니다. 셀 불투명도도 함께 적용되어 알파 0이면 빛도 보이지 않는다. CellBox의 `Use Sweep Glow`를 끄면 기존 셀 색만 표시한다.
- 검증: 코드/URP 셰이더 컴파일, 네 종류 셀의 효과 끔/켬 렌더 비교, 비플레이 31항목(실제 씬 기본값·12개 참조·효과 끄기·알파 0·공유 재질 보존·속성 복원) 통과. 실제 전투 플레이는 미실행이다.

## 7. 유지보수 시 찾아갈 파일

| 확인하려는 내용 | 위치 |
|---|---|
| 버튼 배치·이미지·씬 연결 | `Assets/_ProtoType_Merge/Scenes/TmpBattleScene.unity` |
| 전투 메뉴 버튼 연결 | `Assets/_ProtoType_Merge/JC/Scripts/UI/Battle/BattleMenuButton.cs` |
| 자동·배속·도주·토글 기능 | `Assets/_ProtoType_Merge/KJ/Scripts/UI/`의 `AutoBattleToggleButton.cs`, `BattleSpeedToggleButton.cs`, `RunButton.cs`, `ToggleButton.cs` |
| 전투 스킬 사용 가능 효과·선택 테두리 | `Assets/_ProtoType_Merge/JC/Scripts/UI/Battle/BattleSkillButtonVisual.cs` (상단 재개분 참조) |
| 공통 스킬 효과·셀 투명도 설정 | 같은 폴더의 `BattleSkillSweepSettings.cs`, `BattleCellVisualSettings.cs` |
| 공용/기존 호버 효과·선택 테두리 | `Assets/_ProtoType_Merge/JC/Scripts/UI/`의 `ButtonEffectController.cs`, `ButtonEffectActiveToggle.cs`, `HoverOverlayTint.cs`, `SelectedSkillHighlight.cs` |
| 메뉴·일시정지 | 같은 `JC/Scripts/UI/`의 `SystemMenuController.cs`, `Modal.cs`, `ModalPauseGate.cs` 및 `Assets/Resources/CommonUIManager.prefab` |
| 현재 하단 정보·히어로 4슬롯/코어 선택 | `Assets/_ProtoType_Merge/KJ/Scripts/UI/`의 `HeroInfoPanel.cs`, `SkillButtonController.cs`, `SkillButtonTooltip.cs` |
| 기존 스킬 아이콘 | `Assets/_ProtoType_Merge/JC/Scripts/UI/SkillButtonIcon.cs` |
| 빌런 호버 정보창 | `Assets/_ProtoType_Merge/JC/Scripts/UI/Battle/BattleEnemyInfoTooltip.cs` |
| 공용 스킬 설명 | `Assets/_ProtoType_Merge/JC/Scripts/UI/Lobby/`의 `SkillTooltip.cs`, `ClassSkillTooltipText.cs`, `WeaponTooltipText.cs` |
| 턴 순서·전투 UI 연결 | `Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/`의 `TurnOrderUI.cs`, `TurnSlotUI.cs`, `BattleUIManager.cs`, `BattleFlowManager.cs` |
| 시작·턴 팝업 표시 | `Assets/_ProtoType_Merge/JC/Scripts/UI/Battle/BattleTurnBanner.cs` |
| 결과창 카드·연결 | `Assets/_ProtoType_Merge/KJ/Scripts/UI/`의 `BattleResultPanel.cs`, `BattleResultView.cs`, `HeroInfoResult.cs` |
| EXP 미리보기 | `ASB/Scripts/Persistence/`의 `BattleRewardPlan.cs`, `BattleResultPersistenceHandler.cs` 및 `DH/Scripts/Manager/Persistence/PersistentUnitRepository.cs` (모두 `Assets/_ProtoType_Merge/` 아래) |
| 수동 타겟팅 입력 | `Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs` |

폴더 이름이나 Git의 최종 수정자만으로 현재 편집 담당을 확정하지 않는다.

## 8. 합의된 개편 방향과 아직 적용하지 않은 부분

2026-09-15 기준 합의된 1~7단계 고정 UI 구현이 모두 적용되어 있다. 마지막 7단계인 히어로·코어의 고정 설명, 호버/선택/안내 우선순위, 적 턴 임시 문구, 공용 호버 호출 분리는 비플레이 검증을 마쳤으며 사용자 플레이 검수가 남아 있다. 툴팁 전용 문구 작성과 이벤트 적 원본의 잘못된 3번 스킬 정리는 이후 작업이다. 결과창·시작/턴 팝업은 후속 구현까지 완료하여 플레이 검수를 기다린다. 최종 삭제 정리는 계속 보류한다.

| 구역 | 현재 상태 | 합의된 다음 형태 |
|---|---|---|
| 1. 우상단 네 버튼 | 개편·사용자 플레이 검수 완료 | 현 기능 유지, 효과 품질 개선은 후속 |
| 2. 좌상단 턴 순서 | 개편·사용자 작동 검수 완료 | 배경 리소스 교체·관련 Transform은 사용자 후속 작업 |
| 3. 하단 전체 배경 | 배치·사용자 검수 완료 | 구역별 내용 연결 |
| 4. 하단 좌측 정보 | 새 영역 이관·비플레이 검증 완료 | 사용자 플레이 검수 대기, 랭크는 임시 배치 |
| 5. 코어스킬 | 개편·사용자 작동 확인 완료 | 코어 종류별 아이콘 미결선 |
| 6. 히어로 스킬 | 4슬롯·매 시전 수동 선택 구현, 비플레이 검증 완료 | 사용자 플레이 검수 대기, 현재 연결 스킬 외 실행 미결선 |
| 7. 하단 우측 설명 | 고정 영역 연결·비플레이 검증 완료 | 사용자 플레이 검수 대기, 전용 설명 문구는 후속 |
| 적 정보 툴팁 | 사용자 출력 확인, 텍스트 자동 크기 보완·비플레이 검증 완료 | 자동 크기 보완분 사용자 검수, 설명 문구·데이터는 후속 보고서 참조 |
| 결과창 | 선배치 4카드·EXP/랭크/IP 연결·비플레이 검증 완료 | 전투 승패/모의·탐사 기존 결과창 플레이 검수 |
| 시작/턴 팝업 | 시작 1초·매 턴 2초 이동/페이드·클릭 시 최대 0.3초 퇴장·스킵 순번 표시 구현 | 블러/속도선 초안과 클릭/자동/배속/일시정지 체감 검수 후 기획 조정 |

- 새 고정 UI는 Scale=1 및 RectTransform 직접 배치를 사용한다. 자동 LayoutGroup 및 런타임 배치 덮어쓰기는 제거한다. 보존하는 옛 UI 원형, 월드 HP바, 공용 Canvas의 엔진 관리 스케일과 구분한다.
- 기존 이름·HP·IP 오브젝트는 교체 후 씬에 비활성으로 남겨 비교한다. 새 표시와 옛 표시의 제어가 동시에 실행되지 않아야 한다.
- 적 턴의 우측 설명은 우선 **'적 행동 중'**만 표시한다. 실제 적 스킬 데이터·시전 시작 이벤트 연결은 보류하며 `BattleManager.cs`는 이 개편의 편집 대상에서 제외한다.
- 스킬 선택 안내: 제목 **'스킬 선택'**, 본문 **'사용할 스킬 버튼 또는 코어 스킬을 클릭하세요.'**
- 잠금은 획득 요구 레벨과 캐릭터 레벨로 판단한다. 기본 스킬과 그 강화판은 같은 표시 슬롯을 사용한다. 레벨 잠금과 실행 미결선은 구분하며, 미결선 기능은 UI 작업 중 새로 구현하지 않는다. 잠긴 버튼에는 호버 설명을 추가하지 않는다.
- 강화 단계 불일치, 스킬 결선, 아군 초상화 호버, 필드 클릭 시전은 구현 범위에서 제외/보류한다. 단축키도 별도 합의 없이 개편하지 않는다.
- 빌런 툴팁은 공용 SkillTooltip과 별개로 전투 Canvas에 미리 배치하며 DDOL로 만들지 않는다. 패널·정보·호버 및 선택/행동 중 숨김을 구현했다. 최신 합의는 스킬 선택 중·공격 실행 중 숨김, 빌런 등급 공란, 최대 5슬롯이다. 슬롯 위치는 에디터에서 고정하고 표시 개수에 따라 배경 높이만 코드로 조절한다. 제목 배경·구분선·HP 게이지는 기존 히어로 정보창의 자산·구성을 최대한 재사용한다. 데이터 및 남은 상태 연결 확인 사항은 별도 보고서에 정리한다.
- 공유 UI와 다른 팀원의 영역은 사용자 확인 없이 범위를 넓혀 수정하지 않는다. KJ/JC의 UI 코드와 ASB의 전투 코드가 연결되어 있다. 현재 합의 밖의 신설 스크립트는 필요성과 역할을 먼저 검토받는다.
- 삭제는 최종 검증 이후 진행한다. 다른 씬에서 쓰는 스크립트·프리팹은 사용 씬 개편/미사용 씬 정리 이후 다시 참조를 확인한다. `BattleSimulationScene_Legacy`의 사용 여부를 이름만으로 판단하지 않는다.

## 9. 검수와 문서 갱신 기준

- 우상단 버튼: 에이전트의 컴파일·씬 참조·저장본 재열기·비플레이 배치 확인 완료. 이어서 사용자가 플레이모드에서 정상 동작 및 현재 요구 조건 충족을 확인했다.
- 턴 순서: 저장본 재열기, 다섯 슬롯 참조, Scale=1·자동 정렬 비활성, 슬롯 중복 생성 방지, 1유닛 순서의 다섯 칸 반복, 빈 순서 숨김, 기존 씬 생성 경로를 비플레이 환경에서 확인했다. 편집 모드 미리보기 확인 완료. 이후 사용자 배치 수정에 맞춰 현재 턴을 패널 고정 표시로 이관하고 다섯 슬롯 크기를 맞췄으며, 저장본에서 슬롯별 표시 참조가 모두 해제되어 있는지 확인했다. 에이전트는 플레이모드를 실행하지 않았으며, 이후 사용자가 작동 확인을 완료했다.
- 하단 배경: 저장본 재열기, 배경 연결·구역 구조·1920×244 크기·Scale1·자동 배치 및 신규 동작 스크립트 없음, 미리보기 확인 완료. 플레이모드는 실행하지 않았다.
- 하단 정보: 저장본·컨트롤러 단일 활성·Scale1·초상화 중복생성 방지·현재 유닛 초기화·HP/IP 이벤트·적 초상화·턴 전환 구독 해제·빈 유닛 초기화·기존 IP 형식 유지 확인. 미리보기 확인 완료. 플레이모드는 실행하지 않았으며 사용자 검수 대기다.
- 코어스킬 출력: 저장본 이관·동일 toggle2 참조·토글 클릭과 선택 표시·Scale1·미리보기 확인 완료. 이후 사용자가 코어스킬 동작을 확인했다. 6단계에서 함께 바뀐 선택 해제 동작은 이번 플레이 검수 대상이다.
- 히어로 스킬: 컴파일, 저장본의 4슬롯 참조·첫 버튼 재사용·Scale1·자동배치 없음, 미리보기 확인 완료. 격리된 PreviewScene에서 현재 데이터의 4캐릭터 Lv1 잠금/연결, 강화판 같은 슬롯, 기존 연결 기본판 보존, 미결선 표시, 선택/코어 전환/취소 동기화, 턴 시작 미선택, 자동전투·시전 중 재선택 차단, 레벨 미달 단축키 차단, 적 턴 비활성, 이벤트 구독 해제를 확인했다. 프로젝트에 테스트 스크립트를 추가하지 않았다. 실제 대상 클릭·시전·턴 진행을 포함한 플레이 검수는 아직 수행하지 않았다.
- 동일 캐릭터의 다음 턴에서 스킬 선택이 해제되는 동작은 사용자가 플레이모드에서 확인했다.
- 버튼 전환·우클릭 취소: Unity 컴파일 및 격리된 PreviewScene의 비플레이 검증 43항목 통과. 저장된 5개 PointerClick 참조, 비활성 버튼의 실제 이벤트 전달, 오른쪽/활성 클릭 중복 처리 방지, 선택 거절 시 이전 선택 제거, 공격 실행 중·자동전투 입력 차단과 표시 유지, 턴 경계 강제 초기화, IP·장착·현재 행동 유닛 불변을 확인했다. 씬 변경은 5개 EventTrigger와 부착 참조뿐이며 기존 모든 Transform/RectTransform 및 나머지 오브젝트 블록은 동일하다. 새 프로젝트 테스트 파일은 없고 검증 코드는 저장소 밖 임시 폴더에 있다. 이후 사용자가 선택 취소 기능의 작동 확인을 완료했다. 이 확인을 별도 ASB 전투 정지 버그의 해결 확인으로 확대하지 않는다.
- 빌런 정보창: Unity 컴파일, 저장본 참조·비플레이 분리 환경 37항목 확인, 2행·5행 렌더링 확인 완료. 0~5행 높이/고정 위치, 실제 HP·능력치 표시, 선택/전투 종료 숨김, 슬롯 유닛 참조를 확인했다. 기존 씬 블록은 Canvas의 새 컴포넌트 참조와 자식 목록만 바뀌었고 기존 RectTransform 값은 모두 보존했다. 실제 마우스 호버·전투 상황은 사용자 플레이 검수 대기다. 이어서 승인받은 BattleFlowManager 상태 조회를 연결하고 관련 비플레이 20항목을 통과했다. 기존 점유 API/완료 이벤트, 적 코루틴 반환·dispose·빈 대상, 루프 중지/비활성/전투 종료 정리, 선택 없는 공격 중 숨김을 확인했다. 실제 적 AI·반격 애니메이션을 실행한 플레이 검증은 아니다. 검증 코드는 저장소 밖 임시 폴더에 두었다.
- 빌런 툴팁 출력은 이후 사용자가 정상 동작을 확인했다. 텍스트 넘침 피드백에 따라 20개 TMP의 자동 크기·줄바꿈·말줄임·내부 여백만 보완했다. 긴 이름/스킬명, 큰 능력치 숫자, 긴 설명 및 극단적으로 긴 문장의 비플레이 렌더링과 텍스트 영역 내 배치를 확인했다. 기존 모든 RectTransform·오브젝트·문구·스크립트 참조는 보존했다. 보완분 플레이 검수는 사용자 대기다.
- 하단 고정 설명: Unity 컴파일 및 비플레이 38항목 통과. 저장본 참조, 사용 가능/잠금 버튼 호버, 히어로↔코어 선택 복귀, 취소 후 안내, 호버한 채 턴 전환, 적 턴·종료·비활성 정리, 이벤트 구독 해제를 확인했다. 실제 카탈로그의 4캐릭터 기본 스킬·안내·코어·적 턴·긴 문장의 렌더링을 확인했다. 기존 씬의 변경 블록은 컨트롤러 참조·다섯 버튼의 고정 설명 참조·설명 부모 자식 목록뿐이며 기존 모든 RectTransform 값은 보존했다. 마지막 파일 비교에서 Unity 검증 중 NotoSansKR-Light SDF.asset의 자동 저장 변경을 감지했다. 이 폰트는 작업 전에도 수정 상태였으므로 전체 원복하지 않았다. 나머지 무관한 작업 파일의 해시는 동일하다. 에이전트는 플레이모드를 실행하지 않았으며 사용자 검수 대기다. 결과창·턴 팝업은 이후 별도 구현·검증을 완료했다(위 현재 구조 참조).
- 다음 작업마다 해당 구역의 오브젝트 경로, 실제 부착 컴포넌트, 데이터 출처, 이벤트 흐름, 생성/비활성화 방식, 공용 의존성을 갱신한다.
- 구현된 내용은 '현재 구조' 본문에 반영하고, '미적용' 표에서도 상태를 바꾼다. 과거 구조를 현재 구조 옆에 계속 누적하지 않는다. 비교용으로 실제 보존 중인 오브젝트는 예외적으로 명시한다.
- 검수는 에이전트 확인과 사용자 플레이 확인을 구분한다. 예정·보류 기능을 실제 동작처럼 설명하지 않는다.
- 최종 개편 완료 후 남은 사용 씬·프리팹·스크립트를 대조하고 정리 결과를 반영하여 이 문서를 완성한다. 대화 기록이나 로컬 scratchpad 파일을 필수 지식으로 요구하지 않는다.

## 문서 저장 위치와 단계별 기록

후속 결과창·전투 시작/턴 팝업의 검토는 [구현 검토 문서](20260915-battle-results-turn-review.md)를 참조한다. 해당 문서 1~7절은 검토 이력, 8절은 승인 후 실제 구현·검증 기록이며, 현재 실제 구조는 본문을 우선한다.

2026-09-15 사용자 결정: JC의 구현 현황·분석·재확인·인수인계 레포트는 프로젝트 루트가 아닌 `Assets/_ProtoType_Merge/JC/Docs/`에 저장한다. 이번 전투 UI 작업의 단계별 기록도 같은 폴더에 모았다.

현재 구현은 이 문서, 미결정·후속 항목은 [BattleSceneUI_FollowUps.md](BattleSceneUI_FollowUps.md)를 기준으로 읽는다. 아래 문서는 각 작성 시점의 이력이며, 폐기된 초기안이나 이후 변경된 상태를 현재 사양으로 적용하지 않는다.

- [전투씬 UI 개편 구현 준비·진행 — 2026-09-11](20260911-battle-ui-preparation-d692d3d44f.md)
- [전투 UI 재시작 준비 — 기존 구조·기여 이력·작업 경계](20260912-battle-ui-restart-7a50f3e001.md)
- [하단 현재 행동 유닛 정보 UI](20260913-battle-actor-3672ec1bbd.md)
- [전투씬 하단 그래픽 레이아웃](20260913-battle-bottom-77a263838e.md)
- [전투씬 우상단 네 버튼 — 1번 단위 구현](20260913-battle-top-buttons-fb21da9279.md)
- [턴 순서 UI 개편](20260913-battle-turn-order-a44d1b6ce4.md)
- [전투씬 UI 개편 합의 수정 및 정리 후보](20260913-battle-ui-plan-review-2da3cc57f2.md)
- [코어스킬 버튼 출력부](20260914-battle-core-319659722e.md)
- [빌런 툴팁 구현 상태](20260914-battle-enemy-tooltip-8f232847bc.md)
- [히어로 4슬롯 UI — 20260914-d43bebd1e8](20260914-battle-hero-skills-d43bebd1e8.md)
- [전투 스킬 선택 취소 구현](20260914-battle-skill-cancel-0b124ee881.md)
- [전투씬 하단 우측 고정 설명 — c084bd161c](20260915-battle-description-c084bd161c.md)
- [반격으로 적 사망 후 전투가 멈추는 문제 — ASB 인수인계](20260914-ASB-counterattack-battle-stall.md)
