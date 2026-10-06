# 율리아 및 적 유닛의 메시·VFX·애니메이션 구현 구분

작성·확인일: 2026-10-06  
대상: `JC_BattleTestScene`과 해당 씬이 사용하는 적 프리팹  
작성 목적: 기존 에셋과 기능, 이번 대화에서 제가 추가한 구현, 임시 구현과 남은 부분을 구분합니다.

## 1. 먼저 확인하실 내용

**율리아 본체 모델, 증폭기 모델, 증폭기의 `2010_Orb`, 본체의 공격 Timeline과 줄 번개 VFX는 기존 구현을 사용합니다. 제가 새로 만든 것은 JC 테스트용 연결·상태 연출, 장막의 임시 시각 표현, 굴렁쇠의 임시 절차적 외형과 움직임입니다. 새 캐릭터 FBX·리깅·애니메이션 클립은 제작하지 않았습니다.**

| 대상 | 메시·외형 | 공격 VFX | 상태 VFX | 애니메이션·움직임 |
|---|---|---|---|---|
| 율리아 본체 `40001` | 기존 `Madonna.001.fbx` 재사용 | 기존 폭풍·빔 프리팹 및 Timeline 재사용 | JC 손 위 구체, 공급선, 피격 펄스, 전환 표시 추가 | 기존 Animator·클립·Timeline 유지. 흡수 연결 코드는 JC 독립 사본 |
| 나락의 증폭기 `40002` | 기존 `Amplifier.001.fbx`와 `2010_Orb` 재사용 | 기존 폭풍 VFX를 JC 시전 경로에서 호출 | JC 충전 표시, 축소 소멸, 연기, 확대 복구, 공급선 추가 | 원본 Animator 없음. 새 클립 없이 코드로 구체 크기·연출 시간 제어 |
| 공멸의 증폭기 `40003` | `40002`와 같은 기존 외형 재사용 | 기존 빔 VFX를 JC 시전 경로에서 호출 | `40002`와 같은 JC 상태 연출 | 원본 Animator 없음. 새 클립 없음 |
| 장막 `40004` | 기존 율리아 모델을 사용하는 JC 임시 프리팹 | 장막 행동용 JC 연출 경로. 별도 공격 클립 제작 없음 | JC 보라색 타원막·링·맥동·피격 흔들림·소멸 연기 | 기존 Hit 포즈를 정지시킴. 장막의 움직임은 코드로 구현 |
| 절망의 굴렁쇠 `40005` | 기존 원기둥을 실행 인스턴스에서 숨기고 JC 링·금속판 외형 생성 | JC 돌진 잔상·도착 위치 파괴 연출 | JC 소환진, 피격 펄스, 금속 파편·연기 | 원본 Animator 없음. 부유·회전·돌진 모두 코드로 구현 |
| 그 외 일반 적 8종 | 기존 모델 유지 | 이번 작업에서 새로 제작한 VFX 없음 | 이번 작업에서 새로 제작한 VFX 없음 | 기존 컨트롤러 유지. 이번 작업에서 신규 클립 없음 |

“기존”은 **이 JC 전투 테스트 작업을 시작하기 전에 프로젝트에 존재하던 것**을 뜻합니다. 기존 에셋이 과거 다른 대화에서 제작됐더라도 이번 신규 제작량에 포함하지 않습니다. “JC 신규”는 이번 대화에서 제가 추가한 것을 뜻하며, 원본을 복사해 연결을 분리한 코드와 순수 신규 시각 표현을 다시 구분합니다.

## 2. 확인 방법과 적용 범위

실제 프리팹을 Unity `AssetDatabase`로 읽어 메시·Animator·컨트롤러·클립·Timeline 참조를 확인했습니다. 이어서 JC 소스의 실제 상태 처리, 원본 HP 표시 코드, Git 등록 이력, 이전 전투 검증 기록과 캡처를 대조했습니다. 이번 브리핑을 위해 Play를 다시 실행하거나 씬·프리팹·프로필을 수정하지 않았습니다.

분리 기준은 다음과 같습니다.

| 구분 | 의미 | 예 |
|---|---|---|
| 기존 원본 재사용 | 기존 에셋 자체와 동작을 사용 | 본체 FBX, AnimatorController, 공격 Timeline, 폭풍·빔 프리팹 |
| 기존 코드의 JC 독립 연결 | 원본 코드 사양을 출발점으로 JC 매니저에 연결한 별도 코드 | `JcMadonnaSkillReceiveTimeline`, `JcSkillPresentationDirector` |
| JC 신규 시각 표현 | 이번 작업에서 추가한 셰이더·재질·절차적 외형·상태 효과 | 장막, 굴렁쇠 프레임, 연기·파편·충전 표시 |
| 미구현·임시 | 정식 에셋이나 완성된 개별 연출로 보아서는 안 되는 부분 | 정식 장막·굴렁쇠 모델, 별도 파괴 클립, 정밀한 공급 연출 |

테스트 기반과 임시 장막 프리팹은 10/05 단계에서 만들었고, 상태 VFX와 굴렁쇠 외형·돌진 폴리싱은 10/06 단계에서 추가했습니다. 함께 전달된 기능 커밋은 `00054757`, 현재 확인한 통합 HEAD는 `61baaa92`입니다. 작업 시작 전 기준 `0705af74`와 현재 HEAD를 비교했을 때 아래 기존 적 프리팹·본체 공격 연출·ASB 애니메이션 영역에는 차이가 없습니다.

