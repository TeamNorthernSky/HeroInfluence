using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JC.VFX.EditorTools
{
    /// <summary>Blender 작업 파일에서 내보낸 저스티스 등장 도발을 안정적인 .anim 참조에 반영합니다.</summary>
    public static class JusticeTauntAnimationTools
    {
        public const string FbxPath = "Assets/RenderFX/_Seam/Animation/Justice/Blender/JC_EnterJustice_Taunt.fbx";
        public const string ClipPath = "Assets/RenderFX/_Seam/Animation/Justice/JC_EnterJustice_Taunt.anim";
        private const string ReferencePath = "Assets/RenderFX/_Seam/Data/FirstPass/Animation/FP_Fighter_Taunt.anim";
        private const string MenuPath = "JC/Animation/Justice/Blender 도발을 작업 클립에 반영";

        [MenuItem(MenuPath)]
        public static void ReimportAndBake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이를 종료한 뒤 애니메이션을 반영하세요.");

            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            var reference = AssetDatabase.LoadAssetAtPath<AnimationClip>(ReferencePath);
            if (importer == null || reference == null || importer.defaultClipAnimations.Length != 1
                || importer.clipAnimations.Length != 1)
                throw new InvalidOperationException("도발 FBX의 단일 Take와 원본 도발 클립이 필요합니다.");

            // .meta의 보정된 Humanoid 기준 자세는 유지합니다. 편집으로 늘어난 프레임도 반영합니다.
            var take = importer.defaultClipAnimations[0];
            var clip = importer.clipAnimations.Single();
            clip.firstFrame = take.firstFrame;
            clip.lastFrame = take.lastFrame;
            clip.takeName = take.takeName;
            clip.loopTime = false;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = false;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();
            var imported = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (avatar == null || !avatar.isValid || !avatar.isHuman || imported == null || !imported.humanMotion || imported.length <= 0f)
                throw new InvalidOperationException("도발 FBX의 Humanoid 가져오기에 실패했습니다. 기존 작업 클립은 유지합니다.");

            var destination = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (destination == null)
            {
                destination = new AnimationClip();
                AssetDatabase.CreateAsset(destination, ClipPath);
            }

            // 같은 .anim을 갱신하므로 Animator의 참조와 GUID가 바뀌지 않습니다.
            EditorUtility.CopySerialized(imported, destination);
            destination.name = "JC_EnterJustice_Taunt";
            destination.hideFlags = HideFlags.None;
            var events = AnimationUtility.GetAnimationEvents(reference);
            foreach (var animationEvent in events)
                animationEvent.time *= imported.length / reference.length;
            AnimationUtility.SetAnimationEvents(destination, events);
            EditorUtility.SetDirty(destination);
            AssetDatabase.SaveAssetIfDirty(destination);
            Debug.Log($"[Justice Animation] 도발 반영 완료: {destination.length:0.###}초, {destination.frameRate:0.###}fps, 이벤트 {events.Length}개");
        }

        [MenuItem(MenuPath, true)]
        private static bool CanBake() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
