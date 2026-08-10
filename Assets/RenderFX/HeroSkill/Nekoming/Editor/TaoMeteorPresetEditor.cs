using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(TaoMeteorPreset))]
    public class TaoMeteorPresetEditor : Editor
    {
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/Taosenaiyo";
        // ★변종 인식(260807) — _Basic/_Alter 프리셋은 제 변종의 프리팹·재질만 만진다(베이크 상호 오염 방지).
        string Sfx => JcPresetEditorUtil.VariantSuffix(target) ?? "_Alter";
        string MeteorPrefab => DIR + "/Prefabs/Tao_MeteorOrb" + Sfx + ".prefab";
        string CometMat => DIR + "/Materials/TaoComet" + Sfx + ".mat";
        string HeadMat => DIR + "/Materials/TaoCometHead" + Sfx + ".mat";

        // 따름 잠금 — 색·밝기 외 전부(위치·비행·리본 크기·플리커)는 Basic 이 정본.
        static readonly string[] TransformProps =
        {
            "spawnSocketName", "spawnOffset", "impactOffset",
            "worldSize", "speed", "arcHeight", "trailTime", "cometLength", "cometWidth", "headSize",
            "tailStartWidth", "tailEndWidth", "tailTaper", "tailFade",
            "flickerAmp", "flickerSpeed", "rimPos", "rimSoft", "rimFade",
        };

        public override void OnInspectorGUI()
        {
            JcPresetEditorUtil.DrawWithFollowLock(serializedObject, "JC.TaoMeteor.Fold", TransformProps);
            var p = (TaoMeteorPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
                JcPresetEditorUtil.DrawSaveButton(target);
            }
            EditorGUILayout.HelpBox("적용: 비행 값은 Tao_MeteorOrb 프리팹(ProjectileVfx/CometShell)에, 혜성 룩은 TaoComet.mat에 베이크.\nlivePreview가 켜져 있으면 플레이 중에도 즉시 반영됩니다.", MessageType.Info);
        }

        static readonly (string preset, string mat)[] MatFloats =
        {
            ("tailStartWidth","_TailStartWidth"),
            ("tailEndWidth","_TailEndWidth"), ("tailTaper","_TailTaper"), ("tailFade","_TailFade"),
            ("flickerAmp","_FlickerAmp"), ("flickerSpeed","_FlickerSpeed"),
            ("intensity","_Intensity"),
            ("rimIntensity","_RimIntensity"), ("rimPos","_RimPos"), ("rimSoft","_RimSoft"),
            ("rimFade","_RimFade"),
        };
        static readonly (string preset, string mat)[] MatColors =
        {
            ("headColor","_ColorHead"), ("tailColor","_ColorTail"), ("rimColor","_RimColor"),
        };
        // 머리 재질은 개별×마스터 곱을 베이크(프리뷰 꺼져도 동일 룩). 캡처 시 마스터로 나눠 복원.
        static readonly (string preset, string mat)[] HeadFloats =
        {
            ("headFillIntensity","_FillIntensity"), ("headRimIntensity","_RimIntensity"),
        };
        static readonly (string preset, string mat)[] HeadPowers =
        {
            ("headFillPower","_FillPower"), ("headRimPower","_RimPower"),
        };
        static readonly (string preset, string mat)[] HeadColors =
        {
            ("headColor","_FillColor"), ("rimColor","_RimColor"),
        };

        void Apply(TaoMeteorPreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(MeteorPrefab);
            try
            {
                var pv = root.GetComponent<ProjectileVfx>();
                var so = new SerializedObject(pv);
                so.FindProperty("worldSize").floatValue = p.worldSize;
                so.FindProperty("speed").floatValue = p.speed;
                so.FindProperty("arcHeight").floatValue = p.arcHeight;
                so.FindProperty("trailTime").floatValue = p.trailTime;
                so.ApplyModifiedPropertiesWithoutUndo();

                var cs = root.GetComponentInChildren<CometShell>(true);
                if (cs != null)
                {
                    var cso = new SerializedObject(cs);
                    cso.FindProperty("length").floatValue = p.cometLength;
                    cso.FindProperty("width").floatValue = p.cometWidth;
                    cso.ApplyModifiedPropertiesWithoutUndo();
                }
                var headT = root.transform.Find("CometHead");
                if (headT != null) headT.localScale = Vector3.one * p.headSize;
                PrefabUtility.SaveAsPrefabAsset(root, MeteorPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(CometMat);
            if (mat != null)
            {
                var pso = new SerializedObject(p);
                foreach (var (pf, mf) in MatFloats) mat.SetFloat(mf, pso.FindProperty(pf).floatValue);
                foreach (var (pf, mf) in MatColors) mat.SetColor(mf, pso.FindProperty(pf).colorValue);
                EditorUtility.SetDirty(mat);
            }
            var hmat = AssetDatabase.LoadAssetAtPath<Material>(HeadMat);
            if (hmat != null)
            {
                var pso = new SerializedObject(p);
                foreach (var (pf, mf) in HeadFloats) hmat.SetFloat(mf, pso.FindProperty(pf).floatValue * p.intensity);
                foreach (var (pf, mf) in HeadPowers) hmat.SetFloat(mf, pso.FindProperty(pf).floatValue);
                foreach (var (pf, mf) in HeadColors) hmat.SetColor(mf, pso.FindProperty(pf).colorValue);
                EditorUtility.SetDirty(hmat);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[TaoMeteorPreset] 프리팹/재질에 적용 완료");
        }

        void Capture(TaoMeteorPreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(MeteorPrefab);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CometMat);
            if (root == null || mat == null) { Debug.LogError("[TaoMeteorPreset] 프리팹/재질 없음"); return; }
            Undo.RecordObject(p, "Capture Tao Meteor");
            var pv = root.GetComponent<ProjectileVfx>();
            var so = new SerializedObject(pv);
            var pso = new SerializedObject(p);
            pso.FindProperty("worldSize").floatValue = so.FindProperty("worldSize").floatValue;
            pso.FindProperty("speed").floatValue = so.FindProperty("speed").floatValue;
            pso.FindProperty("arcHeight").floatValue = so.FindProperty("arcHeight").floatValue;
            pso.FindProperty("trailTime").floatValue = so.FindProperty("trailTime").floatValue;
            var cs = root.GetComponentInChildren<CometShell>(true);
            if (cs != null)
            {
                var cso = new SerializedObject(cs);
                pso.FindProperty("cometLength").floatValue = cso.FindProperty("length").floatValue;
                pso.FindProperty("cometWidth").floatValue = cso.FindProperty("width").floatValue;
            }
            foreach (var (pf, mf) in MatFloats) pso.FindProperty(pf).floatValue = mat.GetFloat(mf);
            foreach (var (pf, mf) in MatColors) pso.FindProperty(pf).colorValue = mat.GetColor(mf);
            var hmat = AssetDatabase.LoadAssetAtPath<Material>(HeadMat);
            if (hmat != null)
            {
                float master = Mathf.Max(mat.GetFloat("_Intensity"), 1e-3f);   // 머리 재질은 곱 베이크라 마스터로 나눠 복원
                foreach (var (pf, mf) in HeadFloats) pso.FindProperty(pf).floatValue = hmat.GetFloat(mf) / master;
                foreach (var (pf, mf) in HeadPowers) pso.FindProperty(pf).floatValue = hmat.GetFloat(mf);
                var headT = root.transform.Find("CometHead");
                if (headT != null) pso.FindProperty("headSize").floatValue = headT.localScale.x;
            }
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(p);
            Debug.Log("[TaoMeteorPreset] 현재값 캡처 완료");
        }
    }
}