> 본 게임 원본을 수정하지 않았다는 것과, JC 실행 인스턴스의 동작이 원본과 같다는 것은 다릅니다. JC 씬에서는 원본 프리팹을 생성한 뒤 일부 제어 컴포넌트를 JC 대응 컴포넌트로 교체·비활성화합니다. 구체적인 차이는 아래에 적었습니다.

## 3. 율리아 본체 — 40001

### 3.1 기존 메시·리깅·소켓

원본은 [Unit_VillanMadonna_40001.prefab](C:/Dev/HeroInfluence/Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanMadonna_40001.prefab)입니다. 내부 `CommonModel_Madonna`의 SkinnedMeshRenderer는 [Madonna.001.fbx](C:/Dev/HeroInfluence/Assets/Resources/FBXModel/Villain/Madonna/Madonna.001.fbx)를 참조하며, 실제 메시 이름은 `tripo_node_8953e898`입니다.

기존 메시·스킨·본 구조·원본 재질을 다시 제작하지 않았습니다. 손 소켓과 기존 `UnitSocketHolder`, `StrikerHandPoseLateLock`도 이번에 새로 만든 것이 아닙니다. JC 손 위 에너지 구체는 기존 `UnitVisualProfile.AttackEffectSocket`, 없으면 `R_HandSocket`을 찾아 붙입니다. 구체는 캐릭터 메시의 일부가 아니라 별도 런타임 VFX입니다.

### 3.2 기존 애니메이션

원본 컨트롤러는 [Madonna_Ani_Controller.controller](C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/AniController/AniController/Enemy/Madonna_Ani_Controller.controller)입니다. 이번 작업에서 컨트롤러나 클립 내용을 변경하지 않았습니다.

| 실제 컨트롤러 상태 | 실제 연결된 기존 클립 | 이번 작업의 처리 |
|---|---|---|
| `Idle` | `Idle_Battle_MagicWand` | 재사용 |
| `ClassSkill_1` | `Attack03_Start_MagicWand` | 기존 공격 준비 모션 재사용 |
| `ClassSkill_2` | `Attack03_Maintain_MagicWand` | 기존 공격 유지 모션 재사용 |
| `ClassSkill_3` | `LevelUp_MagicWand` | 기존 연결 유지. 모든 흡수 연출이 이 클립을 사용한다고 단정하지 않음 |
| `Hit` | `GetHit01_MagicWand` | 기존 피격 모션 재사용. 장막에서는 이 포즈의 일부를 정지시킴 |
| `Dead` | `Die01_MagicWand` | 기존 사망 연결 유지. 새 사망 클립 제작 없음 |
| 이동·가드·기타 | `MoveForward`, `MoveReturn`, `Gaurd`, `Dash`, 무기 스킬 등 기존 DumiAnimation 클립 | 연결 유지. 율리아 전용 완성 모션으로 새로 제작한 것이 아님 |
| `FixWrist/WristOverride` | `WristOverride` | 기존 손목 보정 연결 유지 |

주요 MagicWand 클립은 `Assets/_ProtoType_Merge/ASB/AniController/Animation/CopyAnimation/`의 FBX를 참조합니다. 컨트롤러에 존재하는 모든 상태가 현재 율리아 AI에서 사용되거나 개별 검증됐다는 뜻은 아닙니다.

### 3.3 기존 공격 Timeline·VFX

| 스킬 | 기존 연출 에셋 | 실제 사용하는 내용 | 제가 추가한 범위 |
|---|---|---|---|
| 나락의 폭풍 `400011` | `SkillPresentation_400011.asset` → `Skill400011_40001_Storm.playable`, `FX_400011_RowLightning.prefab` | 기존 준비·유지 모션, Timeline/Cue 타이밍, 대상 줄의 보라색 번개와 감전 표현 | JC 매니저에서 기존 연출을 실행하도록 연결. 번개 자체 새 제작 없음 |
| 공멸의 궤적 `400012` | `SkillPresentation_400012.asset` → `Skill400012_40001_Beam.playable`, `FX_400012_RowBeam.prefab` | 기존 준비·유지 모션, 관통하는 한 줄 번개·대상 감전 | 기존 에셋 재사용. 빔 자체 새 제작 없음 |
| 소환 관련 `400013` | `SkillPresentation_400013.asset` → `Skill400011_40001.playable` | 기존 본체 시전 모션. 이 Presentation의 공격 Cue에는 전용 VFX 등록이 없음 | 생성되는 굴렁쇠 쪽에 JC 소환진·상승·외형을 추가 |
| 증폭기 공급 수신 `400014` 관련 | `MadonnaSkillReceiveTimeline`, `Skill400014_SkillReceive.playable` | 기존 예약 성공 이벤트에 반응하는 별도 Director·팔 트랙 연결 | JC Blackboard/Flow에 연결하는 `JcMadonnaSkillReceiveTimeline` 사본과 공급선 추가 |

**주의:** 현재 `Skill400014_SkillReceive.playable`을 실제로 읽었을 때 Animation Track의 클립은 `Idle_Battle_MagicWand`입니다. 컨트롤러의 `ClassSkill_3`에 `LevelUp_MagicWand`가 있다는 이유만으로 “현재 수신 Timeline이 LevelUp 모션을 재생한다”고 설명하면 부정확합니다. 기획의 모션 명칭, 컨트롤러 상태, 실제 Timeline 클립은 각각 구분해야 합니다.

