# 튜토리얼 첫 전투 오류 분석·수정 레포트

- 작성일: 2026-10-02 (KST)
- 기준: JC 브랜치, HEAD `bb33504f`
- 범위: 적 사망 시 HP 표시 이상, 콘솔 오류 4종, 전투 후 안내 누락 및 협회 직행
- 상태: 수정·비플레이 검증 완료. 수정 후 실제 플레이 검수는 사용자 확인 대기.
- 이번 작업에서 커밋하지 않았습니다.

## 1. 결론

서로 다른 두 문제가 함께 발생했습니다.

1. 드론 한 마리 안에 전투용 컴포넌트와 HP바가 두 벌 들어 있었습니다. 실제 전투 본체는 HP 5, 내부 모델은 HP 27을 가지고 있었습니다. 본체가 죽어도 내부 모델의 HP바가 남아 최대 HP가 증가한 것처럼 보였습니다. 같은 중복 구조 때문에 적 셀 3곳에서 중복 점유 오류도 발생했습니다.
2. 탐사 씬의 전투 진입 대상이 튜토리얼 전용 씬에서 일반 전투 씬으로 바뀌었고, 복귀 대상도 탐사에서 협회로 바뀌었습니다. 이 때문에 전용 안내를 실행할 Director가 없었고, 첫 전투 후 탐사 과정을 건너뛰었습니다.

원인은 계산식이나 피해 처리 함수가 아니라 **프리팹 구성과 씬에 저장된 연결값**이었습니다. 따라서 C# 로직을 우회하거나 오류 검사를 없애지 않고, 잘못된 구성을 수정했습니다.

## 2. 공격 전후 실측

사용자가 직접 공격하고 사망 직후 일시정지한 상태를 읽었습니다. 에이전트는 공격·프레임 진행을 수행하지 않았습니다.

| 항목 | 공격 전, 프레임 28318 | 사망 직후, 프레임 40679 |
|---|---|---|
| 본체 instance ID | -197032 | 동일 |
| 본체 HP / 사망 상태 | 5/5, 생존 | 0/5, 사망 |
| 본체 HP바 실제 채움 | 1 | 0 |
| 내부 모델 instance ID | -197140 | 동일 |
| 내부 모델 HP / 사망 상태 | 27/27, 생존 | 27/27, 생존 |
| 내부 모델 HP바 실제 채움 | 1 | 1 |

사망 직후 본체의 소모 연출은 약 0.307초 경과했으며, 잔상 채움은 약 0.387이었습니다. 본체의 HP 0 처리와 소모 연출은 정상이고, 내부 모델의 가득 찬 HP바가 별도로 켜져 있었습니다.

다른 드론 두 마리도 같은 중복 구조였습니다. 내부 모델은 `OccupiedCell`이 없어 전투 참가에서는 제외됐지만 HP바 컴포넌트는 계속 동작했습니다. `DroneMotion.owner`는 세 마리 모두 실제 본체를 가리켰습니다.

## 3. 콘솔 오류 4종

현재 실행에서 네 오류 모두 전투 초기화 시 발생했습니다. 사망 공격으로 새로 생긴 오류가 아닙니다. 로그 조회에는 이전 실행의 같은 네 오류도 함께 남아 있었습니다.

| 오류 | 직접 원인 | 이번 조치 |
|---|---|---|
| Grid_3_0 중복 점유, units=2 | 본체와 내부 모델의 BattleCharactor를 각각 검출 | 내부 모델의 중복 전투 컴포넌트 제거 |
| Grid_2_1 중복 점유, units=2 | 동일 | 동일 |
| Grid_3_2 중복 점유, units=2 | 동일 | 동일 |
| TUT_01인데 TutorialBattleDirector 없음 | TmpBattleScene으로 잘못 진입 | TutorialBattleScene 진입 복원 |

`BattleSceneManager.SyncGridOccupancy()`는 각 셀 아래의 `BattleCharactor`를 검색하고 둘 이상이면 오류를 냅니다. 이 검사는 잘못된 구조를 정확히 발견한 것이므로 유지했습니다. 단순히 컴포넌트를 비활성화하는 방법도 검색에서 제외된다는 보장이 없으므로 사용하지 않았습니다.

## 4. 관련 커밋과 책임 범위

커밋에 실제로 들어 있는 변경과, 화면 증상이 처음 관찰된 시점을 구분합니다.

