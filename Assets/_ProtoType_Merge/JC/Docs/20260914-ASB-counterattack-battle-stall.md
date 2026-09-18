# 반격으로 적 사망 후 전투가 멈추는 문제 — ASB 인수인계

작성: 2026-09-14 / JC 측 Codex 분석  
대상 독자: ASB 및 ASB가 사용하는 개발 에이전트  
상태: **로그·코드 분석 완료, 수정 및 플레이 재현 검증은 미실행**

## 1. 먼저 읽을 결론

가장 유력한 원인은 **적 오브젝트가 실행 주체인 공격 코루틴과, 적 사망 후 오브젝트 삭제의 수명 충돌**입니다.

현재 적은 `EnemyScript.StartCoroutine(battleManager.ExecuteGridSkill(...))`으로 공격합니다. 반격까지 처리하는 도중 적이 사망하면 `EnemySpawner`가 약 0.8초 뒤 적 오브젝트를 삭제합니다. 이때 적이 실행 주체인 `ExecuteGridSkill` 코루틴은 중단될 수 있지만, `BattleManager`가 별도로 실행한 반격·연출 처리 코루틴은 계속 진행할 수 있습니다. 전투 흐름은 중단된 적 공격 코루틴의 정상 완료를 기다리므로 종료 판정으로 돌아가지 못하는 것으로 추정됩니다.

관측 로그는 이 설명과 일치합니다. 다만 당시 Play Mode는 분석 시점에 이미 종료되어 있었고, 코루틴 대기 지점이나 `OnBattleEnded` 발행 여부를 직접 관측하지 못했습니다. **수정 전 임시 로그로 중단 지점을 확인하고, 수정 후 같은 조건으로 재현 검증해야 합니다.**

첫 수정 후보는 `EnemyScript.cs`의 클래스/무기스킬 실행 두 곳에서 코루틴 실행 주체를 전투 동안 유지되는 `BattleManager`로 바꾸는 것입니다. 신규 스크립트나 UI 재구축은 필요하지 않을 것으로 보입니다. 이 문서는 수정 방향을 검토할 자료이며, 아래 제안은 아직 적용한 코드가 아닙니다.

## 2. 분석 기준과 작업 경계

| 항목 | 확인한 상태 |
|---|---|
| 프로젝트 | HeroInfluence |
| Unity | 2022.3.62f3 |
| 로컬 브랜치 | JC |
| 분석 기준 HEAD | `d3c0316e1feaf30cd1059ae112c53df2a60fd866` |
| 시작 씬 | `Assets/_ProtoType_Merge/Scenes/DHScene_3.unity` |
| 전투 씬 | `Assets/_ProtoType_Merge/Scenes/TmpBattleScene.unity` |
| 관측 전투 | 시작 지점에서 우측 이동 후 조우하는 이벤트 전투. 로그의 `Battle=BE006` |
| 발생 일시 | 2026-09-14 11:11, 한국 시간 |
| 관련 핵심 코드 | EnemyScript / EnemySpawner / BattleFlowManager / BattleManager는 분석 당시 HEAD와 차이 없음 |
| 별도 미커밋 변경 | 전투 UI 개편에 따른 InputHandler, SkillButtonController, SkillButtonTooltip, TurnOrderUI, HeroInfoPanel, 전투씬 등 |

문서의 코드 경로는 모두 **저장소 루트 기준**입니다. 줄 번호는 위 기준 코드의 위치이며, ASB 브랜치에서는 함수명으로 다시 찾아야 합니다. 이 파일과 저장소만 있으면 읽을 수 있도록 작성했으며, JC PC의 대화 기록·임시 스크립트·개인 메모리를 전제로 하지 않습니다.

