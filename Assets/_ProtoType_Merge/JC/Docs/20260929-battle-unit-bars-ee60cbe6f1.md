# 전투 머리 위 HP/IP 개편

- 작업 ID: 20260929-ee60cbe6f1
- 승인: 사용자 구현 계획 검토 후 "구현 시작 해 주세요". 기존 UnitHPBar, 전투 월드용 Variant, 파란 IP 색상 변형, 아군·적 연결을 포함한다.
- 환경: C:/Dev/HeroInfluence, JC, Unity 2022.3.62f3. 시작 Git 상태 clean.
- 상태: 구현 완료·비플레이 검증 완료·사용자 플레이 검수 대기. 커밋하지 않았다.

## 적용 내용

- UnitHPBar의 회전 고정을 제거하고 LateUpdate에서 카메라 화면과 평행하게 정렬한다. 유닛 원점 위 기준 높이와 화면 여백을 분리하고 기존 100px 덮어쓰기를 제거했다.
- HP/IP의 기존 BattleCharactor 이벤트를 재사용한다. HP 최대값 0일 때 숫자도 0/0으로 초기화한다. IP 최대값 0 또는 미표시 설정이면 행 전체를 숨긴다. 미초기화·사망·카메라 뒤·렌더 범위 밖에서 숨기고 복귀·부활·재활성화 시 갱신한다. OnDisable에서 구독을 해제하고 바 자체도 숨겨 유닛 초기화가 Canvas를 켜더라도 표시가 되살아나지 않게 했다.
- 기존 에디터 메뉴를 공통 Variant 생성/교체에 연결하고 Undo를 제공한다. 새로운 런타임 스크립트나 별도 데이터 관리 시스템은 없다.
- `JC/BattleUI/UnitBars/HPCanvas_Battle.prefab`: 기존 HPCanvas 기반 Variant. HP 위/IP 아래, 원본 비율의 둥근 바, 어두운 빈 영역, 흰색 숫자·어두운 외곽선. 모든 Graphic의 Raycast Target을 끈다.
- HP와 프레임은 `_Ui_Sprites/Icon/Bar`의 기존 자산을 재사용한다. 두 파일은 지정된 SVN 원본과 SHA256이 동일하다. 파란 IP와 빈 게이지는 기존 `Tools/UiRecolorForge`의 RecolorEngine으로 생성했다. PNG 옆 JSON에 매개변수를 보존했다. 원본 크기 1974×166 및 알파 유지.
- 연결: PlayerUnit 5, BattlePrefab/EnemyUnit 12, CommonModel_Drone 1, 기존 EnemyUnit 3 = 21개 프리팹. 적은 showInfluence=false. 고급 몬스터 HP 숫자의 잘못된 IP 연결을 교정했다.
- 월드 게이지 이외 4,074개 직렬화 기록은 시작 상태와 동일하다. 기존 누락 모델을 포함한 모델·전투·스포너 설정은 그대로 보존했다. 고정 정보창이 사용하는 HPCanvas 원본 및 기존 Variant, 씬 파일은 변경하지 않았다.

## 조절 위치

- 유닛 프리팹 루트 `UnitHPBar`의 **Anchor Height**: 모델별 기준 높이, 월드 단위. 에디터 렌더러의 상단을 기준으로 초깃값을 저장했고 렌더러가 없는 기존 누락 모델에는 1.95를 사용했다. 애니메이션 바운드를 매 프레임 추적하지 않아 모션마다 바가 출렁이지 않는다.
- **Screen Offset**: 화면 여백, 기본 (0,24), 세로 1080px 기준. 화면 높이에 비례한다.
- **Screen Width**: 기본 170, 세로 1080px 기준 화면 폭. 카메라 거리와 모델 배율에 따른 과도한 크기 차이를 보정한다. 0은 월드 크기 유지.
- **Show Influence / Hide When Dead**: IP 행·사망 시 숨김 설정.
- 공통 외형·간격·숫자 크기는 `HPCanvas_Battle.prefab`에서 조절한다. HP/IP 행 중심 y=+11/-11, 바 190×16, 숫자 크기 14. 모든 조작 필드에 한국어 툴팁을 추가했다.

## 실행한 검증

