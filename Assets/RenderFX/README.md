# 현재 히어로 이펙트 관리

Unity 상단 메뉴 **RenderFX → 히어로 이펙트 관리**를 여세요.
캐릭터별 네 스킬의 일반·강화 효과, 색상·크기 조절 프리셋, 파티클과 재질을 같은 화면에서 선택하거나 열 수 있습니다. 목록은 실제 전투 카탈로그를 직접 읽습니다.

## 평소에 볼 폴더

`Assets/RenderFX/HeroSkill/캐릭터/스킬/Prefabs`

- 이 `Prefabs` 폴더 하나에 현재 전투에서 사용하는 프리팹만 둡니다.
- 같은 스킬 아래에 `Runtime/Prefabs`, `VisualVariants`, `ExternalFX`를 따로 만들지 않습니다.
- 현재 쓰는 프리셋과 재질은 같은 스킬의 `Presets`, `Materials`에 둡니다.
- 여러 스킬이 함께 쓰는 효과는 해당 캐릭터의 `Common`에서 관리하며, 관리 화면에는 사용하는 스킬마다 표시됩니다.

## 플레어 봄

`Assets/RenderFX/HeroSkill/Lumina/FlareBomb/Prefabs`

| 파일 | 수정할 내용 |
|---|---|
| `FX_Lumina_FlareBomb_Charge_Basic` | 일반 차징 |
| `FX_Lumina_FlareBomb_Projectile_Basic` | 일반 투사체 |
| `FX_Lumina_FlareBomb_Charge_Enhanced` | 강화 차징·투사체 |
| `FX_Lumina_FlareBomb_Fire_Basic` | 일반 불꽃 색상·입자 |
| `FX_Lumina_FlareBomb_Fire_Enhanced` | 강화 불꽃 색상·입자 |
| `FX_Lumina_FlareBomb_Hit_Basic` | 일반 피격 효과 |
| `FX_Lumina_FlareBomb_Hit_Enhanced` | 강화 피격 효과 |

불꽃 색상은 `Fire_Basic` 또는 `Fire_Enhanced`를 열고 파티클의 `Start Color`, `Color over Lifetime`을 수정하세요.

## 따로 분리한 자료

- `Assets/RenderFX/_Archive/HeroSkill`: 현재 히어로 전투 카탈로그에서 사용하지 않는 이전 작업물. 삭제하지 않고 보존합니다. 다른 테스트 씬·옛 도구의 참조는 유지됩니다.
- `Assets/RenderFX/_Support/HeroSkill`: 내부 부품, 메시·텍스처·셰이더, 스크립트와 편집 도구. 일반 이펙트 수정 시 이 폴더를 찾을 필요가 없습니다.
- `Assets/RenderFX/_Support/Editor`: 현재 이펙트 관리 화면과 경로 호환 처리.
- `Assets/RenderFX/_Seam/Data/Merged`: 현재 전투의 이펙트·연출 연결 데이터.
- `Assets/RenderFX/EnemySkill`: 적 이펙트. 이번 정리는 히어로부터 적용했습니다.

## 작업 규칙

- 이름: `FX_캐릭터_스킬_용도_Basic/Enhanced`. 공용 효과는 `Shared`로 구분합니다.
- 새 외부 이펙트는 프리팹과 재질·텍스처·메시·Shader Graph를 독립 복사한 뒤 사용합니다. 패키지 원본은 수정하지 않습니다.
- 복사한 내부 리소스는 `_Support`에, 실제 수정하는 현재 프리팹은 스킬의 `Prefabs`에 둡니다.
- 이동·이름 변경 시 GUID, 기존 색상·입자 설정, `Resources` 로드 키를 보존합니다.
- 프리셋 적용·캡처는 현재 전투에서 참조되는 프리팹을 우선 대상으로 사용합니다.