현재 JC 작업은 전투 UI 개편입니다. 이번 문서 작성에서 전투 코드, 씬, 리소스를 수정하지 않았습니다. `BattleManager.cs`는 JC UI 개편의 편집 제외 대상이므로 이 버그를 UI 작업에 끼워 넣어 수정하지 않았습니다. ASB 측 구현은 ASB의 작업 범위와 현재 브랜치 상태를 확인한 뒤 진행할 사안입니다.

사용자가 직접 조정한 전투씬 RectTransform은 의도한 배치 변경입니다. 이 버그를 수정하면서 이전 배치로 되돌리지 않습니다. 이전에 진행하던 피해량·캐릭터/적 스펙 변경 조사는 사용자 지시로 중단했으며, 이번 문서 역시 밸런스 분석·수정을 목적으로 하지 않습니다.

## 3. 사용자 관측과 실제 로그

사용자는 루미나의 스킬 선택 해제가 잘 되는지 확인하기 위해 저스티스의 턴을 숫자키 `4`로 스킵했습니다. 이후 적이 저스티스를 공격했고, 저스티스의 반격으로 적이 죽었으나 결과창이 나오지 않고 진행이 멈췄다고 보고했습니다.

로그에는 캐릭터 고유 이름 대신 직업명이 출력됩니다. 이 전투의 `파이터`는 저스티스, `블래스터`는 루미나, `서포터`는 네코밍, `스트라이커`는 블랙 불릿입니다.

### 같은 실행에서 확인한 직전 진행

1. 루미나가 방패병을 공격했습니다. 방패병은 살아남았습니다.
2. 네코밍이 루미나를 회복했습니다.
3. 블랙 불릿의 공격으로 소총수가 사망했습니다. 이후 저스티스 턴으로 정상 진행했습니다.
4. 저스티스가 `4`로 스킵했습니다.
5. 남아 있던 방패병이 저스티스를 공격했습니다.
6. 저스티스의 반격으로 방패병이 사망했고, 그 뒤 진행이 멈췄습니다.

위 순서는 관측한 실행 한 건입니다. 치명타·반격이 확률에 의존하므로 그대로 조작한다고 항상 재현된다는 뜻은 아닙니다. 핵심 조건은 **자기 공격을 처리하던 적이, 반격 처리 도중 사망하여 삭제되는 것**입니다.

### 핵심 로그 발췌

시각은 모두 2026-09-14 한국 시간이며, 아래 HP 숫자는 사망 시점을 식별하기 위한 관측값입니다.

| 시각 | 기록 | 의미 |
|---|---|---|
| 11:11:30.135 | `[BattleFlow] 유닛 제거: Enemy:빌런연합 소총수_... (refreshQueue=False)` | 첫 적 제거 뒤에는 계속 진행함 |
| 11:11:30.244 | `[턴 시작] 플레이어 진영: 파이터` | 저스티스 턴 진입 |
| 11:11:36.061 | `[BattleFlow] PlayerSkillActionResolved 수신: actor=파이터` | 스킵의 턴 종료 통지 도착 |
| 11:11:36.062 | `[InputHandler] 턴 스킵: 파이터` | 스킵 코드 실행 |
| 11:11:36.062 | `[BattleFlow] 플레이어 턴 종료: resolved=True, currentUnit=파이터, isDead=False, battleOver=False` | 플레이어 턴 정상 종료 |
| 11:11:36.081 | `[턴 시작] 적 진영: 빌런연합 방패병` | 적 턴으로 정상 전환 |
| 11:11:37.203 | `[Combat] 빌런연합 방패병 -> 파이터 dmg=2.0 applied=2.0 (Crit: False)` | 적 공격 적용 |
| 11:11:38.062 | `[Combat] 파이터 근접 반격 발동! (계수 0.5)` | 반격 시작 |
| 11:11:38.442 | `[HP] 빌런연합 방패병 HP changed: 0/6` | 마지막 적 사망 |
| 11:11:38.444 | `[Combat] 파이터 -> 빌런연합 방패병 dmg=2.0 applied=2.0 (Crit: False)` | 반격 피해 적용 |
| 11:11:39.243 | `[BattleFlow] 유닛 제거: Enemy:빌런연합 방패병_... (refreshQueue=False)` | 사망 약 0.8초 뒤 참가자 제거 |
| 11:11:42.045 | `[IdleDiag] ReturnToIdleAction → PlayIdleAnimation (unit=Charactor_10001_...)` | 저스티스 복귀 연출 진행 |
| 11:11:42.249 | `[CombatEvent] execution=9f556801b37a4adea344ad00b6fbf360, ... death=1` | 반격 결과 요약 |
| 11:11:42.249 | `[CombatEvent] execution=c945d137b67e4ab493615c7562397150, ... death=0` | 원래 적 공격 결과 요약 |