| 커밋 / 날짜 | 확인된 변경 | 이번 문제와의 관계 |
|---|---|---|
| `763f0485` / 09-03 | `[feat] 모델 프리펩 최신화`. CommonModel_Drone에 EnemyScript, BattleCharactor, UnitHPBar 및 HP 27 설정이 존재 | 공용 모델 자체가 단순 외형만이 아니라 전투 상태까지 가진 구조임을 확인 |
| `8aba8264` / 09-03 | `[feat] 유닛 프리펩 관리 매니저 구성`. Unit_VillanDrone_20001 생성. 자체 BattleCharactor와 공용 드론 모델을 함께 포함하고 중복 컴포넌트 제거 설정 없음 | 현재 중복 구조가 만들어진 직접적인 결합 지점 |
| `87c7c879` / 09-30 | `[feat] 전투 HP·IP 바 개편 및 회복·결과 보상 연출 추가`. 본체와 공용 모델의 바를 공용 HP바 구성으로 갱신 | 이미 존재한 중복을 제거하지 않은 채 양쪽 HP바가 유지됨. 중복 전투 컴포넌트의 최초 생성 커밋은 아님 |
| `6b826420` / 10-02 19:42 | `[chore] 빌드 전 커밋`. TutorialExploreScene의 전투 진입·복귀 설정 4쌍 변경 | Director 누락, 전용 안내 미실행, 협회 직행의 직접 원인 |

`6b826420`에서 바뀐 값은 다음과 같습니다.

- 전투 진입: `TutorialBattleScene` → `TmpBattleScene`
- 전투 복귀: `TutorialExploreScene` → `TutorialLobbyScene`

HP 중복 구조는 최근 머지보다 앞선 이력에 이미 있었습니다. 따라서 세 증상이 모두 최근 머지 한 번으로 처음 만들어졌다고 설명하면 부정확합니다. 중복 HP바가 화면에서 처음 눈에 띄게 된 정확한 시점은 과거 버전 플레이로 검증하지 않았습니다.

## 5. 관련 스크립트가 맡는 역할

아래는 원인 분석에 사용한 스크립트입니다. 이번 수정에서는 C# 파일 내용을 변경하지 않았습니다.

| 스크립트 | 역할과 확인 결과 |
|---|---|
| `ASB/Scripts/Data/Core/BattleCharactor.cs` | 실제 HP와 사망 처리. 본체는 TakeDamage에서 0/5를 알리고 Die에서 사망 처리함. 내부 모델은 별개의 인스턴스라 본체와 상태를 공유하지 않았음 |
| `ASB/Scripts/Unit/EnemyScript.cs` | 적 데이터 공급. 내부 공용 모델에도 붙어 있어 별도 HP 27 상태를 구성함 |
| `ASB/Scripts/Battle/Core/UnitHPBar.cs` | 자신에게 연결된 BattleCharactor의 HP를 표시. 내부 바는 내부 모델을 생존 상태로 판단해 계속 표시함 |
| `ASB/Scripts/Battle/Core/BattleSceneManager.cs` | 셀 점유 연결, 튜토리얼 Director 연결, 결과 안내 통지, 복귀 씬 결정. 중복 검출과 Director 오류는 정상적인 진단임 |
| `DH/Scripts/Tutorial/TutorialCombatLauncher.cs` | 씬에 저장된 진입·복귀 값을 CombatContext와 씬 전환에 사용. 코드 기본값보다 씬에 저장된 잘못된 값이 실제 동작을 결정함 |
| `ASB/Scripts/Battle/Tutorial/Flows/TutorialBattle01Flow.cs` | 첫 전투 결과 시 step3 안내를 요청. Director가 없으면 이 흐름이 실행되지 않음 |
| `JC/Tutorial/Scripts/JcTutorialBattleGuide.cs` | 스킬 습득·결과 설명 표시 및 입력 차단. 일반 전투 씬에는 이 전용 구성이 없음 |
| `KJ/Scripts/UI/BattleResultPanel.cs` | 스킬 습득 창과 결과 내용을 순차 표시. 이번에는 이 로직을 변경할 필요가 없었음 |

경로의 ASB/DH/JC/KJ 접두는 모두 `Assets/_ProtoType_Merge/` 아래입니다.

## 6. 실제 수정한 파일과 방식

### A. 전투 드론 프리팹

파일: `Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanDrone_20001.prefab`

내부 `Model/CommonModel_Drone` 인스턴스에 아래 제거 오버라이드를 저장했습니다.

- 중복 `UnitHPBar` 제거
- 중복 `EnemyScript` 제거
- 중복 `BattleCharactor` 제거
- 내부 `HPCanvas` 제거

본체의 전투 컴포넌트와 HP바는 그대로 유지했습니다. 애니메이션·이펙트·모델과 `DroneMotion`의 본체 연결도 유지했습니다.

공용 `CommonModel_Drone.prefab` 원본은 변경하지 않았습니다. 탐사용 `DH/Prefabs/Enemy/LowerMonster.prefab`도 이를 사용하기 때문에, 전투 프리팹 안에서만 중복을 제거했습니다. 별도의 모델 사본이나 런타임 강제 삭제 코드는 만들지 않았습니다.

