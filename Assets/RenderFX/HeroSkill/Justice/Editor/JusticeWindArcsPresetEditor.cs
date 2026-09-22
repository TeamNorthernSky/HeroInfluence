using UnityEditor;
using UnityEngine;

namespace JC.VFX.EditorTools
{
    /// <summary>
    /// 바람무늬 원호 프리셋 인스펙터 — 「적용」이 대상 프리팹에 확정 기록한다.
    /// 런타임은 바인더가 스폰마다 프리셋을 다시 읽으므로, 플레이 중 튜닝은 다음 재생에 반영된다.
    /// </summary>
    [CustomEditor(typeof(JusticeWindArcsPreset))]
    public class JusticeWindArcsPresetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var p = (JusticeWindArcsPreset)target;

            EditorGUILayout.Space(8);
            if (GUILayout.Button(new GUIContent("적용 — 프리팹에 확정 기록", "현재 원호의 색·폭·생성·이동 설정을 연결된 프리팹에 적용하고 저장합니다. 프리뷰와 전투가 함께 사용합니다."), GUILayout.Height(26)))
                Apply(p);
            JcPresetEditorUtil.DrawSaveButton(target, wide: true);   // 튜닝 저장 공식 규격
        }

        private static void Apply(JusticeWindArcsPreset p)
        {
            var t = p.targets;
            string prefabPath = t.arcsPrefab != null ? AssetDatabase.GetAssetPath(t.arcsPrefab) : null;
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning("[JusticeWindArcs] " + p.name + ": 대상 프리팹이 비어 있어 건너뜁니다.", p);
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                JusticeWindArcsRuntime.ApplyWindArcs(root, p, t.ribbonMaterial);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();
            Debug.Log("[JusticeWindArcs] " + p.name + " → 프리팹에 확정 기록 완료", p);
        }
    }
}
