# 전투 UI 후속 구현 검토 — 결과창·시작/턴 팝업

확인: 2026-09-15. **결과창·시작/턴 팝업은 사용자 승인 후 구현·비플레이 검증 완료, 플레이 검수 대기다.** 현재 구현 정본은 [BattleSceneUI.md](BattleSceneUI.md), 미결정 사항은 [BattleSceneUI_FollowUps.md](BattleSceneUI_FollowUps.md)다. 아래 1~7절은 검토 시점의 분석·제안 이력이며, 최신 승인·구현 결과는 8절과 현재 구현 정본을 우선한다.

## 1. 기획서 확인과 비교 범위

- 현재 원본: `D:/SVN/2_Documents/강현재/알파2 필요 업무/H.I 알파2 UI 개편 기획서 V3.91.xlsx`.
- 사용자 메시지의 `2/_Documents` 대신 실제 폴더는 `2_Documents`다. 이번 확인에서 파일을 찾아 원본을 읽기만 했다.
- 비교 원본: `D:/SVN/2_Documents/강현재/이전 문서/H.I 알파2 UI 개편 기획서 V3.9.xlsx`.
- 두 문서의 셀 값/수식은 동일했다. 전투 씬 개편안의 도형 XML과 참조 이미지 내용도 동일했다. V3.91에서 협회 씬 개편안에 그림 객체 하나가 추가된 것은 확인했지만 전투 UI의 신규 요구사항으로 포함하지 않는다.
- 문서 개요의 변경 이력은 V3.9까지만 기재되어 있다. 이번 비교가 이전에 읽었던 모든 버전이나 같은 파일명의 모든 수정 이력까지 증명하는 것은 아니다.
- 전투 요구의 상당 부분이 셀이 아닌 **그림과 도형 텍스트**에 있다. 셀 문구만 추출해서 누락 여부를 판단하면 안 된다.
- 읽기 전후 두 XLSX의 SHA-256 동일 확인. 추출 파일과 검증 스크립트는 저장소 밖 scratchpad에 둔다.

## 2. 현재 구현과 대조한 남은 항목

위치는 `전투 씬 개편안` 워크시트의 행/도형 배치 기준이며, 도형 텍스트를 해당 셀 값이라고 뜻하지 않는다.

| 항목 | 기획 위치·내용 | 현재 상태 / 제안 |
|---|---|---|
| 전투 시작 표시 | 69~116행, C84 부근 도형: 중앙에 전투 시작 UI, 1초 후 제거 | 고정 7단계 밖에서 빠져 있던 별도 시작 배너. 턴 팝업과 함께 설계 |
| 매 유닛 턴 표시 | D84 부근 도형: 상단 중앙 플레이어 턴/빌런 턴, 1초 후 제거 | 좌상단 현재 턴 표식/필드 화살표와 다른 UI. 같은 진영이 연속 행동해도 유닛마다 새로 표시 |
| 도주 확인 팝업 | 91~116행 이미지·101행 설명 | RunButton은 RequestFlee를 즉시 호출. 현재 확인창 없음. 사용자 확인 뒤 기존 도주 요청을 호출하도록 별도 작업 필요 |
| 선택 불가 안내 | 221행 우측 도형: 선택 불가 타일 클릭 시 커서 좌측, 공간 없으면 우측에 안내 1초 | 입력은 무효 대상을 무시하며 해당 안내 UI는 없음. 클릭 판정 연결에는 InputHandler 담당 확인 필요 |
| 스킬명 말풍선 | 248~269행: 유효 대상 클릭 후 스킬명 말풍선, 스킬 발동 | 현재 고정 설명 패널이 대신하는 기능이 아님. 말풍선만의 후속 표시 연결 필요. 카메라/대화 연출은 기존 보류 유지 |
| 승리/패배 결과 | 293~333행: 제목·초상화·랭크·EXP 게이지/획득량·전후 IP·확인 | 기존 결과 기능은 있으나 개편 레이아웃·게이지·랭크 아이콘·전후 IP 표시는 미적용 |
| 현재 턴/타깃 강조 | D30, 69~84, 178~242, 265~286행 | 기존 ASB 타깃 표시가 있으므로 기능이 전혀 없다고 판단하지 않는다. 행동 종료까지 유지, 복수/전체/관통의 실제 표시 등은 다음 전투 검수 항목 |
| 적 정보 호버의 타일 강조 | 153행 도형: 적 정보 표시 시 노랑 타일로 대상 표시 | 현재 BattleEnemyInfoTooltip의 패널 출력과 별개. 호버 대상 표시까지 구현되었다고 간주하지 않으며 현재 행동 타일과의 우선순위 검토 필요 |
| 카메라·필드 상태정보 | D28, D33: 시야·필드 HP/상태정보 개선 | 고정 7단계의 완성 범위와 별개. 기존 HP바 유지 및 사용자의 카메라/Transform 조정 합의를 우선하고 임의 재구현하지 않음 |

