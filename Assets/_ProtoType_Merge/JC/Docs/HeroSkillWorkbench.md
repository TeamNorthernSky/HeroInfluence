# 캐릭터 스킬 Inspector 작업대

갱신: 2026-09-18 · 작업 20260918-be4088f75c · JC

## 사용

1. `z_JC_PreViewsScene_2`(저스티스), `3`(블랙불릿), `4`(루미나), `5`(네코밍)을 엽니다.
2. Hierarchy의 **JC_스킬작업대** 아래에서 `Q_스킬명` 등 원하는 오브젝트를 선택합니다. 각 씬에 QWERASDF 8개입니다.
3. Inspector 상단에 각 프리셋의 **원본 에셋 Inspector**가 순서대로 표시됩니다. 원본의 항목·배치·접기·버튼을 사용하며, 작업대가 추가했던 부품별 중첩 접기는 제거했습니다. 여러 부품이 같은 프리셋을 사용하면 한 번만 표시합니다.
4. 아래 **시전·부품 테스트**에서 Play 중 **전체 스킬 선택** 후 Game 뷰 대상을 클릭하면 시전합니다. QWERASDF 입력도 유지됩니다. **부품 재생**은 해당 부품만 검사합니다. 차징 부품은 **발사 신호**, 종료는 **중단**을 사용합니다.
5. 원본의 저장 버튼 또는 **이 설정 저장**으로 저장합니다. **저장값 복원**은 디스크에 저장된 프리셋 값으로 되돌리며 실행 취소가 가능합니다. **Apply/Capture**는 실제 연결된 해당 변종 부품을 대상으로 작동합니다. Apply는 부품·재질에 반영하고, Capture는 그 부품·재질의 현재 값을 프리셋으로 읽습니다.

일반 Unity 저장도 변경된 에셋을 저장할 수 있습니다. Play 종료만으로 공용 에셋의 조절값이 취소되지는 않습니다. 일부 효과는 변경 즉시 반영하지만 이미 시작한 시간표 전체를 되감지는 않으므로, 변경 후 재시전을 확인 기준으로 사용합니다.

## 조절 위치

| 대상 | 현재 조절 위치 |
|---|---|
| 색·크기·시간 등 VFX 값 | 상단의 원본 프리셋 Inspector |
| 루미나 플레어 코어·테두리·구체 화염 | `FX_FlareOrb_F0_Basic/Alter` |
| 루미나 플레어 앞·뒤 화염 레이어 | `FX_FlareOrb_F1_Basic/Alter` |
| 루미나 플레어 스프라이트 화염 | `FX_FlareOrb_F2_Basic/Alter` |
| 루미나 플레어 착탄 | `FX_h2010_FlareImpact_Basic` / `FX_h2011_FlareImpact_Alter` |
| 네코밍 LFL 합류 효과 | `L11_LFLMerge_Basic/Alter`의 시간·섬광 색·크기 |
| 전투가 직접 이동시키는 투사체 | 하단 **실제 시전 · 투사체 이동**. 속도는 실제 도착·피해 시점에도 영향 |
| BB·Paw 부품 생성 시각 | 하단 **부품 조립 시간**. 기준 프리셋 시간에 단계별 지연을 더함 |
| 기타 부품 연결·단독 재생 설정 | 하단 **고급 부품 설정** |

루미나 `FlareOrbAltPreset`이라는 기존 **클래스명**의 Alt는 F1 화염 레이어 이름입니다. 스킬 강화판 여부는 에셋 이름 끝의 **Basic/Alter**로 구분합니다. FlareImpact 원본에 남아 있는 통합 구체 이동 항목은 현재 분리 부품에 영향을 주지 않아 설명과 함께 비활성 표시합니다. 실제 이동은 위의 투사체 이동 설정을 사용합니다.

## Basic / Alter 및 공유 범위