조회한 로그에서 이 사건 시간대의 Error/Exception은 없었습니다. 남아 있던 이전 오류는 10:58의 에디터 검증용 Probe 오류였으며 이 전투의 실행 오류와 구분했습니다.

**주의:** `유닛 제거` 로그는 `BattleFlowManager.RemoveUnit()`에서 출력되며 `Destroy(go)` 그 자체의 로그는 아닙니다. `KeepCorpseAfterDeath` 분기 등을 거친 실제 삭제 여부는 재현 중 확인해야 합니다. 또한 현재 코드에는 모든 적 턴 종료 및 `OnBattleEnded` 발행을 무조건 기록하는 로그가 없으므로, 종료 로그가 없다는 이유만으로 이벤트 미발행까지 확정하지 않습니다.

## 4. 실제 호출 구조: 함수 소속과 코루틴 실행 주체를 구분

아래 대괄호는 함수가 선언된 클래스가 아니라 **해당 코루틴을 시작한 MonoBehaviour**입니다.

```text
BattleFlowManager.BattleLoop                         [BattleFlowManager]
  └─ yield return RunEnemyTurn(CurrentUnit)          [같은 전투 흐름]
       └─ StartCoroutine(enemyScript.RunAITurn(...)) [BattleFlowManager]
            └─ StartCoroutine(
                 battleManager.ExecuteGridSkill())  [EnemyScript: 사망하면 삭제되는 적]
                 └─ StartCoroutine(
                      ApplySkillExecutionResultRoutine()) [BattleManager]
                      ├─ 적의 공격 피해·연출 처리
                      └─ DrainFollowUps()                [BattleManager]
                           └─ CounterAttackActionCommand [BattleManager]
                                └─ ExecuteCounterSkill() [BattleManager]
                                     └─ 반격 피해·연출 처리
```

### A. 전투 흐름은 적 행동이 끝난 다음 종료 여부를 검사

`Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs`

- `RunEnemyTurn()` / 약 580~590줄: 전투 매니저 자신이 `enemyScript.RunAITurn(...)`을 시작합니다.
- `BattleLoop()` / 약 506줄: `yield return RunEnemyTurn(CurrentUnit)`으로 적 행동 완료를 기다립니다.
- 바로 다음에서 `ShouldEndBattle()`를 검사하고, 최종적으로 `HandleBattleCompletion()`으로 갑니다.
- `CompleteBattle()` / 약 405줄이 `OnBattleEnded`를 발행합니다.

플레이어 턴은 `WaitUntil` 조건에 사망·전투 종료가 포함돼 있습니다. 반면 적 턴은 위 코루틴을 기다리는 동안 매 프레임 별도로 `ShouldEndBattle()`를 검사하는 구조가 아닙니다. `HandleUnitDied()`가 `CurrentUnit=null`로 만들어도 이 대기를 직접 해제하지 않습니다.

### B. 그 안에서 실행 주체가 적 오브젝트로 바뀜

`Assets/_ProtoType_Merge/ASB/Scripts/Unit/EnemyScript.cs` / 약 225, 236줄:

```csharp
yield return StartCoroutine(battleManager.ExecuteGridSkill(self, target, highlightSkill));
```