기존 사용자 합의가 원본 예시보다 우선한다. 선택 가능 대상은 임시 연두색, 전체 공격은 빈 타일까지 빨강, 취소는 다른 스킬 버튼/우클릭이다. 기획의 보라 타일·타일 외 클릭 취소·하단 패널 클릭 취소를 이번 검토만으로 복구하지 않는다. 연출/데이터 결선/강화 불일치도 자동으로 구현 범위에 넣지 않는다.

## 3. 결과창의 현재 흐름과 유지해야 할 경계

```text
BattleSceneManager.PostBattleSequence
  → 남은 연출·사망 처리 대기
  → BuildBattleRewardPlan: 저장하지 않고 결과 미리보기 계산
  → BattleUIManager.ShowBattleResultUI(result, plan)
      → 씬의 ResultPanel 부모 활성화
      → 승리/패배 공용 프리팹 Instantiate
      → BattleResultPanel.Show
          → 캐릭터 결과 카드 생성
          → 스킬 획득/교체 창 순차 처리
          → 결과 내용 및 확인 버튼 노출
  → OnAccepted 대기
  → GetSkillResults → CommitBattleRewardPlan
  → 탐사/반환 씬으로 전환
```

- `Show(result, plan)`, `OnAccepted`, `GetSkillResults()`는 외부 연결 계약으로 유지하는 것을 권한다. UI 표시만을 위해 보상을 미리 저장하거나 확인 시점의 저장을 두 번 실행하면 안 된다.
- 모의 전투는 plan=null로 결과 UI를 호출한다. null plan을 모두 오류로 바꾸면 이 경로가 깨진다. 일반 전투의 정상 plan, 빈 파티, 모의 전투를 구분해야 한다.
- `VictoryResultPanel`/`DefeatResultPanel`은 내용 없는 `BattleResultView` 파생 클래스다. 별도 승패 계산을 하고 있지 않으며 단순히 파일 수만으로 삭제하지 않는다.
- **같은 공용 프리팹은 DH의 CombatPromptService가 탐사 중 전투 생략 승/패 및 도주 실패에 사용한다.** 프리팹 원본을 수정하면 탐사도 함께 바뀐다. 이번에는 전투씬 인스턴스만 개편하고 공용 사용처 이관은 별도 협의한다.
- 현재 RequestFlee는 결과를 Defeat로 요청한다. 도주 확인창 추가를 이유로 이를 Escape로 변경하거나 보상/귀환 정책을 바꾸지 않는다.

### 화면 부분의 리팩토링 필요성

1. 결과 패널뿐 아니라 카드도 실행 중 Instantiate/Destroy하므로 편집 모드에서 실제 결과 레이아웃을 확인하기 어렵다.
2. HeroInfoResult.Apply가 Portrait_Image를 매번 생성하고 Scale=0.75로 보정한다. 기존 HeroInfo 프리팹에도 글자/초상화의 비균등 Scale이 있다. 승/패 카드 변형은 이 원본을 상속한다.
3. 승/패 HeroIndex에는 GridLayoutGroup이 있다. 직접 배치하려면 전투씬 인스턴스에서 자동 정렬을 사용하지 않아야 한다.
4. BattleResultPanel.Show는 확인 버튼에 익명 listener를 매번 추가하고 해제하지 않는다. 지금은 매번 생성하는 구조지만 선배치 인스턴스를 재사용하면 중복 호출을 막도록 바꿔야 한다.
5. 스킬 획득창을 띄우기 위해 직계 자식 계층을 순회하여 결과 내용을 숨긴다. 명시적인 ResultContent/SkillSelectionHost 참조로 나누면 계층을 수정해도 숨김 범위를 추적하기 쉽다.

