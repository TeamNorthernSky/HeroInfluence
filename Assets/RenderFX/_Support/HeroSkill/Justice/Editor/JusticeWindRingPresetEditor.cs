using UnityEditor;
using UnityEngine;

namespace JC.VFX.EditorTools
{
    /// <summary>
    /// 바람무늬 방사광 프리셋 인스펙터 — 「적용」이 대상 재질·프리팹에 확정 기록한다.
    /// 런타임은 바인더가 스폰마다 프리셋을 다시 읽으므로, 플레이 중 튜닝은 다음 재생에 반영된다.
    /// </summary>
    [CustomEditor(typeof(JusticeWindRingPreset))]
    public class JusticeWindRingPresetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var p = (JusticeWindRingPreset)target;

            EditorGUILayout.Space(8);
            if (GUILayout.Button("적용 — 재질·프리팹에 확정 기록", GUILayout.Height(26)))
                Apply(p);
            JcPresetEditorUtil.DrawSaveButton(target, wide: true);   // 튜닝 저장 공식 규격
        }

        private static void Apply(JusticeWindRingPreset p)
        {
            var t = p.targets;

            if (t.windMaterial != null) EditorUtility.SetDirty(t.windMaterial);

            // ★굽기 모드 — 여기서만 공유 재질 에셋에 기록(평시 스폰은 재질 무접촉).
            JusticeTrailPresetRuntime.WriteSharedMaterials = true;
            try
            {
                string prefabPath = t.windPrefab != null ? AssetDatabase.GetAssetPath(t.windPrefab) : null;
                if (string.IsNullOrEmpty(prefabPath))
                {
                    // 재질만이라도 반영한다 — 프리팹 없이 재질 튜닝만 할 수 있게.
                    JusticeWindRingRuntime.ApplyWindRing(new GameObject("_tmp"), p, t.windMaterial);
                    var tmp = GameObject.Find("_tmp");
                    if (tmp != null) Object.DestroyImmediate(tmp);
                    Debug.LogWarning("[JusticeWindRing] " + p.name + ": 대상 프리팹이 비어 있어 재질만 기록했습니다.", p);
                }
                else
                {
                    var root = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        JusticeWindRingRuntime.ApplyWindRing(root, p, t.windMaterial);
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            }
            finally
            {
                JusticeTrailPresetRuntime.WriteSharedMaterials = false;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[JusticeWindRing] " + p.name + " → 재질·프리팹에 확정 기록 완료", p);
        }
    }
}
