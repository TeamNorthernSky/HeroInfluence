using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(TaoFeatherPreset))]
    public class TaoFeatherPresetEditor : Editor
    {
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/Taosenaiyo/Prefabs";
        // ★변종 인식(260807) — _Basic/_Alter 프리셋은 제 부품 프리팹(Tao_Revive±)만 만진다.
        string Sfx => JcPresetEditorUtil.VariantSuffix(target) ?? "_Alter";
        string TaoPrefab => JcPresetPartTargets.Path(target, DIR + "/Tao_Revive" + Sfx + ".prefab");

        // 따름 잠금 — 색·밝기 외 전부(스폰·거동·형태)는 Basic 이 정본.
        static readonly string[] TransformProps =
        {
            "maxFeathers", "spawnIntervalMin", "spawnIntervalMax", "lifetimeMin", "lifetimeMax",
            "spawnRadius", "baseYOffset", "riseSpeedMin", "riseSpeedMax", "spiralSpeed",
            "swayAmp", "swayFreq", "spinMax", "sizeMin", "sizeMax", "fadeInFrac", "fadeOutFrac",
            "bend", "barbFreq", "barbAmount",
        };

        public override void OnInspectorGUI()
        {
            JcPresetEditorUtil.DrawWithFollowLock(serializedObject, "JC.TaoFeather.Fold", TransformProps);
            var p = (TaoFeatherPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("▶ 프리팹에 적용","이 프리셋을 참조하는 대상 프리팹에 현재 값을 확정합니다."), GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button(new GUIContent("● 현재값 캡처","대상 프리팹의 값을 현재 프리셋으로 읽습니다. 프리셋 파일 저장은 별도입니다."), GUILayout.Height(30))) Capture(p);
                JcPresetEditorUtil.DrawSaveButton(target);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 FeatherBurst(TaoFeatherBurst)에 반영.\n깃털 형태(벤드/깃가지) 항목은 프리셋+livePreview로만 구동됩니다.", MessageType.Info);
        }

        static readonly string[] Fields =
        {
            "maxFeathers","spawnIntervalMin","spawnIntervalMax","lifetimeMin","lifetimeMax",
            "spawnRadius","baseYOffset","riseSpeedMin","riseSpeedMax","spiralSpeed",
            "swayAmp","swayFreq","spinMax","sizeMin","sizeMax","fadeInFrac","fadeOutFrac","intensity",
        };

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("preset")?.objectReferenceValue == presetAsset;

        void Apply(TaoFeatherPreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(TaoPrefab);
            bool dirty = false;
            foreach (var fb in root.GetComponentsInChildren<TaoFeatherBurst>(true))
            {
                if (!UsesPreset(fb, p)) continue;
                var so = new SerializedObject(fb);
                var pso = new SerializedObject(p);
                foreach (var f in Fields) CopyFloatLike(pso, so, f);
                so.FindProperty("color").colorValue = p.color;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
            }
            if (dirty) PrefabUtility.SaveAsPrefabAsset(root, TaoPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            if (dirty) Debug.Log("[TaoFeatherPreset] 프리팹에 적용 완료");
            else Debug.LogError("[TaoFeatherPreset] 이 에셋을 참조하는 TaoFeatherBurst 없음");
        }

        void Capture(TaoFeatherPreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(TaoPrefab);
            if (root != null)
            {
                foreach (var fb in root.GetComponentsInChildren<TaoFeatherBurst>(true))
                {
                    if (!UsesPreset(fb, p)) continue;
                    Undo.RecordObject(p, "Capture Tao Feather");
                    var so = new SerializedObject(fb);
                    var pso = new SerializedObject(p);
                    foreach (var f in Fields) CopyFloatLike(so, pso, f);
                    pso.FindProperty("color").colorValue = so.FindProperty("color").colorValue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(p);
                    Debug.Log("[TaoFeatherPreset] 현재값 캡처 완료");
                    return;
                }
            }
            Debug.LogError("[TaoFeatherPreset] 이 에셋을 참조하는 TaoFeatherBurst 없음");
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