### 권장 씬 구조와 파일 범위

```text
BattleSceneCanvas
├─ ResultPanel                     기존 부모 활용, 초기 비활성
│  ├─ Dim                          결과 표시 중 배경 입력 차단
│  ├─ ResultContent
│  │  ├─ Background / ResultTitle  승리·패배 Sprite 교체
│  │  ├─ HeroSlots
│  │  │  └─ Slot_1 ... Slot_4      4인 고정, 선배치
│  │  │     └─ Portrait / Rank / ExpFill / GainedExp / IpBeforeAfter
│  │  └─ Confirm
│  └─ SkillSelectionHost           기존 순차 스킬 선택 경로 유지 후보
└─ BattleNotices                   항상 활성인 제어 위치
   ├─ BattleStartBanner            중앙, 초기 비활성
   └─ TurnBanner                   상단 중앙, 초기 비활성
```

모든 배치/크기는 씬의 RectTransform으로 조절한다. Scale=1, 자동 LayoutGroup 없음. 코드가 자식 위치를 덮어쓰지 않는다. 빈 카드·빈 데이터는 기존 내용이 남지 않게 지우고 숨긴다. 표시 예시를 위한 별도 런타임 데이터나 테스트 파일은 추가하지 않는 방향이다.

| 파일/자산 | 제안하는 변경 | 확인할 경계 |
|---|---|---|
| TmpBattleScene.unity | 결과 내용·카드·초상화·게이지/아이콘·배너를 선배치 | 기존 사용자 배치 보존, 삭제 후보는 기록 |
| BattleUIManager.cs | 연결된 씬 결과 인스턴스가 있으면 이를 사용; 미연결 기존 씬은 기존 프리팹 경로 유지 | ASB 영역. 수정 전 확인 필요 |
| BattleResultPanel.cs | 기존 외부 계약 유지, 선배치 카드 처리, 반복 Show 정리, 확인 callback 중복 방지 | KJ 공용 코드. 탐사 사용처 회귀 확인 필요 |
| BattleResultView.cs | 결과 내용 부모·선배치 카드 참조를 명시 | KJ 공용. 기존 프리팹 미연결 경로 유지 |
| HeroInfoResult.cs | 선배치 Image/TMP/게이지 참조로 표시, 기존 생성 경로는 아직 사용하는 씬에만 유지 | KJ 공용. 공용 원본 자산 직접 개편은 별도 |
| 결과 공용 프리팹 | 이번 전투씬 개편에서 원본은 보존 | 탐사 사용처 이관은 나중에 별도 진행 |
| 성장/보상 코드 | EXP 결과 미리보기 데이터 제공 방식 검토 | ASB/DH 담당과 별도 확인, UI에서 성장 계산 복제 금지 |

권장안은 결과용 신규 스크립트 없이 기존 네 파일을 좁게 수정하는 것이다. 다만 아직 확정/승인된 변경 목록은 아니다. 공용 미연결 경로는 기존 사용처를 위해 보존하며, 모든 씬 이관 완료 후 정리 대상으로 남긴다.

### 기존 리소스와 부족한 데이터