- Unity 컴파일 완료: isCompiling=false, scriptCompilationFailed=false.
- 21개 프리팹의 HP/IP Image, 숫자, Canvas, RectTransform 참조, Variant 연결, 내부 회전·배율, 가로 채움 방향, Raycast Target 검사: 실패 0.
- 씬 연결: 일반·튜토리얼 전투의 스포너는 직접 프리팹을 직렬화하지 않고 등록표 또는 Resources 경로로 유닛을 읽는다. 씬의 정적 의존성만으로 판정하지 않고 실제 Resources.LoadAll 경로의 아군 5/5·적 12/12 및 공용/테스트 등록표의 Variant 연결을 확인했다. 독립 테스트씬은 정적 의존성에도 Variant가 포함된다.
- 별도 PreviewScene의 비플레이 검사 23항목: 초기값, 기존 경로 참조 복원, 현재 전투 카메라 각도, 거리별 170px 폭, 부모 회전·균일 배율, 직교 카메라, HP 0/초과/저체력, HP/IP 이벤트, IP 0/복귀/적 숨김, 사망/부활, 카메라 뒤/복귀, 비활성 중 이벤트 해제와 표시 보존, 재활성화. 실패 0.
- 실제 Unity UI 렌더: 100%/60%/5%/0% 잔량과 숫자 가독성 확인. TmpBattleScene의 (7,12,0), (60,-90,0), FOV60 카메라 및 그리드에 아군 4·적 6을 편집 상태로 임시 배치해 수평 정렬·표시 크기를 확인했다. 임시 객체·카메라 설정을 저장하지 않고 추가로 연 씬을 닫았다. 이것은 플레이 검증이 아니다.
- 보존·검증 자료: `C:/Dev/_scratchpad/hp-ip-ee60cbe6f1/`. `before-prefabs`, `UnitHPBar.before.cs`, `migration.json`, `asset-audit.txt`, `behavior-tests.txt`, `bars-preview.png`, `battle-editor-preview.png`.
- 비플레이 재현: 해당 폴더의 `audit.cs`, `verify_behavior.cs`를 프로젝트의 Unity script-execute로 실행한다. 구현용 `migrate.py`는 이미 적용되었으며 재실행하지 않는다.
- 최종 상태: 기존 DHScene_3만 열려 있고 dirty=false, Edit mode, 컴파일 오류 없음. `git diff --check` 통과. 기존 씬 파일·공용 HPCanvas·모델 자산의 변경 없음.

## 사용자 플레이 확인

1. TmpBattleScene 또는 독립 `z_JC_BattleVisualTestbed`에서 아군 4·적 6의 바가 카메라와 평행하며 모델을 가리지 않는지 확인한다.
2. 피해·회복 시 HP, 스킬 사용 시 IP의 게이지와 숫자가 함께 갱신되는지 확인한다.
3. 적에게 IP 행이 표시되지 않고, 사망 시 숨김·부활 시 복귀하는지 확인한다.
4. 전열/후열, 다른 해상도, 스킬 이동 중의 높이·가독성 및 튜토리얼 전투를 확인한다. 독립 테스트씬의 Z 초기화 후에도 중복 바가 없어야 한다.

유닛 바 배치는 정적 편집 검증을 통과했으나 실제 애니메이션·전투 흐름의 결과는 사용자 플레이 검수까지 완료로 판정하지 않는다. 기존 적 모델의 누락 참조는 이번 UI 변경으로 수정한 항목이 아니다.


## 후속 — 두께·위치·소모 잔상 (20260929-e2c6b71435)

- 사용자 승인: 검토 예시 채택 후 구현 요청. 피해 계산·사망 판정·턴 진행·유닛 제거 코드는 제외하며 잔상 전에 캐릭터가 제거되는 경우는 사용자가 직접 테스트한다.
- UnitHPBar의 기존 이벤트를 재사용하고 HP/IP 각각 표시 끝점과 경과 시간을 보관한다. HP 0.5초/빨강→주황→노랑, IP 0.2초/흰색. 실제값·숫자는 즉시 반영하고 남은 구간은 기존 초록/파랑이다. 감소량과 전투 배속에 관계없는 시간이며 timeScale=0에서 정지한다.
- `JC_BattleUI_VFX/UnitBars`의 `BattleUnitBarVisualSettings`: Screen Offset (0,34), Screen Width 170(1080 기준), Row Height 22, Row Gap 6, Font Size 17(프리팹 단위). Hp Loss/Ip Loss 각각 Duration, Curve(Linear/Accelerate/Decelerate/Custom), Custom Curve. HP Loss Colors와 IP Loss Color로 소모 구간 색을 설정한다. 전 항목 한국어 툴팁을 넣었다.
- 설정은 TmpBattleScene 및 JC_Testbed_BattleRuntime.prefab에 추가했다. 독립 테스트씬은 중첩 프리팹 상속으로 반영되며 초기화 재생성에도 유지된다. 설정이 없는 씬은 기존 위치/폭과 기본 잔상 시간을 사용한다.
- 공용 바 Variant에 HP/IP Loss 이미지를 실제 채움 뒤에 추가했다. 기존 Recolor Forge로 무채색 바를 생성하고 UI_bar_loss.png와 재현 프리셋 JSON을 저장했다. 원본 HP/frame, 기존 공용 HPCanvas, 유닛 프리팹 21개는 이번 후속에서 추가 수정하지 않았다.
- 연속 감소는 현재 표시 끝점에서 최신 목표로 다시 시작한다. 같은 값의 이벤트는 타이머를 재시작하지 않는다. 회복/최대치 변경/부활/재활성화는 잔상을 초기화한다. 사용자 곡선은 0~1로 제한하고 역행을 막으며 끝에서 목표값에 맞춘다. Duration=0은 즉시 완료한다.
- 사망 시 UnitHPBar의 표시만 잔상 종료까지 유지한다. 유닛이 먼저 파괴되면 함께 종료하며 객체 수명을 늘리지 않는다. BattleCharactor/EnemySpawner/BattleFlowManager/BattleManager 및 KJ/DH 코드는 변경하지 않았다.
- 검증: Unity 컴파일 오류 없음, 비플레이 격리 36항목 통과, 두 씬에서 저장 설정 1개씩 확인, 21개 유닛 Loss 참조 상속 확인, 0/0.1/0.25/0.4초 렌더 확인. 실제 Play는 미실행이다.
- 사용자 검수: 일반 전투와 z_JC_BattleVisualTestbed에서 피격·연속 피격·IP 소비, 설정값 변경, 일시정지/배속, Z 초기화, 사망과 유닛 제거 시 표시 종료를 확인한다.
- 검증 근거와 시작 백업: `C:/Dev/_scratchpad/hp-ip-e2c6b71435`의 behavior-tests.txt, asset-audit.txt, loss-preview.png, before-hashes.json.


