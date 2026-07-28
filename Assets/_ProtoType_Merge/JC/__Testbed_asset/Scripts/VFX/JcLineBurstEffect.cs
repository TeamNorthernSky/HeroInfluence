using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 방사형 선 폭발(Line Burst).
    ///
    /// 유래: 2026-07-28 저스티스 주먹 궤적 작업 중 우발적으로 나온 형태.
    /// per-particle 트레일을 쓰는 궤적 파티클에 매우 큰 초기 속도가 실리면서,
    /// 사방으로 뻗어나가는 긴 선다발이 그려졌다. 의도한 결과는 아니었지만 독립 이펙트로 쓸 만해
    /// 재현 가능한 형태로 분리해 보존한다.
    ///
    /// 원리:
    ///   구형(Sphere) 분포 + 큰 startSpeed → 입자가 사방으로 직선 비행
    ///   + per-particle 트레일(worldSpace) → 비행 자취가 그대로 선으로 남음
    ///   + dieWithParticles=false → 입자가 죽어도 선이 잠시 남아 사라짐
    /// 속도·수명·트레일 수명의 조합이 선의 길이를 정한다.
    ///
    /// ASB 연출 규약과 호환된다(ISkillEffectBehaviour). Cue의 Spawn으로 띄우면 즉시 1회 터진다.
    ///
    /// 주의: 진단용으로 EditorApplication.update에 훅을 거는 방식은 쓰지 않는다.
    /// 대상이 파괴된 뒤에도 훅이 남아 매 프레임 예외를 던지고, 도메인 리로드가 꺼져 있으면
    /// 플레이 정지로도 해제되지 않는다(2026-07-28 실제 사고). 계측이 필요하면 이펙트 자신이
    /// 생성·소멸 시점에 한 줄씩 기록하게 한다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    [DisallowMultipleComponent]
    public class JcLineBurstEffect : MonoBehaviour, ISkillEffectBehaviour
    {
        [Header("색")]
        [Tooltip("선 색. 가산 합성이므로 1을 넘기면 발광한다.")]
        [ColorUsage(true, true)] public Color lineColor = new Color(4.4f, 0.10f, 0.11f);
        [Tooltip("꼬리 쪽으로 갈수록 도달하는 색.")]
        [ColorUsage(true, true)] public Color tailColor = new Color(1.2f, 0.02f, 0.04f);

        [Header("폭발")]
        [Tooltip("한 번에 터지는 선의 수.")]
        [Range(1, 200)] public int lineCount = 40;
        [Tooltip("선의 뻗는 속도(m/s). 클수록 길고 빠르게 뻗는다.")]
        [Range(1f, 60f)] public float speedMin = 12f;
        [Range(1f, 60f)] public float speedMax = 34f;
        [Tooltip("입자 수명(초). 선이 뻗는 시간.")]
        [Range(0.05f, 2f)] public float lifeMin = 0.18f;
        [Range(0.05f, 2f)] public float lifeMax = 0.45f;
        [Tooltip("발생 구 반경(m). 0에 가까울수록 한 점에서 터진다.")]
        [Range(0f, 1f)] public float originRadius = 0.05f;
        [Tooltip("중력 배수. 음수면 위로 흩어진다.")]
        [Range(-3f, 3f)] public float gravity = 0f;

        [Header("선")]
        [Tooltip("선의 굵기(입자 크기가 선 폭이 된다).")]
        [Range(0.005f, 0.5f)] public float widthMin = 0.04f;
        [Range(0.005f, 0.5f)] public float widthMax = 0.12f;
        [Tooltip("입자가 죽은 뒤 선이 남아 있는 시간(초).")]
        [Range(0.02f, 2f)] public float trailLifetime = 0.35f;
        [Tooltip("선의 정점 간격(m). 작을수록 매끄럽다.")]
        [Range(0.001f, 0.05f)] public float minVertexDistance = 0.01f;

        [Header("재질")]
        [Tooltip("입자·선에 함께 쓸 재질. 가산 파티클 셰이더 권장.")]
        public Material lineMaterial;

        [Header("정리")]
        [Tooltip("재생 후 자동으로 파괴할지(원샷 사용 시).")]
        public bool destroyWhenFinished = true;
        [Range(0f, 2f)] public float extraLifetime = 0.2f;

        private ParticleSystem ps;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            Configure();
        }

        private void Reset()
        {
            ps = GetComponent<ParticleSystem>();
            Configure();
        }

        /// <summary>현재 필드 값으로 파티클 시스템을 구성한다. 런타임·에디터 공용.</summary>
        public void Configure()
        {
            if (ps == null) ps = GetComponent<ParticleSystem>();
            if (ps == null) return;

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = Mathf.Max(0.1f, lifeMax);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, Mathf.Max(lifeMin, lifeMax));
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, Mathf.Max(speedMin, speedMax));
            main.startSize = new ParticleSystem.MinMaxCurve(widthMin, Mathf.Max(widthMin, widthMax));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(lineCount, 8);
            main.gravityModifier = gravity;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.rateOverDistance = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)lineCount) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = originRadius;
            shape.randomDirectionAmount = 0f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(lineColor, 0f), new GradientColorKey(tailColor, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            // ★핵심 — 비행 자취를 선으로 남긴다.
            var tm = ps.trails;
            tm.enabled = true;
            tm.mode = ParticleSystemTrailMode.PerParticle;
            tm.ratio = 1f;
            tm.lifetime = new ParticleSystem.MinMaxCurve(trailLifetime);
            tm.minVertexDistance = minVertexDistance;
            tm.worldSpace = true;
            tm.dieWithParticles = false;
            tm.sizeAffectsWidth = true;
            tm.inheritParticleColor = true;
            tm.textureMode = ParticleSystemTrailTextureMode.Stretch;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.renderMode = ParticleSystemRenderMode.Billboard;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                if (lineMaterial != null)
                {
                    rend.sharedMaterial = lineMaterial;
                    rend.trailMaterial = lineMaterial;
                }
            }
        }

        /// <summary>지정 위치에서 1회 터뜨린다.</summary>
        public void Play(Vector3 worldPos)
        {
            transform.position = worldPos;
            Configure();
            ps.Clear(true);
            ps.Play(true);
            if (destroyWhenFinished) Destroy(gameObject, lifeMax + trailLifetime + extraLifetime);
        }

        /// <summary>ASB 연출 규약 진입점. Cue의 Spawn 위치에서 터진다.</summary>
        public void Play(SkillEffectContext ctx)
        {
            Vector3 pos = ctx != null ? ctx.SpawnPosition : transform.position;
            Play(pos);
        }
    }
}