- 프로젝트에 `Assets/_Ui_Sprites/BattleScene/Result/`의 UI_box_main / victory / defeat / characterFrame / profilerFrame / IP / button_confirm이 있다.
- `Assets/_Ui_Sprites/Icon/Bar/UI_bar_fill(Exp).png`, UI_bar_frame, Rank 폴더의 랭크 아이콘, 기존 IP 아이콘을 재사용할 수 있다. 이 범위는 SVN 추가 복사 없이 진행 가능하다.
- UnitRewardPreview에는 UnitIndex, OldLevel, NewLevel, GainedExp, OldInfluence, NewInfluence가 있다. 전후 IP와 획득량 표시는 바로 연결할 수 있다.
- **현재 EXP/필요 EXP 및 증가 후 잔여 EXP/다음 필요 EXP는 preview에 없다.** UnitPersistentData.Exp/MaxExp는 있지만 SimulateFinalLevel은 레벨만 반환한다. 여러 번 레벨업했을 때의 게이지를 단순 `(현재 EXP + 획득량)/현재 MaxExp`로 표시하면 틀린다.
- 사용자 방향: 담당자 협의 후 EXP/랭크 연결까지 함께 진행한다. 기존 성장 계산 담당에서 저장 없이 계산한 전후 EXP/분모를 제공하고 UI는 그 값만 표시한다. 게이지를 그리려고 먼저 보상을 Commit하지 않는다.
- 랭크는 UnitRankLookup으로 기존 성장 테이블의 NewLevel 값을 조회할 수 있으나 기획의 등급별 NeedExpieriencePoint 정의와 일치하는지 확인해야 한다. 데이터의 랭크가 없거나 이미지가 없으면 임의 F로 치환하지 않는다.
- 사용자 확정: 플레이어 파티는 4인 고정이다. 결과 카드는 4개만 선배치한다. PartyFormation.SlotCount=6을 파티 정원으로 해석했던 이전 제안은 철회한다. 전후열 위치를 다루는 공용 PartyFormation의 6칸 구조는 이번 UI 작업에서 변경하지 않는다. 실제 입력이 4명을 넘는다면 기획/데이터 불일치로 확인하며 6인 UI를 추가하지 않는다.
- 현재 스킬 획득/교체 창은 결과보다 먼저 표시된다. 네 스킬 습득 방식과 충돌할 여지가 있어도 이 UI 작업에서 제거·자동 확정하지 않는다. 우선 기존 순서를 유지하는 것을 권한다.

## 4. 매 유닛 턴 팝업 구현 계획

기획: 플레이어 턴/빌런 턴, 상단 중앙, 1초 후 자동 제거. `UI_box_turnbox(blue/red).png`를 원래 용도인 턴 팝업에 사용한다. 좌상단 TurnOrderPanel 배경의 사용자 교체 작업과 혼용하지 않는다. 전투 시작 배너는 중앙이며 같은 제어 스크립트에서 다룰 수 있다. 시작 제목 전용 자산을 추가 복사하기 전에 기존 배너 자산+TMP로 충족할지 정한다.

신규 스크립트 제안은 **BattleTurnBanner.cs 1개**다. 씬의 배너 참조·표시 시간·표시/숨김을 소유하고 결과 계산·턴 순서 계산·전역 DDOL은 소유하지 않는다. 추가 생성 전 사용자 검토 대상이다.

### 이벤트 연결에서 주의할 부분

- 기존 OnTurnStarted는 상태이상 처리가 끝나고 실제 행동할 수 있는 유닛에서만 발행된다. 기절·턴 시작 피해로 사망하여 건너뛴 순번에는 발행되지 않는다. 이를 그대로 사용하면 '모든 순번'을 표시하지는 못한다.
- 같은 진영의 연속 턴도 매 이벤트 다시 표시해야 한다. 진영이 바뀌었을 때만 표시하는 lastTeam 비교는 사용하지 않는다.
- 사용자 지정 임시 사양: 아군·적 모두 **1초 경과 또는 표시 후 게임 화면 좌/우클릭 중 먼저 발생한 조건으로 닫고 나서 행동을 시작**한다. 비차단 표시는 이번 계획에서 제외한다. 실제 체감은 구현 후 기획자와 검토한다.
- 팝업 종료 클릭은 팝업을 닫는 데만 소비한다. 같은 클릭이 하단 버튼·필드 공격으로 전달되지 않도록 하고, 대기 중 키보드 스킬 선택·턴 스킵도 막는다. 팝업이 열리기 전부터 누르고 있던 버튼으로 즉시 닫히지 않게 한다.
- **배너 종료 → 상태이상/건너뛰기 판정 → 기존 행동 가능 이벤트/자동전투 시작** 순서를 제안한다. 기존 OnTurnStarted의 상태이상 처리 이후라는 의미는 바꾸지 않는다. ASB와 실제 대기 지점을 협의한다.
- AcquireFlowLock을 기존 OnTurnStarted 콜백에서 바로 걸기만 하는 방식은 권하지 않는다. AutoBattleController도 같은 이벤트로 코루틴을 시작하고, 기본 0.5초 thinkDelay 뒤 TryClaimPlayerAction이 잠금 때문에 실패하면 재시도하지 않고 종료한다. 1초 팝업 잠금과 겹치면 아군 자동 턴이 행동하지 못할 가능성이 있다. 정적 코드에서 확인한 충돌 가능성이며 플레이 재현을 수행한 것은 아니다.
- 사용자 확정: 건너뛴 순번에도 팝업을 표시한다. 기절·턴 시작 피해 사망 판정 전에 별도 표시/대기 지점을 두고, 종료 후 기존 건너뛰기 처리를 계속한다. 팝업 종료가 기절 유닛의 행동 허용을 뜻하지 않는다. 이미 사망하여 순번에 들어오지 않는 유닛까지 새 순번을 만들지는 않는다. 기존 OnTurnStarted를 상태이상 처리 앞으로 옮기면 과거 기절 자동행동 문제가 재발할 수 있다.
- AutoBattleController는 OnTurnStarted 외에도 자동 모드 켜기에서 코루틴을 시작한다. 따라서 Flow 대기뿐 아니라 이 진입도 보호해야 한다. InputHandler는 Update와 버튼이 호출하는 선택 진입을 함께 보호한다.
- 전투 시작 배너는 첫 유닛 턴 배너보다 먼저 표시되어야 한다. 씬의 OnEnable만으로 시작하면 유닛/전투 초기화 전에 시간이 소모될 수 있으므로 전투 초기화 완료 지점을 기준으로 연결한다.
- 전투 종료/씬 비활성/튜토리얼·메뉴 중첩에서 표시와 대기 토큰을 정리한다. 시간 표시를 위해 Time.timeScale을 임의 변경하지 않는다. 배속과 일시정지 중 타이머 정책을 함께 정한다.

