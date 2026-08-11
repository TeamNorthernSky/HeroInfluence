using UnityEditor;
using UnityEngine;

namespace JC.VFX.EditorTools
{
    /// <summary>
    /// 호 파편 프리셋 인스펙터 — 「적용」이 대상 재질·프리팹에 확정 기록한다.
    /// 런타임은 바인더가 스폰마다 프리셋을 다시 읽으므로, 플레이 중 튜닝은 다음 재생에 반영된다.
    /// </summary>
    [CustomEditor(typeof(JusticeArcShardPreset))]
    public class JusticeArcShardPresetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var p = (JusticeArcShardPreset)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "형상: 이등변 삼각형을 절단선으로 깎은 조각(한쪽 절단=삼각형, 양쪽=사각형).\n" +
                "절단점은 파편마다 난수로 정해져 변주가 무한하다.\n" +
                "내부=흰 심, 테두리=색 팔레트. 축소·페이드 시점은 서로 독립.",
                MessageType.None);

            if (GUILayout.Button("적용 — 재질·프리팹에 확정 기록", GUILayout.Height(26)))
                Apply(p);
            JcPresetEditorUtil.DrawSaveButton(target, wide: true);   // 튜닝 저장 공식 규격
        }

        private static void Apply(JusticeArcShardPreset p)
        {
            var t = p.targets;
            if (t.shardMaterial != null) EditorUtility.SetDirty(t.shardMaterial);

            string prefabPath = t.shardPrefab != null ? AssetDatabase.GetAssetPath(t.shardPrefab) : null;
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning("[JusticeArcShard] " + p.name + ": 대상 프리팹이 비어 있어 건너뜁니다.", p);
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            // ★굽기 모드 — 여기서만 공유 재질 에셋에 기록(평시 스폰·라이브는 MPB 비파괴).
            JusticeTrailPresetRuntime.WriteSharedMaterials = true;
            try
            {
                JusticeArcShardRuntime.ApplyArcShard(root, p, t.shardMaterial);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                JusticeTrailPresetRuntime.WriteSharedMaterials = false;
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[JusticeArcShard] " + p.name + " → 재질·프리팹에 확정 기록 완료", p);
        }
    }
}
