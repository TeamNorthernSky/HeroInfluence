using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JC.Indicators.EditorTools
{
    [CustomEditor(typeof(JcBuildingHoverOutlineController))]
    public sealed class JcBuildingHoverOutlineEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (JcBuildingHoverOutlineController)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Play 중에도 [설정 저장]을 누르면 윤곽선 설정값만 별도 에셋에 저장됩니다. 저장값은 Play 종료 후와 다음 실행에 적용됩니다. 저장하지 않은 조절값은 Play 종료 시 되돌아갑니다.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("설정 저장", "현재 윤곽선 설정값을 디스크에 저장합니다. 씬 전체나 다른 에셋은 저장하지 않습니다.")))
                    JcBuildingHoverOutlineSave.Save(controller);
                if (GUILayout.Button(new GUIContent("저장값 불러오기", "마지막 저장값으로 현재 조절값을 되돌립니다.")))
                {
                    Undo.RecordObject(controller, "건물 호버 윤곽선 불러오기");
                    controller.LoadSavedSettings();
                    if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                    SceneView.RepaintAll();
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class JcBuildingHoverOutlineSave
    {
        private const string Folder = "Assets/_ProtoType_Merge/JC/Indicators/Resources/JC_BuildingHover";
        private const string Pending = "JC.BuildingHover.SavedDuringPlay:";
        static JcBuildingHoverOutlineSave()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                foreach (var c in Object.FindObjectsByType<JcBuildingHoverOutlineController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    string key = Pending + c.gameObject.scene.path;
                    if (!SessionState.GetBool(key, false)) continue;
                    Undo.RecordObject(c, "플레이 중 저장한 건물 호버 설정 복원");
                    c.LoadSavedSettings();
                    EditorUtility.SetDirty(c);
                    EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                    SessionState.EraseBool(key);
                }
                SceneView.RepaintAll();
            };
        }

        public static void Save(JcBuildingHoverOutlineController controller)
        {
            var scene = controller.gameObject.scene;
            if (string.IsNullOrEmpty(scene.path))
                throw new System.InvalidOperationException("먼저 씬에 파일 이름을 지정하여 저장해주세요.");
            EnsureFolder(Folder);
            string path = Folder + "/" + scene.name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<JcBuildingHoverOutlineProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<JcBuildingHoverOutlineProfile>();
                profile.values = controller.Values;
                AssetDatabase.CreateAsset(profile, path);
            }
            else
            {
                Undo.RecordObject(profile, "건물 호버 윤곽선 설정 저장");
                profile.values = controller.Values;
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            if (Application.isPlaying) SessionState.SetBool(Pending + scene.path, true);
            Debug.Log("[JC Indicator] 건물 호버 윤곽선 설정 저장 완료: " + path, controller);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