## 5. 담당자 협의용 수정 파일 — 2026-09-15 조건 반영

아래 담당 표기는 파일 소속 폴더 기준의 협의 대상이다. 현재 담당권이나 개인별 최종 책임자를 확정하는 뜻은 아니다. **기존 C# 10개, 신규 제안 C# 1개, 전투씬 1개**가 현재 예상 범위다. 추가 수정이 필요하면 적용 전에 별도로 확인한다.

사용자 추가 확인: **DH·ASB 영역은 이번 목록에 대한 협의를 마쳤다.** KJ 영역은 PR 중복과 타 씬 영향 검토 단계다. 이번 요청은 조회이며 구현 착수 지시로 확대하지 않는다.

| 담당 영역 | 저장소 기준 파일 | 필요한 변경 |
|---|---|---|
| ASB | `Assets/_ProtoType_Merge/ASB/Scripts/Persistence/BattleRewardPlan.cs` | UnitRewardPreview에 전후 EXP·필요 EXP 표시값 추가 |
| ASB | `Assets/_ProtoType_Merge/ASB/Scripts/Persistence/BattleResultPersistenceHandler.cs` | 성장 계산 결과를 미리보기에 담기. 보상량·확인 후 저장 시점 유지 |
| DH | `Assets/_ProtoType_Merge/DH/Scripts/Manager/Persistence/PersistentUnitRepository.cs` | 저장 없는 기존 EXP 시뮬레이션에서 최종 레벨뿐 아니라 잔여 EXP·다음 필요 EXP도 제공. 기존 호출 호환 유지 |
| ASB | `Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleUIManager.cs` | 씬 선배치 결과창 사용, 미연결 기존 사용처의 프리팹 경로 유지 |
| KJ | `Assets/_ProtoType_Merge/KJ/Scripts/UI/BattleResultPanel.cs` | 선배치 4카드 사용, 명시적 내용 부모, 재표시 초기화·확인 콜백 중복 방지 |
| KJ | `Assets/_ProtoType_Merge/KJ/Scripts/UI/BattleResultView.cs` | 선배치 카드/결과 내용 참조 추가 |
| KJ | `Assets/_ProtoType_Merge/KJ/Scripts/UI/HeroInfoResult.cs` | 초상화·랭크·EXP 게이지/획득량·전후 IP 출력, 새 경로의 런타임 초상화 생성/Scale 보정 제거 |
| ASB | `Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs` | 전투 시작/매 순번 배너 대기 연결, 건너뛰는 순번 포함, 기존 행동 이벤트 의미 보존 |
| ASB | `Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs` | 팝업 대기 중 선택/공격/우클릭 취소/스킵 입력 차단, 닫기 클릭의 후속 행동 전달 방지 |
| ASB | `Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/AutoBattleController.cs` | 팝업 대기 중 자동 모드 켜기 진입 보호, 닫힌 후 정상 행동 시작 보장 |
| JC 신규 제안 | `Assets/_ProtoType_Merge/JC/Scripts/UI/Battle/BattleTurnBanner.cs` | 씬 배너 참조, 표시 타이머·클릭 종료·정리만 담당. 생성 전 사용자 검토 대상 |
| 전투씬 | `Assets/_ProtoType_Merge/Scenes/TmpBattleScene.unity` | 4카드 결과창·배너·배너 대기 중 입력 차단 영역 선배치, 기존 사용자 RectTransform 보존 |