위의 생략된 호출 대상은 `this`, 즉 `EnemyScript`입니다. 인자로 넘긴 IEnumerator가 `BattleManager`의 메서드에서 만들어졌다는 사실은 코루틴의 실행 주체를 바꾸지 않습니다.

따라서 `RunAITurn` 자체는 BattleFlowManager에서 실행되지만, 그 안에서 기다리는 `ExecuteGridSkill`은 적 오브젝트에 종속됩니다. 이 **중간 한 단계의 실행 주체 차이**가 핵심입니다.

### C. 공격의 하위 연출·반격은 BattleManager에서 계속 실행될 수 있음

`Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleManager.cs`

- `ExecuteGridSkill()` 약 1010줄: `StartCoroutine(ApplySkillExecutionResultRoutine(...))`을 호출합니다. 여기의 `this`는 BattleManager입니다.
- `ApplySkillExecutionResultRoutine()` 약 740~754줄: 반격 큐를 처리한 뒤 `LogEventTrackingSummary()`와 완료 콜백을 호출합니다.
- `ExecuteCounterSkill()` 약 1065~1105줄: 반격 피해와 연출을 처리합니다.
- `DrainFollowUps()` 약 1153줄: 후속 명령을 기다립니다.

`Assets/_ProtoType_Merge/ASB/Scripts/Battle/Command/CounterAttackActionCommand.cs`도 `battleManager.StartCoroutine(...)`을 사용합니다.

따라서 적이 사라진 뒤에도 BattleManager가 소유한 하위 작업이 끝나면서 `CombatEvent` 로그를 남기는 상황이 가능합니다. **그 로그는 하위 결과 처리의 완료이며, EnemyScript가 시작했던 최상위 ExecuteGridSkill의 정상 복귀나 턴 종료를 증명하지 않습니다.** 최상위 `OnSkillResolved` 발행은 `ExecuteGridSkill`의 대기 이후에 있으므로, 하위 요약 로그와 구분해서 계측해야 합니다.

### D. 적 삭제 시점이 반격 완료보다 빠름

`Assets/_ProtoType_Merge/ASB/Scripts/Unit/EnemySpawner.cs`

- `_corpseRemovalDelay` 기본값: 0.8초, 약 35줄.
- `HookEnemyDeathRemoval()` → `OnDied` 구독: 약 788줄.
- `ScheduleCorpseRemoval()` → 지연 제거: 약 795줄.
- `RemoveCorpseNow()` → `RemoveUnit(..., refreshQueue:false)` 후, 시체 보존 대상이 아니면 `Destroy(go)`: 약 807~828줄.

관측된 참가자 제거 시점은 HP 0 이후 약 0.8초이며, 반격 결과 요약보다 약 3초 빠릅니다. 이 시간 순서가 실행 주체 소멸 가설을 뒷받침합니다.

`EncounterParticipant.KeepCorpseAfterDeath=true`인 적은 오브젝트를 남기는 별도 분기입니다. 참가자 제거 로그만으로 두 경우를 구분할 수 없으므로 재현 시 해당 값과 실제 `OnDestroy`를 기록해야 합니다.

## 5. 확정한 사실과 아직 남은 가설

| 구분 | 판단 |
|---|---|
| 확정 | 4키 스킵은 턴 종료 이벤트를 전달했고 적 턴으로 넘어갔습니다. |
| 확정 | 마지막 방패병이 반격으로 사망했고 약 0.8초 뒤 참가자 제거 로그가 남았습니다. |
| 확정 | 반격과 원래 공격의 하위 결과 요약은 그 뒤에 기록됐습니다. |
| 확정 | 적 공격 최상위 코루틴의 실행 주체는 EnemyScript이며, 하위 반격 처리는 BattleManager입니다. |
| 확정 | 전투 흐름은 적 행동 코루틴 대기 이후에 종료 여부를 검사합니다. |
| 가장 유력한 가설 | 적 삭제가 최상위 공격 코루틴을 중단하고, 상위 적 턴 대기가 재개되지 못합니다. |
| 미확인 | 해당 실행에서 실제 Destroy가 실행됐는지, 그 직후 각 코루틴이 정확히 어디서 멈췄는지. |
| 미확인 | `OnBattleEnded`가 발행되지 않았는지, 발행 후 결과창 이전의 연출 대기에서 멈췄는지. |
| 미실행 | 에이전트의 Play 재현, 수정안 적용 및 수정 후 회귀 검증. |

