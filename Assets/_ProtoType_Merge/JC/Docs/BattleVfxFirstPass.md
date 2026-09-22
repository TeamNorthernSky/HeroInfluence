# 전투 VFX·애니메이션 1차 구현 현황

갱신: 2026-09-17 / 작업: 20260917-da8d3ec83f / 프로젝트: C:/Dev/HeroInfluence, JC

## 현재 결과

히어로 4명 × 4계열의 기본·강화판 **32개 실행 경로에 임시 연출을 연결**했다. 기존 모션과 VFX를 재사용하고, 실제 전투 프리팹의 애니메이션 이벤트를 보완했다. 스킬 선택·대상·수치·강화 규칙은 앞선 기반 구현을 사용한다. 리스크 구현은 보류한다.

실제 프리팹과 BattleManager를 사용하는 격리 Play 검사에서 시전, 피해·회복·부활, VFX 생성, 복귀와 정리를 확인했다. 실제 TmpBattleScene의 병합 카탈로그 연결도 확인했다. 이번 검사는 전투 버튼을 직접 클릭하는 전체 게임 진행 검증과는 범위가 다르다.

| 대상 | 이번 적용 |
|---|---|
| 저스티스 8개 | 기존 등장/펀치/대쉬 연결 유지, 실제 프리팹용 타격·궤적 이벤트 보완. 크래쉬 기본/강화 피격 효과 연결 |
| 루미나 8개 | 기존 차징·투사체·체인·프리즘 연출을 기본/강화 각각 명시적으로 연결. 빈 큐 정리 |
| 블랙 불릿 8개 | 에임 샷을 공용 임시 사격으로 사용. 더블 버스트는 기존 2회 판정마다 각각 사격하며 첫 발 처치 시 종료 |
| 네코밍 8개 | 단일 회복/LFL의 기본·강화 구체·오라 연결, Paw 착탄과 피해 연결 및 실제 회복 아군 표시, TAO 부활→전체 공격/공격만 분기 연결 |

## 핵심 보완

- **단일 회복:** 구형 `heal_projectile` 유지 연결을 제거했다. 표시되지 않던 구체는 전투의 이동 시작 시 Show, 도착 시 BurstAt을 호출한다. 이동과 회복 판정은 기존 전투 경로가 담당한다.
- **LFL:** 양손 차징 → 주 회복 구체 → 기존 추가 대상 투사체 → 각 대상 오라. 최신 Basic/Alter 부품을 쓰되, 구체 합류 연출은 후속이다.
- **TAO:** 선택 시체가 있으면 부활 후 Barrage, 없으면 부활 준비 모션을 생략한다. 실제 확정 적 목록으로 Barrage를 만들고 타격 표시가 발생한 뒤 피해를 적용한다. 2배속에서 준비 모션의 ReturnIdle이 후속 공격을 끊던 문제를 작업용 복제 클립에서 해결했다.
- **Paw:** 빔 착탄 신호를 전투에 전달해 피해를 맞춘다. 실제 피해 기반 회복 대상에 기존 단일 치유 오라를 재사용한다. 기본/강화는 현재 같은 Paw 외형이다.
- **실제 애니메이터:** 테스트베드만의 클립 대신 실제 4개 전투 프리팹에 JC 작업용 OverrideController를 연결했다. 원본 FBX와 ASB 원본 컨트롤러는 수정하지 않았다.

## 정확한 변경 파일

아래 경로는 모두 `C:/Dev/HeroInfluence/` 기준이다. UI 작업에서 수정한 씬·UI 스크립트는 이번 작업의 변경 파일에 포함하지 않는다.

### 기존 스크립트 8개

