using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(ProjectileOrbPreset))]
    public class ProjectileOrbPresetEditor : Editor
    {
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/Heal";
        const string LFL = "Assets/RenderFX/HeroSkill/Nekoming/LetsFightingLove";
        // ★변종 자동 인식 — 프리셋 이름이 _Silver 로 끝나면 은백 재질·프리팹을 대상으로 잡는다.
        //   (FlareOrb 에디터의 Sfx 패턴. 금 프리셋으로 은백을 덮는 사고 방지)
        string Sfx => target != null && target.name.EndsWith("_Basic") ? "_Basic" : "_Alter";   // 개편 후 전 자산이 접미를 가진다
        // ★소속 분기 — L2(LFL) 프리셋이면 LFL 전용 재질·프리팹만 만진다(힐과 완전 절연, 260807).
        bool Lfl => JcPresetEditorUtil.IsLfl(target);
        string MatCoreInner => (Lfl ? LFL + "/Materials/LFL_" : DIR + "/Materials/") + "ProjectileCoreInner" + Sfx + ".mat";   // 스파이키 코어
        string MatRimShell  => (Lfl ? LFL + "/Materials/LFL_" : DIR + "/Materials/") + "ProjectileRimShell" + Sfx + ".mat";    // 매끈 외곽선
        string MatSparkle   => (Lfl ? LFL + "/Materials/LFL_" : DIR + "/Materials/") + "ProjectileSparkleStar" + Sfx + ".mat";
        string MatSpikeBurst => (Lfl ? LFL + "/Materials/LFL_" : DIR + "/Materials/") + "SpikeBurstAdditive" + Sfx + ".mat";
        string MatStarFlash  => (Lfl ? LFL + "/Materials/LFL_" : DIR + "/Materials/") + "StarFlashAdditive" + Sfx + ".mat";
        string ProjPrefab   => Lfl ? LFL + "/Prefabs/LFL_Projectile" + Sfx + ".prefab" : DIR + "/Prefabs/ProjectileOrb" + Sfx + ".prefab";

        static readonly string[] TransformProps =
        {
            "useChargeOrbPosition", "spawnSocketName", "spawnOffset", "impactSocketName", "impactOffset",
            "worldSize", "speed", "arcHeight", "trailTime", "trailAutoBySpeed", "trailLengthWorld",
            "burstScaleMul", "burstDuration", "coreSize", "rimSize",
        };

        public override void OnInspectorGUI()
        {
            JcPresetEditorUtil.DrawWithFollowLock(serializedObject, "JC.ProjOrb.Fold", TransformProps);
            var p = (ProjectileOrbPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 값을 ProjectileOrbCore.mat + ProjectileOrb 프리팹(ProjectileVfx/Sparkles) + 전용 스파클 재질에 반영.\n적용 후 새로 재생하면 반영됩니다.", MessageType.Info);
        }

        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        void Apply(ProjectileOrbPreset p)
        {
            // CoreInner(스파이키 코어): 코어 색/밝기 + 스파이크. 림은 꺼둠.
            var inner = Load<Material>(MatCoreInner);
            inner.SetColor("_CoreColor", p.coreColor);
            inner.SetFloat("_CoreIntensity", p.coreIntensity); inner.SetFloat("_CorePower", p.corePower);
            inner.SetFloat("_CoreSpikeAmount", p.coreSpikeAmount); inner.SetFloat("_RimSpikeAmount", p.rimSpikeAmount);
            inner.SetFloat("_SpikeFreq", p.spikeFreq); inner.SetFloat("_SpikeSpeed", p.spikeSpeed); inner.SetFloat("_SpikeSharp", p.spikeSharp);
            inner.SetFloat("_RimIntensity", 0f); inner.SetFloat("_GapStrength", 0f); inner.SetFloat("_FadeMul", 1f);
            EditorUtility.SetDirty(inner);
            // RimShell(매끈 외곽선): 림 색/밝기. 코어·스파이크 꺼둠.
            var shell = Load<Material>(MatRimShell);
            shell.SetColor("_RimColor", p.rimColor);
            shell.SetFloat("_RimIntensity", p.rimIntensity); shell.SetFloat("_RimPower", p.rimPower);
            shell.SetFloat("_CoreIntensity", 0f); shell.SetFloat("_CoreSpikeAmount", 0f); shell.SetFloat("_RimSpikeAmount", 0f);
            shell.SetFloat("_GapStrength", 0f); shell.SetFloat("_FadeMul", 1f);
            EditorUtility.SetDirty(shell);
            var sparkleMat = Load<Material>(MatSparkle); if (sparkleMat) { sparkleMat.SetColor("_BaseColor", p.sparkleColor); EditorUtility.SetDirty(sparkleMat); }
            var spikeMat = Load<Material>(MatSpikeBurst); if (spikeMat) { spikeMat.SetColor("_BaseColor", p.rayColor); EditorUtility.SetDirty(spikeMat); }
            var starMat2 = Load<Material>(MatStarFlash); if (starMat2) { starMat2.SetColor("_BaseColor", p.starColor); EditorUtility.SetDirty(starMat2); }
            AssetDatabase.SaveAssets();

            var proj = PrefabUtility.LoadPrefabContents(ProjPrefab);
            var so = new SerializedObject(proj.GetComponent<ProjectileVfx>());
            so.FindProperty("worldSize").floatValue = p.worldSize;
            so.FindProperty("speed").floatValue = p.speed;
            so.FindProperty("arcHeight").floatValue = p.arcHeight;
            so.FindProperty("trailTime").floatValue = p.trailTime;
            so.FindProperty("trailAutoBySpeed").boolValue = p.trailAutoBySpeed;
            so.FindProperty("trailLengthWorld").floatValue = p.trailLengthWorld;
            so.FindProperty("burstScaleMul").floatValue = p.burstScaleMul;
            so.FindProperty("burstDuration").floatValue = p.burstDuration;
            so.ApplyModifiedPropertiesWithoutUndo();
            var sp = proj.transform.Find("Sparkles").GetComponent<ParticleSystem>();
            var spm = sp.main; spm.startSize = new ParticleSystem.MinMaxCurve(p.sparkleSizeMin, p.sparkleSizeMax); spm.startLifetime = p.sparkleLifetime;
            var spe = sp.emission; spe.rateOverTime = p.sparkleRate;
            var spsh = sp.shape; spsh.radius = p.sparkleShapeRadius;
            // 코어/림 사이즈 (2오브젝트 상대 크기 = 갭)
            var vInner = proj.transform.Find("Visual/CoreInner");
            var vShell = proj.transform.Find("Visual/RimShell");
            if (vInner) vInner.localScale = Vector3.one * p.coreSize;
            if (vShell) vShell.localScale = Vector3.one * p.rimSize;
            // 스파이크 버스트(방사 레이)
            var sb = proj.transform.Find("SpikeBurst").GetComponent<ParticleSystem>();
            var sbm = sb.main; sbm.startSpeed = p.raySpeed;
            sbm.startLifetime = p.raySpeed > 0.01f ? p.rayReachRadius / p.raySpeed : 0.25f;
            sbm.startSize = new ParticleSystem.MinMaxCurve(p.raySizeMin, p.raySizeMax);
            var sbe = sb.emission; sbe.rateOverTime = p.rayRate;
            var sbsh = sb.shape; sbsh.radius = p.rayCenterSize;
            sb.GetComponent<ParticleSystemRenderer>().lengthScale = p.rayLengthScale;
            // 스타 플래시
            var sf = proj.transform.Find("StarFlash").GetComponent<ParticleSystem>();
            var sfm = sf.main; sfm.startSize = p.starSize; sfm.startLifetime = p.starLifetime;
            var sfe = sf.emission; sfe.rateOverTime = p.starRate;
            var sfr = sf.rotationOverLifetime; sfr.z = new ParticleSystem.MinMaxCurve(p.starSpinSpeed * Mathf.Deg2Rad);
            PrefabUtility.SaveAsPrefabAsset(proj, ProjPrefab);
            PrefabUtility.UnloadPrefabContents(proj);

            Debug.Log("[ProjectileOrbPreset] 프리팹에 적용 완료");
        }

        void Capture(ProjectileOrbPreset p)
        {
            Undo.RecordObject(p, "Capture Projectile Orb");
            var inner = Load<Material>(MatCoreInner);
            p.coreColor = inner.GetColor("_CoreColor");
            p.coreIntensity = inner.GetFloat("_CoreIntensity"); p.corePower = inner.GetFloat("_CorePower");
            p.coreSpikeAmount = inner.GetFloat("_CoreSpikeAmount"); p.rimSpikeAmount = inner.GetFloat("_RimSpikeAmount");
            p.spikeFreq = inner.GetFloat("_SpikeFreq"); p.spikeSpeed = inner.GetFloat("_SpikeSpeed"); p.spikeSharp = inner.GetFloat("_SpikeSharp");
            var shell = Load<Material>(MatRimShell);
            p.rimColor = shell.GetColor("_RimColor"); p.rimIntensity = shell.GetFloat("_RimIntensity"); p.rimPower = shell.GetFloat("_RimPower");
            var sparkleMat = Load<Material>(MatSparkle); if (sparkleMat) p.sparkleColor = sparkleMat.GetColor("_BaseColor");

            var proj = Load<GameObject>(ProjPrefab);
            var so = new SerializedObject(proj.GetComponent<ProjectileVfx>());
            p.worldSize = so.FindProperty("worldSize").floatValue;
            p.speed = so.FindProperty("speed").floatValue;
            p.arcHeight = so.FindProperty("arcHeight").floatValue;
            p.trailTime = so.FindProperty("trailTime").floatValue;
            p.trailAutoBySpeed = so.FindProperty("trailAutoBySpeed").boolValue;
            p.trailLengthWorld = so.FindProperty("trailLengthWorld").floatValue;
            p.burstScaleMul = so.FindProperty("burstScaleMul").floatValue;
            p.burstDuration = so.FindProperty("burstDuration").floatValue;
            var sp = proj.transform.Find("Sparkles").GetComponent<ParticleSystem>();
            p.sparkleSizeMin = sp.main.startSize.constantMin; p.sparkleSizeMax = sp.main.startSize.constantMax;
            p.sparkleLifetime = sp.main.startLifetime.constant; p.sparkleShapeRadius = sp.shape.radius;
            p.sparkleRate = sp.emission.rateOverTime.constant;
            var vInner = proj.transform.Find("Visual/CoreInner");
            var vShell = proj.transform.Find("Visual/RimShell");
            if (vInner) p.coreSize = vInner.localScale.x;
            if (vShell) p.rimSize = vShell.localScale.x;
            var sb = proj.transform.Find("SpikeBurst").GetComponent<ParticleSystem>();
            p.raySpeed = sb.main.startSpeed.constant; p.rayReachRadius = sb.main.startLifetime.constant * p.raySpeed;
            p.raySizeMin = sb.main.startSize.constantMin; p.raySizeMax = sb.main.startSize.constantMax;
            p.rayRate = sb.emission.rateOverTime.constant; p.rayCenterSize = sb.shape.radius;
            p.rayLengthScale = sb.GetComponent<ParticleSystemRenderer>().lengthScale;
            var spikeMat = Load<Material>(MatSpikeBurst); if (spikeMat) p.rayColor = spikeMat.GetColor("_BaseColor");
            var sf = proj.transform.Find("StarFlash").GetComponent<ParticleSystem>();
            p.starSize = sf.main.startSize.constant; p.starLifetime = sf.main.startLifetime.constant;
            p.starRate = sf.emission.rateOverTime.constant; p.starSpinSpeed = sf.rotationOverLifetime.z.constant * Mathf.Rad2Deg;
            var starMat2 = Load<Material>(MatStarFlash); if (starMat2) p.starColor = starMat2.GetColor("_BaseColor");

            EditorUtility.SetDirty(p);
            Debug.Log("[ProjectileOrbPreset] 현재값 캡처 완료");
        }
    }
}