원본 [RowLightningCueEffect.cs](C:/Dev/HeroInfluence/Assets/ASB_Work/Effect/RowLightningCueEffect.cs)는 기존 히어로 `2020 ChainLightningSkill`의 볼트 렌더러와 `LightningShock`을 사용합니다. 폭풍·빔의 줄 방향, 그리기·유지·페이드, 보라색 덮어쓰기와 감전 템플릿은 이 기존 코드에 이미 있었습니다. 이번 JC 상태 VFX 셰이더로 공격 번개를 다시 만든 것이 아닙니다.

### 3.4 제가 추가한 본체 상태 표현

| 상태·트리거 | JC 신규 표현 | 실제 구현·제약 |
|---|---|---|
| 대기 | 손 위의 보라색 에너지 구체와 서로 다른 방향의 링 | `JcYuliaUnitVfx.Configure`가 기존 손 소켓에 유지형 `Charge`를 부착 |
| 증폭기 스킬 공급·예약 성공 | 증폭기에서 본체 상체로 이어지는 보라색 공급선과 구체 섬광 | `JcYuliaVfxDirector.Supply`. 기존 흡수 Timeline과 별도로 표시 |
| 피격 | 유닛 주변으로 퍼지는 링 펄스 | `OnHpChanged`에서 HP 감소를 관찰. 본체 피격 클립은 기존 것을 사용 |
| 페이즈 진입 | 상승하는 링·회복형 펄스 | `PhaseEntry`. 새 본체 전환 애니메이션 클립은 없음 |
| 실제 사망 이벤트 | 보라색 연기, 유지 구체 종료 | `OnDied`에 반응. 전체전의 HP 50% 장막 교체는 사망과 별도 경로 |

공급 표현은 두 개의 흔들리는 선으로 연결을 보여주는 간소화 구현입니다. 레퍼런스의 세밀한 구체 분리·흡수 궤적이나 전용 새 신체 모션을 완성한 상태는 아닙니다. 3페이즈 진입에서도 기존 본체가 연속적으로 변형되는 메시 애니메이션을 제작하지 않았고, JC 흐름에서 장막 유닛으로 교체합니다.

## 4. 나락·공멸의 증폭기 — 40002 / 40003

### 4.1 기존 메시와 2010_Orb

두 원본 전투 프리팹은 다음과 같습니다.

- [Unit_VillanAmplifier_40002.prefab](C:/Dev/HeroInfluence/Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanAmplifier_40002.prefab)
- [Unit_VillanAmplifier_40003.prefab](C:/Dev/HeroInfluence/Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanAmplifier_40003.prefab)

두 프리팹 모두 [CommonModel_VillanAmplifier.prefab](C:/Dev/HeroInfluence/Assets/Resources/prefab/CommonPrefab/EnemyUnit/CommonModel_VillanAmplifier.prefab)을 사용합니다. 외곽 프레임은 [Amplifier.001.fbx](C:/Dev/HeroInfluence/Assets/Resources/FBXModel/Villain/Amplifire/Amplifier.001.fbx)의 메시 `futuristic+portal+base+3d+model`입니다. 기본 Sphere와 소켓 등도 원본에 존재합니다. 제가 이 외곽 모델을 새로 모델링하거나 원본 재질을 수정하지 않았습니다.

**`2010_Orb`의 원본은 [FlareBombOrbFullDark.prefab](C:/Dev/HeroInfluence/Assets/RenderFX/HeroSkill/Lumina/FlareBomb/Prefabs/FlareBombOrbFullDark.prefab)입니다.** 증폭기의 중첩 프리팹 GUID를 Unity에서 해석해 확인했습니다. 사용자가 제작한 플레어 봄 계열 구체가 증폭기에 이식된 구조이며, 이번 작업에서 구체 본체를 새로 제작한 것이 아닙니다.

원본 구체에는 `FlareOrbLayerToggle`, `FlareOrbShell`, `FlareOrbSpriteAura`, `FireLightFlicker`와 Sphere·Quad 기반 레이어가 이미 포함돼 있습니다. `TeardropShell` MeshFilter가 편집 상태에서 비어 있는 것은 이번에 메시를 제거해서 생긴 현상이 아닙니다. 해당 외형은 기존 절차적 셸 코드가 사용하는 부분이며, 비어 있는 편집 참조만으로 Missing Mesh라고 판정하지 않았습니다.

### 4.2 원래 있던 HP 반응과 새 연출의 차이

원본 [AmplifierOrbHealthVisual.cs](C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Unit/AmplifierOrbHealthVisual.cs)의 `HandleHpChanged`는 **HP가 0보다 크면 `2010_Orb`를 켜고, 그렇지 않으면 즉시 끕니다.** 외곽 증폭기 모델은 사망 후에도 유지됩니다. 따라서 증폭기 파괴·복구의 시각 처리가 전혀 없었던 것은 아닙니다.

JC에서는 생성된 인스턴스에 한해서 이 컴포넌트를 비활성화하고 `JcYuliaUnitVfx`가 구체의 표시를 맡습니다. 원본 프리팹의 컴포넌트·스크립트 파일은 그대로 유지됩니다.

