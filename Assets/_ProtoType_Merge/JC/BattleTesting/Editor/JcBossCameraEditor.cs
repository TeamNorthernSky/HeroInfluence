using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    [CustomEditor(typeof(JcBossCamera))]
    public sealed class JcBossCameraEditor : UnityEditor.Editor
    {
        const string WarningPref = "HeroInfluence.JC.BossCamera.ShowStageWarning";

        public override void OnInspectorGUI()
        {
            var settings = (JcBossCamera)target;
            if (!settings.Draft) return;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("저장 대상",settings.Source,typeof(JcBossCameraProfile),false);
            var draft = new SerializedObject(settings.Draft);
            draft.Update();
            EditorGUILayout.HelpBox("수정값은 현재 전투에 바로 반영됩니다. 플레이 종료 후에도 유지하려면 프로필에 저장하세요.",MessageType.Info);
            EditorGUILayout.PropertyField(draft.FindProperty("viewA"),new GUIContent("보스전 기본 카메라"),true);
            EditorGUILayout.PropertyField(draft.FindProperty("aim"),true);
            EditorGUILayout.LabelField("실내 무대 경계", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(draft.FindProperty("rightWallX"));
            EditorGUILayout.PropertyField(draft.FindProperty("rightMargin"));
            if (draft.ApplyModifiedProperties()) settings.PreviewEditorDraft();
            bool show = EditorPrefs.GetBool(WarningPref,true);
            bool toggle = EditorGUILayout.Toggle("무대 밖 노출 경고 표시",show);
            if (toggle != show) EditorPrefs.SetBool(WarningPref,toggle);
            if (toggle && settings.IsBasePreview && settings.PreviewOutsideStage)
                EditorGUILayout.HelpBox("이 기본 구도에서는 무대 밖이 보입니다. 편집값은 그대로 미리보기 중입니다.",MessageType.Warning);
            if (settings.IsBasePreview)
            {
                if (GUILayout.Button("편집 미리보기 종료 · 실제 전투 구도 보기")) settings.EndBasePreview();
            }
            else if (settings.CanCaptureBase && GUILayout.Button("기본 구도 편집 미리보기")) settings.PreviewEditorDraft();
            if (toggle && !settings.IsBasePreview && settings.BoundaryChangePending)
                EditorGUILayout.HelpBox("현재 구도가 새 벽 범위에 들어오면 좁힌 경계가 적용됩니다.",MessageType.Info);
            if (toggle && !settings.IsBasePreview && settings.AtBoundary)
                EditorGUILayout.HelpBox("요청한 이동이 무대 경계에 닿았습니다. 카메라는 이동 가능한 마지막 구도에 있습니다.",MessageType.Info);
            using (new EditorGUI.DisabledScope(!settings.CanCaptureBase))
                if (GUILayout.Button("현재 화면에서 기본 구도 가져오기")) settings.CaptureBase();
            using (new EditorGUI.DisabledScope(!settings.IsAimShot))
                if (GUILayout.Button("현재 화면에서 블랙 불릿 공통 구도 가져오기")) settings.CaptureAim();
            if (GUILayout.Button("현재 값을 프로필에 저장"))
            {
                Undo.RecordObject(settings.Source,"보스전 카메라 프로필 저장");
                Copy(settings.Draft,settings.Source);
                EditorUtility.SetDirty(settings.Source);
                AssetDatabase.SaveAssetIfDirty(settings.Source);
            }
            if (GUILayout.Button("프로필 불러오기 · 즉시 적용"))
            {
                Copy(settings.Source,settings.Draft);
                settings.PreviewEditorDraft();
            }
        }

        static void Copy(JcBossCameraProfile from, JcBossCameraProfile to)
        {
            to.rightWallX = from.rightWallX; to.rightMargin = from.rightMargin;
            to.viewA = from.viewA.Copy(); to.viewB = from.viewB.Copy();
            to.aim = JsonUtility.FromJson<JcBossCameraProfile.AimShot>(JsonUtility.ToJson(from.aim));
        }
    }
}
