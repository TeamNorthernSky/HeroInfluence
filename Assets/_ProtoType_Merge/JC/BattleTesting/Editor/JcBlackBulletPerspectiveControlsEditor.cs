using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-11 | 정본1의 선택 슬롯과 정본2의 공통 원근감을 같은 조절 오브젝트에서 편집합니다.
    [CustomEditor(typeof(JcBlackBulletPerspectiveControls))]
    public sealed class JcBlackBulletPerspectiveControlsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var controls = ((JcBlackBulletPerspectiveControls)target).Controls;
            if (!controls || !controls.Draft) { EditorGUILayout.HelpBox("공통 프로필을 연결해 주세요.", MessageType.Warning); return; }
            bool authored = controls.Draft.canon == JcBlackBulletCameraCanon.Authored;
            EditorGUILayout.HelpBox(authored ? "정본1: 선택한 스킬·적 칸의 원근감입니다. 같은 촬영값을 모든 전투가 공유합니다." : "정본2: 1은 승인된 현재 구도입니다. 낮추면 원근감이 완화됩니다. 모든 전투에 적용됩니다.", MessageType.Info);
            if (authored) JcBlackBulletCameraControlsEditor.DrawSlot(controls);
            var source = authored ? (Object)controls.Draft.authoredProfile : controls.profile;
            var draft = authored ? (Object)controls.AuthoredDraft : controls.Draft;
            if (!source || !draft) { EditorGUILayout.HelpBox("정본1 저장 프로필을 연결해 주세요.", MessageType.Warning); return; }
            string path = authored ? "skill" + (controls.editingSkill + 1) + ".targets.Array.data[" + controls.editingTarget + "].perspectiveStrength" : "perspectiveStrength";
            var values = new SerializedObject(draft); values.Update();
            EditorGUILayout.PropertyField(values.FindProperty(path), new GUIContent("원근감 비율"));
            if (values.ApplyModifiedProperties()) controls.Reapply();
            EditorGUILayout.LabelField("현재 단계", controls.Phase);
            if (GUILayout.Button("현재 원근감만 프로필에 저장"))
            {
                Undo.RecordObject(source, "JC 원근감 저장");
                var saved = new SerializedObject(source); saved.Update();
                saved.FindProperty(path).floatValue = values.FindProperty(path).floatValue;
                saved.ApplyModifiedProperties(); EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source);
            }
            if (GUILayout.Button("저장된 원근감 불러오기"))
            {
                var saved = new SerializedObject(source); saved.Update();
                values.FindProperty(path).floatValue = saved.FindProperty(path).floatValue;
                values.ApplyModifiedProperties(); controls.Reapply();
            }
            if (GUILayout.Button("원근감 1로 보기"))
            {
                values.FindProperty(path).floatValue = 1;
                values.ApplyModifiedProperties(); controls.Reapply();
            }
        }
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
