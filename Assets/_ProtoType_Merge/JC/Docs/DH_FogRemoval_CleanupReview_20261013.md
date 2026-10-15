# DH 안개 삭제 후 잔여 참조와 한글 손상 정리 요청

수신: DH / 요청: JC / 작성일: 2026-10-13

안개 기능 폐기 결정을 유지하면서 삭제 후 남은 씬·자산 참조와 GridManager의 한국어 손상을 정리해 주세요. 안개 시스템 복원이나 이동 규칙 재설계 요청은 아닙니다.

기준 커밋은 `96a56fb20e3da5a6c5f9e04be6970aa61b29508e`이며 실제 안개 삭제는 부모 커밋 `7ea773ee`입니다. 아래 경로는 저장소 루트 기준이고 줄 번호는 이 기준 버전입니다. 정적 참조·Git 이력 검토 결과이며 실제 Unity 빌드·플레이 결과는 포함하지 않습니다. 이 레포트 작성 과정에서 대상 소스·씬·프리셋은 수정하지 않았습니다.

## 정리 대상

| 대상 | 확인 내용 | 요청 작업 |
|---|---|---|
| `Assets/_ProtoType_Merge/DH/LevelDataMaker.unity` | 삭제된 안개 스크립트 참조 7개 | 안개 컴포넌트와 연결 제거. 레벨 제작 기능은 유지 |
| `Assets/_ProtoType_Merge/Scenes/FogGrid_Test.unity` | 삭제된 안개 스크립트 참조 5개 | 다른 테스트 용도 확인 후 안개 참조 제거 또는 씬 폐기 |
| `Assets/Resources/RenderFX/FogPreset_Default.asset` 및 `.meta` | 삭제된 FogPreset 타입을 사용하는 폐기 대상 프리셋 | 프리셋과 meta 삭제, 아래 프리팹 잔여 항목 함께 정리 |
| `Assets/Resources/RenderFX/RenderFXManager.prefab` | 코드에서 사라진 안개 필드 5개가 직렬화 데이터에 남음 | 안개 잔여 필드만 제거. 프리팹 전체 삭제는 이번 필수 정리에서 제외 |
| `Assets/_ProtoType_Merge/DH/Scripts/Manager/Grid/GridManager.cs` | 안개 삭제 커밋에서 한국어 헤더·툴팁·주석 손상 | 삭제 전 버전의 한국어 문구만 복원 |

## 씬의 삭제된 스크립트 참조

### LevelDataMaker

다음 7개 m_Script 참조가 남아 있습니다. 스크립트가 삭제됐으므로 해당 컴포넌트는 Missing Script 대상입니다.

| 줄 | 삭제된 타입 |
|---:|---|
| 46923 | TurnFogController |
| 46940 | PartyFogRevealer |
| 46957 | FogGridManager |
| 46988 | FogRenderManager |
| 47007 | OutpostFogRevealer |
| 47026 | EnemyFogVisibilitySystem |
| 47042 | HeroUnionFogRevealer |

안개 전용 컴포넌트와 GameObject의 컴포넌트 목록, 해당 컴포넌트를 가리키는 연결을 함께 제거해 주세요. 컨테이너 자체를 지울 경우 안개 외 자식·컴포넌트가 없는지도 확인해 주세요. 씬을 새로 열었을 때 Missing Script가 없어야 하며 기존 레벨 제작 기능을 보존해야 합니다.

### FogGrid Test

916행 OutpostFogRevealer, 932행 FogRenderManager, 953행 TurnFogController, 969행 PartyFogRevealer, 985행 FogGridManager가 남아 있습니다.

이 씬을 안개 외의 테스트에 사용하지 않는다면 씬과 meta를 함께 폐기하는 방법이 적절합니다. 다른 테스트 용도가 있으면 씬은 유지하고 위 컴포넌트만 제거해 주세요. 두 씬 모두 현재 EditorBuildSettings의 빌드 씬 목록에는 없으므로, 이 누락만으로 본게임 실행 장애가 발생했다고 판단한 것은 아닙니다.

## FogPreset 삭제와 프리팹 잔여 데이터

`FogPreset_Default.asset`은 JC가 만든 자산이지만 안개 폐기 후 사용할 타입과 적용 코드가 모두 사라졌으므로 삭제 대상으로 정리하면 됩니다. 새 구름 그림자 기능을 위해 이 프리셋을 복원할 필요는 없습니다.

프리셋 GUID는 `372a5e1476327004babdf99a7c0184db`입니다. 현재 Assets의 씬·프리팹·asset 검색에서 외부 직렬화 참조는 `RenderFXManager.prefab` 49행의 activePreset 1건입니다. 아래 5개 항목은 현재 RenderFXManager 코드에 존재하지 않는 잔여 데이터입니다.

- `fogEnabledByDefault`
- `logToggle`
- `activePreset`
- `fogMaterial`
- `applyPresetOnSceneLoad`