| 상태 | 기존 구현 | JC에서 제가 추가·변경한 표현 |
|---|---|---|
| 정상 대기 | 기존 금속 프레임과 플레어 봄 계열 구체 | 그대로 재사용 |
| 능력개방·공급 | 기존 전투 예약 로직, 본체 수신 Timeline 연결 | 구체 주변 Charge 섬광, 증폭기→본체 공급선 |
| 직접 공격 | 기존 공격 사양과 폭풍·빔 VFX 에셋 | Animator 없이 시전 준비→기존 VFX→피해 콜백을 진행하는 JC 연출 경로 |
| 피격 | 기존 HP 갱신 | 주변 피격 링 펄스 |
| HP 0·파괴 | 구체 즉시 OFF, 금속 프레임 유지 | 구체를 약 2초 동안 축소해 OFF, 보라색 연기. 금속 프레임 유지 |
| HP 회복·재활성 | 구체 즉시 ON | 구체를 다시 켜고 약 1.1초 동안 원래 크기로 확대, 상승 링 표시 |
| 에너지·충전 누적 | 기존 스택과 JC 규칙의 전투 수치 | 유지형 구체·링 추가, 스택에 따라 시각 크기 증가 |
| 과부하 | JC 규칙에서 장막 피해 배율 등 처리 | 공통 충전 표시를 사용. 과부하 전용 파열·폭발 연출은 별도 제작하지 않음 |

구체 복구는 **현재 위치에서 크기를 회복**하는 구현입니다. 주변 Recover 링은 위로 올라가지만 구체 자체를 아래에서 위로 이동시키는 별도 모션은 없습니다. “복구 상승”이라는 설명만으로 구체 이동까지 구현됐다고 이해하시면 안 됩니다.

충전 표시 크기는 시각적으로 최대 5스택까지만 커지며, 실제 전투 스택에 상한을 추가하지 않습니다. 3페이즈의 충전은 `Rules.GetCharge`를 읽고, 그 외에는 `BattleCharactor.EnergyStack`을 읽습니다. VFX가 HP·예약·충전 값을 직접 변경하지 않습니다.

### 4.3 애니메이션과 공격 경로

두 원본 증폭기 프리팹에는 Animator가 없습니다. 따라서 새 AnimatorController나 `.anim`을 만들어 공격·파괴·복구를 구동한 것이 아닙니다. 기존 구체 내부의 자체 VFX 동작에 JC의 크기 변화·링·연기와 코루틴 타이밍을 추가했습니다.

`JcYuliaSkillPresentation.Run`은 준비 표시 후 `40002`이면 기존 폭풍, `40003`이면 기존 빔을 재생하고 전투 실행기가 제공한 피해 콜백을 호출합니다. 본체의 Timeline을 증폭기에게 그대로 붙여 재생하는 구조는 아닙니다. 공격의 피해 판정과 연출은 분리되어 있습니다.

## 5. 장막 — 40004

### 5.1 기존에 있던 것과 없던 것

`40004`의 유닛 데이터와 보스전 사양을 참조할 수 있는 기존 기반은 존재합니다. 그러나 현재 사용하는 원본 적 전투 프리팹 목록에는 **정식 `40004` 장막 프리팹·전용 모델이 없습니다.** 기존 3페이즈 관련 코드나 데이터까지 전부 없었다는 뜻은 아닙니다.

제가 만든 [JC_YuliaVeil_Temporary.prefab](C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/JC/BattleTesting/JC_YuliaVeil_Temporary.prefab)은 기존 율리아 프리팹을 기반으로 한 테스트용 프리팹입니다. 내부 모델·스킨·컨트롤러는 기존 본체와 같습니다. 장막 전용 새 캐릭터 모델은 제작하지 않았습니다.

### 5.2 제가 추가한 장막 표현

| 구성 | 구현 | 구분 |
|---|---|---|
| 내부 율리아 외형 | 기존 `Madonna.001.fbx` | 기존 메시 재사용 |
| 정지 포즈 | 기존 `Base Layer.Hit` 상태를 정규화 시간 0.35에서 평가한 뒤 Animator 비활성화 | 기존 클립의 포즈 재사용 + JC 정지 처리 |
| 외부 장막 | Sphere를 타원체로 늘린 에너지 표면, 가장자리 강조·맥동 | JC 신규 절차적 VFX |
| 세 방향 링 | 세로·측면·수평 방향의 보라색 링 | JC 신규 LineRenderer 표현 |
| 피격 | 링 펄스와 장막 표면의 좌우 흔들림 | JC 신규 코드 동작. 새 피격 클립 없음 |
| 장막 진입 | Recover 링, 증폭기에서 장막으로 공급선, 전환 연출 대기 | JC 신규 연결·VFX |
| 장막의 행동·소환 | Animator가 멈춘 상태에서도 JC 전용 연출 경로로 진행 | JC 신규 시전 연결. 신체 시전 모션 없음 |
| 파괴 | 유지 표면·링 종료, 보라색 연기 | JC 신규 VFX. 전용 붕괴 클립 없음 |

현재 장막은 정식 모델·정식 포즈·정밀한 결합 장면을 대체하는 임시 표현입니다. Hit 포즈가 없는 대체 컨트롤러라면 해당 포즈 평가를 건너뛰고 Animator를 멈추므로, 정식 에셋 교체 시 포즈 연결을 다시 확인해야 합니다.

장막에는 별도 HP를 사용합니다. 본체 HP 50% 이하에서 3페이즈로 넘어가는 조건과 초과 피해 미이월 등은 JC 전투 규칙입니다. 이러한 게임 규칙이 장막 셰이더나 파티클에 들어 있는 것은 아닙니다.

## 6. 절망의 굴렁쇠 — 40005

### 6.1 기존 외형

원본은 [Unit_VillanWheel_40005.prefab](C:/Dev/HeroInfluence/Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanWheel_40005.prefab)이며, 내부 [CommonPrefab_Wheel.prefab](C:/Dev/HeroInfluence/Assets/Resources/prefab/CommonPrefab/EnemyUnit/CommonPrefab_Wheel.prefab)의 메시는 **Unity 기본 Cylinder**입니다. 원본에는 전투 컴포넌트·HP 표시·콜라이더·소켓이 있으며 Animator는 없습니다.

