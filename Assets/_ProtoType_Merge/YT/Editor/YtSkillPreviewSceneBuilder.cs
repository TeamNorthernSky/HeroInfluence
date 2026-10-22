using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YT.Preview
{
    public static class YtSkillPreviewSceneBuilder
    {
        public const string ScenePath = "Assets/_ProtoType_Merge/YT/SkillPreviewScene.unity";
        private const string SourcePath = "Assets/_ProtoType_Merge/YT/BattlePresentationScene.unity";

        [MenuItem("Tools/YT/Create Skill Preview Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("컴파일이 끝난 편집 모드에서 실행하세요.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("이미 프리뷰 씬이 있습니다. 기존 씬을 열어 사용하세요.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("현재 씬을 먼저 저장하세요.");
            if (!AssetDatabase.CopyAsset(SourcePath, ScenePath)) throw new InvalidOperationException("씬 복사 실패");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var roots = scene.GetRootGameObjects();
            var preview = roots.SelectMany(r => r.GetComponentsInChildren<BattlePresentationPreview>(true)).Single();
            string configPath = "Assets/_ProtoType_Merge/YT/SkillPreviewConfig.asset";
            string cameraPath = "Assets/_ProtoType_Merge/YT/SkillPreviewCamera.asset";
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(preview.config), configPath))
                throw new InvalidOperationException("프리뷰 전투 설정 복사 실패");
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(preview.cameraProfile), cameraPath))
                throw new InvalidOperationException("프리뷰 카메라 설정 복사 실패");
            preview.config = AssetDatabase.LoadAssetAtPath<SimulationBattleConfig>(configPath);
            preview.cameraProfile = AssetDatabase.LoadAssetAtPath<JC.BattleTesting.JcBattleCameraProfile>(cameraPath);
            var owner = new GameObject("YT_SkillPreview");
            var rig = owner.AddComponent<YtSkillPreviewController>();
            rig.presentation = preview;
            rig.battle = preview.battle.GetComponent<BattleManager>();
            rig.flow = preview.battle.GetComponent<BattleFlowManager>();
            rig.battleHud = roots.FirstOrDefault(r => r.name == "BattleSceneCanvas");
            if (rig.battleHud != null) rig.battleHud.SetActive(false);
            foreach (var input in roots.SelectMany(r => r.GetComponentsInChildren<InputHandler>(true))) input.enabled = false;
            foreach (var automatic in roots.SelectMany(r => r.GetComponentsInChildren<AutoBattleController>(true))) automatic.enabled = false;
            foreach (var controller in roots.SelectMany(r => r.GetComponentsInChildren<SkillButtonController>(true))) controller.enabled = false;
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = owner;
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            Debug.Log("[YT Preview] Created " + ScenePath);
        }
    }
}
