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
        // ★대상 자산은 프리셋이 들고 있다(JusticeTrailPreset.targets).
        // 예전엔 여기에 등장! 경로를 상수로 박아 두어, 펀치 프리셋에서 적용/캡처를 누르면
        // 등장! 프리팹·재질에 쓰이는 버그가 있었다. 변종이 늘면 걷잡을 수 없어 데이터로 옮겼다.
        const string RestartKey = "JC.JusticeTrailPreset.RestartOnPush";

        protected override string LiveKey => "JC.JusticeTrailPreset.LivePreview";
        protected override string HelpText =>
            "실시간 반영: 슬라이더를 움직이면 (1) 살아 있는 인스턴스에 즉시 반영되고 " +
            "(2) 프리팹의 Binder가 스폰마다 프리셋을 읽으므로 '적용' 없이도 다음 재생에 반영된다.\n" +
            "파티클은 이미 방출된 입자에 소급되지 않으므로, '변경 시 파티클 재시작'이 켜져 있으면 즉시 새 값으로 다시 뿜는다.\n" +
            "궤적(PenStrokes)·입자(SparkDots)·타격은 색·발광·하이라이트를 전부 따로 가진다. 공유 항목은 없다.\n" +
            "적용: 현재 값을 프리팹·재질에 확정 기록(스냅샷). 캡처: 프리팹 값을 프리셋으로 역방향 읽기.";

        public override void OnInspectorGUI()
        {
            bool restart = EditorPrefs.GetBool(RestartKey, true);
            bool newRestart = EditorGUILayout.ToggleLeft("변경 시 파티클 재시작 (즉시 새 값으로 다시 뿜음)", restart);
            if (newRestart != restart) EditorPrefs.SetBool(RestartKey, newRestart);
            base.OnInspectorGUI();

            EditorGUILayout.Space(2);
            JcPresetEditorUtil.DrawSaveButton(target, wide: true);   // 튜닝 저장 공식 규격 — 프리셋 파일 확정은 명시 클릭만
        }

        /// <summary>
        /// ★대상 자산이 비어 있는 섹션은 숨긴다.
        /// 이 프리셋 하나가 궤적·타격·호 획·포인트 획을 겸하는데, 스킬마다 쓰는 조합이 다르다
        /// (용권풍은 호 획+포인트만 쓴다). 안 쓰는 섹션까지 다 펼쳐 두면 어느 값이 화면에 영향을 주는지
        /// 구분이 안 되고, 무관한 값을 만지다 헛돌기 쉽다.
        /// 판정 기준은 targets — 「적용」이 기록할 대상이 없으면 그 섹션은 애초에 무의미하다.
        /// </summary>
        protected override void DrawBody()
        {
            var t = ((JusticeTrailPreset)target).targets;
            var skip = new System.Collections.Generic.List<string>();
            if (t.trailPrefab == null) { skip.Add("trail"); skip.Add("spark"); }
            if (t.impactPrefab == null) skip.Add("impact");
            if (t.arcStrokePrefab == null) skip.Add("arcStroke");
            if (t.arcPointPrefab == null) skip.Add("arcPoint");

            serializedObject.Update();
            SerializedProperty it = serializedObject.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(it);
                    continue;
                }
                if (skip.Contains(it.propertyPath)) continue;
                EditorGUILayout.PropertyField(it, true);
            }
            serializedObject.ApplyModifiedProperties();

            if (skip.Count > 0)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(
                    "대상 자산이 없어 숨긴 섹션: " + string.Join(", ", skip) +
                    "\ntargets에 해당 프리팹을 넣으면 다시 나타난다.", MessageType.None);
            }
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

                // 프리셋이 여러 개(스킬 × 색 변종)라 특정 경로를 고를 수 없다.
                // 살아 있는 바인더가 실제로 참조하는 프리셋만 다시 밀어 넣는다.
                EditorApplication.delayCall += () =>
                {
                    foreach (var b in Object.FindObjectsByType<JusticeTrailPresetBinder>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (b.ActivePreset != null) b.ApplyNow();
                    }
                };
            };
        }

        /// <summary>
        /// 살아 있는 인스턴스에 즉시 반영.
        /// **이 프리셋을 참조하는 바인더에만** 적용한다 — 예전엔 씬의 모든 이펙트에 무차별로 밀어 넣어,
        /// 펀치 프리셋을 만지면 등장! 인스턴스의 재질까지 갈아치웠다.
        /// </summary>
        static void LivePushStatic(JusticeTrailPreset p)
        {
            bool restart = EditorPrefs.GetBool(RestartKey, true);

            foreach (var binder in Object.FindObjectsByType<JusticeTrailPresetBinder>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (binder.Preset != p && binder.PresetAlt != p) continue;

                binder.Apply(p);
                if (!restart) continue;
                foreach (var ps in binder.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!ps.isPlaying) continue;
                    ps.Clear(true);
                    ps.Play(true);
                }
            }

            // 재질 전역 쓰기는 하지 않는다 — 라이브 반영은 위 binder.Apply(MPB 경로)가 인스턴스별로 처리.
            // 재질 에셋 기록은 「적용」 버튼(WriteSharedMaterials=true) 전용.

            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        /// <summary>대상 프리팹의 에셋 경로. 비어 있으면 빈 문자열.</summary>
        static string PathOf(Object o) => o == null ? "" : AssetDatabase.GetAssetPath(o);

        public static void Apply(JusticeTrailPreset p)
        {
            // ★굽기 모드 — 이 블록 안에서만 공유 재질 에셋에 기록된다(평시 스폰·라이브는 MPB 비파괴).
            JusticeTrailPresetRuntime.WriteSharedMaterials = true;
            try
            {
                ApplyBake(p);
            }
            finally
            {
                JusticeTrailPresetRuntime.WriteSharedMaterials = false;
            }
        }

        static void ApplyBake(JusticeTrailPreset p)
        {
            var t = p.targets;
            JusticeTrailPresetRuntime.ApplyMaterials(null, p, t.strokeMaterial, t.impactSparkMaterial);
            if (t.strokeMaterial != null) EditorUtility.SetDirty(t.strokeMaterial);
            if (t.impactSparkMaterial != null) EditorUtility.SetDirty(t.impactSparkMaterial);

            string trailPath = PathOf(t.trailPrefab);
            if (string.IsNullOrEmpty(trailPath))
            {
                Debug.LogWarning("[JusticeTrailPreset] " + p.name + ": 대상 궤적 프리팹이 비어 있어 건너뜁니다.", p);
            }
            else
            {
                var trail = PrefabUtility.LoadPrefabContents(trailPath);
                try
                {
                    JusticeTrailPresetRuntime.ApplyTrail(trail, p, t.sparkMaterial, t.strokeMaterial);
                    var fx = trail.GetComponent<JcSocketTrailEffect>();
                    if (fx != null)
                    {
                        var so = new SerializedObject(fx);
                        so.FindProperty("fadeOutExtraSeconds").floatValue = p.fadeOutExtraSeconds;
                        so.FindProperty("socketOffset").vector3Value = p.socketOffset;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(trail, trailPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(trail); }
            }

            string arcPath = PathOf(t.arcStrokePrefab);
            if (!string.IsNullOrEmpty(arcPath))
            {
                var arc = PrefabUtility.LoadPrefabContents(arcPath);
                try
                {
                    JusticeTrailPresetRuntime.ApplyArcStroke(arc, p, t.arcStrokeMaterial);
                    PrefabUtility.SaveAsPrefabAsset(arc, arcPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(arc); }
                if (t.arcStrokeMaterial != null) EditorUtility.SetDirty(t.arcStrokeMaterial);
            }

            string pointPath = PathOf(t.arcPointPrefab);
            if (!string.IsNullOrEmpty(pointPath))
            {
                var point = PrefabUtility.LoadPrefabContents(pointPath);
                try
                {
                    JusticeTrailPresetRuntime.ApplyArcPoint(point, p, t.arcPointMaterial);
                    PrefabUtility.SaveAsPrefabAsset(point, pointPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(point); }
                if (t.arcPointMaterial != null) EditorUtility.SetDirty(t.arcPointMaterial);
            }

            // [레거시] 호 참격 V1(slashPrefab) 기록은 1차 완성(260729)과 함께 중단 — 보존 자산은 더 건드리지 않는다.

            string impactPath = PathOf(t.impactPrefab);
            if (string.IsNullOrEmpty(impactPath))
            {
                // 대쉬처럼 타격 스파크 대신 호 획을 쓰는 프리셋은 비어 있는 게 정상이다.
                if (string.IsNullOrEmpty(arcPath))
                    Debug.LogWarning("[JusticeTrailPreset] " + p.name + ": 대상 타격 프리팹이 비어 있어 건너뜁니다.", p);
            }
            else
            {
                var impact = PrefabUtility.LoadPrefabContents(impactPath);
                try
                {
                    JusticeTrailPresetRuntime.ApplyImpact(impact, p, t.flashMaterial);
                    PrefabUtility.SaveAsPrefabAsset(impact, impactPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(impact); }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[JusticeTrailPreset] " + p.name + " → 프리팹·재질에 확정 기록 완료", p);
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
            // 중력은 입자 계열 전용이라 공통 캡처 대상이 아니다(호출부에서 따로 읽는다).

            var em = ps.emission;
            g.enabled = em.enabled;
            g.rateOverTime = em.rateOverTime.constant;
            g.rateOverDistance = em.rateOverDistance.constant;
        }

        public static void Capture(JusticeTrailPreset p)
        {
            Undo.RecordObject(p, "Capture Justice Trail");

            var im = p.targets.impactSparkMaterial;
            if (im != null)
            {
                if (im.HasProperty("_Tint")) p.impact.tint = im.GetColor("_Tint");
                if (im.HasProperty("_Emission")) p.impact.emission = im.GetFloat("_Emission");
            }

            var trail = p.targets.trailPrefab;
            if (trail != null)
            {
                var trailT = trail.transform.Find(JusticeTrailPresetRuntime.TrailName);
                var sparkT = trail.transform.Find(JusticeTrailPresetRuntime.SparkName);

                var tps = trailT != null ? trailT.GetComponent<ParticleSystem>() : null;
                if (tps != null)
                {
                    CaptureCommon(tps, p.trail);
                    p.trail.shapeRadius = tps.shape.radius;
                    // 트레일 배율은 1 고정 정책이라 캡처하지 않는다.
                    p.trail.trailMinVertexDistance = tps.trails.minVertexDistance;
                }

                var sps = sparkT != null ? sparkT.GetComponent<ParticleSystem>() : null;
                if (sps != null)
                {
                    CaptureCommon(sps, p.spark);
                    p.spark.gravity = sps.main.gravityModifier.constant;   // 중력을 갖는 유일한 계열
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

            var impact = p.targets.impactPrefab;
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
                    // 프리팹의 파티클 파라미터 → 화면상 치수로 되돌린다(바인더 역산의 역방향).
                    float wMax = main.startSize.constantMax;
                    p.impact.width = wMax;
                    p.impact.widthVariation = wMax > 0.0001f ? 1f - main.startSize.constantMin / wMax : 0f;
                    p.impact.enabled = ps.emission.enabled;

                    var bursts = new ParticleSystem.Burst[ps.emission.burstCount];
                    ps.emission.GetBursts(bursts);
                    if (bursts.Length > 0 && bursts[0].count.constant > 0)
                        p.impact.burstCount = (int)bursts[0].count.constant;

                    var rend = ps.GetComponent<ParticleSystemRenderer>();
                    if (rend != null)
                    {
                        p.impact.length = wMax * rend.lengthScale;
                        p.impact.velocityScale = rend.velocityScale;
                    }
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
            Debug.Log("[JusticeTrailPreset] " + p.name + " ← 현재값 캡처 완료", p);
        }
    }
}
