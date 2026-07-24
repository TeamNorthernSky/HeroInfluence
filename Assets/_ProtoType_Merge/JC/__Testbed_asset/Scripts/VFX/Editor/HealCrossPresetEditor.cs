using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(HealCrossPreset))]
    public class HealCrossPresetEditor : Editor
    {
        // 이 프리셋 SO를 쓰는 컴포넌트가 있을 수 있는 프리팹들(힐 오라 + PawForYou 타격 재사용).
        // 적용 대상은 "preset 필드가 이 에셋을 참조하는" 컴포넌트만 — 색 변형 에셋이 서로를 덮지 않게.
        static readonly string[] PrefabPaths =
        {
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/HealSkill/HealOrbit.prefab",
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/PawForYou/PawForYou.prefab",
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/PawForYou/PawForYouMistake.prefab",
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/Taosenaiyo/Taosenaiyo.prefab",
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (HealCrossPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 CrossBurst(HealCrossBurst)에 반영(힐/Paw 프리팹 자동 탐색).\nlivePreview가 켜져 있으면 플레이 중에도 새 십자부터 즉시 반영됩니다.", MessageType.Info);
        }

        static readonly string[] Fields =
        {
            "maxCrosses","spawnIntervalMin","spawnIntervalMax","lifetimeMin","lifetimeMax",
            "radius","baseYOffset","riseSpeedMin","riseSpeedMax","riseAccelMul","riseAccelTime",
            "popOvershoot","popFrac","endFrac","bobAmp","bobFreq","spinMax",
            "sizeMin","sizeMax","fadeInFrac","fadeOutFrac",
            "intensity","barWidth","barLength","softness",
        };

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("preset")?.objectReferenceValue == presetAsset;

        void Apply(HealCrossPreset p)
        {
            int applied = 0;
            foreach (var path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;
                foreach (var cb in root.GetComponentsInChildren<HealCrossBurst>(true))
                {
                    if (!UsesPreset(cb, p)) continue;
                    var so = new SerializedObject(cb);
                    var pso = new SerializedObject(p);
                    foreach (var f in Fields) CopyFloatLike(pso, so, f);
                    so.FindProperty("color").colorValue = p.color;
                    so.FindProperty("billboardYOnly").boolValue = p.billboardYOnly;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                    applied++;
                }
                if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            if (applied == 0) Debug.LogError("[HealCrossPreset] 이 에셋을 참조하는 CrossBurst 없음");
            else Debug.Log($"[HealCrossPreset] 프리팹에 적용 완료 ({applied}곳)");
        }

        void Capture(HealCrossPreset p)
        {
            foreach (var path in PrefabPaths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (var cb in root.GetComponentsInChildren<HealCrossBurst>(true))
                {
                    if (!UsesPreset(cb, p)) continue;
                    Undo.RecordObject(p, "Capture Heal Cross");
                    var so = new SerializedObject(cb);
                    var pso = new SerializedObject(p);
                    foreach (var f in Fields) CopyFloatLike(so, pso, f);
                    pso.FindProperty("color").colorValue = so.FindProperty("color").colorValue;
                    pso.FindProperty("billboardYOnly").boolValue = so.FindProperty("billboardYOnly").boolValue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(p);
                    Debug.Log("[HealCrossPreset] 현재값 캡처 완료");
                    return;
                }
            }
            Debug.LogError("[HealCrossPreset] 이 에셋을 참조하는 CrossBurst 없음");
        }

        // int/float 겸용 복사(같은 이름 프로퍼티)
        static void CopyFloatLike(SerializedObject from, SerializedObject to, string name)
        {
            var pf = from.FindProperty(name);
            var pt = to.FindProperty(name);
            if (pf == null || pt == null) return;
            if (pf.propertyType == SerializedPropertyType.Integer) pt.intValue = pf.intValue;
            else pt.floatValue = pf.floatValue;
        }
    }
}
