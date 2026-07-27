using UnityEngine;
using UnityEditor;
using JC.VFX.Seam;

namespace JC.VFX
{
    /// <summary>
    /// 저스티스 주먹 연출 프리셋 에디터. 공통 골격은 FlareOrbPresetEditorBase 참조.
    ///
    /// 대상이 ParticleSystem 모듈이라 MPB가 통하지 않으므로 실시간 반영을 두 겹으로 만든다.
    ///   1) 런타임 — 프리팹의 JusticeTrailPresetBinder가 스폰마다 프리셋을 읽는다.
    ///   2) 에디터 — 살아 있는 인스턴스에 즉시 반영하고, 이미 방출된 입자 때문에 변화가 안 보이는 것을
    ///      파티클 재시작으로 보완한다.
    /// 반영 로직은 JusticeTrailPresetRuntime에 두어 런타임/에디터가 같은 코드를 쓴다.
    /// </summary>
    [CustomEditor(typeof(JusticeTrailPreset))]
    public class JusticeTrailPresetEditor : FlareOrbPresetEditorBase
    {
        const string JDIR = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/J_Justice";
        static string TrailPrefab  => JDIR + "/JC_JusticeFistTrail.prefab";
        static string ImpactPrefab => JDIR + "/JC_JusticeImpactBurst.prefab";
        static string StrokeMat    => JDIR + "/JusticePenStroke.mat";
        static string ImpactMat    => JDIR + "/JusticeImpactSpark.mat";
        static string SparkMat     => JDIR + "/JusticeSparkDot.mat";
        static string FlashMat     => JDIR + "/JusticeImpactFlash.mat";
        const string PresetPath    = JDIR + "/FX_JusticeTrailPreset.asset";
        const string RestartKey    = "JC.JusticeTrailPreset.RestartOnPush";

        protected override string LiveKey => "JC.JusticeTrailPreset.LivePreview";
        protected override string HelpText =>
            "실시간 반영: 슬라이더를 움직이면 (1) 살아 있는 인스턴스에 즉시 반영되고 " +
            "(2) 프리팹의 Binder가 스폰마다 프리셋을 읽으므로 '적용' 없이도 다음 재생에 반영된다.\n" +
            "파티클은 이미 방출된 입자에 소급되지 않으므로, '변경 시 파티클 재시작'이 켜져 있으면 즉시 새 값으로 다시 뿜는다.\n" +
            "궤적(PenStrokes)·입자(SparkDots)·타격은 각자 발광과 하이라이트를 따로 가진다. 본체 팔레트만 공유.\n" +
            "적용: 현재 값을 프리팹·재질에 확정 기록(스냅샷). 캡처: 프리팹 값을 프리셋으로 역방향 읽기.";

        public override void OnInspectorGUI()
        {
            bool restart = EditorPrefs.GetBool(RestartKey, true);
            bool newRestart = EditorGUILayout.ToggleLeft("변경 시 파티클 재시작 (즉시 새 값으로 다시 뿜음)", restart);
            if (newRestart != restart) EditorPrefs.SetBool(RestartKey, newRestart);
            base.OnInspectorGUI();
        }

        protected override void LivePush() => LivePushStatic((JusticeTrailPreset)target);
        protected override void ApplyPreset() => Apply((JusticeTrailPreset)target);
        protected override void CapturePreset() => Capture((JusticeTrailPreset)target);