Unity가 생성한 제거 오버라이드를 확인한 뒤 불필요한 재직렬화 변경은 제외했습니다. 최종 차이는 기존 빈 제거 목록을 세 컴포넌트와 한 GameObject의 제거 목록으로 바꾼 부분뿐입니다.

### B. 탐사 튜토리얼 씬

파일: `Assets/_ProtoType_Merge/Scenes/TutorialExploreScene.unity`

전투 진입 컴포넌트 네 곳을 모두 다음과 같이 복원했습니다.

- `tutorialBattleSceneName`: `TutorialBattleScene`
- `returnSceneName`: `TutorialExploreScene`

총 8개 문자열만 변경했습니다. 협회 방문 기능 자체의 `tutorialLobbySceneName`은 정상적인 별도 기능이므로 유지했습니다. 전투 전용 씬에 있는 기존 안내 UI와 Director를 재사용합니다.

## 7. 검증 결과와 한계

완료한 검증:

- 수정 전 실제 플레이의 동일 개체를 공격 전후로 비교해 원인 확인.
- 수정 시 Unity는 이미 Edit mode였음을 확인. 추가 플레이 실행 없음.
- 수정한 드론 프리팹을 Unity에 동기 재임포트하고 LoadPrefabContents로 다시 로드.
- BattleCharactor, EnemyScript, UnitHPBar, Canvas가 각각 1개임을 확인.
- 본체 HP 이미지·텍스트·Canvas·소유 유닛 참조가 유효함을 확인.
- DroneMotion.owner가 본체를 가리키며 Missing Script가 없음을 확인.
- 탐사 씬의 진입·복귀 설정 네 쌍 확인. 역치환 시 수정 전 바이트와 완전히 일치하여 8개 값 외 변경 없음을 확인.
- 전투 씬의 Director가 BattleSceneManager와 같은 GameObject에 있고, 안내 UI 참조·결과 패널 참조가 씬 내부의 실제 객체를 가리키는지 정적 검사.
- 두 튜토리얼 씬이 Build Settings에서 활성 상태임을 확인.
- 대상 파일의 git diff --check 통과.
- 사용자가 씬을 리로드한 뒤 TutorialExploreScene, Edit mode, dirty=False, compiling=False 확인. 열린 씬의 실제 TutorialCombatLauncher 4개도 수정된 진입·복귀 값을 가지고 있음을 확인.

검증 도구의 제한:

- 씬을 Preview Scene으로 로드하는 임시 검사 스크립트는 현재 Unity API에 OpenPreviewScene이 없어 컴파일되지 않았습니다. 제품 코드 컴파일 실패가 아닙니다. 해당 검사를 통과로 계산하지 않고, 씬 YAML 객체 참조 및 빌드 설정 검사로 대체했습니다.
- 제품 C# 변경이 없어 새 코드 빌드를 수행하지 않았습니다.
- 수정 후 실제 전투와 안내 타이밍은 플레이로 재검증하지 않았습니다. 기존 콘솔 오류 기록은 지우지 않았으므로, 수정 전 오류가 콘솔에 남아 있을 수 있습니다.

사용자 플레이 확인 순서:

1. TutorialExploreScene에서 첫 전투 진입 → TutorialBattleScene인지 확인.
2. 전투 초기화 시 셀 중복 점유 오류 3개와 Director 누락 오류가 새로 발생하지 않는지 확인.
3. 드론 처치 → HP 0/5로 감소하고 소모 연출 후 숨겨짐. 27/27 바가 남지 않는지 확인.
4. 첫 승리 후 스킬 습득·전투 결과 안내 확인.
5. 결과 확인 후 TutorialExploreScene으로 돌아가 다음 탐사 튜토리얼을 이어가는지 확인.

## 8. 근거 자료

프로젝트 외 임시 진단 자료:

- `C:/Dev/_scratchpad/tutorial-live-b6a158d1f4/before.json`: 공격 전 수치
- `C:/Dev/_scratchpad/tutorial-live-b6a158d1f4/after.json`: 사망 직후 수치
- `C:/Dev/_scratchpad/tutorial-live-b6a158d1f4/structure.json`: 셀 연결·HP Canvas·DroneMotion 소유자
- `C:/Dev/_scratchpad/tutorial-live-b6a158d1f4/errors.json`: 콘솔 오류와 스택
- `C:/Dev/_scratchpad/tutorial-fix-ce8ab5321f/verify.json`: Unity 프리팹 재로드 검증
- `C:/Dev/_scratchpad/tutorial-fix-ce8ab5321f/scene-static.txt`: 씬 연결 정적 검사

기존 프리뷰 씬 5개, 폰트 2개, ProjectSettings의 사용자 변경은 이번 수정 대상에 포함하지 않았습니다.