즉, 이번 작업 전에 굴렁쇠의 전투용 객체는 있었지만 레퍼런스와 같은 완성형 링 모델·애니메이션이 원본에 들어 있던 것은 아닙니다.

### 6.2 제가 만든 임시 메시

`JcYuliaUnitVfx.BuildWheel`은 **JC에서 생성된 원본 인스턴스의 MeshRenderer만 숨기고**, 자식 `JC_DespairWheelVisual`을 만듭니다. 원본 프리팹과 원본 Cylinder 에셋을 삭제·수정하지 않습니다.

| 부분 | JC 신규 제작 방식 | 정식 에셋 여부 |
|---|---|---|
| 원형 금속 링 | 반지름 0.48m, 관 두께 0.055m의 절차적 Torus. 48분할×8단면 | 실행 중 생성하는 임시 Mesh. 새 FBX 아님 |
| 외곽 금속판 | Unity Cube 메시 8개를 원주에 배치 | 기본 메시를 조합한 임시 외형 |
| 보라색 에너지 원 | LineRenderer 링 3개 | JC 신규 VFX |
| 금속 재질 | `JC_DespairWheelFrame.mat` | JC 신규 재질. 원본 재질 변경 없음 |

정식 텍스처·노멀맵·UV 아트워크·리깅을 제작한 모델이 아닙니다. `motion_09`의 금속 링과 에너지 외곽을 식별할 수 있도록 간소화했습니다.

### 6.3 상태·돌진·파괴

| 상태 | JC 신규 구현 | 실제 범위·제약 |
|---|---|---|
| 생성 | 바닥 소환진, 아래에서 약 0.7m 올라오는 외형 | 기본 상승 시간 0.8초. 별도 소환 연기는 현재 없음 |
| 대기 | 상하 부유와 회전 | 기본 부유 진폭 0.07m, 회전 35도/초. 카메라를 향하는 외형 |
| 공격 준비 | Charge 펄스 | 전용 새 공격 클립 없음 |
| 돌진 | 목표의 월드 위치로 루트 이동, 보라색 Trail | 기본 이동 구간 0.4초. 논리 점유 셀과 사거리는 바꾸지 않음 |
| 충돌·공격 | 실제 피해 콜백 후 후속 대기 | 새 물리 충돌 기반 피해가 아니라 기존 전투 판정 사용 |
| 일반 피격 | 공통 피격 링 펄스 | 별도의 변형·찌그러짐·Hit 클립 없음 |
| 파괴·자폭 | 링 섬광, 보라색 연기, 금속 파편 12개 | Cube 메시 파티클. 실제 프레임을 분해한 파편이나 파편 충돌 없음 |
| 자폭 위치 | 피해·반격 처리 후 실제 사망까지 돌진 도착 위치 유지 | 파괴 VFX가 원래 칸으로 돌아가 보이지 않도록 JC에서 보완 |
| 재소환 | 사망 객체 점유·참가자를 정리한 뒤 새 객체 생성 | 같은 객체의 부활 애니메이션이 아니라 신규 소환 |

파편은 고정 시각 난수 시드 `40005`를 사용하며 전투 난수에는 관여하지 않습니다. 돌진 Trail은 별도 JC 임시 VFX 루트에서 관리해 자폭 객체가 제거돼도 잠깐 남아 사라집니다. 전투 재시작 시에도 회수합니다.

기획 이미지의 복잡한 기계 구조·정밀한 소환 연기·전용 충돌 폭발·물리적으로 분해되는 금속 조각까지 구현한 것은 아닙니다. 현재는 전투 타이밍과 화면 가독성을 테스트할 수 있는 임시 형태입니다.

## 7. 그 외 일반 적 유닛

이번 율리아 작업에서 새 메시·VFX·애니메이션을 제작한 일반 적은 없습니다. 실제 적 전투 프리팹 폴더에는 다음 8종이 추가로 존재합니다. 현재 율리아 전체전·단독 프리셋에는 이들을 새로 배치하지 않았습니다.

| 기존 전투 프리팹 | 확인한 기존 외형 | 기존 주요 Animator | 이번 작업의 신규 메시/VFX/클립 |
|---|---|---|---|
| `Unit_LowerMonster_20001` | RPGTinyHeroWavePBR 몸체·창·가방 등 | `LowerVillan_Ani_Controller.overrideController` | 없음 |
| `Unit_MiddleMonster_20002` | RPGTinyHeroWavePBR 몸체·무기·방패 등 | `MiddleVillan_Ani_Controller.overrideController` | 없음 |
| `Unit_AdvancedMonster_20003` | RPGTinyHeroWavePBR 몸체·검·방패 등 | `AdvencedVillan_Ani_Controller.overrideController` | 없음 |
| `Unit_VillanDrone_20001` | `Villain/Drone/Drone.001.fbx` | 현재 프리팹에서 Animator 없음 | 없음 |
| `Unit_VillanGun_20002` | `Villan_Gun.EXP_Model.fbx`, 기존 총 메시 | `VillanGun_Ani_Controller.controller` | 없음 |
| `Unit_VillanSheild_20003` | `Villan_Sheild.EXP_Model.fbx` 등 | `VillanSheild_Ani_Controller.controller` | 없음 |
| `Unit_VillanTurret_20004` | `Villain/Turret/turret.001.fbx` | 현재 프리팹에서 Animator 없음 | 없음 |
| `Unit_VillanUMT_20005` | `Villain/UMT/UMT.001.fbx` | `UMT_Ani_Controller.controller` | 없음 |

