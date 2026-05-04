# VFX Catalog (JC 개인용)

> AI 에이전트 및 본인의 VFX 프리팹 조회용 카탈로그.
> 위치: `Assets/JC_Work/_Personal/_Docs/VFX_Catalog.md`
> gitignore 대상 (`_Personal/` 전체 제외). 공용화 시 `Assets/JC_Work/Docs_jc/`로 이동.
> 스키마는 초안이며, 이펙트 추가 시 필드를 다듬는다.

---

## 스키마 정의

각 엔트리는 아래 필드를 갖는다.

| 필드 | 필수 | 설명 |
|---|---|---|
| `id` | ✓ | 호출용 고유 ID. 네임스페이스 규칙: `vfx.{category}_{variant}` |
| `이름` | ✓ | 사람이 읽는 이름 |
| `경로` | ✓ | 프리팹 에셋 경로 (Assets/ 부터) |
| `유형` | ✓ | Particle / Shader / Sprite / Composite |
| `원본 출처` | — | 구매/수정 에셋 출처 (Asset Store 이름 등) |
| `기본 Duration` | ✓ | 1회 재생 시간 (초). 루프면 `looping` |
| `Looping` | ✓ | Yes / No / Loopable (선택적) |
| `Play On Awake` | ✓ | Yes / No |
| `기본 색상` | — | 주 색상 계열 (`#0077FF` 등) |
| `권장 Scale` | — | 적용 대상 대비 권장 로컬 scale |
| `대상 유형` | — | 캐릭터 / 바닥 투영 / 월드 고정 등 |
| `적합 연출` | — | 이 이펙트가 잘 어울리는 상황 |
| `커스터마이즈 포인트` | — | 기본 노출된 조절 수단 (컴포넌트/파라미터) |
| `의존 컴포넌트` | — | 필수 부착 컴포넌트 |
| `주의사항` | — | 알려진 제약/함정 |
| `커밋 여부` | ✓ | Yes (공용) / No (개인용) |

### ID 네이밍 규칙
- 소문자 + 스네이크 케이스
- `vfx.` 접두사 고정
- category: `rings`, `shield`, `pillar`, `impact`, `trail` 등
- variant: 색상·크기·스타일 구분 (`_blue`, `_red`, `_sm`, `_lg`)
- 예: `vfx.rings_blue`, `vfx.shield_sphere_gold`

---

## 엔트리 목록

### `vfx.rings_blue`

| 필드 | 값 |
|---|---|
| 이름 | Rings (푸른 상승 고리) |
| 경로 | `Assets/JC_Work/_Personal/_Prefab/VFX_Rings.prefab` |
| 유형 | Particle (Composite: Rings + Embers + Smoke) |
| 원본 출처 | Unity Particle Pack — `EffectExamples/Misc Effects/Respawn.prefab`에서 `Rings` 자식만 분리 |
| 기본 Duration | 2s (원본) |
| Looping | No (런타임 재생 시 코드에서 loop=true로 전환하여 연장 가능) |
| Play On Awake | No (Simulate/스크립트 트리거 필요) |
| 기본 색상 | `#007BFF` (파랑) + 흰색 하이라이트 랜덤 믹스 |
| 권장 Scale | (1, 1, 1) — 원본 Respawn 루트가 scale=2였으나 분리 시 1로 리셋됨. 캐릭터 크기에 맞춰 조정 |
| 대상 유형 | 캐릭터 (캡슐 정도 크기의 타겟에 자식으로 부착) |
| 적합 연출 | 텔레포트, 리스폰, 버프 적용, 마법 시전, 소환 |
| 커스터마이즈 포인트 | `RingsColorController` 컴포넌트 (ring_color, intensity, apply_to_children) |
| 의존 컴포넌트 | ParticleSystem (RequireComponent) |
| 주의사항 | Embers/Smoke 자식 포함된 Composite. `Color Over Lifetime` 모듈 활성 시 색상 제어가 덮어씌워질 수 있음. `[ExecuteAlways]`로 에디터 내 실시간 프리뷰 지원 |
| 커밋 여부 | No (개인용) |

#### 사용 예시

```csharp
// 1. 씬에 부착: Player 자식으로 프리팹 인스턴시에이트
// 2. Inspector에서 Ring Color 지정
// 3. 재생: VFXKeyTester 컴포넌트가 숫자키 5로 5초간 재생 제어

// 코드로 직접 재생:
var vfx = player.Find("VFX_Rings");
foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>()) ps.Play();
```

---

## 추후 추가 예정 이펙트 (플레이스홀더)

아래는 아직 없음. 이 구조로 추가해 나간다.

- `vfx.pillar_gold` — 황금빛 빛 기둥 (Cylinder + 커스텀 셰이더, 텍스처 없음)
- `vfx.shield_sphere_blue` — 에너지 쉴드 (Sphere + Fresnel 셰이더)
- `vfx.impact_blue` — 타격 플래시 (파티클 원샷)

---

## 변경 이력

- 2026-04-14: 초안 작성, `vfx.rings_blue` 등록