| 파일 | 변경 이유 |
|---|---|
| `Assets/ASB_Work/Effect/JcVfxPresentationAdapter.cs` | 확정 대상 전달, TAO/Paw 착탄 신호, 종료·중단·직접 생성 효과의 수명 관리 |
| `Assets/_ProtoType_Merge/ASB/Scripts/Battle/Presentation/SkillPresentationDirector.cs` | TAO 부활 대상 없는 경우 준비 동작 생략 |
| `Assets/_ProtoType_Merge/ASB/Scripts/Battle/Sequence/ProjectileImpactAction.cs` | JC 회복 구체 표시와 도착 버스트 호출 |
| `Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs` | Paw 실제 후속 회복 직후 치유 효과 표시. 수치·대상 선정은 유지 |
| `Assets/RenderFX/HeroSkill/Nekoming/Scripts/TaoBarrageVfx.cs` | 타격 표시 완료 이벤트 |
| `Assets/RenderFX/HeroSkill/Nekoming/Scripts/PawForYouVfx.cs` | 빔 착탄 이벤트 |
| `Assets/RenderFX/HeroSkill/_Shared/Scripts/ProjectileVfx.cs` | 전투가 이동을 담당할 때의 도착 버스트 진입점 |
| `Assets/RenderFX/HeroSkill/_Shared/Scripts/JcImpactFlashDirector.cs` | 직접 생성되는 크래쉬 등의 피격 효과 재생·정리 |

### 기존 프리팹·연결 데이터

- `Assets/Resources/prefab/BattlePrefab/PlayerUnit/Unit_Fighter_10001.prefab`
- `Assets/Resources/prefab/BattlePrefab/PlayerUnit/Unit_Blaster_10002.prefab`
- `Assets/Resources/prefab/BattlePrefab/PlayerUnit/Unit_Striker_10003.prefab`
- `Assets/Resources/prefab/BattlePrefab/PlayerUnit/Unit_Supporter_10004.prefab`
  - 위 4개는 내부 Animator의 작업용 컨트롤러 참조만 추가했다.
- `Assets/RenderFX/_Seam/Data/Merged/JC_MergeSources.asset`
- `Assets/RenderFX/_Seam/Data/Merged/JC_Merged_SkillPresentationCatalog.asset`
- `Assets/RenderFX/_Seam/Data/Merged/JC_Merged_EffectRegistry.asset`
  - FirstPass 재료를 병합 설정에 등록하고 기존 병합 도구로 재생성했다.

### 새 에셋

`Assets/RenderFX/_Seam/Data/FirstPass/` 아래에 생성했다(각 Unity meta 포함).

- `JC_FirstPass_Catalog.asset`: 32개 바인딩. 기존 저스티스 전용 6개를 참조하고 나머지 26개는 `JC_FirstPass_<ID>.asset`으로 분리.
- `JC_FirstPass_Effects.asset`: 16개 효과 바인딩. ID 95001~95006/95101~95106, 95020, 95030/95130, 95040.
- `Prefabs/`: 회복 차징/착지, LFL 차징/착지, TAO 부활/Barrage의 Basic·Alter 12개와 Paw 1개.
- `Animation/`: 4개 `JC_FirstPass_<직업>.overrideController`, 20개 `FP_<직업>_<상태>.anim`. 기존 모션 복제 후 이벤트·루프만 보정한 임시본이다.

## 검증 결과와 한계

- Unity 프로젝트 컴파일 통과. 32개 연출 바인딩·모션 상태·큐·효과 참조 검사에서 누락 없음.
- **1배속:** 실제 4개 프리팹으로 기본16/강화16을 순차 실행. 완료·효과 적용·VFX 생성·복귀·어댑터 정리 검사 통과, 해당 실행 경고 없음.
- **2배속:** 32개 실행 중 TAO 2개에서 공격 큐 누락을 발견. 나머지 30개 통과. 준비 클립 수정 후 TAO 기본/강화의 부활 후 공격을 각각 재검사해 통과했다.
- Paw 착탄 보정 후 기본/강화 1배속 추가 검사: 피해 각 1회, 후속 아군 회복 각 1회, 치유 오라 생성 확인. 2배속에서도 착탄 신호 각 1회 확인.
- **분기 검사 14개:** Paw 기본/강화, TAO 시체 없음/두 시체 중 선택/부활 사용 후 재시전/기본·강화 부활 2배속/적 1명 2배속, LFL 자신/추가 대상 없음 2배속, 체인 추가 대상 없음, 열 안 사망 유닛, 두 발 사격/첫 발 처치. 73개 검사 항목 통과, 마지막 실행 경고·예외 없음. 종료 후 어댑터와 피격 효과 잔존 없음.
- 검증 도구의 적 진영 초기화 누락과 비활성 유닛 재배치 오류는 검사 스크립트에서 수정 후 재검사했다. 게임의 대상 선정 로직을 이 이유로 변경하지 않았다.
- TmpBattleScene을 저장된 상태로 복귀했다. Edit, dirty=False. 실제 씬의 BattleVisualDirector는 갱신된 병합 카탈로그/이펙트 레지스트리를 참조한다.
- **미검증:** 실제 UI 클릭부터 전투 승리/씬 전환까지의 전체 흐름, 모든 장비·적 조합, 최종 전투 카메라/조명에서의 가독성. 이번 격리 검사는 전투 종료 플로우를 생성하지 않았다.

