using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// T8(부활 오라 타이밍 마스터) 에디터 — B/A 공용이라 두 변종 프리팹(Tao_Revive_Basic/_Alter) 모두에 적용.
    /// 힐 9번(AuraMaster) 선례. 착지 오프셋은 스테퍼(위치 소유 ②)가 읽으므로 프리팹 베이크 대상이 아니다.
    /// </summary>
    [CustomEditor(typeof(TaoReviveMasterPreset))]
    public class TaoReviveMasterPresetEditor : Editor
    {
        static readonly string[] PrefabPaths =
        {
            "Assets/RenderFX/HeroSkill/Nekoming/Taosenaiyo/Prefabs/Tao_Revive_Basic.prefab",
            "Assets/RenderFX/HeroSkill/Nekoming/Taosenaiyo/Prefabs/Tao_Revive_Alter.prefab",
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (TaoReviveMasterPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
                JcPresetEditorUtil.DrawSaveButton(target);
            }
            EditorGUILayout.HelpBox("적용: 타이밍(페이드인/유지/아웃)을 Tao_Revive_Basic·_Alter 두 프리팹의 TaoReviveVfx 에 반영.\n" +
                "landOffset 은 스테퍼가 이 자산에서 직접 읽습니다(베이크 없음). livePreview 켜져 있으면 플레이 중 즉시 반영.", MessageType.Info);
        }

        void Apply(TaoReviveMasterPreset p)
        {
            int applied = 0;
            foreach (var path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                var vfx = root.GetComponent<TaoReviveVfx>();
                if (vfx == null) { PrefabUtility.UnloadPrefabContents(root); continue; }
                var so = new SerializedObject(vfx);
                so.FindProperty("fadeInTime").floatValue = p.fadeInTime;
                so.FindProperty("sustainTime").floatValue = p.sustainTime;
                so.FindProperty("fadeOutTime").floatValue = p.fadeOutTime;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                applied++;
            }
            if (applied == 0) Debug.LogError("[TaoReviveMasterPreset] 대상 프리팹 없음 — 경로를 확인하세요");
            else Debug.Log($"[TaoReviveMasterPreset] 프리팹에 적용 완료 ({applied}곳)");
        }

        void Capture(TaoReviveMasterPreset p)
        {
            foreach (var path in PrefabPaths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var vfx = root != null ? root.GetComponent<TaoReviveVfx>() : null;
                if (vfx == null) continue;
                var so = new SerializedObject(vfx);
                Undo.RecordObject(p, "Capture Tao Revive Master");
                p.fadeInTime = so.FindProperty("fadeInTime").floatValue;
                p.sustainTime = so.FindProperty("sustainTime").floatValue;
                p.fadeOutTime = so.FindProperty("fadeOutTime").floatValue;
                EditorUtility.SetDirty(p);
                Debug.Log($"[TaoReviveMasterPreset] 현재값 캡처 완료 ({root.name})");
                return;
            }
            Debug.LogError("[TaoReviveMasterPreset] 대상 프리팹 없음 — 경로를 확인하세요");
        }
    }
}