프리셋과 meta 삭제 시 위 잔여 필드도 함께 정리해 주세요. 필드가 현재 코드에서 사용되지 않는다는 점과 직렬화 참조가 남아 있다는 점을 구분해야 합니다. 이 참조만으로 런타임 예외가 발생한다고 단정하지는 않습니다.

### RenderFXManager 현재 사양

프리팹에는 루트 GameObject 1개, Transform과 RenderFXManager 컴포넌트만 있으며 자식은 없습니다. 연결 스크립트는 `Assets/RenderFX/RenderFXManager.cs`입니다.

현재 동작은 첫 씬 로드 전 Resources 프리팹 생성, 프리팹 부재 시 매니저 GameObject 생성, Instance 중복 제거, 루트 이동 및 DontDestroyOnLoad, 파괴 시 Instance 해제뿐입니다. Update, 셰이더 제어, 프리셋 적용, 씬별 효과 전환은 없습니다.

삭제 직전에는 안개 렌더 토글, 씬별 안개 게이트, 프리셋 적용, 시야 반경·SDF·안개 영역 제어, 안개 재질 백업·복원을 담당했습니다. DOF·환경 조명·지역 팔레트·건물 실루엣을 이 매니저가 제어한 것은 아닙니다. 해당 기능은 JcDofController/JcDofFocusTracker, JcEnvironmentController, JcRegionPaletteController, JcBuildingSilhouetteController 등 별도 컴포넌트가 담당합니다.

프리팹만 삭제해도 Bootstrap이 빈 매니저를 다시 생성하므로 매니저 전체 폐기는 프리팹 잔여 필드 정리와 별도 사안입니다. 이번에는 5개 잔여 필드 정리로 범위를 제한하고, 매니저 골격의 유지·폐기는 구름 그림자 등 후속 구조 검토와 구분해 주세요.

## GridManager 한국어 복원

현재 28행 Header, 36행과 57행 Tooltip 및 일부 주석이 깨졌습니다. Git blame상 `7ea773ee`에서 발생한 변경입니다. `7ea773ee^`의 같은 파일을 참고하여 한국어만 복원해 주세요.

복원할 Inspector 문구는 다음과 같습니다.

- landTransform Header: `Grid 기준 오브젝트`
- obstacleLayerMask Tooltip: `이 레이어에 있는 콜라이더는 장애물로 간주합니다.`
- obstacleCheckFill Tooltip: `셀 워커블 검사 시, 셀 크기 대비 체크 박스 비율(너무 크면 오탐, 너무 작으면 통과).`

gridOrigin 부근과 GetLandSurfaceY의 한국어 주석도 함께 복원하고 UTF-8로 보존해 주세요. 파일 전체를 이전 버전으로 되돌리면 안개 관련 필드와 이동 제한이 되살아나므로 문구만 선별 복원해야 합니다.

## 파일 담당과 변경 범위

- LevelDataMaker는 DH 생성·관리 씬입니다. GridManager도 초기 경로가 DH_Work인 DH 영역 코드입니다.
- FogGrid_Test의 초기 경로는 KJ_Work/Scenes입니다. 최초 이관 커밋 작성자가 JC라는 이유로 JC 원작 씬으로 단정하지 않습니다.
- FogPreset_Default와 RenderFXManager.prefab은 JC의 `3960f3dd`에서 추가됐습니다. 현재 안개 폐기에 따른 잔여 정리 대상으로 함께 기재했습니다.
- RenderFX 폴더 전체를 삭제하지 마세요. 스킬 VFX·환경·DOF와 구름 그림자 구현에 참고할 셰이더는 이번 정리 대상이 아닙니다.
- JC/BattleTesting의 본게임 공유 재질 3개와 JcYuliaEnergy.shader도 안개 정리와 무관합니다.

## 완료 확인

1. 유지하는 제작·테스트씬에서 위 안개 Missing Script가 없어야 합니다.
2. FogPreset_Default와 meta를 삭제했다면 폐기 GUID의 직렬화 참조도 없어야 합니다.
3. RenderFXManager.prefab의 안개 필드 5개가 제거되고 다른 컴포넌트·리소스는 보존되어야 합니다.
4. GridManager 한국어가 정상 표시되고 안개 삭제 후의 실행 로직은 그대로여야 합니다.
5. Unity 재임포트·컴파일 오류 여부를 확인합니다. 실제 플레이는 JC가 직접 진행하며, 탐사 이동·협회 진입 경로 제한·미니맵·저장 후 재진입을 확인합니다.

참고로 DHScene_3의 큰 diff는 대부분 레코드 재정렬입니다. 안개 삭제 전후 오브젝트 ID 비교에서는 FogSystem 관련 9개 레코드 삭제와 참조 정리 3개 레코드 수정만 확인됐습니다. 이 레포트는 월드 드레싱이나 DOF 복구를 요청하는 내용이 아닙니다.