이 목록은 실제 프리팹의 외형·컨트롤러 확인 결과입니다. “Animator 없음”은 “움직임·전투 기능 전부 없음”을 뜻하지 않습니다. 코드나 별도 연출기가 움직임을 처리할 수 있습니다. 일반 적의 모든 스킬별 VFX 연결과 완성도를 이번 율리아 검증에서 전수 검증한 것은 아니므로, **신규 제작 없음과 기존 VFX 미구현을 혼동하지 않아야 합니다.** 프리팹 이름의 ID가 겹치는 경우도 이번에 임의로 정리하지 않았습니다.

## 8. 공통 VFX 구조와 제가 새로 만든 파일

### 8.1 기존 히어로 구조에서 가져온 계약

기존 [VfxEffect.cs](C:/Dev/HeroInfluence/Assets/RenderFX/HeroSkill/_Shared/Scripts/VfxEffect.cs)의 재생·정지·배속·종료 통지 구조와 `ISkillEffectBehaviour`/`SkillEffectContext`를 사용했습니다. 기존 히어로 효과·공용 등록부·기존 번개 재질을 수정하지 않았습니다. 구조를 참조한 것과 모든 상태 VFX를 기존 카탈로그에 등록한 것은 다릅니다. 신규 상태 VFX는 JC 씬의 Director와 전용 프리셋에서 관리합니다.

### 8.2 신규 파일별 역할과 원본 대응

아래 파일의 기준 폴더는 [BattleTesting/Vfx](C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/JC/BattleTesting/Vfx)입니다.

| 신규 파일·함수 | 역할 | 대응되는 기존 영역·재사용 부분 |
|---|---|---|
| `JcYuliaVfxPreset.cs`, `JC_YuliaVfxPreset.asset` | JC 상태 VFX의 색·시간·재질·장막 크기 | 히어로 VFX의 프리셋 구조를 참고. 신규 설정 에셋 |
| `JcYuliaEnergy.shader` | 에너지 선, 가장자리·맥동 표면, 연기용 투명 표시 | 신규 셰이더. 기존 공격 번개 셰이더 대체 아님 |
| `JC_YuliaEnergy.mat`, `JC_YuliaSmoke.mat`, `JC_DespairWheelFrame.mat` | 신규 상태 에너지·연기·임시 금속 재질 | 원본 본체·증폭기·히어로 재질과 별도 |
| `JcYuliaVfxEffect.Play/Draw/Stop` | Pulse·Summon·Smoke·Recover·Veil·Supply·Charge·WheelBreak 8종 표시·회수 | 기존 `VfxEffect`와 스킬 효과 인터페이스 사용 |
| `JcYuliaVfxDirector.Attach/Show/Supply/PhaseEntry/WheelTrail` | 상태 이벤트→효과 연결, 공급·전환·Trail 관리 | 기존 `BattleVisualDirector`/`UnitEffectPresenter` 및 `MadonnaSkillReceiveTimeline.PlayReceive` 영역에 대응 |
| `JcYuliaUnitVfx.Configure/Hp/Died/Update/BuildWheel` | 유닛별 지속 상태, HP 반응, 임시 굴렁쇠 외형 | 기존 `AmplifierOrbHealthVisual.HandleHpChanged`, `BattleCharactor.OnHpChanged/OnDied`, 기존 모델·소켓에 대응 |
| `JcYuliaSkillPresentation.Supports/Run` | Animator가 없거나 정지된 보조 유닛의 시전 진행 | `SkillPresentationDirector`의 단일/AoE 연출 및 피해 콜백 역할에 대응 |

기존 코드의 독립 사본은 [BattleTesting/Compatibility](C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/JC/BattleTesting/Compatibility)에 있습니다. 특히 `JcMadonnaSkillReceiveTimeline`은 기존 기능을 JC 매니저 참조로 바꾼 것이고, `JcSkillPresentationDirector`에는 보조 유닛의 신규 연출 경로를 호출하는 분기가 있습니다. **두 파일의 기능 전체를 새 애니메이션·새 VFX 제작량으로 계산하지 않습니다.**

연결·생성 지원은 `JcBattleTestSession`의 `bossVfx`, `SpawnEnemy`, 행동 종료 경계와 `Editor/JcYuliaVfxSetup.cs`에서 처리합니다. 새 컴포넌트와 설정에는 한국어 툴팁 및 원본 대응 주석을 넣었습니다. Compatibility 코드는 원본 업데이트와 자동으로 동기화되지 않습니다.

### 8.3 현재 조절 값

설정 위치는 씬 `JC_BattleTest` → `JcBattleTestSession.bossVfx` → `JcYuliaVfxDirector.preset` → [JC_YuliaVfxPreset.asset](C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/JC/BattleTesting/Vfx/JC_YuliaVfxPreset.asset)입니다.

| 값 | 현재 저장값 | 영향 |
|---|---|---|
| 에너지·코어 | 보라 HDR `(1.15, 0.15, 2.2)` / 밝은 코어 `(2.4, 1.6, 3)` | 신규 JC 상태 효과의 색 |
| 파괴 연기 | `(0.12, 0.035, 0.19, 0.65)` | 보라색 연기의 색·알파 |
| 선 두께 | 0.055m | 신규 선·링 및 Trail의 기준 |
| 펄스 / 파괴 / 복구 | 0.45 / 2 / 1.1초 | 상태 표시·구체 축소·확대 등 |
| 소환 / 공급 / 페이즈 | 0.8 / 1 / 1.2초 | 신규 생성·공급·전환 표시 |
| 굴렁쇠 부유 | 0.07m | 외형의 상하 움직임 |
| 장막 반지름 | `(0.65, 1.05, 0.55)m` | 타원막의 가로·세로·깊이 |