검증 자료: `C:/Dev/_scratchpad/vfx-first-pass-da8d3ec83f/`

- `binding-audit.txt`, `final-audit.txt`
- `runtime-1x.txt`: 32개 통과
- `runtime-2x.txt`: 최초 TAO 실패를 포함한 원본 기록 유지. 해결 후 결과는 `edge-results.txt`의 `TAO 2x revive variant 0/1` 참조
- `edge-results.txt`, `edge-warnings.txt`: 최종 14개 분기 검사
- `skill-4021.png`, `skill-4041.png`: 격리 카메라의 착탄 시점 캡처. 실제 씬 최종 룩 기준은 아님

## 다음 보강

1. 실제 전투 화면에서 크기·위치·속도·가림부터 조정한다. TAO는 밝기와 화면 점유가 큰 기존 시안이며 피격 기둥도 임시다.
2. 블랙 불릿 3개는 에임 샷 모션/탄도를 재사용한다. LFL 구체 합류, Paw 강화 외형, 루미나·크래쉬의 추가 전용 연출을 보강한다.
3. 새 모션은 이 실행 흐름 위에서 한 스킬씩 교체한다. 이후 카메라와 효과음을 붙인다. 현재 일부 VFX의 내부 재생 길이는 배속과 완전히 비례하지 않으며, 확인한 1·2배속에서 판정·종료가 유지되는 수준이다.

협회 코어 5종, 리스크 효과, 신규 카메라 연출·사운드 매니저 제작은 이번 범위 밖이다. 커밋하지 않았다.


## 착수 당시 제작 목록과 후속 미술·모션 과제

ID는 현재 런타임 기준이다. 오래된 기획서의 참조 인덱스와 이름이 다를 수 있으므로 이름·계열을 함께 대조한다. 아래 모션은 현재 연출 데이터의 상태명이며 최종 모션 확정안이 아니다.

