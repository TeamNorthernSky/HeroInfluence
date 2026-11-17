using UnityEditor;
using UnityEngine;
namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-17 | 기존 DOF 프로필의 Gaussian 값만 초안으로 편집합니다. 저장 버튼 전에는 공유 에셋을 변경하지 않습니다.
    [CustomEditor(typeof(JcBossDistantDof))]
    public sealed class JcBossDistantDofEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var control=(JcBossDistantDof)target;
            using(new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("공유 저장 프로필",control.Source,typeof(JC.Env.JcDofProfile),false);
            if(!control.Draft) { EditorGUILayout.HelpBox("DOF 프로필 연결이 필요합니다.",MessageType.Info);return; }
            EditorGUILayout.HelpBox("보스 원경 전용 Gaussian입니다. 시작 거리는 실내·캐릭터의 가장 먼 깊이보다 3m 뒤로 자동 보정하며, 시작~종료 간격은 유지합니다. 탐사·은행 DOF는 변경하지 않습니다.",MessageType.Info);
            var draft=new SerializedObject(control.Draft);draft.Update();var settings=draft.FindProperty("settings");
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("gaussianStart"),new GUIContent("최소 블러 시작 거리"));
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("gaussianEnd"),new GUIContent("기준 최대 블러 거리"));
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("gaussianMaxRadius"),new GUIContent("최대 블러 반경"));
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("highQualitySampling"),new GUIContent("고품질 샘플링"));
            draft.ApplyModifiedProperties();
            EditorGUILayout.LabelField("실내/캐릭터 최대 깊이",control.ProtectedDepth.ToString("0.0"));
            EditorGUILayout.LabelField("실제 시작 / 종료",$"{control.ActualStart:0.0} / {control.ActualEnd:0.0}");
            EditorGUILayout.HelpBox(control.Status,MessageType.Info);
            if(GUILayout.Button("현재 값을 프로필에 저장"))
            {
                Undo.RecordObject(control.Source,"JC 보스 원경 DOF 저장");
                control.Source.settings=control.Draft.settings.Sanitized();
                EditorUtility.SetDirty(control.Source);AssetDatabase.SaveAssetIfDirty(control.Source);
            }
            if(GUILayout.Button("저장값 다시 불러오기"))control.ReloadDraft();
            EditorGUILayout.HelpBox("JC_TestStage/JC_BossDistantDOF에서 조절합니다. 플레이 종료 전 프로필 저장을 누르면 본게임 보스전과 테스트에 함께 유지됩니다.",MessageType.Info);
            if(Application.isPlaying)Repaint();
        }
    }
}
