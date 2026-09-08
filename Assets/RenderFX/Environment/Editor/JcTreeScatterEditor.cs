using System.IO;
using UnityEditor;
using UnityEngine;

namespace JC.Env.EditorTools
{
    /// <summary>산포기 인스펙터 — 뿌리기 / 지우기 / CSV 내보내기.</summary>
    [CustomEditor(typeof(JcTreeScatter))]
    public class JcTreeScatterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var s = (JcTreeScatter)target;

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 뿌리기 (기존 삭제 후)", GUILayout.Height(28)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(s.root != null ? s.root.gameObject : s.gameObject, "Tree Scatter");
                    s.Scatter();
                    EditorSceneManagerMark(s);
                }
                if (GUILayout.Button("✕ 지우기", GUILayout.Height(28), GUILayout.Width(90)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(s.root != null ? s.root.gameObject : s.gameObject, "Tree Scatter Clear");
                    s.Clear();
                    EditorSceneManagerMark(s);
                }
            }
            if (GUILayout.Button("CSV 내보내기 (좌표 목록)"))
            {
                string path = EditorUtility.SaveFilePanel("나무 좌표 CSV", "", $"tree_positions_{System.DateTime.Now:yyMMdd}.csv", "csv");
                if (!string.IsNullOrEmpty(path)) { File.WriteAllText(path, s.ToCsv()); Debug.Log($"[JcTreeScatter] CSV 저장: {path} ({s.lastCount}개)"); }
            }
            EditorGUILayout.HelpBox($"현재 {s.lastCount}개. 씬뷰: 주황=맵 바운드(제외), 초록=밴드 외곽, 파랑=클립 영역, 빨강=추가 제외.", MessageType.None);
        }

        private static void EditorSceneManagerMark(JcTreeScatter s)
        {
            if (!Application.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
        }
    }
}
