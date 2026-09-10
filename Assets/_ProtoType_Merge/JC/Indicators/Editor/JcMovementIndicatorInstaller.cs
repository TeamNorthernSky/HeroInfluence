using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.Indicators.EditorTools
{
    public static class JcMovementIndicatorInstaller
    {
        public const string ProfilePath = "Assets/_ProtoType_Merge/JC/Indicators/Profiles/MovementIndicator.asset";
        public const string ShaderPath = "Assets/_ProtoType_Merge/JC/Indicators/Shaders/JcMovementIndicator.shader";

        [MenuItem("JC/인디케이터/이동 인디케이터 설치·연결 (활성 씬)")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이 종료 후 설치하세요.");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("활성 씬이 없습니다.");
            var clicks = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ClickSelectionController>(true)).ToArray();
            if (clicks.Length != 1) throw new InvalidOperationException("현재 씬의 ClickSelectionController가 정확히 하나여야 합니다.");
            var fields = new SerializedObject(clicks[0]);
            var path = fields.FindProperty("pathPreviewRenderer").objectReferenceValue as PathPreviewRenderer;
            var marker = fields.FindProperty("marker").objectReferenceValue as Transform;
            var grid = fields.FindProperty("gridManager").objectReferenceValue as GridManager;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (path == null || marker == null || grid == null || shader == null)
                throw new InvalidOperationException("경로·표식·그리드·셰이더 연결을 확인하세요.");
            var profile = AssetDatabase.LoadAssetAtPath<JcMovementIndicatorProfile>(ProfilePath);
            if (profile == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_ProtoType_Merge/JC/Indicators/Profiles"))
                    AssetDatabase.CreateFolder("Assets/_ProtoType_Merge/JC/Indicators", "Profiles");
                profile = ScriptableObject.CreateInstance<JcMovementIndicatorProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath); AssetDatabase.SaveAssetIfDirty(profile);
            }
            var roots = scene.GetRootGameObjects().Where(g => g.name == "JC_Indicator").ToArray();
            if (roots.Length > 1) throw new InvalidOperationException("JC_Indicator 루트가 중복되어 있습니다.");
            GameObject root = roots.FirstOrDefault();
            if (root == null)
            {
                root = new GameObject("JC_Indicator"); SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "JC 인디케이터 루트 설치");
            }
            var controllers = root.GetComponentsInChildren<JcMovementIndicatorController>(true);
            if (controllers.Length > 1) throw new InvalidOperationException("이동 인디케이터 설정이 중복되어 있습니다.");
            var controller = controllers.FirstOrDefault();
            if (controller == null)
            {
                var child = new GameObject("MovementIndicator"); child.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(child, "이동 인디케이터 설정 설치");
                controller = Undo.AddComponent<JcMovementIndicatorController>(child);
                controller.Configure(path, marker, grid, shader, profile);
            }
            else
            {
                // 재연결 시 미저장 초안과 사용자가 고른 프로파일을 보존한다.
                Undo.RecordObject(controller, "이동 인디케이터 재연결");
                var draft = controller.Settings;
                controller.Configure(path, marker, grid, shader, controller.Profile != null ? controller.Profile : profile);
                controller.Settings = draft;
            }
            EditorUtility.SetDirty(controller); EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = controller.gameObject;
            Debug.Log("[JC Indicator] JC_Indicator/MovementIndicator 설치 완료. 씬은 직접 저장하세요.");
        }
    }
}