Unity 공식 문서에는 실행 주체가 파괴되거나 그 GameObject가 비활성화되면 코루틴이 중단된다고 설명되어 있습니다. [StartCoroutine 공식 문서](https://docs.unity.cn/ScriptReference/MonoBehaviour.StartCoroutine.html)

중단·삭제된 하위 코루틴을 기다리던 부모가 재개되지 않는 패턴도 공식 이슈에 보고되어 있습니다. 이는 이번 가설을 뒷받침하는 엔진 동작 참고이며, 이 프로젝트에서의 재현 확인을 대신하지 않습니다. [Unity 공식 이슈: Parent Coroutine does not resume…](https://issuetracker.unity3d.com/issues/parent-coroutine-does-not-resume-when-yielding-to-child-coroutine-which-is-destroyed-slash-stopped)

### 함께 구분할 대안: 결과창 이전 연출 대기

`BattleSceneManager.PostBattleSequence()`는 `OnBattleEnded` 이후에도 다음 순서로 기다립니다.

```text
OnBattleEnded
→ HandleBattleEndedForTransition
→ PostBattleSequence
→ WaitForActivePresentationSequence
→ WaitForRemainingCueEffectsAndDeathAnimations
→ 결과/보상 처리
→ ShowBattleResultUI
```

`WaitForActivePresentationSequence()`는 `battleManager.Presentation.IsSequenceRunning`이 false가 되길 기다립니다. 이 값이 남아 있다면, 종료 판정은 됐어도 결과창은 보이지 않을 수 있습니다. 현재 로그만으로 완전히 배제하지 않습니다. 아래 계측에서 `OnBattleEnded`가 확인된다면 조사 우선순위를 이 경로로 전환해야 합니다. 결과창 자체를 새로 만드는 것은 첫 대응이 아닙니다.

## 6. UI 개편·스킵·반격과의 관계

이번 UI 작업은 실제 입력 상태와 버튼 표시를 맞추고, 수동 턴 시작 시 자동 스킬 선택을 제거했습니다. `InputHandler.SkipCurrentTurn()`의 기존 점유권 요청 및 `PlayerSkillActionResolved(actor, null)` 호출은 변경하지 않았습니다. `ResetTargetingState()`에 UI 동기화 이벤트가 추가됐지만 실제 관측에서는 그 뒤 스킵과 다음 적 턴이 정상 진행했습니다.

또한 다음 두 상태는 별개입니다.

- `InputHandler.PendingAction`: 플레이어가 수동 시전을 위해 선택해 둔 행동.
- `BattleCharactor.SelectedSkillData`: 유닛에 실행 연결된 스킬 데이터.

스킵하거나 선택 표시를 지워도 연결된 스킬 데이터 자체는 제거하지 않습니다. 기존 반격 판정은 `CollectCounterAttackRequests()`에서 피격 유닛의 `SelectedSkillData`를 읽습니다. 따라서 **스킵 후 반격이 나온 사실 자체는 선택 해제 실패의 증거가 아닙니다.** 이번 조사 대상은 반격의 발생 여부가 아니라, 그 반격으로 적이 죽은 뒤 전투가 정지한 현상입니다.

이번 UI 개편이 원인을 만들었다고 단정할 근거는 발견하지 못했습니다. 다만 UI 변경 전후의 동일 조건 A/B 재현을 수행하지 않았으므로 UI 변경과 완전히 무관하다고 실험적으로 확정한 상태도 아닙니다.

## 7. 관련 커밋: 수정 범위 판단용

| 커밋 | 작성자 / 날짜 | 확인한 내용 |
|---|---|---|
| `bff38e2489730b405391240fa294eac4ca1844bd` | Bin9825 / 2026-06-17 | 현재 EnemyScript의 해당 `StartCoroutine(ExecuteGridSkill)` 호출 줄에 대한 blame 결과. 제목 `[fix] 광역 공격시 바닥이 보이도록 수정함` |
| `ffbbcccf64822b21303c2785b83e91278f45b8c8` | Bin9825 / 2026-09-09 | 적 사망 이벤트 구독, 지연 제거, 시체 보존 분기 및 `Destroy(go)`가 추가됨. 제목 `[feat] 4구역 보스전 기믹 완성` |
| `1315fd5d9bd06e3740ff99b1a6ed81244ee149d3` | Bin9825 / 2026-09-11 | 전투 흐름의 튜토리얼 확장과 최상위 스킬 완료 이벤트 등이 추가됨. 제목 `[feat] 튜토리얼  구조 1차 완성` |

9월 9일의 삭제 기능은 기존에 있던 적 소유 코루틴과 충돌할 조건을 추가한 것으로 보입니다. 이는 변경의 상호작용에 대한 분석이지, 그 커밋만을 원인으로 확정한 회귀 실험 결과는 아닙니다. 해당 커밋 전체를 되돌리는 방안은 제안하지 않습니다.

관련 네 핵심 파일은 분석 당시 로컬 미커밋 변경이 없었습니다. ASB 에이전트는 먼저 자기 브랜치에 위 코드가 같은 형태로 존재하는지 확인해야 합니다. 최신 코드에서 이미 수정됐을 가능성은 이 문서의 기준 시점 밖입니다.

## 8. 수정 전 원인을 확정하는 관측 지점

현재 재현 실행은 종료되어 있으므로 다음 재현에서 관측합니다. 아래는 임시 진단 제안이며 이 문서 작성 중 실제로 삽입하지 않았습니다. 공용 프레임워크나 영구 모니터를 새로 만들 필요는 없습니다.

동일한 적의 식별자와 스킬 `ExecutionId`, 프레임 번호/경과 시간을 함께 남기고, **삭제 후 오브젝트 이름을 읽다가 새 예외가 나지 않도록 식별 문자열을 미리 보존**합니다.

| 위치 | 기록할 경계/상태 | 목적 |
|---|---|---|
| EnemyScript.RunAITurn | ExecuteGridSkill 대기 진입 / 대기 복귀 | 적 공격 하위 대기에서 멈췄는지 |
| EnemySpawner.RemoveCorpseNow | 적 식별자, keepCorpse, Destroy 직전 | 참가자 제거와 실제 파괴를 구분 |
| 적 오브젝트의 종료 관측 | 실제 OnDestroy 또는 디버거 관측 | 파괴 시각 확인 |
| BattleManager.ExecuteGridSkill | ApplySkillExecutionResultRoutine 대기 전후, 최종 완료 이벤트/콜백 | 하위 요약 이후 최상위가 복귀했는지 |
| BattleFlowManager.RunEnemyTurn | RunAITurn 대기 진입 / 복귀 | 전투 흐름으로 제어가 돌아왔는지 |
| BattleFlowManager.CompleteBattle | 결과 종류 및 호출 횟수 | 종료 판정 여부/중복 여부 |
| BattleSceneManager.PostBattleSequence | 진입, 각 연출 대기 전후, 결과창 생성 진입 | 종료 판정 뒤에 멈춘 경우를 구분 |

판별 기준:

- **적 Destroy 확인 + 하위 CombatEvent 확인 + ExecuteGridSkill 대기 복귀 없음 + CompleteBattle 없음**이면 실행 주체 소멸 가설을 강하게 확정할 수 있습니다.
- **CompleteBattle/OnBattleEnded 확인 + PostBattleSequence 연출 대기 복귀 없음**이면 결과창 이전 대기 상태를 조사합니다. `Presentation.IsSequenceRunning` 및 관련 활성 시퀀스 수, 타임스케일/일시정지 상태를 확인합니다.
- 새 예외가 나오면 먼저 해당 예외의 최초 스택을 확인합니다. 기존 가설에 맞추려고 오류를 무시하지 않습니다.

## 9. 최소 수정 후보와 주의할 경계

### 우선 후보: 적 공격 코루틴의 실행 주체만 이관

`EnemyScript.RunAITurn()`의 클래스스킬/무기스킬 두 분기에 공통인 아래 호출이 대상입니다.

```csharp
// 현재: 실행 주체가 EnemyScript이므로 적 삭제에 종속됩니다.
yield return StartCoroutine(
    battleManager.ExecuteGridSkill(self, target, highlightSkill));

// 검토안: 전투 동안 살아 있는 BattleManager가 실행합니다.
yield return battleManager.StartCoroutine(
    battleManager.ExecuteGridSkill(self, target, highlightSkill));
```

이렇게 하면 바깥 `RunAITurn`의 실행 주체는 기존 BattleFlowManager이고, 기다리는 공격 코루틴의 실행 주체는 BattleManager가 됩니다. 적 한 개가 삭제되어도 둘의 작업 수명은 유지됩니다. 현재 직접 원인 후보를 해결하는 데 `BattleManager.cs` 자체의 편집이나 새 매니저 추가가 필수로 보이지는 않습니다.

적 삭제 정책을 그대로 유지하면 시전 완료 시점의 `actor`는 Unity의 파괴된 오브젝트 참조일 수 있습니다. 현재 `ApplyCollateralDamage()`는 `actor == null` 가드가 있고 `SkillResolutionContext.WasPlayerAction`도 null을 검사하지만, **ASB 브랜치의 추가 구독자·완료 콜백까지 안전하다는 뜻은 아닙니다.** 수정 후 최상위 `OnSkillResolved`가 실제 발행되는 경로에서 파괴된 시전자 접근 여부와 이벤트 의미를 확인해야 합니다. 발견된 문제에 한해서 좁게 보완합니다.

`yield return battleManager.ExecuteGridSkill(...)`처럼 IEnumerator를 직접 이어 실행하는 방식도 현재 바깥 실행 주체를 따르게 하는 대안입니다. 실행·취소 방식이 달라지므로 두 방식을 동시에 적용하지 말고, ASB가 전투 종료/씬 전환 정책과 맞는 방식 하나를 선택해 검증합니다.

### 첫 수정에서 피할 우회

- 시체 제거 지연을 늘려 해결됐다고 판단하지 않습니다. 연출 길이·배속·후속 행동에 따라 다시 충돌할 수 있습니다.
- 적 사망 즉시 결과창을 강제로 호출하거나, 모든 전투 종료를 사망 콜백으로 옮기지 않습니다. 진행 중 반격·연출·후속 결과 처리와 종료 순서를 함께 검토해야 하는 별도 변경입니다.
- 적 턴 종료를 만들려고 `PlayerSkillActionResolved`를 임의 발행하지 않습니다. 이 이벤트는 플레이어 행동/스킵의 계약입니다.
- 스킬 선택 상태나 UI 버튼을 되돌려 원인을 가리지 않습니다.
- 전체 시체 삭제 기능·보스 시체 보존 기능을 제거하지 않습니다.

### 추가 점검 후보 — 자동으로 수정 범위를 넓히지 않음

`EnemyScript.RunAITurn()` 약 157줄의 `StartCoroutine(hostageScenario.ExecuteThreat(...))`에도 적이 실행 주체가 되는 형태가 있습니다. 이번 방패병 공격의 직접 경로는 아니므로 별도 점검 후보로 둡니다. 또 마지막 적이 아닌 공격자가 반격으로 사망하는 경우에도 동일한 적 턴 대기 문제가 생길 수 있습니다.

## 10. 재현·회귀 검증과 완료 기준

최소한 아래 항목을 구분하여 결과를 남깁니다. 실제 플레이 실행과 임시 진단 코드 적용은 ASB 측 세션의 승인 범위에 따릅니다.

| 경우 | 기대 결과 |
|---|---|
| 보고된 조건: 저스티스 스킵 → 마지막 적 공격 → 저스티스 반격으로 적 사망 | 적 삭제와 무관하게 행동 처리 종료, Victory 이벤트 1회, 결과창 정상 표시 |
| 저스티스가 스킵하지 않은 경우의 동일 반격 사망 | 정상 종료. 스킵이 필수 발생 조건인지 구분 |
| 다른 적이 살아 있는 상태에서 공격자가 반격으로 사망 | 결과창을 띄우지 않고 다음 생존 유닛 턴으로 진행 |
| 반격이 없거나 반격 후 적이 생존 | 기존 적 턴 종료/다음 턴 진행 유지 |
| 플레이어의 일반 공격으로 마지막 적 사망 | 기존 정상 종료 유지 |
| 클래스스킬 및 무기스킬 분기 | 두 호출부 모두 실행 주체와 완료 동작 확인. 실제 사용하는 분기부터 검증 |
| 배속 변경 및 메뉴 일시정지 후 재개 | 시체 제거/연출 길이 차이에도 대기가 영구 정지하지 않음 |
| 적 시체 보존·부활을 사용하는 기존 전투 | 삭제 정책을 건드리지 않았으며 기존 전투 지속/부활 흐름 유지 |
| 전투씬 종료 또는 재진입 | 이전 전투의 코루틴/후속 큐/종료 이벤트가 새 전투에 남지 않음 |
| UI 회귀 | 수동 스킬 선택 → 대상 클릭 → 시전 후 선택 해제, 4키 스킵 정상 유지 |

반격이 확률적으로 발생하지 않은 실행은 이 버그의 통과 검증으로 세지 않습니다. 재현용 제어가 필요하면 테스트에 한정해 적용하고 제품의 스탯/테이블 변경으로 남기지 않습니다. 검증하려는 것은 피해량의 적절성이 아니라 **사망·삭제가 진행 중 행동의 완료 전달을 끊는가**입니다.

완료 시에는 다음을 기록합니다.

1. 실제로 멈췄던 대기 지점과 그 근거 로그.
2. 최종 수정 파일과 실행 주체 변경 내용.
3. 적 사망/삭제 → 반격 및 원공격 완료 → 턴 해결 → 전투 종료 이벤트 → 결과창의 확인 결과.
4. 남은 적이 있을 때 다음 턴으로 진행한 결과.
5. 임시 진단 코드 정리 여부 및 미검증 항목.

## 11. ASB 에이전트의 시작 순서

1. 현재 checkout의 변경 상태를 확인하고 JC의 씬 배치·UI 변경을 보존합니다.
2. 이 문서의 기준 코드와 자기 브랜치의 EnemyScript / EnemySpawner / BattleFlowManager / BattleManager를 비교합니다.
3. 보고된 전투 또는 같은 수명 조건에서 §8의 대기 경계를 관측합니다.
4. 원인이 일치하면 §9의 작은 수정부터 적용하고 §10으로 검증합니다.
5. 원인이 다르면 최초로 복귀하지 않은 경계를 기준으로 분석을 갱신합니다. 특히 `OnBattleEnded` 이전인지 이후인지부터 구분합니다.

이 문서는 원인 분석과 작업 인수인계 자료입니다. 문서 작성자가 적용한 버그 수정은 없으며, ASB에게 메시지 전송·커밋·push도 수행하지 않았습니다.