재사용하며 현재 수정 계획에 넣지 않는 파일: `UnitPersistentData.cs`(Exp/MaxExp 있음), `UnitRankLookup.cs`(레벨 기반 랭크 조회), `PartyFormation.cs`(배치 구조 보존), `BattleSceneManager.cs`(Show/OnAccepted/GetSkillResults 계약 보존), `BattleManager.cs`(편집 제외). 공용 결과 프리팹·탐사 결과 처리와 스킬 획득/교체창도 유지한다. 공용 코드의 추가 필드는 기존 경로에서 미설정이어도 기존 표시가 유지되어야 한다.

## 6. 권장 구현 순서와 남은 세부 사항

1. 위 파일 목록으로 담당자와 협의한다. 결과는 4인 고정, EXP/랭크는 협의 후 실제 연결까지 함께 진행하는 방향이다.
2. 결과창 그래픽/4카드를 씬에 배치하고, 합의한 성장 미리보기 API를 연결한다. 보상 계산 변경과 UI 표시 변경의 책임은 구분한다.
3. 기존 Show/OnAccepted/GetSkillResults 계약을 유지하며 실제 값·반복 표시 정리·확인 처리를 연결한다. 승/패/도주/모의 전투와 공용 탐사 경로를 검증한다.
4. 신규 배너 스크립트 범위를 확인하고, 배너를 씬에 배치한 뒤 시작/매 유닛 턴 대기를 연결한다. 좌/우클릭 종료 및 건너뛰는 순번을 포함한다.
5. 도주 확인, 선택 불가 안내, 스킬명 말풍선, 필드 타일 강조의 미반영 사항을 각각 별도 작은 작업으로 진행한다. 데이터/툴팁 전용 문구 정리는 기존 후속 목록을 유지한다.

남은 세부 정책 제안: 배너의 1초는 배속과 무관한 실제 1초로 하고, 메뉴/튜토리얼 일시정지 중에는 타이머도 멈추며 해당 모달 클릭을 배너가 가져가지 않는다. 이는 사용자 확정 사항과 구분한 제안이다. 클릭 종료는 이번에 지정된 턴 팝업에 적용하며 전투 시작 배너의 클릭 종료까지 자동 확대하지 않는다.

기존 스킬 교체창은 별도 변경 지시 전까지 유지하는 것으로 계획한다. BattleManager는 현 단계의 편집 대상이 아니다. DH·ASB는 위 목록의 협의 완료를 전달받았으며 KJ 공용 코드까지 승인된 것으로 확대하지 않는다.

## 7. KJ PR 중복 및 다른 씬 영향 확인 — 2026-09-15

### GitHub 실제 상태

