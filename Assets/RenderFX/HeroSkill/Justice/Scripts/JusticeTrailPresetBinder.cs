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

        /// <summary>
        /// 궤적용 그라데이션 — 본체 색만.
        /// 하이라이트는 수명 그라데이션에서 빠졌다: 획 중심선 코어(StrokeCore 셰이더)로 개념 교체.
        /// </summary>
        static Gradient BuildTrailGradient(JusticeTrailPreset.TrailGroup g) =>
            BuildCore(g.headColor, g.midColor, g.tailColor,
                      Color.white, 0f, 0f, g.emission, g.chromaHold, g.alphaHold);

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

        /// <summary>
        /// 획 양끝 테이퍼 — ★수명(나이) 기반 크기 곡선.
        ///
        /// 처음엔 widthOverTrail(폭 곡선)로 했는데, 그 곡선은 트레일 「길이」에 정규화된다.
        /// 갓 태어난 트레일은 길이가 정점 몇 개뿐이라 곡선 전체가 압축되고, 곡선 한가운데의
        /// 폭 100%가 그 짧은 구간에 그대로 적용된다 → 길이 몇 cm에 폭 1m대 쿼드가 서는
        /// 「진행 방향 가로 글리치」가 태어날 때마다 찍혔다.
        ///
        /// sizeOverLifetime은 시간 기반이고, 트레일 정점은 기록 시점의 입자 크기를 폭으로
        /// 굽는다. 입자가 폭 0에서 태어나 자라면 트레일 길이와 무관하게 시작이 항상 가늘다.
        /// 수명 끝에서 다시 줄어 꼬리도 뾰족해진다.
        /// </summary>
        static void ApplyStrokeTaper(ParticleSystem ps, float endFade)
        {
            var sol = ps.sizeOverLifetime;
            float f = Mathf.Clamp(endFade, 0f, 0.45f);
            if (f < 0.001f)
            {
                sol.enabled = false;
                return;
            }
            sol.enabled = true;
            sol.separateAxes = false;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(f, 1f),
                new Keyframe(1f - f, 1f), new Keyframe(1f, 0f)));
        }

        /// <summary>코어 단면 파라미터를 재질 uniform으로. StrokeCore 셰이더가 아닌 재질에는 조용히 무시된다.</summary>
        static void PushCoreParams(Material m, Color coreColor, float coreEmission, float coreWidth,
                                   float coreSharpness, float bodyFalloff, float endFade)
        {
            if (m == null) return;
            if (m.HasProperty("_CoreColor")) m.SetColor("_CoreColor", Boost(coreColor, coreEmission, 1f));
            if (m.HasProperty("_CoreWidth")) m.SetFloat("_CoreWidth", coreWidth);
            if (m.HasProperty("_CoreSharp")) m.SetFloat("_CoreSharp", coreSharpness);
            if (m.HasProperty("_BodyFalloff")) m.SetFloat("_BodyFalloff", bodyFalloff);
            if (m.HasProperty("_EndFade")) m.SetFloat("_EndFade", endFade);
        }

        /// <summary>궤적 계열 — per-particle 트레일 사용. 획 중심선 코어는 재질(StrokeCore 셰이더)이 그린다.</summary>
        public static void ApplyTrailGroup(ParticleSystem ps, JusticeTrailPreset.TrailGroup g, Material strokeMaterial = null)
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
            tm.lifetime = new ParticleSystem.MinMaxCurve(1f);   // 입자 수명에 대한 배율 — 1 고정, 잔상은 수명 Min/Max로
            tm.minVertexDistance = g.trailMinVertexDistance;
            tm.worldSpace = true;
            tm.dieWithParticles = false;
            tm.sizeAffectsWidth = true;
            tm.widthOverTrail = new ParticleSystem.MinMaxCurve(1f);   // 길이 정규화 곡선은 짧은 트레일에서 글리치 — 테이퍼는 수명 기반으로
            tm.inheritParticleColor = true;
            tm.textureMode = ParticleSystemTrailTextureMode.Stretch;   // ★uv.y=폭 — StrokeCore가 단면을 그리는 전제

            ApplyStrokeTaper(ps, g.endFade);   // 양끝 테이퍼(수명 기반) — 뭉툭한 시작·번짐·가로 글리치 차단

            if (strokeMaterial != null)
            {
                // 예전엔 렌더러 재질을 안 만져서, 복제된 프리팹이 원본 스킬의 궤적 재질을
                // 계속 물고 있는 커플링이 있었다(대쉬 궤적이 등장! 재질을 사용). 여기서 결선한다.
                var rend = ps.GetComponent<ParticleSystemRenderer>();
                if (rend != null)
                {
                    // ★입자 빌보드는 끈다 — 입자는 앵커일 뿐, 그림은 트레일이 전부다.
                    // StrokeCore를 빌보드에 그리면 획 머리에 화면 수평의 밝은 선이 찍혀
                    // "코어가 테두리에 있는 것처럼" 보이는 결함이 있었다.
                    rend.renderMode = ParticleSystemRenderMode.None;
                    rend.trailMaterial = strokeMaterial;
                }
                PushCoreParams(strokeMaterial, g.coreColor, g.coreEmission, g.coreWidth, g.coreSharpness, g.bodyFalloff, g.endFade);
            }
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

        /// <summary>
        /// 호 획 V2 — 재설계(260729). 획 하나의 일생 규칙:
        ///   두께: 0 → 최대(증가 비율 growRatio) → 유지 → 0 (소멸 비율 fadeRatio)
        ///   소멸: 전체 유지 후 알파 페이드 — 트레일 정점이 개별 만료로 뒤에서 지워지지 않는다.
        ///
        /// 구현 배선:
        ///   sizeOverLifetime  (0,0)→(grow,1)→(fadeStart,1)→(1,0)   ← 두께 규칙
        ///   colorOverLifetime 알파 1 유지 → fadeStart부터 0으로       ← 소멸 규칙
        ///   trails.lifetime 배율 1 → 정점이 획 일생 내내 생존(혜성 없음)
        ///   trails.dieWithParticles = true → 입자 사망(알파 0) 시 일괄 소거
        /// </summary>
        public static void ApplyArcStroke(GameObject root, JusticeTrailPreset p, Material arcMaterial = null)
        {
            if (root == null || p == null) return;
            var g = p.arcStroke;

            float life = Mathf.Max(g.strokeLifetime, 0.1f);
            float lifeMin = Mathf.Clamp(g.strokeLifeMin, 0.05f, life);
            // ★grow·fade는 일생 비율(260729 전환) — 곡선이 수명 정규화라 비율이 그대로 키가 된다.
            // 어떤 수명의 획이든(랜덤 추첨·잔여 클램프 압축 포함) 같은 비율 규칙을 받는다.
            float growT = Mathf.Clamp(g.growRatio, 0.01f, 0.98f);
            float fadeStart = Mathf.Clamp(1f - g.fadeRatio, growT + 0.01f, 0.99f);

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, life);
                main.startSize = new ParticleSystem.MinMaxCurve(g.widthMin, Mathf.Max(g.widthMin, g.widthMax));
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f);   // 이동은 앵커가 전담
                main.maxParticles = g.maxParticles;
                main.gravityModifier = 0f;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;   // 앵커에 실려 주행

                var em = ps.emission;
                em.enabled = g.enabled;
                em.rateOverTime = 0f;
                em.rateOverDistance = g.enabled ? g.rateOverDistance : 0f;
                em.SetBursts(new ParticleSystem.Burst[0]);

                // ★공전하는 직사각형 노즐 — X=반경 방향(고른 분포), Y·Z=두께.
                // 회전은 JcArcStrokeEffect가 주행 각도에 맞춰 매 서브스텝 갱신한다(신규 입자에만 적용).
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(
                    Mathf.Max(g.nozzleRadius * 2f, 0.002f),
                    Mathf.Max(g.nozzleThickness, 0.005f),
                    Mathf.Max(g.nozzleThickness, 0.005f));
                shape.rotation = new Vector3(0f, -90f + g.nozzleTilt, 0f);   // 시작각 0(12시) 기준 초기값 — 런타임에 덮임
                shape.randomDirectionAmount = 0f;

                // 색은 수명 그라데이션, 알파는 「유지 → 소멸 구간 페이드」 — BuildCore의 hold가 곧 fadeStart.
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(
                    BuildCore(g.headColor, g.midColor, g.tailColor,
                              Color.white, 0f, 0f, g.emission, g.chromaHold, fadeStart));

                // ★두께 규칙: 0 → 최대(growRatio) → 유지 → 0(fadeRatio). Width 값 = 최대 두께.
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.separateAxes = false;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0f), new Keyframe(growT, 1f),
                    new Keyframe(fadeStart, 1f), new Keyframe(1f, 0f)));

                var tm = ps.trails;
                tm.enabled = true;
                tm.mode = ParticleSystemTrailMode.PerParticle;
                tm.ratio = 1f;
                tm.lifetime = new ParticleSystem.MinMaxCurve(1f);   // 배율 1 — 정점이 획 일생 내내 생존
                tm.minVertexDistance = g.trailMinVertexDistance;
                tm.worldSpace = true;
                tm.dieWithParticles = true;    // ★사망 = 알파 0 시점 — 일괄 소거(혜성·잔재 없음)
                tm.sizeAffectsWidth = true;
                // ★길이 방향 방추형 — widthOverTrail은 획 길이에 정규화된 폭 곡선.
                // sizeAffectsWidth는 리본 「전체」를 현재 입자 크기로 라이브 스케일하므로(정점에 구워지지 않음),
                // 시작·끝을 가늘게 하려면 이 곡선이 필수다. 과거의 「탄생 넓은 쿼드」 글리치는
                // 탄생 크기가 0(sizeOverLifetime)이 된 지금은 성립하지 않는다.
                float et = Mathf.Clamp(g.edgeTaper, 0f, 0.45f);
                tm.widthOverTrail = et < 0.001f
                    ? new ParticleSystem.MinMaxCurve(1f)
                    : new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                        new Keyframe(0f, 0f), new Keyframe(et, 1f),
                        new Keyframe(1f - et, 1f), new Keyframe(1f, 0f)));
                tm.inheritParticleColor = true;
                tm.textureMode = ParticleSystemTrailTextureMode.Stretch;

                var rend = ps.GetComponent<ParticleSystemRenderer>();
                if (rend != null && arcMaterial != null)
                {
                    rend.renderMode = ParticleSystemRenderMode.None;   // 입자는 앵커 — 그림은 트레일이 전부
                    rend.trailMaterial = arcMaterial;
                }
            }

            // 재질은 몸통 단면만 — 코어(중심선)는 V2에서 제거된 기능이라 검정으로 눌러 무력화한다.
            PushCoreParams(arcMaterial, Color.black, 0f, 0.01f, 1f, g.bodyFalloff, 0.05f);

            var fx = root.GetComponent<Seam.JcArcStrokeEffect>();
            if (fx != null)
            {
                fx.AngleStart = g.angleStart;
                fx.AngleEnd = g.angleEnd;
                fx.Radius = g.radius;
                fx.SweepDuration = g.sweepDuration;
                fx.EaseOut = g.easeOut;
                fx.NozzleTilt = g.nozzleTilt;
                fx.SpawnOffset = g.spawnOffset;
                // 말단 컷은 별도 결선이 없다 — 컴포넌트가 「잔여 < Stroke Life Min」으로 스스로 판정한다.
                // (구 growTime 절대 초 컷은 비율 전환으로 전제가 소멸해 폐기, 260729)
                fx.EmissionRamp = g.emissionRamp;
                fx.EmissionDecay = g.emissionDecay;
                fx.BaseRateOverDistance = g.rateOverDistance;
                fx.StrokeLifeMin = lifeMin;
                fx.StrokeLifeMax = life;   // 잔여 시간 클램프의 원본값 — 컴포넌트가 매 서브스텝 조여 간다
                fx.SkipShortRemainder = g.skipShortRemainder;
                fx.ThinDecay = g.thinDecay;
                fx.ThinFarBoost = g.thinFarBoost;
                // 차등 기준 거리 = 획들이 실제 분포하는 반경 대역(궤도 ± 퍼짐)
                fx.ThinRadiusInner = Mathf.Max(0f, g.radius - g.nozzleRadius);
                fx.ThinRadiusOuter = g.radius + Mathf.Max(g.nozzleRadius, 0.05f);
                // 재조준이 점프할 「하강 구간 시작」 — 두께 곡선(fadeStart)과 반드시 일치해야 한다.
                fx.FadeStartNorm = fadeStart;
            }
        }

        /// <summary>
        /// 호 포인트 획 — 본 호 획 위에 겹치는 액센트 가닥. 파티클 구성은 ApplyArcStroke와
        /// 같은 규칙(획 일생·방추형·트레일)이고, 궤도·수렴은 JcArcPointEffect에 밀어 넣는다.
        /// 전용 재질을 받으므로 단면 감쇠(bodyFalloff)도 본 획과 독립이다.
        /// </summary>
        public static void ApplyArcPoint(GameObject root, JusticeTrailPreset p, Material pointMaterial = null)
        {
            if (root == null || p == null) return;
            var g = p.arcPoint;

            float life = Mathf.Max(g.strokeLifetime, 0.1f);
            float lifeMin = Mathf.Clamp(g.strokeLifeMin, 0.05f, life);
            float growT = Mathf.Clamp(g.growRatio, 0.01f, 0.98f);
            float fadeStart = Mathf.Clamp(1f - g.fadeRatio, growT + 0.01f, 0.99f);

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, life);
                main.startSize = new ParticleSystem.MinMaxCurve(g.widthMin, Mathf.Max(g.widthMin, g.widthMax));
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
                main.maxParticles = g.maxParticles;
                main.gravityModifier = 0f;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;

                var em = ps.emission;
                em.enabled = g.enabled;
                em.rateOverTime = 0f;
                em.rateOverDistance = g.enabled ? g.rateOverDistance : 0f;
                em.SetBursts(new ParticleSystem.Burst[0]);

                // ★탄생 위치 = 시작 오프셋 — 상자 X(반경 방향) 스케일이 [Offset Min, Max] 대역 폭이고,
                // 대역 중심으로의 이동·공전 회전은 JcArcPointEffect가 매 서브스텝 준다.
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(
                    Mathf.Max(Mathf.Abs(g.offsetMax - g.offsetMin), 0.002f),
                    Mathf.Max(g.nozzleThickness, 0.005f),
                    Mathf.Max(g.nozzleThickness, 0.005f));
                shape.rotation = new Vector3(0f, -90f, 0f);
                shape.randomDirectionAmount = 0f;

                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(
                    BuildCore(g.headColor, g.midColor, g.tailColor,
                              Color.white, 0f, 0f, g.emission, g.chromaHold, fadeStart));

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.separateAxes = false;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0f), new Keyframe(growT, 1f),
                    new Keyframe(fadeStart, 1f), new Keyframe(1f, 0f)));

                var tm = ps.trails;
                tm.enabled = true;
                tm.mode = ParticleSystemTrailMode.PerParticle;
                tm.ratio = 1f;
                tm.lifetime = new ParticleSystem.MinMaxCurve(1f);
                tm.minVertexDistance = g.trailMinVertexDistance;
                tm.worldSpace = true;
                tm.dieWithParticles = true;
                tm.sizeAffectsWidth = true;
                float et = Mathf.Clamp(g.edgeTaper, 0f, 0.45f);
                tm.widthOverTrail = et < 0.001f
                    ? new ParticleSystem.MinMaxCurve(1f)
                    : new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                        new Keyframe(0f, 0f), new Keyframe(et, 1f),
                        new Keyframe(1f - et, 1f), new Keyframe(1f, 0f)));
                tm.inheritParticleColor = true;
                tm.textureMode = ParticleSystemTrailTextureMode.Stretch;

                var rend = ps.GetComponent<ParticleSystemRenderer>();
                if (rend != null && pointMaterial != null)
                {
                    rend.renderMode = ParticleSystemRenderMode.None;
                    rend.trailMaterial = pointMaterial;
                }
            }

            PushCoreParams(pointMaterial, Color.black, 0f, 0.01f, 1f, g.bodyFalloff, 0.05f);

            var fx = root.GetComponent<Seam.JcArcPointEffect>();
            if (fx != null)
            {
                fx.AngleStart = g.angleStart;
                fx.AngleEnd = g.angleEnd;
                fx.Radius = g.radius;
                fx.SweepDuration = g.sweepDuration;
                fx.EaseOut = g.easeOut;
                fx.OffsetMin = g.offsetMin;
                fx.OffsetMax = g.offsetMax;
                fx.ConvergeExp = g.convergeExp;
                fx.SpawnOffset = g.spawnOffset;
                fx.ExtraLinger = g.extraLinger;
                fx.EmissionRamp = g.emissionRamp;
                fx.EmissionDecay = g.emissionDecay;
                fx.BaseRateOverDistance = g.rateOverDistance;
                fx.StrokeLifeMin = lifeMin;
                fx.StrokeLifeMax = life;
                fx.SkipShortRemainder = g.skipShortRemainder;
            }
        }

        /// <summary>
        /// 호 참격 — 앵커 주행형. 파티클은 발 궤적(PenStrokes)과 같은 구성:
        /// Local 시뮬레이션으로 앵커에 실려 이동하고, worldSpace 트레일이 획을 남긴다.
        /// 궤도·주행은 JcArcSweepEffect 컴포넌트에 밀어 넣는다.
        /// </summary>
        public static void ApplySlash(GameObject root, JusticeTrailPreset p, Material slashMaterial = null)
        {
            if (root == null || p == null) return;
            var g = p.slash;

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                bool isFragment = ps.gameObject.name == "Fragments";

                var main = ps.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f);   // 이동은 앵커가 전담 (파편은 아래에서 덮음)
                main.maxParticles = g.maxParticles;
                main.gravityModifier = 0f;                              // 방사광 — 무중력
                main.simulationSpace = ParticleSystemSimulationSpace.Local;   // 앵커에 실려 함께 주행

                var em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new ParticleSystem.Burst[0]);

                var shape = ps.shape;
                shape.enabled = true;

                if (isFragment)
                {
                    // 파편 — 궤도 주위 원판(xz)에 불규칙하게 태어나고,
                    // Circle 방출의 기본 방향이 반경 바깥이라 startSpeed가 곧 이탈 속도가 된다.
                    main.startLifetime = new ParticleSystem.MinMaxCurve(g.fragmentLifeMin, Mathf.Max(g.fragmentLifeMin, g.fragmentLifeMax));
                    main.startSize = new ParticleSystem.MinMaxCurve(g.fragmentSizeMin, Mathf.Max(g.fragmentSizeMin, g.fragmentSizeMax));
                    main.startSpeed = new ParticleSystem.MinMaxCurve(g.fragmentDriftMin, Mathf.Max(g.fragmentDriftMin, g.fragmentDriftMax));

                    bool fragOn = g.enabled && g.fragmentRate > 0.001f;
                    em.enabled = fragOn;
                    em.rateOverDistance = fragOn ? g.fragmentRate : 0f;

                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = Mathf.Max(g.fragmentSpread, 0.001f);
                    shape.radiusThickness = 1f;                          // 원판 전체에 흩어짐 = 불규칙한 간격
                    shape.arc = 360f;
                    shape.rotation = new Vector3(90f, 0f, 0f);           // 원판을 xz(궤도 평면)에 눕힘
                    shape.randomDirectionAmount = 0f;
                }
                else
                {
                    // 본 획 — 앵커 주위로 흩어져 태어난다 → 반경이 제각각인 평행 획들.
                    main.startLifetime = new ParticleSystem.MinMaxCurve(g.lifeMin, Mathf.Max(g.lifeMin, g.lifeMax));
                    main.startSize = new ParticleSystem.MinMaxCurve(g.sizeMin, Mathf.Max(g.sizeMin, g.sizeMax));

                    em.enabled = g.enabled;
                    em.rateOverDistance = g.enabled ? g.rateOverDistance : 0f;   // 주행이 곧 방출

                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = Mathf.Max(g.nozzleRadius, 0.001f);
                    shape.randomDirectionAmount = 0f;
                }

                var col = ps.colorOverLifetime;
                col.enabled = true;
                // 본체 색만 — 하이라이트는 획 중심선 코어(StrokeCore 셰이더)가 그린다.
                col.color = new ParticleSystem.MinMaxGradient(
                    BuildCore(g.headColor, g.midColor, g.tailColor,
                              Color.white, 0f, 0f, g.emission, g.chromaHold, g.alphaHold));

                // 그림은 트레일이 그린다. worldSpace라 앵커가 지나간 자리에 획이 남는다.
                var tm = ps.trails;
                tm.enabled = true;
                tm.mode = ParticleSystemTrailMode.PerParticle;
                tm.ratio = 1f;
                tm.lifetime = new ParticleSystem.MinMaxCurve(1f);   // 입자 수명에 대한 배율 — 1 고정, 잔상은 수명 Min/Max로
                tm.minVertexDistance = g.trailMinVertexDistance;
                tm.worldSpace = true;
                tm.dieWithParticles = false;
                tm.sizeAffectsWidth = true;
                tm.widthOverTrail = new ParticleSystem.MinMaxCurve(1f);   // 길이 정규화 곡선은 짧은 트레일에서 글리치 — 테이퍼는 수명 기반으로
                tm.inheritParticleColor = true;
                tm.textureMode = ParticleSystemTrailTextureMode.Stretch;

                ApplyStrokeTaper(ps, g.endFade);   // 양끝 테이퍼(수명 기반) — 뭉툭한 시작·번짐·가로 글리치 차단

                var rend = ps.GetComponent<ParticleSystemRenderer>();
                if (rend != null && slashMaterial != null)
                {
                    // ★입자 빌보드는 끈다 — 입자는 앵커일 뿐, 그림은 트레일이 전부다(궤적과 동일 결함 예방).
                    rend.renderMode = ParticleSystemRenderMode.None;
                    rend.trailMaterial = slashMaterial;
                }
                PushCoreParams(slashMaterial, g.coreColor, g.coreEmission, g.coreWidth, g.coreSharpness, g.bodyFalloff, g.endFade);
            }

            var fx = root.GetComponent<Seam.JcArcSweepEffect>();
            if (fx != null)
            {
                fx.AngleStart = g.angleStart;
                fx.AngleEnd = g.angleEnd;
                fx.Radius = g.radius;
                fx.SweepDuration = g.sweepDuration;
                fx.EaseOut = g.easeOut;
                fx.SpawnOffset = g.spawnOffset;
                fx.FadeOutExtraSeconds = g.fadeOutExtraSeconds;
                // 말단 쐐기 방지 — 폭 성장 시간(endFade × 가장 긴 수명)만큼 방출을 미리 끊는다.
                fx.EmitTailCutoffSeconds = g.endFade * Mathf.Max(g.lifeMax, g.fragmentLifeMax);
            }
        }

        /// <summary>
        /// 용권풍 — 원형으로 뿌린 입자를 수직축 주위로 공전·상승시키고, 그림은 per-particle 트레일이 그린다.
        ///
        /// 반경 변화는 velocityOverLifetime의 radial(바깥으로 미는 속도)로 만든다.
        /// 상승 속도와 수명이 정해지면 도달 높이가 정해지므로, 원하는 높이를 역산해 linear.y를 넣는다.
        ///     상승속도 = 높이 / 평균수명
        ///     확장속도 = (끝반경 - 시작반경) / 평균수명
        /// </summary>
        public static void ApplyVortexGroup(ParticleSystem ps, JusticeTrailPreset.VortexGroup g)
        {
            if (ps == null || g == null) return;

            float lifeMax = Mathf.Max(g.lifeMin, g.lifeMax);
            float lifeAvg = Mathf.Max(0.01f, (g.lifeMin + lifeMax) * 0.5f);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(g.lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);   // 방향은 전부 velocityOverLifetime이 준다
            main.startSize = new ParticleSystem.MinMaxCurve(g.sizeMin, Mathf.Max(g.sizeMin, g.sizeMax));
            main.maxParticles = g.maxParticles;
            main.gravityModifier = 0f;                              // 방사광이라 무중력
            main.simulationSpace = ParticleSystemSimulationSpace.Local;   // 회오리가 통째로 전진해야 한다

            var em = ps.emission;
            em.enabled = g.enabled;
            em.rateOverTime = g.enabled ? g.rateOverTime : 0f;
            em.rateOverDistance = 0f;

            // 바닥 링에서 태어난다. radiusThickness=0이면 원 둘레에서만 나온다.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = g.radiusStart;
            shape.radiusThickness = 0f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.rotation = new Vector3(90f, 0f, 0f);   // 원을 수평으로 눕힌다(기본은 XY 평면)
            shape.randomDirectionAmount = 0f;

            // 공전 + 상승 + 확장
            float rise = g.height / lifeAvg;
            float expand = (g.radiusEnd - g.radiusStart) / lifeAvg;
            float jitter = Mathf.Clamp01(g.orbitJitter);

            // ★Unity 제약: x/y/z 세 축, orbital 세 축은 각각 "같은 커브 모드"여야 한다.
            // 처음에 y만 Curve, orbitalY만 TwoConstants로 섞었다가 매 프레임
            // "Particle Velocity curves must all be in the same mode" 에러가 폭주했다(A키 사고).
            // 한 축이라도 Curve/TwoConstants를 쓰면 나머지 축도 같은 모드의 0으로 맞춘다.
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;

            // 공전 3축 — 전부 TwoConstants로 통일(지터 없으면 min=max).
            float orbMin = g.orbitSpeed * (1f - jitter);
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(orbMin, g.orbitSpeed);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalOffsetX = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalOffsetY = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalOffsetZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.radial = new ParticleSystem.MinMaxCurve(expand, expand);   // orbital 그룹과 같은 모드 유지

            // 선형 3축 — 상승 프로파일이 곡선이므로 전부 Curve 모드로 통일.
            var flat = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
            var riseProfile = Mathf.Abs(g.radiusCurve - 1f) < 0.01f
                ? new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f))
                : RiseCurve(g.radiusCurve);
            vel.x = new ParticleSystem.MinMaxCurve(1f, flat);
            vel.y = new ParticleSystem.MinMaxCurve(rise, riseProfile);
            vel.z = new ParticleSystem.MinMaxCurve(1f, flat);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                BuildCore(g.headColor, g.midColor, g.tailColor,
                          g.highlightColor, g.highlightRatio, g.highlightEmission,
                          g.emission, g.chromaHold, g.alphaHold));

            // 그림은 트레일이 그린다. worldSpace라 회오리가 전진하면 줄기가 지나온 자리에 남는다.
            var tm = ps.trails;
            tm.enabled = true;
            tm.mode = ParticleSystemTrailMode.PerParticle;
            tm.ratio = 1f;
            tm.lifetime = new ParticleSystem.MinMaxCurve(1f);   // 입자 수명에 대한 배율 — 1 고정, 잔상은 수명 Min/Max로
            tm.minVertexDistance = g.trailMinVertexDistance;
            tm.worldSpace = true;
            tm.dieWithParticles = false;
            tm.sizeAffectsWidth = true;
            tm.inheritParticleColor = true;
            tm.textureMode = ParticleSystemTrailTextureMode.Stretch;
        }

        /// <summary>
        /// 상승 프로파일. curve &gt; 1이면 초반에 빠르게 올랐다 느려져 위쪽이 촘촘해지고(=벌어짐이 두드러짐),
        /// curve &lt; 1이면 반대가 된다.
        /// </summary>
        static AnimationCurve RiseCurve(float curve)
        {
            var c = new AnimationCurve();
            const int N = 6;
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                c.AddKey(t, Mathf.Pow(1f - t, Mathf.Max(0.2f, curve - 1f) * 0.8f));
            }
            return c;
        }

        /// <summary>용권풍 프리팹 루트에 프리셋 반영. 이동·수명은 컴포넌트가, 형상은 파티클이 맡는다.</summary>
        public static void ApplyVortex(GameObject root, JusticeTrailPreset p, Material vortexMaterial = null)
        {
            if (root == null || p == null) return;
            var g = p.vortex;

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ApplyVortexGroup(ps, g);
                if (vortexMaterial == null) continue;
                var rend = ps.GetComponent<ParticleSystemRenderer>();
                if (rend != null) rend.trailMaterial = vortexMaterial;
            }

            var fx = root.GetComponent<Seam.JcVortexEffect>();
            if (fx == null) return;
            fx.SpawnOffset = g.spawnOffset;
            fx.TravelSpeed = g.travelSpeed;
            fx.TravelDistance = g.travelDistance;
            fx.TravelDelay = g.travelDelay;
            fx.Duration = g.duration;
            fx.FadeOutExtraSeconds = g.fadeOutExtraSeconds;
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
        public static void ApplyTrail(GameObject root, JusticeTrailPreset p, Material sparkMaterial = null,
                                      Material strokeMaterial = null)
        {
            if (root == null || p == null) return;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.gameObject.name == TrailName) ApplyTrailGroup(ps, p.trail, strokeMaterial);
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
        public enum Kind { Trail, Impact, Vortex, Slash, ArcStroke, ArcPoint }

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

        [Tooltip("용권풍 나선 줄기용 재질.")]
        [SerializeField] private Material vortexMaterial;

        [Tooltip("[레거시] 구 호 참격용 재질.")]
        [SerializeField] private Material slashMaterial;

        [Tooltip("호 획용 재질(Testbed/Justice/StrokeCore).")]
        [SerializeField] private Material arcStrokeMaterial;

        [Tooltip("호 포인트 획용 재질 — 본 획과 분리해 단면 감쇠를 독립 조절한다.")]
        [SerializeField] private Material arcPointMaterial;

        public JusticeTrailPreset Preset { get => preset; set => preset = value; }
        public JusticeTrailPreset PresetAlt { get => presetAlt; set => presetAlt = value; }

        /// <summary>지금 실제로 쓰이는 프리셋. 에디터 라이브 반영이 대상을 가려내는 데도 쓴다.</summary>
        [Tooltip("★켜면 이 인스턴스는 무조건 알트(강화·적색) 프리셋을 쓴다.\n" +
                 "UseAlternate는 프리뷰 전용 전역 스위치라 실전투에서는 아무도 켜 주지 않고, static이라\n" +
                 "유닛마다 다른 값을 가질 수도 없다. 강화 스킬(HS1011 등)이 독립 스킬이 된 이상\n" +
                 "프리팹 자체가 「나는 강화판이다」를 들고 있어야 한다 — 그 스위치가 이 필드다.")]
        [SerializeField] private bool forceAlternate;

        public bool ForceAlternate { get => forceAlternate; set => forceAlternate = value; }

        public JusticeTrailPreset ActivePreset =>
            ((forceAlternate || UseAlternate) && presetAlt != null) ? presetAlt : preset;

        private void Awake() => ApplyNow();

        public void ApplyNow() => Apply(ActivePreset);

        /// <summary>지정한 프리셋으로 반영한다. 에디터가 특정 변종을 강제 반영할 때도 쓴다.</summary>
        public void Apply(JusticeTrailPreset p)
        {
            if (p == null) return;

            switch (kind)
            {
                case Kind.Trail:  JusticeTrailPresetRuntime.ApplyTrail(gameObject, p, sparkMaterial, strokeMaterial); break;
                case Kind.Impact: JusticeTrailPresetRuntime.ApplyImpact(gameObject, p, flashMaterial); break;
                case Kind.Vortex: JusticeTrailPresetRuntime.ApplyVortex(gameObject, p, vortexMaterial); break;
                case Kind.Slash:  JusticeTrailPresetRuntime.ApplySlash(gameObject, p, slashMaterial); break;
                case Kind.ArcStroke: JusticeTrailPresetRuntime.ApplyArcStroke(gameObject, p, arcStrokeMaterial); break;
                case Kind.ArcPoint: JusticeTrailPresetRuntime.ApplyArcPoint(gameObject, p, arcPointMaterial); break;
            }

            if (!applyMaterials) return;

            // 자기 종류의 재질만 만진다.
            // 예전엔 타격 프리팹도 strokeMaterial을 들고 있어, 스폰할 때마다 궤적 재질을 덮어썼다.
            if (kind == Kind.Trail) JusticeTrailPresetRuntime.ApplyMaterials(p, strokeMaterial, null);
            else if (kind == Kind.Impact) JusticeTrailPresetRuntime.ApplyMaterials(p, null, impactMaterial);
        }
    }
}