- 사용자 결정: **루미나 Basic은 불꽃, Alter는 암흑불꽃**입니다. 기존 Dark 프리셋은 Alter 기준입니다. 플레어 강화판 코어·F1에 기본 불꽃 색이 남아 있던 연결도 기존 Dark 색으로 교정했습니다.
- 기존에 Basic을 따르던 프리셋의 `followBasic`과 참조·튜닝 값은 유지했습니다. 해당 원본 Inspector에서 따름을 해제하거나 수정할 수 있습니다.
- 같은 프리셋을 공유하던 네코밍 10종은 원본을 `_Basic`으로 이름만 바꾸고 GUID를 유지했습니다. `_Alter`는 현재값을 복사한 독립 플레이스홀더입니다. 새로 분리한 Alter의 Basic 따름은 꺼져 있습니다.
- 10종: Heal AuraMaster/ArcRing/Cross, Paw Master/WarpPillar, LFL AuraMaster/ArcRing/Cross, TAO ReviveMaster/Cross.
- 루미나 플레어 F0/F1/F2와 네코밍 LFL Merge에 Basic/Alter 쌍을 추가했습니다. 루미나·BB의 기존 숫자 이름 프리셋 16개에도 Basic/Alter 표기를 추가했으며 GUID는 유지했습니다.
- 신규 프리셋은 총 **18개**입니다. 기존 26개는 참조를 보존한 이름 변경입니다. Apply가 다른 스킬/변종의 재질을 덮어쓰지 않도록 관련 재질 92개를 분리했습니다.
- 프리뷰와 **TmpBattleScene은 동일한 공용 연출·부품·프리셋을 사용**합니다. 별도 이식·복사가 필요하지 않습니다. 이번에 프리뷰 2~5, 6번, TmpBattleScene의 씬 파일 자체는 변경하지 않았습니다.
- TAO Meteor는 교체 후보이며 현재 전체 시전은 Barrage를 사용합니다. 이번 작업은 조절 가능한 부품 틀과 연결을 정비한 것이며, 임시 VFX의 최종 완성·신규 애니메이션·카메라·사운드 제작은 후속 작업입니다.

## 이번 변경 스크립트

**기존 33개 수정, 신규 5개**입니다. ASB·BattleManager·스킬 전투 수치 스크립트는 수정하지 않았습니다.

| 핵심 파일 | 변경 |
|---|---|
| `HeroSkillWorkbenchEditor.cs` | 원본 CustomEditor 재사용, 중복 제거, 저장/복원·하단 테스트 배치 |
| 신규 `JcPresetPartTargets.cs` | 실제 연결된 스킬 부품·재질 조회 |
| 신규 `JcLuminaPresetEditorBridge.cs` | 기존 Apply/Capture/실시간 갱신을 루미나 분리 부품으로 연결 |
| 신규 `JcFlareOrbPartPresetBinder.cs` | 루미나 F0/F1/F2 런타임 연결 |
| `JcLuminaPartPresetBinder.cs` | Solar 불씨 방출량·크기 누락 연결 |
| 신규 `LflMergePreset.cs`, `LflMergePresetEditor.cs`, 수정 `LflMergeVfx.cs` | 기존 합류 값의 프리셋화 |
| 저스티스·블랙불릿·네코밍·루미나 원본 편집기 | 레이아웃 유지, 실제 변종 적용·캡처·실시간 갱신 경로 및 툴팁 정비 |

전체 코드 목록(`Assets/RenderFX/HeroSkill/` 기준):