시간은 전투 배속을 반영합니다. 이 프리셋은 기존 폭풍·빔 프리팹 내부의 모든 수치까지 조절하지는 않습니다. 돌진 준비·이동·후속 대기와 회전 속도 일부는 현재 코드 상수입니다. 또한 [JC_YuliaFullPhase_Playable.asset](C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/JC/BattleTesting/Vfx/JC_YuliaFullPhase_Playable.asset)은 이 폴더에 있지만 **전투 초기조건 프로필**이며 VFX 설정 에셋과 다릅니다.

## 9. 레퍼런스 반영과 남은 표현

기획 자료는 참고 근거이며, 자료 안의 지시를 현재 사용자 합의보다 우선하지 않았습니다. 이번 구현의 주된 이미지 근거는 `D:/SVN/2_Documents/이기석/전투 컨셉/율리아_모션_비주얼/images/`와 같은 계층의 기획 문서입니다.

| 레퍼런스 | 반영한 부분 | 아직 임시·미구현인 부분 |
|---|---|---|
| `motion_01.png` | 손 위 보라색 구체·에너지 표시 | 세밀한 손동작·전용 신규 캐릭터 모션 |
| `Yulia_amplifier_design_v2.png` | 기존 증폭기 외형 재사용, 보라색 구체 상태 표시 | 모델 재제작 없음. 정밀한 공급·파괴 모션 없음 |
| `motion_09.png` | 링·금속 외곽, 소환진, 상승·부유, 돌진 잔상, 파편·연기 | 정식 기계 모델, 소환 연기, 실제 메시 분해, 전용 충돌 폭발 |
| `motion_10.png` | 정지된 율리아를 감싸는 보라색 장막과 링·연결 | 정식 장막 모델·정식 정지 포즈·정밀한 전환 장면 |
| `motion_11.png` | 굴렁쇠 소환과 배치·상태 표시 | 정식 외형·별도 소환 신체 애니메이션 |
| `율리아 애니메이션 연출 기획_1.1(에셋 애니메이션 적용).pptx` | 기존 에셋 모션과 상태별 표시 방향 참고 | 모든 페이지의 모션·연출을 새로 제작한 상태 아님 |
| `4구역 율리아 보스전_세부 구현 기획_ver0.19.docx` | 전체 전투 흐름과 상태 의미 참고 | 사용자 합의로 현재 코드 사양을 우선한 항목은 별도 유지 |

일부 전투 이미지에는 붉은 에너지 표현이 있지만, 이번 상태 효과는 기존 공격의 보라색과 보라색 모션·증폭기 이미지에 맞췄습니다. 색상 차이가 있는 참고 자료를 모두 같은 최종 사양으로 취급하지 않았습니다.

휴식·무력화 등 모든 논리 상태에 각각 전용 VFX를 새로 만들지는 않았습니다. 참고 없는 음향·대사·엔딩 컷신·일반 적 신규 효과도 추가하지 않았습니다. 현재 구성은 **전투 흐름을 끝까지 돌리며 연출을 조절할 수 있는 테스트 구현**이고, 모든 상태의 정식 아트·애니메이션 제작이 완료됐다는 의미는 아닙니다.

## 10. 기존 등록 이력과 제작자 판단 범위

| 확인한 내용 | Git 근거 | 해석 |
|---|---|---|
| 증폭기 공통 모델 프리팹 최초 등록 | `f90c44a2`, 2026-09-08 18:09 KST, `Bin9825`, 보스·소환물 프리팹 추가 | 프리팹 프로젝트 등록자는 확인 가능 |
| 증폭기 공통 프리팹에 `2010_Orb` 중첩·HP 표시 연결 추가 | `6b26a2eb`, 2026-10-02 13:47 KST, `Bin9825` | 기존 플레어 봄 구체의 증폭기 이식이 이 커밋에서 확인됨 |
| 폭풍·빔 프리팹, `RowLightningCueEffect`, `AmplifierOrbHealthVisual` 등록 | `6b26a2eb`, 같은 일시·작성자 | 이번 JC 전투씬 작업 이전의 기존 구현 |
| 이번 JC 테스트·상태 VFX 등록 | `00054757`, 2026-10-06, `rock-oon` 명의 | 이 대화의 JC 구현이 후속 전달 작업에서 함께 등록됨 |

**Git 작성자는 프로젝트에 등록·통합한 계정입니다. 모델 원작자·외부 에셋 제작자·코드 작성에 사용한 도구까지 입증하는 정보는 아닙니다.** 본체·증폭기 FBX 자체는 현재 경로에서 Git ignore 대상이며, FBX와 `.meta`의 해당 경로 추가 이력은 조회되지 않았습니다. 따라서 FBX 원작자나 메시 파일 자체의 최초 추가일을 이 자료만으로 확정하지 않습니다. 기존 프리팹의 등록일과 메시 제작일을 분리해야 합니다.

`2010_Orb`의 원형은 사용자 제작 플레어 봄 VFX라는 대화 맥락과 실제 프리팹 참조가 일치합니다. 증폭기 이식 커밋이 Bin9825 명의라는 사실을 구체 원형까지 ASB가 새로 제작했다는 의미로 설명하지 않습니다.

