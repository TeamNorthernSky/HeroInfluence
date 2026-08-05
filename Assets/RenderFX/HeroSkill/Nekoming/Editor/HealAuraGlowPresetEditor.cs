using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// 오라 글로우 프리셋 에디터 — 260805 변종 인식 개편.
    /// ★적용/캡처 대상은 「preset 필드가 이 에셋을 참조하는 HealAuraGlow」만(첫 자식 폴백 제거).
    /// </summary>
    [CustomEditor(typeof(HealAuraGlowPreset))]
    public class HealAuraGlowPresetEditor : Editor
    {
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/Heal";
        static readonly string[] PrefabPaths =
        {
            DIR + "/Prefabs/HealOrbit_Basic.prefab",
            DIR + "/Prefabs/HealOrbit_Alter.prefab",
        };

        static readonly string[] TransformProps = { "width", "height", "groundOffsetY" };

        static readonly string[] Fields =
        {
            "width", "height", "groundOffsetY", "intensity", "opacity",
            "bottomFade", "verticalBias", "topMin", "topMax", "topSoft", "topNoiseScale", "topNoiseSpeed",
            "facePower", "streakTiling", "streakStrength", "streakScroll",
            "wobbleAmount", "wobbleSpeed", "riseGrow",
        };

        public override void OnInspectorGUI()
        {
            JcPresetEditorUtil.DrawWithFollowLock(serializedObject, "JC.AuraGlow.Fold", TransformProps);
            var p = (HealAuraGlowPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 AuraGlow 에만 반영(Basic·Alter 프리팹 자동 탐색).\nlivePreview가 켜져 있으면 플레이 중에도 즉시 반영됩니다.", MessageType.Info);
        }

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("preset")?.objectReferenceValue == presetAsset;

        void Apply(HealAuraGlowPreset p)
        {
            int applied = 0;
            foreach (var path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;
                foreach (var fg in root.GetComponentsInChildren<HealAuraGlow>(true))
                {
                    if (!UsesPreset(fg, p)) continue;
                    var so = new SerializedObject(fg);
                    var pso = new SerializedObject(p);
                    foreach (var f in Fields) CopyFloat(pso, so, f);
                    so.FindProperty("color").colorValue = p.color;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                    applied++;
                }
                if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            if (applied == 0) Debug.LogError("[HealAuraGlowPreset] 이 프리셋을 참조하는 AuraGlow 없음 — 결선을 확인하세요(폴백 없음)");
            else Debug.Log($"[HealAuraGlowPreset] 프리팹에 적용 완료 ({applied}곳)");
        }

        void Capture(HealAuraGlowPreset p)
        {
            foreach (var path in PrefabPaths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (var fg in root.GetComponentsInChildren<HealAuraGlow>(true))
                {
                    if (!UsesPreset(fg, p)) continue;
                    Undo.RecordObject(p, "Capture Heal Aura Glow");
                    var so = new SerializedObject(fg);
                    var pso = new SerializedObject(p);
                    foreach (var f in Fields) CopyFloat(so, pso, f);
                    pso.FindProperty("color").colorValue = so.FindProperty("color").colorValue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(p);
                    Debug.Log($"[HealAuraGlowPreset] 현재값 캡처 완료 ({root.name})");
                    return;
                }
            }
            Debug.LogError("[HealAuraGlowPreset] 이 프리셋을 참조하는 AuraGlow 없음 — 결선을 확인하세요(폴백 없음)");
        }

        static void CopyFloat(SerializedObject from, SerializedObject to, string name)
        {
            var pf = from.FindProperty(name);
            var pt = to.FindProperty(name);
            if (pf == null || pt == null) return;
            pt.floatValue = pf.floatValue;
        }
    }
}
