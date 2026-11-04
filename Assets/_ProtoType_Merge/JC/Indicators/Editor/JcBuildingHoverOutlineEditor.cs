using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JC.Indicators.EditorTools
{
    [CustomEditor(typeof(JcBuildingHoverOutlineController))]
    public class JcBuildingHoverOutlineEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (MonoBehaviour)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Play 중에도 [설정 저장]을 누르면 윤곽선 설정값만 별도 에셋에 저장됩니다. 저장값은 Play 종료 후와 다음 실행에 적용됩니다. 저장하지 않은 조절값은 Play 종료 시 되돌아갑니다.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("설정 저장", "현재 윤곽선 설정값을 디스크에 저장합니다. 씬 전체나 다른 에셋은 저장하지 않습니다.")))
                    JcBuildingHoverOutlineSave.Save(controller);
                if (GUILayout.Button(new GUIContent("저장값 불러오기", "마지막 저장값으로 현재 조절값을 되돌립니다.")))
                {
                    Undo.RecordObject(controller, "건물 호버 윤곽선 불러오기");
                    JcBuildingHoverOutlineSave.Load(controller);
                    if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                    SceneView.RepaintAll();
                }
            }
        }
    }

    [CustomEditor(typeof(JcInteractionHoverOutlineController))]
    public sealed class JcInteractionHoverOutlineEditor : JcBuildingHoverOutlineEditor { }

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
                Restore(Object.FindObjectsByType<JcBuildingHoverOutlineController>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                Restore(Object.FindObjectsByType<JcInteractionHoverOutlineController>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            };
        }

        private static void Restore(MonoBehaviour[] controllers)
        {
            foreach (var c in controllers)
                {
                    string key = Pending + c.GetType().Name + c.gameObject.scene.path;
                    if (!SessionState.GetBool(key, false)) continue;
                    Undo.RecordObject(c, "플레이 중 저장한 건물 호버 설정 복원");
                    Load(c);
                    EditorUtility.SetDirty(c);
                    EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                    SessionState.EraseBool(key);
                }
                SceneView.RepaintAll();
        }

        public static void Load(MonoBehaviour controller)
        {
            if (controller is JcBuildingHoverOutlineController building) building.LoadSavedSettings();
            else if (controller is JcInteractionHoverOutlineController interaction) interaction.LoadSavedSettings();
        }

        public static void Save(MonoBehaviour controller)
        {
            var scene = controller.gameObject.scene;
            if (string.IsNullOrEmpty(scene.path))
                throw new System.InvalidOperationException("먼저 씬에 파일 이름을 지정하여 저장해주세요.");
            bool building = controller is JcBuildingHoverOutlineController;
            var folder = building ? Folder : "Assets/_ProtoType_Merge/JC/Indicators/Resources/JC_InteractionHover";
            EnsureFolder(folder);
            string path = folder + "/" + scene.name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            bool create = profile == null;
            if (create) profile = building ? (ScriptableObject)ScriptableObject.CreateInstance<JcBuildingHoverOutlineProfile>()
                : ScriptableObject.CreateInstance<JcInteractionHoverOutlineProfile>();
            else Undo.RecordObject(profile, "호버 윤곽선 설정 저장");
            if (building) ((JcBuildingHoverOutlineProfile)profile).values = ((JcBuildingHoverOutlineController)controller).Values;
            else ((JcInteractionHoverOutlineProfile)profile).values = ((JcInteractionHoverOutlineController)controller).Values.Copy();
            if (create) AssetDatabase.CreateAsset(profile, path);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            if (Application.isPlaying) SessionState.SetBool(Pending + controller.GetType().Name + scene.path, true);
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
