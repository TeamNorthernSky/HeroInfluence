using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>호 파편 프리셋 → 파티클·재질·컴포넌트 반영 (정적 적용 로직).</summary>
    public static class JusticeArcShardRuntime
    {
        public static void ApplyArcShard(GameObject root, JusticeArcShardPreset p, Material shardMat = null)
        {
            if (root == null || p == null) return;

            float life = Mathf.Max(p.lifeMax, 0.05f);
            float lifeMin = Mathf.Clamp(p.lifeMin, 0.02f, life);

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, life);
                main.startSize = new ParticleSystem.MinMaxCurve(p.sizeMin, Mathf.Max(p.sizeMin, p.sizeMax));
                main.startSpeed = new ParticleSystem.MinMaxCurve(p.speedMin, Mathf.Max(p.speedMin, p.speedMax));
                main.maxParticles = p.maxParticles;
                main.gravityModifier = p.gravity;
                // ★World 시뮬레이션 — 방출 지점만 앵커를 따라가고, 튄 파편은 월드에서 독립 비행.
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                // 파편 자세: 진행 방향 기준 좌우 랜덤 기울기(도 → 라디안)
                float tilt = p.tiltAngle * Mathf.Deg2Rad;
                main.startRotation3D = false;
                main.startRotation = new ParticleSystem.MinMaxCurve(-tilt, tilt);

                var em = ps.emission;
                em.enabled = true;
                em.rateOverTime = 0f;
                em.rateOverDistance = p.rateOverDistance;
                em.SetBursts(new ParticleSystem.Burst[0]);

                // 접선 발사 — 앵커의 +Z가 접선이므로 원뿔을 그 축으로 쏜다.
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = p.spreadAngle;
                shape.radius = Mathf.Max(p.nozzleRadius, 0.001f);
                shape.rotation = Vector3.zero;
                shape.position = Vector3.zero;
                shape.randomDirectionAmount = 0f;

                // 색: 테두리가 쓰는 수명 그라데이션 + fadeStart부터 알파 감쇠
                var col = ps.colorOverLifetime;
                col.enabled = true;
                float fs = Mathf.Clamp01(p.fadeStart);
                var grad = new Gradient();
                grad.SetKeys(
                    new[] {
                        new GradientColorKey(p.headColor, 0f),
                        new GradientColorKey(p.midColor, 0.5f),
                        new GradientColorKey(p.tailColor, 1f)
                    },
                    new[] {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, fs),
                        new GradientAlphaKey(0f, 1f)
                    });
                col.color = new ParticleSystem.MinMaxGradient(grad);

                // 크기: shrinkStart부터 shrinkEndScale로 축소 (페이드와 독립)
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.separateAxes = false;
                float ss = Mathf.Clamp01(p.shrinkStart);
                var curve = new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(ss, 1f),
                    new Keyframe(1f, Mathf.Clamp01(p.shrinkEndScale)));
                sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = p.drag > 0.001f;
                lim.dampen = 0f;
                lim.drag = p.drag;
                lim.multiplyDragByParticleSize = false;
                lim.multiplyDragByParticleVelocity = true;

                var tm = ps.trails;
                tm.enabled = false;   // 트레일은 이번 구현에서 제외(확정)

                var rend = ps.GetComponent<ParticleSystemRenderer>();
                if (rend != null)
                {
                    rend.renderMode = ParticleSystemRenderMode.Billboard;
                    // ★진행 방향 정렬 — 파편의 뾰족한 끝이 날아가는 쪽을 향한다.
                    rend.alignment = ParticleSystemRenderSpace.Velocity;
                    if (shardMat != null) rend.sharedMaterial = shardMat;

                    // ★파티클별 고정 난수를 셰이더로 — 절단점이 파편마다 달라져 변주가 무한해진다.
                    var streams = new List<ParticleSystemVertexStream>
                    {
                        ParticleSystemVertexStream.Position,
                        ParticleSystemVertexStream.Color,
                        ParticleSystemVertexStream.UV,               // TEXCOORD0.xy
                        ParticleSystemVertexStream.StableRandomXY,   // TEXCOORD0.zw (난수 2개를 한 번에)
                    };
                    rend.SetActiveVertexStreams(streams);
                }
            }

            if (shardMat != null)
            {
                shardMat.SetColor("_InnerColor", p.innerColor);
                shardMat.SetFloat("_InnerEmission", p.innerEmission);
                shardMat.SetFloat("_EdgeEmission", p.edgeEmission);
                shardMat.SetFloat("_TriWidth", p.triWidth);
                shardMat.SetFloat("_TriHeight", p.triHeight);
                shardMat.SetFloat("_CutMin", p.cutMin);
                shardMat.SetFloat("_CutMax", p.cutMax);
                shardMat.SetFloat("_EdgeWidth", p.edgeWidth);
                shardMat.SetFloat("_EdgeSoft", p.edgeSoft);
            }

            var fx = root.GetComponent<Seam.JcArcShardEffect>();
            if (fx != null)
            {
                fx.AngleStart = p.angleStart;
                fx.AngleEnd = p.angleEnd;
                fx.Radius = p.radius;
                fx.SweepDuration = p.sweepDuration;
                fx.EaseOut = p.easeOut;
                fx.SpawnOffset = p.spawnOffset;
                fx.ExtraLinger = p.extraLinger;
                fx.LifeMaxForCleanup = life;
            }
        }
    }

    /// <summary>
    /// 프리팹에 붙여 두면 스폰될 때마다 프리셋을 읽어 반영한다.
    /// 색 변종은 JusticeTrailPresetBinder.UseAlternate 정적 플래그를 공유한다(기본=청 / 알트=적).
    /// </summary>
    [DisallowMultipleComponent]
    public class JusticeArcShardBinder : MonoBehaviour
    {
        [Tooltip("읽어올 프리셋(기본 버전). 비우면 아무것도 하지 않는다.")]
        [SerializeField] private JusticeArcShardPreset preset;

        [Tooltip("+스킬 버전 프리셋(적색). 비어 있으면 기본을 그대로 쓴다.")]
        [SerializeField] private JusticeArcShardPreset presetAlt;

        [Tooltip("파편 재질. 프리셋 targets와 같은 것을 가리킨다.")]
        [SerializeField] private Material shardMaterial;

        public JusticeArcShardPreset Preset { get => preset; set => preset = value; }
        public JusticeArcShardPreset PresetAlt { get => presetAlt; set => presetAlt = value; }

        [Tooltip("★켜면 이 인스턴스는 무조건 알트(강화·적색) 프리셋을 쓴다. 강화 스킬 전용 프리팹용.")]
        [SerializeField] private bool forceAlternate;

        public bool ForceAlternate { get => forceAlternate; set => forceAlternate = value; }

        public JusticeArcShardPreset ActivePreset =>
            ((forceAlternate || JusticeTrailPresetBinder.UseAlternate) && presetAlt != null) ? presetAlt : preset;

        private void Awake() => ApplyNow();

        public void ApplyNow() => JusticeArcShardRuntime.ApplyArcShard(gameObject, ActivePreset, shardMaterial);
    }
}
