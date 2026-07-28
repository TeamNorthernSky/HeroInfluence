using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 프리셋 → ParticleSystem 반영 로직 (런타임 공용).
    ///
    /// 에디터 전용으로 두면 "적용" 버튼을 눌러 프리팹에 굽기 전까지 값이 반영되지 않는다.
    /// 런타임에 두고 스폰 시점에 읽게 하면 프리셋을 만지는 즉시 다음 스폰부터 반영된다.
    /// 에디터 라이브 푸시도 같은 코드를 호출해 이중 구현을 피한다.
    ///
    /// 객체 이름 규약: PenStrokes(궤적) · SparkDots(입자) · Flash(타격 섬광).
    /// </summary>
    public static class JusticeTrailPresetRuntime
    {
        public const string TrailName = "PenStrokes";
        public const string SparkName = "SparkDots";
        public const string FlashName = "Flash";

        /// <summary>
        /// 색 × 발광 배수. 알파는 보존한다.
        ///
        /// 단순히 전 채널에 곱하면(chromaHold=0) 가산 합성에서 낮은 채널까지 1을 넘어가
        /// **밝아질수록 흰색으로 탈색**된다. 선홍 (1, 0.40, 0.36)에 5.93을 곱하면
        /// (5.93, 2.37, 2.13)이 되어 사실상 흰색이 되는 식이다.
        ///
        /// chromaHold를 올리면 지배 채널 위주로만 밝아져(가중치 = 채널비²) 낮은 채널이
        /// 1 아래에 머무르므로 밝아져도 색이 유지된다. 흰색(전 채널 동일)은 어느 쪽이든 흰색이다.
        /// </summary>
        static Color Boost(Color c, float emission, float chromaHold)
        {
            float e = Mathf.Max(0f, emission);
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m <= 0.0001f) return new Color(0f, 0f, 0f, c.a);

            float hold = Mathf.Clamp01(chromaHold);
            float Ch(float v)
            {
                float ratio = v / m;                                  // 0..1, 지배 채널이 1
                float w = Mathf.Lerp(1f, ratio * ratio, hold);        // hold=0이면 균등 배수
                return v * (1f + (e - 1f) * w);
            }
            return new Color(Ch(c.r), Ch(c.g), Ch(c.b), c.a);
        }

        /// <summary>
        /// 수명 그라데이션 공통 조립.
        /// 본체는 넘겨받은 팔레트 × bodyEmission, 머리 구간에는 별도 배수를 가진 하이라이트를 얹는다.
        /// 하이라이트를 재질 색에 섞지 않는 이유: 재질에 섞으면 전체가 탈색되어 채도가 무너진다.
        ///
        /// ★팔레트를 인자로 받는다. 예전엔 프리셋 최상단의 공용 팔레트를 직접 읽어
        /// 궤적과 타격 스파크가 같은 색에 묶여 있었다(한쪽만 바꿀 수 없었음).
        /// </summary>
        static Gradient BuildCore(Color headColor, Color midColor, Color tailColor,
                                  Color hlColor, float hlRatio, float hlEmission,
                                  float bodyEmission, float chromaHold, float alphaHold)
        {
            float hold = Mathf.Clamp(alphaHold, 0f, 0.95f);
            Color head = Boost(headColor, bodyEmission, chromaHold);
            Color mid = Boost(midColor, bodyEmission, chromaHold);
            Color tail = Boost(tailColor, bodyEmission, chromaHold);

            var g = new Gradient();
            float hl = Mathf.Clamp(hlRatio, 0f, 0.45f);

            if (hl <= 0.001f)
            {
                g.SetKeys(
                    new[]
                    {
                        new GradientColorKey(head, 0f),
                        new GradientColorKey(mid, 0.45f),
                        new GradientColorKey(tail, 1f),
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, hold),
                        new GradientAlphaKey(0f, 1f),
                    });
                return g;
            }

            float midT = Mathf.Max(hl + 0.15f, 0.45f);
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(Boost(hlColor, hlEmission, chromaHold), 0f),
                    new GradientColorKey(head, hl),
                    new GradientColorKey(mid, midT),
                    new GradientColorKey(tail, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, Mathf.Max(hold, hl)),
                    new GradientAlphaKey(0f, 1f),
                });
            return g;
        }

        /// <summary>궤적용 그라데이션. 하이라이트는 궤적 계열에만 존재한다(입자는 코어가 대신함).</summary>
        static Gradient BuildTrailGradient(JusticeTrailPreset.TrailGroup g) =>
            BuildCore(g.headColor, g.midColor, g.tailColor,
                      g.highlightColor, g.highlightRatio, g.highlightEmission, g.emission, g.chromaHold, g.alphaHold);

        /// <summary>타격 스파크용 그라데이션. 본체는 타격 자기 팔레트, 머리 구간은 타격 하이라이트.</summary>
        public static Gradient BuildImpactGradient(JusticeTrailPreset p)
        {
            var im = p.impact;
            return BuildCore(im.headColor, im.midColor, im.tailColor,
                             im.highlightColor, im.highlightRatio, im.highlightEmission, 1f, 1f, 0.5f);
        }

        /// <summary>방출·수명·크기 등 두 계열이 실제로 공유하는 항목만.</summary>
        static void ApplyCommon(ParticleSystem ps, JusticeTrailPreset.EmitGroupBase g)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(g.lifeMin, Mathf.Max(g.lifeMin, g.lifeMax));
            main.startSpeed = new ParticleSystem.MinMaxCurve(g.speedMin, Mathf.Max(g.speedMin, g.speedMax));
            main.startSize = new ParticleSystem.MinMaxCurve(g.sizeMin, Mathf.Max(g.sizeMin, g.sizeMax));
            main.maxParticles = g.maxParticles;

            // 기본은 무중력. 중력을 쓰는 계열(입자)만 뒤에서 덮어쓴다.
            // 프리팹에 값이 구워져 있어도 여기서 확실히 0으로 눌러 둔다.
            main.gravityModifier = 0f;

            var em = ps.emission;
            em.enabled = g.enabled;
            em.rateOverTime = g.enabled ? g.rateOverTime : 0f;
            em.rateOverDistance = g.enabled ? g.rateOverDistance : 0f;

            // ★Inherit Velocity는 쓰지 않는다. 모든 입자에 동일한 직선 벡터를 더해
            // 원뿔이 만든 방향 차이를 덮어버리기 때문. 뒤로 분출은 팔로워가 startSpeed로 넣는다.
            var iv = ps.inheritVelocity;
            iv.enabled = false;
        }

        /// <summary>궤적 계열 — per-particle 트레일 사용.</summary>
        public static void ApplyTrailGroup(ParticleSystem ps, JusticeTrailPreset.TrailGroup g)
        {
            if (ps == null || g == null) return;
            ApplyCommon(ps, g);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = g.shapeRadius;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(BuildTrailGradient(g));

            var tm = ps.trails;
            tm.enabled = true;
            tm.mode = ParticleSystemTrailMode.PerParticle;
            tm.ratio = 1f;
            tm.lifetime = new ParticleSystem.MinMaxCurve(g.trailLifetime);
            tm.minVertexDistance = g.trailMinVertexDistance;
            tm.worldSpace = true;
            tm.dieWithParticles = false;
            tm.sizeAffectsWidth = true;
            tm.inheritParticleColor = true;
            tm.textureMode = ParticleSystemTrailTextureMode.Stretch;
        }

        static readonly List<ParticleSystemVertexStream> SparkStreams = new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.AgePercent,   // → uv.z. 셰이더가 광점 비율을 계산하는 데 쓴다.
        };

        /// <summary>
        /// 입자 계열 — 궤적을 쓰지 않는다.
        /// 색은 colorOverLifetime이 아니라 **셰이더**가 담당한다(몸통/광점 공간 구조 때문).
        /// 파티클 시스템은 알파 페이드와 크기 축소만 맡는다.
        /// </summary>
        public static void ApplySparkGroup(ParticleSystem ps, JusticeTrailPreset.SparkGroup g,
                                           Material sparkMaterial)
        {
            if (ps == null || g == null) return;
            ApplyCommon(ps, g);

            // ★중력을 갖는 유일한 계열. 궤적·타격 스파크는 방사광이라 무중력이지만,
            // 입자는 바닥 마찰 불꽃 같은 실체 있는 연출로 변주할 여지가 있어 남겼다.
            var gm = ps.main;
            gm.gravityModifier = g.gravity;

            var tm = ps.trails;
            tm.enabled = false;   // 입자 전용 — 트레일 모듈은 사용하지 않는다

            // 방사형 분사 — 원뿔 축은 로컬 +Z. JcSocketTrailEffect가 이 자식을 진행 반대 방향으로 돌린다.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = g.spreadAngle;
            shape.radius = g.nozzleRadius;
            shape.rotation = Vector3.zero;
            shape.arc = 360f;
            shape.randomDirectionAmount = 0f;

            // 분출 후 감속
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = g.drag > 0.001f;
            if (lv.enabled)
            {
                lv.separateAxes = false;
                lv.limit = new ParticleSystem.MinMaxCurve(1000f);   // 속도 자체는 자르지 않는다
                lv.dampen = 0f;
                lv.drag = new ParticleSystem.MinMaxCurve(g.drag);
                lv.multiplyDragByParticleSize = false;
                lv.multiplyDragByParticleVelocity = true;
            }

            // 색은 셰이더가 정하므로 정점 색은 흰색으로 두고 알파만 곡선으로 넘긴다.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var ag = new Gradient();
            float hold = Mathf.Clamp(g.alphaHold, 0f, 0.95f);
            ag.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, hold),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(ag);

            // 점점 작아지다 사라짐
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, Mathf.Clamp01(g.sizeEndScale))));

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            if (rend == null) return;

            rend.renderMode = ParticleSystemRenderMode.Billboard;   // 원형 유지(Stretch면 각진 막대)
            if (sparkMaterial != null) rend.sharedMaterial = sparkMaterial;

            // 셰이더가 나이를 읽으려면 AgePercent 스트림이 필요하다.
            rend.enableGPUInstancing = false;
            rend.SetActiveVertexStreams(SparkStreams);

            Material m = rend.sharedMaterial;
            if (m == null) return;
            // 코어는 자체 발광을 쓴다. 예전처럼 하이라이트 발광을 빌려 쓰면
            // 하이라이트를 끌 때 코어가 검정(=가산에서 구멍)이 되어버린다.
            if (m.HasProperty("_BodyColor")) m.SetColor("_BodyColor", Boost(g.bodyColor, g.emission, g.chromaHold));
            if (m.HasProperty("_CoreColor")) m.SetColor("_CoreColor", Boost(g.coreColor, g.coreEmission, 1f));
            if (m.HasProperty("_Emission")) m.SetFloat("_Emission", 1f);
            if (m.HasProperty("_CoreStart")) m.SetFloat("_CoreStart", g.coreRatioStart);
            if (m.HasProperty("_CoreEnd")) m.SetFloat("_CoreEnd", g.coreRatioEnd);
            if (m.HasProperty("_CoreSharp")) m.SetFloat("_CoreSharp", g.coreSharpness);
            if (m.HasProperty("_BodySoft")) m.SetFloat("_BodySoft", g.bodySoftness);
        }

        public static void ApplyImpact(GameObject root, JusticeTrailPreset p, Material flashMaterial = null)
        {
            if (root == null || p == null) return;
            var im = p.impact;

            var ps = root.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(im.lifeMin, Mathf.Max(im.lifeMin, im.lifeMax));
                main.startSpeed = new ParticleSystem.MinMaxCurve(im.speedMin, Mathf.Max(im.speedMin, im.speedMax));

                // 화면상 치수(굵기·길이) → 파티클 파라미터 역산. 공식은 ImpactGroup 주석 참조.
                float w = Mathf.Max(im.width, 0.001f);
                main.startSize = new ParticleSystem.MinMaxCurve(w * (1f - Mathf.Clamp01(im.widthVariation)), w);

                // 방사광이라 중력을 받지 않는다(입자 계열만 중력을 가진다).
                main.gravityModifier = 0f;

                // ★enabled를 반영하지 않던 버그: 끄기 항목이 아예 없어 스파크를 멈출 수 없었다.
                var em = ps.emission;
                em.enabled = im.enabled;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(im.enabled ? im.burstCount : 0)) });

                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(BuildImpactGradient(p));

                var rend = ps.GetComponent<ParticleSystemRenderer>();
                if (rend != null)
                {
                    // 길이 = startSize × lengthScale 이므로, 가장 굵은 입자가 지정 길이가 되도록 역산한다.
                    // 가는 입자는 비례해서 짧아진다(굵을수록 길다 = 자연스러움).
                    rend.lengthScale = im.length / w;
                    // 예전엔 프리팹에 박혀 있어 조절할 수 없었다. 실측상 길이의 절반 이상을 만든다.
                    rend.velocityScale = im.velocityScale;
                }
            }

            var flashT = root.transform.Find(FlashName);
            var fps = flashT != null ? flashT.GetComponent<ParticleSystem>() : null;
            if (fps != null)
            {
                bool flashOn = im.enabled && im.flashEnabled;

                // 위치·방향은 JcImpactFlashDirector가 스폰 시점에 잡는다(시전자→타깃 기준).
                flashT.localPosition = Vector3.zero;

                // 길이·넓이를 따로 주려면 startSize3D가 필요하다(X=길이, Y=넓이).
                var fmain = fps.main;
                fmain.startSize3D = true;
                fmain.startSizeX = im.flashLength;
                fmain.startSizeY = im.flashWidth;
                fmain.startSizeZ = 1f;
                fmain.startLifetime = im.flashLifetime;
                fmain.maxParticles = 4;

                // 스프라이트 1장 고정 — 형상은 전부 셰이더가 그린다.
                var fem = fps.emission;
                fem.enabled = flashOn;
                fem.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(flashOn ? 1 : 0)) });

                var fshape = fps.shape;
                fshape.enabled = false;   // 한 점에서 1장만 나온다

                // 색·형상은 전용 셰이더가 담당한다. 파티클은 진행도(알파)만 넘긴다.
                // 셰이더가 이 값을 0~1로 정규화해 경계 잠식에 쓴다.
                var fcol = fps.colorOverLifetime;
                fcol.enabled = true;
                var fg = new Gradient();
                float fin = Mathf.Clamp(im.flashFadeIn, 0f, 0.9f);
                if (fin <= 0.001f)
                {
                    fg.SetKeys(
                        new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(im.flashAlpha, 0f), new GradientAlphaKey(0f, 1f) });
                }
                else
                {
                    // 등장 구간을 두면 경계가 바깥으로 자라며 나타난다.
                    fg.SetKeys(
                        new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[]
                        {
                            new GradientAlphaKey(0f, 0f),
                            new GradientAlphaKey(im.flashAlpha, fin),
                            new GradientAlphaKey(0f, 1f),
                        });
                }
                fcol.color = new ParticleSystem.MinMaxGradient(fg);

                var frend = fps.GetComponent<ParticleSystemRenderer>();
                if (frend != null)
                {
                    frend.renderMode = ParticleSystemRenderMode.Billboard;
                    frend.sortingFudge = im.flashSortingFudge;
                    if (flashMaterial != null)
                    {
                        frend.sharedMaterial = flashMaterial;   // 스파크와 재질 분리 → 색·형상 독립
                    }

                    // 타점이 쿼드의 한쪽 끝이 되도록 피벗을 길이 축으로 반 칸 민다.
                    // 이게 없으면 쿼드 중심이 타점이라 절반이 뒤로 삐져나온다.
                    frend.pivot = new Vector3(0.5f, 0f, 0f);

                    Material fm = frend.sharedMaterial;
                    if (fm != null)
                    {
                        if (fm.HasProperty("_BodyColor")) fm.SetColor("_BodyColor", im.flashColor);
                        if (fm.HasProperty("_CoreColor")) fm.SetColor("_CoreColor", Boost(im.flashCoreColor, im.flashCoreEmission, 1f));
                        if (fm.HasProperty("_StartWidth")) fm.SetFloat("_StartWidth", im.flashStartWidth);
                        if (fm.HasProperty("_EndWidth")) fm.SetFloat("_EndWidth", im.flashEndWidth);
                        if (fm.HasProperty("_WidthCurve")) fm.SetFloat("_WidthCurve", im.flashWidthCurve);
                        if (fm.HasProperty("_CoreWidth")) fm.SetFloat("_CoreWidth", im.flashCoreWidth);
                        if (fm.HasProperty("_CoreSharp")) fm.SetFloat("_CoreSharp", im.flashCoreSharpness);
                        if (fm.HasProperty("_BodySoft")) fm.SetFloat("_BodySoft", im.flashSoftness);
                        if (fm.HasProperty("_HeadFade")) fm.SetFloat("_HeadFade", im.flashHeadFade);
                        if (fm.HasProperty("_TailFade")) fm.SetFloat("_TailFade", im.flashTailFade);
                        if (fm.HasProperty("_EdgeFade")) fm.SetFloat("_EdgeFade", im.flashEdgeFade);
                        // 셰이더가 진행도를 0~1로 정규화하는 기준값
                        if (fm.HasProperty("_MaxAlpha")) fm.SetFloat("_MaxAlpha", Mathf.Max(im.flashAlpha, 0.001f));
                    }
                }

                // 방향·오프셋 담당 컴포넌트에 값 전달
                var director = root.GetComponent<JcImpactFlashDirector>();
                if (director != null)
                {
                    director.Offset = im.flashOffset;
                    director.FlipRotation = im.flashFlipRotation;
                }
            }
        }

        /// <summary>궤적 프리팹 루트(자식에 PenStrokes/SparkDots)에 프리셋 반영.</summary>
        public static void ApplyTrail(GameObject root, JusticeTrailPreset p, Material sparkMaterial = null)
        {
            if (root == null || p == null) return;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.gameObject.name == TrailName) ApplyTrailGroup(ps, p.trail);
                else if (ps.gameObject.name == SparkName) ApplySparkGroup(ps, p.spark, sparkMaterial);
            }

            // 분사 방향 정렬·속도 연동은 팔로워가 담당한다(소켓 위치 변화만 사용).
            var fx = root.GetComponent<Seam.JcSocketTrailEffect>();
            if (fx != null)
            {
                fx.AlignChildName = SparkName;
                fx.ScaleEmissionBySpeed = p.spark.scaleEmissionBySpeed;
                fx.SpeedForFullEmission = p.spark.speedForFullEmission;
                fx.EmissionAtRest = p.spark.emissionAtRest;
                fx.BackwardEjectScale = p.spark.backwardEject;
                fx.AlignMaxAngle = p.spark.alignMaxAngle;
                // 오브젝트 수명·오프셋도 실시간 반영 대상(예전엔 "적용"을 눌러야만 반영됐다).
                fx.FadeOutExtraSeconds = p.fadeOutExtraSeconds;
                fx.SocketOffset = p.socketOffset;
            }
        }

        /// <summary>재질 색을 프리셋에 맞춘다(공유 재질이라 즉시 전역 반영).</summary>
        public static void ApplyMaterials(JusticeTrailPreset p, Material strokeMat, Material impactMat)
        {
            if (p == null) return;
            if (impactMat != null)
            {
                if (impactMat.HasProperty("_Tint")) impactMat.SetColor("_Tint", p.impact.tint);
                if (impactMat.HasProperty("_Emission")) impactMat.SetFloat("_Emission", p.impact.emission);
            }
            // 궤적·입자 재질은 중립(흰색) 유지 — 색은 그라데이션이 전담한다.
            if (strokeMat != null && strokeMat.HasProperty("_BaseColor")) strokeMat.SetColor("_BaseColor", Color.white);
        }
    }

    /// <summary>
    /// 이펙트 프리팹에 붙여 두면 스폰될 때마다 프리셋을 읽어 반영한다.
    /// 덕분에 프리셋을 만지면 "적용" 버튼 없이도 다음 재생부터 곧바로 반영된다.
    ///
    /// 한 스킬이 색 변종 2종(기본 / +스킬)을 가지므로 프리셋 슬롯도 두 개다.
    /// 어느 쪽을 쓸지는 스폰 직전에 <see cref="UseAlternate"/>로 정한다 —
    /// Cue가 프리팹을 Instantiate하는 구조라 스폰 인자로 변종을 넘길 자리가 없기 때문.
    /// </summary>
    [DisallowMultipleComponent]
    public class JusticeTrailPresetBinder : MonoBehaviour
    {
        public enum Kind { Trail, Impact }

        /// <summary>다음에 스폰될 이펙트가 +스킬(변종) 프리셋을 쓸지. 재생 직전에 단축키가 세운다.</summary>
        public static bool UseAlternate;

        [Tooltip("읽어올 프리셋(기본 버전). 비우면 아무것도 하지 않는다.")]
        [SerializeField] private JusticeTrailPreset preset;

        [Tooltip("+스킬 버전 프리셋. 비어 있으면 기본을 그대로 쓴다.")]
        [SerializeField] private JusticeTrailPreset presetAlt;

        [Tooltip("이 프리팹이 궤적 묶음인지 타격인지.")]
        [SerializeField] private Kind kind = Kind.Trail;

        [Tooltip("재질 색도 함께 반영(공유 재질이라 전역에 즉시 적용됨).")]
        [SerializeField] private bool applyMaterials = true;

        [SerializeField] private Material strokeMaterial;
        [SerializeField] private Material impactMaterial;

        [Tooltip("입자(SparkDots)용 재질. Testbed/Justice/SparkDot 셰이더를 쓴다.")]
        [SerializeField] private Material sparkMaterial;

        [Tooltip("타격 섬광용 재질. 스파크와 분리해야 색·렌더 큐를 독립 조절할 수 있다.")]
        [SerializeField] private Material flashMaterial;

        public JusticeTrailPreset Preset { get => preset; set => preset = value; }
        public JusticeTrailPreset PresetAlt { get => presetAlt; set => presetAlt = value; }

        /// <summary>지금 실제로 쓰이는 프리셋. 에디터 라이브 반영이 대상을 가려내는 데도 쓴다.</summary>
        public JusticeTrailPreset ActivePreset => (UseAlternate && presetAlt != null) ? presetAlt : preset;

        private void Awake() => ApplyNow();

        public void ApplyNow() => Apply(ActivePreset);

        /// <summary>지정한 프리셋으로 반영한다. 에디터가 특정 변종을 강제 반영할 때도 쓴다.</summary>
        public void Apply(JusticeTrailPreset p)
        {
            if (p == null) return;

            if (kind == Kind.Trail) JusticeTrailPresetRuntime.ApplyTrail(gameObject, p, sparkMaterial);
            else JusticeTrailPresetRuntime.ApplyImpact(gameObject, p, flashMaterial);

            if (!applyMaterials) return;

            // 자기 종류의 재질만 만진다.
            // 예전엔 타격 프리팹도 strokeMaterial을 들고 있어, 스폰할 때마다 궤적 재질을 덮어썼다.
            if (kind == Kind.Trail) JusticeTrailPresetRuntime.ApplyMaterials(p, strokeMaterial, null);
            else JusticeTrailPresetRuntime.ApplyMaterials(p, null, impactMaterial);
        }
    }
}