- 조회 시 열린 PR은 0건. 최근 KJ의 [PR #90 — [chore]UI 리소스 변경](https://github.com/TeamNorthernSky/HeroInfluence/pull/90)은 2026-09-15 10:18:37 KST에 병합되었다.
- PR 변경 파일 **119개 전체**를 페이지별로 조회했다. head=`66cd3656ac91ace35c934f55200fd69fc4366fc7`, base 및 로컬 HEAD=`d16b2aad19f29c3ec70dd5bcb1f4fdf5830a2b3a`, 병합 후 main=`1507d7a97d59af628ad70916242dc45ad13e052d`.
- 수정 예정인 `BattleResultPanel.cs`, `BattleResultView.cs`, `HeroInfoResult.cs`는 PR 변경 목록에 없다. 세 파일은 로컬 미커밋 변경도 없고, 로컬 파일을 Git 정규화한 blob과 원격 main blob도 동일하다. **이 PR로 인한 세 파일의 직접적인 수정 충돌은 없다.** 아직 작성하지 않은 구현이나 이후 추가 커밋까지 충돌 없음을 보장하는 뜻은 아니다.
- TmpBattleScene도 PR 변경 목록에 없다. 로컬의 기존 UI 변경은 보존한다. PR에 포함된 DHScene_3는 사용자 지정 별도 DOF 대화에서 처리하며 여기서 편집하지 않는다.
- PR에는 전투 타일 Sprite 4개의 `.meta` 변경도 있으나 이번 결과창 스크립트와 직접 겹치지 않는다.
- GitHub REST GET과 로컬 Git 조회만 수행했다. fetch/pull/merge/checkout은 하지 않았다.

### 타 씬 영향은 있음 — 공용 코드 사용처

| 사용처 | 확인한 연결 | 범위 판단 |
|---|---|---|
| DHScene_3 | CombatPromptService → 공용 승/패 결과 프리팹 → BattleResultPanel → HeroInfoResult | 실제 탐사 전투 생략 승/패 및 도주 실패 결과에 영향 가능. 우선 회귀 확인 대상 |
| TutorialExploreScene | 같은 CombatPromptService와 승/패 프리팹 참조 | 저장된 씬 연결 확인. 현재 빌드 목록에는 없어 실제 사용 여부는 별도 |
| DH/LevelDataMaker | 같은 CombatPromptService와 승/패 프리팹 참조 | 제작/테스트 씬에서도 공용 결과 경로 영향 가능 |
| BattleSimulationScene_Legacy | 결과 스크립트 및 공용 승/패 프리팹 참조 | 기존 씬 연결 보존. 현재 빌드 목록에 없는 것을 삭제 근거로 삼지 않음 |
| 공용 ResultPanel/BattleSceneCanvas 및 DH 별도 Defeat 프리팹 | BattleResultPanel 컴포넌트 참조 | 자산 참조 확인. 모두 현재 실행 중이라고 단정하지 않음 |

BattleResultView는 직접 부착 참조가 없어도 VictoryResultPanel/DefeatResultPanel의 기반 클래스이므로 그 프리팹 전체에 적용된다. HeroInfoResult는 HeroInfo 원본에 붙고 Win/Lose 카드 변형이 이를 상속한다. 원본 프리팹을 수정하지 않아도 **공용 스크립트 변경은 이 인스턴스들에 적용**된다.

다른 씬의 기존 동작을 유지하는 구현 경계:

1. 전투씬에 새 선배치 참조가 연결된 경우에만 새 표시 경로를 사용한다. 기존 프리팹의 필드명/참조와 생성 경로를 보존한다.
2. 공용 HeroInfoResult의 IP 표시를 무조건 전후 값으로 바꾸지 않는다. 기존 카드는 기존 증감량 표시, 새 전투 카드는 전후 값을 표시하도록 참조에 따라 분리한다. 초상화 생성/Scale 처리도 기존 경로에서 갑자기 제거하지 않는다.
3. Show/OnAccepted/GetSkillResults 계약과 스킬 획득 순서, 확인 후 보상 저장을 유지한다. 확인 버튼 콜백 정리는 우리 코드가 추가한 listener만 대상으로 하며 외부 listener를 전부 지우지 않는다.
4. 추가 발견: PopupEnterSubmitter는 BattleResultPanel 하위의 **이름이 Accept인 버튼**을 찾아 Enter로 실행한다. 기존 제안 계층의 Confirm은 개념명이며, 실제 새 버튼 이름은 Accept로 유지하여 이 공용 스크립트를 추가 수정하지 않는다.
5. 새 결과창 구현 후 탐사 전투 생략 승/패·도주 실패·스킬 획득·Enter 확인과 보상 1회 처리를 검증한다. 이번 검토는 코드/직렬화 참조 분석이며 플레이 검증은 하지 않았다.

## 검증 계획

- 문서 대조: 셀·도형 텍스트·이미지, 원본 해시 보존. 이번 검토에서 완료.
- 결과 UI 구현 후: 씬 선배치·Scale1·LayoutGroup 없음·공용 프리팹 미변경, 정상 4인과 빈 데이터 방어, 승/패/도주, 모의 plan=null, 반복 Show·확인 1회, 여러 레벨 상승·최대 레벨 분모 0, 보상 저장은 기존 시점 1회.
- 배너 구현 후: 첫 진입, 아군→아군/적→적 연속, 기절/턴 시작 사망, 자동/수동/배속, 좌/우클릭·시간 만료·클릭 전달 방지·대기 중 자동 모드 변경, 메뉴/튜토리얼, 전투 종료·씬 전환 중 정리.
- 이번 검토에서는 코드·씬·프리팹·원본 기획서를 수정하지 않았고 Unity Play를 실행하지 않았다.

## 8. 승인 후 실제 구현 — bdb9d94aa3

- 승인: DH·ASB 협의 완료 후 사용자 “3개 모두 클리어. 구현 진행”으로 KJ 3개 수정, 신규 BattleTurnBanner 1개, 실제 1초/일시정지 정책을 승인했다. 결과창과 시작/턴 팝업을 이번 구현 대상으로 한정한다. 도주 확인·선택 불가 안내·스킬명 말풍선·호버 타일 표시는 후속 목록 유지.
- 적용: 기존 C# 10개 + 새 BattleTurnBanner.cs/.meta + TmpBattleScene. 관련 Docs 3개 갱신. 다른 씬과 공용 결과 프리팹 원본, BattleManager, 스킬 데이터 테이블, 원본 XLSX, SVN 리소스는 수정하지 않았다.
- 결과창: 선배치 4카드, 실제 성장 계산 미리보기 EXP/랭크/전후 IP, null plan 정리, 반복 Show와 자신의 확인 listener 정리. 공용 기존 씬은 미연결 필드의 옛 경로 유지. 버튼 이름 Accept 유지. 예시 캐릭터가 그려진 profilerFrame 자산은 공용 프레임으로 사용하지 않았고 단색 Image 테두리를 배치했다.
- 팝업: 시작 자동 1초 → 매 순번 1초 또는 좌/우클릭 → 상태이상/스킵 → 기존 행동 이벤트. Flow에 직접 표시 대기를 연결하며 기존 OnTurnStarted 의미는 유지한다. 투명 차단 영역을 닫힌 다음 프레임까지 유지해 EventSystem 처리 순서에 따른 메뉴/배속 버튼 클릭 전달도 막는다.
- 검증: Unity 컴파일 및 격리 PreviewScene **63항목**, 정상/기절 순번의 수동 코루틴 단계 **20항목** 통과. 저장 없는 EXP 시뮬레이션을 기존 ApplyExpWithLevelUps에 분리 데이터로 적용한 결과와 0/4/15/75/200 EXP에서 비교했다. 실제 Repository/JSON Commit은 호출하지 않았다. 승/패·시작·적 턴을 렌더링 검수했다. 렌더 예시는 테스트 preview와 기존 초상화 라이브러리를 사용한 것이며 실제 전투 보상값이 아니다.
- 검증 한계: Play Mode 미실행. 실제 좌/우클릭, 1초/배속/메뉴/튜토리얼, 자동전투 전환, 탐사 생략/도주 실패 결과 및 확인 후 귀환은 사용자 플레이 검수가 남는다. 턴 시작 피해 사망 분기는 정적 순서 확인이며 이번 수동 시퀀스 실행은 정상/기절 케이스다.
- 보존: 시작 시 파일 백업과 해시를 기록했다. 기존 TmpBattleScene Transform **172개**의 위치·크기·회전·Scale 값 변경 없음. DHScene_3/DOF 및 다른 기존 변경과 폰트 파일의 해시 보존 확인. 원래 결과 부모의 Image는 비활성으로 남겨 삭제하지 않았다. Unity 직렬화의 공백을 없애려고 씬을 일괄 정리하지 않았다.
- 임시 검증 과정: turnLabel 추가 후 에디터 어셈블리 갱신 전 첫 배치 시도에서 필드 미발견으로 중단되었다. 임시로 연 additive 씬을 저장하지 않고 닫은 뒤 컴파일 갱신·재실행하여 성공했다. 제품 코드 컴파일 오류로 숨기거나 검증을 생략하지 않았다.
- 자료: `C:/Dev/_scratchpad/battle-results-bdb9d94aa3/`의 before, baseline.json, build.cs, verify.cs, verify-flow.cs, 보존 검사 및 렌더 PNG. 프로젝트에는 테스트용 스크립트를 추가하지 않았다. 새 런타임 스크립트는 승인된 1개뿐이다.
- 상태: 구현·비플레이 검증·문서 반영 완료, 사용자 플레이 검수 대기. 커밋·push·브랜치 전환 없음.
