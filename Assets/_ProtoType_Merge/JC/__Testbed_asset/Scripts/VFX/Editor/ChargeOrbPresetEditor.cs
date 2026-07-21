using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(ChargeOrbPreset))]
    public class ChargeOrbPresetEditor : Editor
    {
        const string DIR = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/HealSkill";
        static string MatCore    => DIR + "/ChargeOrbCore.mat";
        static string MatSpark   => DIR + "/SparkAdditive.mat";
        static string MatStreak  => DIR + "/StreakAdditive.mat";
        static string MatSparkle => DIR + "/SparkleStarAdditive.mat";
        static string OrbPrefab  => DIR + "/ChargeOrb.prefab";
        static string FollowerPrefab => DIR + "/ChargeTrailFollower.prefab";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (ChargeOrbPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 값을 재질/오브/팔로워 프리팹에 일괄 반영.\n캡처: 현재 프리팹/재질 값을 이 프리셋으로 역방향 읽기.\n적용 후 새로 재생하면 반영됩니다.", MessageType.Info);
        }

        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        void Apply(ChargeOrbPreset p)
        {
            // --- 재질 ---
            var core = Load<Material>(MatCore);
            core.SetColor("_CoreColor", p.coreColor); core.SetColor("_RimColor", p.rimColor);
            core.SetFloat("_CoreIntensity", p.coreIntensity); core.SetFloat("_RimIntensity", p.rimIntensity);
            core.SetFloat("_CorePower", p.corePower); core.SetFloat("_RimPower", p.rimPower); core.SetFloat("_GapStrength", p.gapStrength);
            core.SetFloat("_CoreSpikeAmount", p.coreSpikeAmount); core.SetFloat("_RimSpikeAmount", p.rimSpikeAmount);
            core.SetFloat("_SpikeFreq", p.spikeFreq); core.SetFloat("_SpikeSpeed", p.spikeSpeed); core.SetFloat("_SpikeSharp", p.spikeSharp);
            EditorUtility.SetDirty(core);
            var sparkMat = Load<Material>(MatSpark); if (sparkMat) { sparkMat.SetColor("_BaseColor", p.sparkColor); EditorUtility.SetDirty(sparkMat); }
            var streakMat = Load<Material>(MatStreak); if (streakMat) { streakMat.SetColor("_BaseColor", p.bandColor); EditorUtility.SetDirty(streakMat); }
            var sparkleMat = Load<Material>(MatSparkle); if (sparkleMat) { sparkleMat.SetColor("_BaseColor", p.sparkleColor); EditorUtility.SetDirty(sparkleMat); }
            AssetDatabase.SaveAssets();

            // --- 오브 프리팹: ChargeOrbVfx 크기 + Sparks PS ---
            var orb = PrefabUtility.LoadPrefabContents(OrbPrefab);
            var vfx = orb.GetComponent<ChargeOrbVfx>();
            var so = new SerializedObject(vfx);
            so.FindProperty("startWorldSize").floatValue = p.startWorldSize;
            so.FindProperty("endWorldSize").floatValue = p.endWorldSize;
            so.FindProperty("growDuration").floatValue = p.growDuration;
            so.ApplyModifiedPropertiesWithoutUndo();
            var sparks = orb.transform.Find("Sparks").GetComponent<ParticleSystem>();
            var sm = sparks.main; sm.startSize = new ParticleSystem.MinMaxCurve(p.sparkSizeMin, p.sparkSizeMax); sm.startSpeed = p.sparkSpeed;
            var sem = sparks.emission; sem.rateOverTime = p.sparkRate;
            PrefabUtility.SaveAsPrefabAsset(orb, OrbPrefab);
            PrefabUtility.UnloadPrefabContents(orb);

            // --- 팔로워 프리팹: TrailStreaks + Sparkles ---
            var fol = PrefabUtility.LoadPrefabContents(FollowerPrefab);
            var ts = fol.transform.Find("TrailStreaks").GetComponent<ParticleSystem>();
            var tm = ts.main; tm.startLifetime = p.bandStartLifetime; tm.startSize = p.bandStartSize;
            var tem = ts.emission; tem.rateOverTime = p.bandRate;
            var tiv = ts.inheritVelocity; tiv.curve = new ParticleSystem.MinMaxCurve(p.bandInheritVelocity);
            var tsh = ts.shape; tsh.radius = p.bandShapeRadius;
            var ttr = ts.trails; ttr.lifetime = new ParticleSystem.MinMaxCurve(p.bandTrailLifetime);
            var sp = fol.transform.Find("Sparkles").GetComponent<ParticleSystem>();
            var spm = sp.main; spm.startSize = new ParticleSystem.MinMaxCurve(p.sparkleSizeMin, p.sparkleSizeMax);
            spm.startLifetime = p.sparkleLifetime;
            var spe = sp.emission; spe.rateOverTime = p.sparkleRate;
            var spsh = sp.shape; spsh.radius = p.sparkleShapeRadius;
            PrefabUtility.SaveAsPrefabAsset(fol, FollowerPrefab);
            PrefabUtility.UnloadPrefabContents(fol);

            Debug.Log("[ChargeOrbPreset] 프리팹에 적용 완료");
        }

        void Capture(ChargeOrbPreset p)
        {
            Undo.RecordObject(p, "Capture Charge Orb");
            var core = Load<Material>(MatCore);
            p.coreColor = core.GetColor("_CoreColor"); p.rimColor = core.GetColor("_RimColor");
            p.coreIntensity = core.GetFloat("_CoreIntensity"); p.rimIntensity = core.GetFloat("_RimIntensity");
            p.corePower = core.GetFloat("_CorePower"); p.rimPower = core.GetFloat("_RimPower"); p.gapStrength = core.GetFloat("_GapStrength");
            p.coreSpikeAmount = core.GetFloat("_CoreSpikeAmount"); p.rimSpikeAmount = core.GetFloat("_RimSpikeAmount");
            p.spikeFreq = core.GetFloat("_SpikeFreq"); p.spikeSpeed = core.GetFloat("_SpikeSpeed"); p.spikeSharp = core.GetFloat("_SpikeSharp");
            var sparkMat = Load<Material>(MatSpark); if (sparkMat) p.sparkColor = sparkMat.GetColor("_BaseColor");
            var streakMat = Load<Material>(MatStreak); if (streakMat) p.bandColor = streakMat.GetColor("_BaseColor");
            var sparkleMat = Load<Material>(MatSparkle); if (sparkleMat) p.sparkleColor = sparkleMat.GetColor("_BaseColor");

            var orb = Load<GameObject>(OrbPrefab);
            var so = new SerializedObject(orb.GetComponent<ChargeOrbVfx>());
            p.startWorldSize = so.FindProperty("startWorldSize").floatValue;
            p.endWorldSize = so.FindProperty("endWorldSize").floatValue;
            p.growDuration = so.FindProperty("growDuration").floatValue;
            var sparks = orb.transform.Find("Sparks").GetComponent<ParticleSystem>().main;
            p.sparkSizeMin = sparks.startSize.constantMin; p.sparkSizeMax = sparks.startSize.constantMax; p.sparkSpeed = sparks.startSpeed.constant;
            p.sparkRate = orb.transform.Find("Sparks").GetComponent<ParticleSystem>().emission.rateOverTime.constant;

            var fol = Load<GameObject>(FollowerPrefab);
            var ts = fol.transform.Find("TrailStreaks").GetComponent<ParticleSystem>();
            p.bandStartLifetime = ts.main.startLifetime.constant; p.bandStartSize = ts.main.startSize.constant;
            p.bandRate = ts.emission.rateOverTime.constant; p.bandInheritVelocity = ts.inheritVelocity.curve.constant;
            p.bandShapeRadius = ts.shape.radius; p.bandTrailLifetime = ts.trails.lifetime.constant;
            var sp = fol.transform.Find("Sparkles").GetComponent<ParticleSystem>();
            p.sparkleSizeMin = sp.main.startSize.constantMin; p.sparkleSizeMax = sp.main.startSize.constantMax; p.sparkleRate = sp.emission.rateOverTime.constant;
            p.sparkleLifetime = sp.main.startLifetime.constant; p.sparkleShapeRadius = sp.shape.radius;

            EditorUtility.SetDirty(p);
            Debug.Log("[ChargeOrbPreset] 현재값 캡처 완료");
        }
    }
}