## 후속 — 회복·부활 및 결과 EXP/랭크 (20260930-23e0c45e06)

- 사용자 구현 승인에 따라 HP/IP 증가 구간을 흰색→밝은 저채도 동일 계열색→원래 색으로 연출한다. 부활 HP 0→회복 HP를 포함한다. UnitBars의 Hp Recovery 0.5초/Ip Recovery 0.2초 및 각각의 곡선·Gradient를 사용한다. 한 턴 변화 1회 전제다.
- 결과 EXP는 ResultRewards에서 구간당 0.5초·곡선·색을 설정한다. 기존 SimulateExpProgress와 임시 스냅샷으로 레벨 경계만 구하고, 레벨 상승 시 0으로 초기화해 나머지를 채운다. 실제 성장/보상/저장 로직은 변경하지 않는다.
- EXP 종료 후 랭크 상승을 이전 아이콘→백색→최종 아이콘으로 총 0.5초 1회 표시한다. 스킬 안내 종료 후 카드 활성화부터 시작하며, 조기 확인은 최종값에 맞춘 후 기존 콜백을 한 번만 호출한다.
- KJ 폴더 수정은 기존 JC 결과 UI의 HeroInfoResult와 BattleResultPanel 표시 코드에 한정한다. 다른 KJ/DH 코드, 피해·사망·턴·제거 코드는 보존한다. 21개 유닛 프리팹과 바 Variant도 재편집하지 않았다.
- TmpBattleScene/JC_Testbed_BattleRuntime.prefab에 설정과 4개 ExpGain/ExpTrack을 추가했다. HP/IP 숫자 외곽선 키워드를 활성화하고 결과 EXP 빈 영역을 어둡게 해 흰색 구간의 가독성을 보완했다. 씬 자동 재직렬화로 섞인 무관한 searchEntireScene 항목과 기록 순서 변경은 제거했다.
- 비플레이 43항목 통과: 회복/부활/감소 회귀, 두 번 레벨업과 잔여 EXP, 정확한 임계값, 0 보상/0 시간, 랭크 1회, 숨겨진 카드 대기, 조기 확인/중복 콜백 방지, 미리보기 불일치 안전 처리, 재질 정리. 두 씬 저장 연결·셰이더 및 C# 컴파일·회복/결과 정적 렌더 확인. 실제 Play는 실행하지 않았다.
- 검증 자료: C:/Dev/_scratchpad/recovery-23e0c45e06의 before-hashes.json, behavior-tests.txt, asset-audit.txt, recovery-preview.png, result-preview.png. 시작 백업과 비교해 승인 범위 밖의 기존 파일이 보존됐는지 확인한다.
- 사용자 검수 대기: 실제 힐·부활/IP 회복, 결과 다중 레벨업·랭크 상승, 스킬 안내 이후 시작, 조기 확인 시 저장/귀환, 독립 테스트씬 Z 초기화. 잔상 완료 전 유닛 제거는 기존 합의대로 사용자가 직접 확인한다.


2026-09-30 사용자 확인: HP/IP 및 결과 연출이 잘 작동하는 것으로 보인다는 피드백을 받았고, 커밋부터 main 동기화까지 진행하도록 승인받았다. 구체적인 개별 테스트 항목을 모두 수행했다고 확대 해석하지 않는다.