| 계열 / 기본·강화 ID | 현재 재사용할 자산·모션 | 착수 당시 연결 과제 | 후속 보강 |
|---|---|---|---|
| 저스티스 등장! / 1010·1011 | JC 전용 기본·강화 연출, EnterTheJustice 궤적·피격. MoveForward → ClassSkill_1 → MoveReturn → Taunt | 기존 전용 연결을 기준 사례로 검수. 단일 타격·도발·복귀 확인 | 주먹 자세, 발 고정, 타격감과 도발 표현 |
| 저스티스 펀치 / 1020·1021 | JC 전용 기본·강화 연출, Punch 궤적·피격. ClassSkill_1/2 조합 | 기존 연속 동작과 실제 피해 적용 횟수 확인. 전열 추가 계수를 연출 때문에 중복 적용하지 않기 | 콤보 연결·궤적·손 모양 |
| 저스티스 대쉬 / 1030·1031 | JC 전용 기본·강화 연출, Dash 부품. Dash → ClassSkill_3 → MoveReturn | 앞/뒤 실제 피해 대상과 궤적 일치, 빈 후열 및 복귀 검증 | 새 발차기 모션 필요 여부, 경로·회전·잔상 |
| 저스티스 크래쉬 / 1040·1041 | ASB 이동 공격 연출, ClassSkill_4. CrashImpact 기본·Alter 부품 존재 | 기존 회전 이동 유지, 실제 전체 대상 피해·도발·종료에 최소 피격 표시 연결. 강화판 공용 연출 허용 | 회전 궤적·전용 강화 차이·모션 교체 |
| 플레어 봄 / 2010·2011 | FlareBomb 기본/Dark 부품, ASB 차징·투사체. ClassSkill_2 중심 | 소환 → 발사 → 도착 피해. 기존 Dark의 강화판 사용 적합성 확인 후 연결 | 구체 룩·발사 자세·폭발과 잔상 |
| 체인 라이팅 / 2020·2021 | ChainLightning 기본/Dark, ClassSkill_3 및 WeaponSkill_3 | 주 대상 → 확정된 추가 대상 순서와 타격 시점 일치. 추가 대상이 없어도 종료 | 번개 형태·가지·접지·모션 기울어짐 |
| 솔라 프리즘 / 2030·2031 | SolarPrismSkill, SolarPrismCueEffect, ClassSkill_4 | 선택된 3칸 열의 실제 대상 수에 맞게 생성·발사·피격. 강화판 공용 부품 허용 | 프리즘 형태·배열·강화 차별화 |
| 프리즘 익스플로전 / 2040·2041 | PrismExplosionSkill, PrismExplosionCueEffect, ClassSkill_1 | 차징 → 진영 중앙 이동/낙하 → 전체 폭발·피해·정리 | 폭발·연기·화면 가림·강화 차별화 |
| 블랙 데빌 퍼펙트 에임 샷 / 3010·3011 | K_AimShotVfx, K_BB 전용 기본 연출, ClassSkill_1 | 기존 1발 사격을 원거리 최소 기준으로 검수. 강화판은 우선 같은 부품 사용 가능 | 강화 외형, 총을 쥔 손과 자세 |
| 프론트 라인 브레이크 불릿 / 3020·3021 | ASB ClassSkill_2, 기존 사격/피격 자산 | 에임 샷 계열 탄도 재사용 → 단일 피격. 기존 데이터의 전열 추가 효과 유지 | 굵은 탄도·전용 모션·강화 외형 |
| 트윈 트리거 더블 버스트 / 3030·3031 | ASB ClassSkill_3, fx_right/fx_left 큐 | 기존 모션과 탄도를 재사용해 2발 표시. 각 발의 실제 판정·피격과 연결, 1발째 사망 시 종료 방식 검수 | 양손 총 자세·연속 사격 리듬·강화 외형 |
| 데드엔드 라스트 트리거 / 3040·3041 | ASB ClassSkill_4, fx_right 큐 | 기존 탄도 + 임시 피격 강조로 1발 실행. 체력 비례 계수는 기존 계산 사용 | 해골 피격 효과·결정타 모션·강화 외형 |
| 아픈 거 다 날아가라 / 4010·4011 | Heal의 ChargeOrb/ProjectileOrb/HealOrbit Basic·Alter, ClassSkill_1 | 차징 → 실제 회복 대상에게 구체 도착 → 회복 표시. 기본/강화 부품 참조 혼용 정리 | 손바닥 부착·치유 오라·새 모션 |
| 널 위해 준비했어 / 4020·4021 | PawSpawn/PawWarp/PawBeam 기본·Alter, ClassSkill_2 | 소환 → 대상 위 이동 → 빔 피격. 실제 피해 기반 회복을 받는 아군에도 최소 회복 표시 | 고양이 손·워프·빔 완성도, 공격과 회복의 시각 연결 |
| 우리 다 같이 힘내자(LFL) / 4030·4031 | 전투는 Legacy 기반 Variant. 별도 ChargeOrb/Projectile/Merge/LandAura Basic·Alter 존재, ClassSkill_3 | 확정된 주/추가 회복 대상만 최신 부품에 전달. 양손 차징 → 주 회복 → 추가 회복 흐름 연결 | 구체 합류·분기·착지 표현, 새 양손 모션 |
| 쓰러지면 안 돼(TAO) / 4040·4041 | 전투는 Legacy 부활·Meteor 연결. 별도 Revive/MeteorOrb/Barrage Basic·Alter 존재. ClassSkill_4 → WeaponSkill_3 | 선택 시체가 있으면 부활 → 전체 공격, 없으면 공격만. Barrage 기존 시안이나 기존 광역 부품으로 우선 연결. 실제 적 목록 사용 | Barrage 피격 기둥 등 임시 표현 교체·부활 포즈·전용 공격 모션 |

