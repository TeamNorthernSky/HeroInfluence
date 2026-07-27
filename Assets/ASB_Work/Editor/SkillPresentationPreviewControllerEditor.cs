using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillPresentationPreviewController))]
public class SkillPresentationPreviewControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (SkillPresentationPreviewController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview Control", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Actor", controller.ActorName);
        EditorGUILayout.LabelField("Target", controller.TargetName);
        EditorGUILayout.LabelField("State", controller.IsPlaying ? "실행 중" : "대기");

        using (new EditorGUI.DisabledScope(!Application.isPlaying || controller.IsPlaying))
        {
            if (GUILayout.Button("▶ Play Selected Skill", GUILayout.Height(30)))
            {
                controller.PlaySelectedSkill();
            }
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Reset Preview"))
            {
                controller.ResetPreview();
            }
        }
    }
}