## 11. 실제 검증 근거와 시각 자료

10/06 구현 당시 정상 턴·스킬 실행과 자동 전투로 5회 승리를 확인했습니다. HP·페이즈 강제 변경이나 아군 사망 방지를 사용하지 않았으며, 본체 Timeline/Cue, 증폭기 직접 공격·파괴·복구, 장막 전환, 굴렁쇠 돌진·파괴·재소환, 승리 후 초기화를 확인했습니다. 상세 기록은 [작업 일지](C:/Dev/_MD/_WorkLog/20261005-jc-battle-test-fdcf7bf6cb.md)에 있습니다.

당시 컴파일과 비플레이 계약 검사 271개는 통과했고, 실제 전투의 Unity Error 로그는 없었습니다. 원본 굴렁쇠 Animator 부재 및 초기 주입 전 빈 스킬 조회 경고는 남아 있으며 숨기지 않았습니다. 5회 완주는 전투 흐름의 근거이지 모든 효과의 미적 완성도나 모든 스킬·대상 조합을 보장하는 전수 검수는 아닙니다.

이번 브리핑에서는 위 검증을 다시 수행한 것으로 보고하지 않습니다. 또한 현재 전투 초기조건 프로필에 기존 미커밋 변경이 있어 보존했으며, 당시 검증의 랭크 6 설정이 현재 모든 사용자 조정값과 동일하다고 가정하지 않습니다.

### 11.1 기존 공격 VFX + 신규 손 구체

아래의 긴 줄 번개·감전은 **기존 폭풍 VFX**이며, 본체 손 위의 작은 구체는 **JC 신규 상태 표시**입니다. 양옆 증폭기 프레임·구체의 원형은 기존 에셋입니다.

![기존 폭풍 VFX와 JC 상태 구체](C:/Dev/_scratchpad/jc-yulia-polish/row-FX_400011_RowLightning.png)

### 11.2 JC 장막과 임시 굴렁쇠

보라색 타원막은 **JC 신규**, 안의 율리아 메시와 양옆 증폭기 외형은 **기존**입니다. 이 캡처의 굴렁쇠는 금속판 8개 추가 전의 중간 링 외형이므로 최종 외형 근거로 사용하지 않습니다.

![JC 신규 장막과 중간 굴렁쇠 외형](C:/Dev/_scratchpad/jc-yulia-polish/phase-live.png)

### 11.3 최종 임시 굴렁쇠·돌진 잔상

금속판이 추가된 링과 뒤로 이어지는 얇은 보라색 잔상은 **JC 신규**입니다. 원본 Cylinder 외형은 JC 실행 인스턴스에서 숨깁니다.

![최종 JC 굴렁쇠 임시 외형과 돌진 잔상](C:/Dev/_scratchpad/jc-yulia-polish/final-wheel-dash.png)

### 11.4 파괴 파편의 구분

굴렁쇠 주변 검은 작은 파편과 보라색 파괴 링은 **JC 신규**입니다. 함께 보이는 노란 빔·발바닥 모양 등은 **기존 히어로 공격 효과**이며 율리아 신규 VFX 제작량에 포함하지 않습니다.

![JC 신규 굴렁쇠 파편과 기존 히어로 공격](C:/Dev/_scratchpad/jc-yulia-polish/observed-WheelBreak.png)

캡처는 이 PC의 scratchpad에 보존된 이전 실제 GameView 이미지입니다. 다른 PC로 문서만 옮기면 절대경로 이미지가 자동 전달되지는 않습니다. 현재 `JC/Docs`는 Git ignore 정책을 유지하고 있습니다.

## 12. 향후 교체·이식 시 대응 위치

| 교체·이식할 내용 | 현재 JC 위치 | 확인할 사항 |
|---|---|---|
| 정식 장막 프리팹·정지 모션 | `JcBattleTestSession.veilPrefab`, `JcYuliaUnitVfx.Configure` | 기존 Hit 포즈 강제 정지 처리와 원본 ASB 새 구현의 중복 여부 |
| 정식 굴렁쇠 모델 | `JcYuliaUnitVfx.BuildWheel` | 현재 생성 링·금속판·원본 Renderer 숨김 처리를 교체해야 함. 프리팹 참조만 바꾸면 현재 코드가 새 외형도 숨길 수 있음 |
| 증폭기 파괴·복구 | `JcYuliaUnitVfx.Hp/Died/Update` | 원본 HP 표시 코드와 동시에 구체 표시를 제어하지 않도록 소유권 결정 |
| 본체 공급 수신 | `JcMadonnaSkillReceiveTimeline`, `JcYuliaVfxDirector.Supply` | 기존 수신 Timeline과 공급선은 서로 다른 구성 요소 |
| 보조 유닛 시전 | `JcYuliaSkillPresentation.Run`, `JcSkillPresentationDirector` 분기 | 새 Animator·Timeline이 들어오면 기존 JC 연출 경로와 피해 콜백 중복 방지 |
| 굴렁쇠 자폭·시체 정리 | `JcYuliaSkillPresentation.Run`의 도착 위치 유지, Session의 사망 소환체 정리 | 반격 후 사망 순서·셀 점유 해제·Trail 수명 보존 |
| 색·지속 시간·장막 크기 | `JC_YuliaVfxPreset.asset` | 기존 공격 폭풍·빔 내부 설정과 별도 조절 |

이 표는 현재 대응 관계를 설명합니다. 본게임 매니저·ASB 원본에 이번 구현을 자동 이식하거나 기존 요소를 교체한 작업은 수행하지 않았습니다.