        [InitializeOnLoadMethod]
        static void HookPlayModeRepush()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode) return;
                if (!EditorPrefs.GetBool("JC.JusticeTrailPreset.LivePreview", true)) return;
                var p = AssetDatabase.LoadAssetAtPath<JusticeTrailPreset>(PresetPath);
                if (p != null) EditorApplication.delayCall += () => LivePushStatic(p);
            };
        }

        static void LivePushStatic(JusticeTrailPreset p)
        {
            bool restart = EditorPrefs.GetBool(RestartKey, true);

            foreach (var fx in Object.FindObjectsByType<JcSocketTrailEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                JusticeTrailPresetRuntime.ApplyTrail(fx.gameObject, p, Load<Material>(SparkMat));
                if (!restart) continue;
                foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!ps.isPlaying) continue;
                    ps.Clear(true);
                    ps.Play(true);
                }
            }

            foreach (var binder in Object.FindObjectsByType<JusticeTrailPresetBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (binder.Preset == null) binder.Preset = p;
                binder.ApplyNow();
            }

            JusticeTrailPresetRuntime.ApplyMaterials(p, Load<Material>(StrokeMat), Load<Material>(ImpactMat));

            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void Apply(JusticeTrailPreset p)
        {
            var sm = Load<Material>(StrokeMat);
            var im = Load<Material>(ImpactMat);
            JusticeTrailPresetRuntime.ApplyMaterials(p, sm, im);
            if (sm != null) EditorUtility.SetDirty(sm);
            if (im != null) EditorUtility.SetDirty(im);

            var trail = PrefabUtility.LoadPrefabContents(TrailPrefab);
            try
            {
                JusticeTrailPresetRuntime.ApplyTrail(trail, p, Load<Material>(SparkMat));
                var fx = trail.GetComponent<JcSocketTrailEffect>();
                if (fx != null)
                {
                    var so = new SerializedObject(fx);
                    so.FindProperty("fadeOutExtraSeconds").floatValue = p.fadeOutExtraSeconds;
                    so.FindProperty("socketOffset").vector3Value = p.socketOffset;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(trail, TrailPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(trail); }

            var impact = PrefabUtility.LoadPrefabContents(ImpactPrefab);
            try
            {
                JusticeTrailPresetRuntime.ApplyImpact(impact, p, Load<Material>(FlashMat));
                PrefabUtility.SaveAsPrefabAsset(impact, ImpactPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(impact); }

            AssetDatabase.SaveAssets();
            Debug.Log("[JusticeTrailPreset] 프리팹·재질에 확정 기록 완료");
        }

        /// <summary>방출·수명·크기 등 공통 항목 역방향 읽기. 발광·하이라이트·코어는 프리셋 전용이라 캡처 대상이 아니다.</summary>
        static void CaptureCommon(ParticleSystem ps, JusticeTrailPreset.EmitGroupBase g)
        {
            if (ps == null) return;
            var main = ps.main;
            g.lifeMin = main.startLifetime.constantMin;
            g.lifeMax = main.startLifetime.constantMax;
            g.speedMin = main.startSpeed.constantMin;
            g.speedMax = main.startSpeed.constantMax;
            g.sizeMin = main.startSize.constantMin;
            g.sizeMax = main.startSize.constantMax;
            g.maxParticles = main.maxParticles;
            g.gravity = main.gravityModifier.constant;

            var em = ps.emission;
            g.enabled = em.enabled;
            g.rateOverTime = em.rateOverTime.constant;
            g.rateOverDistance = em.rateOverDistance.constant;
        }

        public static void Capture(JusticeTrailPreset p)
        {
            Undo.RecordObject(p, "Capture Justice Trail");

            var im = Load<Material>(ImpactMat);
            if (im != null)
            {
                if (im.HasProperty("_Tint")) p.impact.tint = im.GetColor("_Tint");
                if (im.HasProperty("_Emission")) p.impact.emission = im.GetFloat("_Emission");
            }

            var trail = Load<GameObject>(TrailPrefab);
            if (trail != null)
            {
                var trailT = trail.transform.Find(JusticeTrailPresetRuntime.TrailName);
                var sparkT = trail.transform.Find(JusticeTrailPresetRuntime.SparkName);

                var tps = trailT != null ? trailT.GetComponent<ParticleSystem>() : null;
                if (tps != null)
                {
                    CaptureCommon(tps, p.trail);
                    p.trail.shapeRadius = tps.shape.radius;
                    var tm = tps.trails;
                    p.trail.trailLifetime = tm.lifetime.constant;
                    p.trail.trailMinVertexDistance = tm.minVertexDistance;
                }

                var sps = sparkT != null ? sparkT.GetComponent<ParticleSystem>() : null;
                if (sps != null)
                {
                    CaptureCommon(sps, p.spark);
                    p.spark.spreadAngle = sps.shape.angle;
                    p.spark.nozzleRadius = sps.shape.radius;
                    p.spark.drag = sps.limitVelocityOverLifetime.enabled ? sps.limitVelocityOverLifetime.drag.constant : 0f;
                    p.spark.sizeEndScale = sps.sizeOverLifetime.enabled ? sps.sizeOverLifetime.size.curve.Evaluate(1f) : 1f;
                }

                var fx = trail.GetComponent<JcSocketTrailEffect>();
                if (fx != null)
                {
                    var so = new SerializedObject(fx);
                    p.fadeOutExtraSeconds = so.FindProperty("fadeOutExtraSeconds").floatValue;
                    p.socketOffset = so.FindProperty("socketOffset").vector3Value;
                }
            }

            var impact = Load<GameObject>(ImpactPrefab);
            if (impact != null)
            {
                var ps = impact.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    p.impact.lifeMin = main.startLifetime.constantMin;
                    p.impact.lifeMax = main.startLifetime.constantMax;
                    p.impact.speedMin = main.startSpeed.constantMin;
                    p.impact.speedMax = main.startSpeed.constantMax;
                    p.impact.sizeMin = main.startSize.constantMin;
                    p.impact.sizeMax = main.startSize.constantMax;
                    p.impact.gravity = main.gravityModifier.constant;
                    p.impact.enabled = ps.emission.enabled;

                    var bursts = new ParticleSystem.Burst[ps.emission.burstCount];
                    ps.emission.GetBursts(bursts);
                    if (bursts.Length > 0 && bursts[0].count.constant > 0)
                        p.impact.burstCount = (int)bursts[0].count.constant;

                    var rend = ps.GetComponent<ParticleSystemRenderer>();
                    if (rend != null) p.impact.lengthScale = rend.lengthScale;
                }

                var flashT = impact.transform.Find(JusticeTrailPresetRuntime.FlashName);
                var fps = flashT != null ? flashT.GetComponent<ParticleSystem>() : null;
                if (fps != null)
                {
                    var fm = fps.main;
                    if (fm.startSize3D)
                    {
                        p.impact.flashLength = fm.startSizeX.constant;
                        p.impact.flashWidth = fm.startSizeY.constant;
                    }

                    p.impact.flashLifetime = fm.startLifetime.constant;
                    p.impact.flashEnabled = fps.emission.enabled;
                }
            }

            EditorUtility.SetDirty(p);
            Debug.Log("[JusticeTrailPreset] 현재값 캡처 완료");
        }
    }
}