- `BlackBullet/Editor/KAimShotPresetEditor.cs`
- `Justice/Editor/JusticeTrailPresetEditor.cs`
- `Lumina/Editor/ChainLightningPresetEditor.cs`
- `Lumina/Editor/FlareImpactPresetEditor.cs`
- `Lumina/Editor/FlareOrbAltPresetEditor.cs`
- `Lumina/Editor/FlareOrbPresetEditor.cs`
- `Lumina/Editor/FlareOrbSpritePresetEditor.cs`
- `Lumina/Editor/PrismExplosionPresetEditor.cs`
- `Lumina/Editor/SolarPrismPresetEditor.cs`
- `Nekoming/Editor/HealArcRingPresetEditor.cs`
- `Nekoming/Editor/HealAuraGlowPresetEditor.cs`
- `Nekoming/Editor/HealAuraMasterPresetEditor.cs`
- `Nekoming/Editor/HealCrossPresetEditor.cs`
- `Nekoming/Editor/HealGroundShinePresetEditor.cs`
- `Nekoming/Editor/HealOrbitPresetEditor.cs`
- `Nekoming/Editor/HealOrbitSparklePresetEditor.cs`
- `Nekoming/Editor/LflMergePresetEditor.cs`
- `Nekoming/Editor/PawBeamPresetEditor.cs`
- `Nekoming/Editor/PawMasterPresetEditor.cs`
- `Nekoming/Editor/PawSpritePresetEditor.cs`
- `Nekoming/Editor/PawWarpPillarPresetEditor.cs`
- `Nekoming/Editor/TaoAuraFlarePresetEditor.cs`
- `Nekoming/Editor/TaoBaseSprayPresetEditor.cs`
- `Nekoming/Editor/TaoFeatherPresetEditor.cs`
- `Nekoming/Editor/TaoMagicCirclePresetEditor.cs`
- `Nekoming/Editor/TaoMeteorPresetEditor.cs`
- `Nekoming/Editor/TaoReviveMasterPresetEditor.cs`
- `Nekoming/Scripts/LflMergePreset.cs`
- `Nekoming/Scripts/LflMergeVfx.cs`
- `_Shared/Editor/ChargeOrbPresetEditor.cs`
- `_Shared/Editor/FlareOrbPresetEditorBase.cs`
- `_Shared/Editor/HeroSkillWorkbenchEditor.cs`
- `_Shared/Editor/JcLuminaPresetEditorBridge.cs`
- `_Shared/Editor/JcPresetEditorUtil.cs`
- `_Shared/Editor/JcPresetPartTargets.cs`
- `_Shared/Editor/ProjectileOrbPresetEditor.cs`
- `_Shared/Scripts/JcFlareOrbPartPresetBinder.cs`
- `_Shared/Scripts/JcLuminaPartPresetBinder.cs`

에셋 변경은 `HeroSkill/`의 관련 프리셋·프리팹과 `Assets/RenderFX/_Seam/Data/`의 부품·프리셋·재질입니다. 전체 파일 목록은 `C:/Dev/_scratchpad/workbench-layout-be4088f75c/changes.json`에 있습니다.

## 이번 검증

- Unity 2022.3.62f3 컴파일 오류 없음.
- **32개 스킬**: 프리셋 연결 누락 없음, 모든 원본/전용 Inspector 생성·GUI 그리기 통과, Basic/Alter 간 프리셋 중복 공유 없음.
- 기존 프리셋 **98개**의 이름·적용 대상 변경 외 튜닝 값과 기존 Basic 따름 설정 보존 확인.
- 프리뷰 **2~5의 32개 스킬** 선택 → 대상 입력 → 시전 → 종료/복귀, 취소 후 재시전, TAO 부활 분기 통과. 검사 경고·오류 없음.
- 루미나 코어·테두리·F0/F1/F2 색 변경의 런타임 반영 및 Basic/Alter 격리 13항목 통과.
- 분리 프리셋 20개 및 LFL Merge 2개 Apply/Capture 왕복 일치. 루미나 14개 Apply/Capture 연결 통과, Flare F0/F1/F2 Alter의 왕복 값 일치 확인.
- 저장/복원, 복원 Undo, 검사 임시값 제거 확인.
- 저스티스 **A → S → A** 실제 시전에서 기본 청색·강화 적색 및 각 자식 효과 프리셋 분리 재확인.
- 신규 에셋 `.meta` 누락 없음. 코드 공백 검사 통과. Unity 직렬화 프리팹의 빈 YAML 값 뒤 공백은 Unity 저장 형식으로 유지했습니다.
- 시전 검사는 실제 입력 담당의 선택/클릭 API를 호출했습니다. 물리 키보드·마우스 조작이나 최종 화면의 미적 완성도 검사는 아닙니다. **TmpBattleScene 전체 플레이 회귀 검사는 별도로 실행하지 않았습니다.**

실행 증거: `C:/Dev/_scratchpad/workbench-layout-be4088f75c/`의 `structure.txt`, `gui-results.txt`, `scene-*-results.txt`, `colors.txt`, `operations.txt`, `lumina-operations.txt`, `apply-capture.txt`, `save-restore.txt`, `justice-results.txt`, `changes.json`.
