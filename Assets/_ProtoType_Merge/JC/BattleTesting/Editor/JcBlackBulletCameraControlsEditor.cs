using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-11 | 정본 선택과 두 초안의 편집/저장을 한 곳으로 통합합니다. 정본1 저장값을 전장별로 복사하지 않습니다.
    [CustomEditor(typeof(JcBlackBulletCameraControls))]
    public sealed class JcBlackBulletCameraControlsEditor : UnityEditor.Editor
    {
        public static readonly string[] Skills = { "1 · 에임 샷", "2 · 브레이크 불릿", "3 · 트윈 트리거", "4 · 데드엔드" };
        public static readonly string[] Targets = { "적 전열 1", "적 전열 2", "적 전열 3", "적 후열 1", "적 후열 2", "적 후열 3" };
        public static void DrawSlot(JcBlackBulletCameraControls controls)
        {
            controls.editingSkill = EditorGUILayout.Popup("편집할 스킬", controls.editingSkill, Skills);
            controls.editingTarget = EditorGUILayout.Popup("편집할 적 칸", controls.editingTarget, Targets);
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
            serializedObject.ApplyModifiedProperties();
            var controls = (JcBlackBulletCameraControls)target;
            if (!controls.Draft) { EditorGUILayout.HelpBox("공통 저장 프로필을 연결해 주세요.", MessageType.Warning); return; }
            EditorGUILayout.HelpBox("모든 전투가 같은 정본을 사용합니다. 기본값은 정본2입니다. 정본 선택 변경은 다음 공격부터, 현재 정본의 조절값 변경은 진행 중인 공격에도 반영됩니다. 코어 스킬은 제외합니다.\n원본은 아래 저장 버튼으로 보존합니다.", MessageType.Info);
            var values = new SerializedObject(controls.Draft); values.Update();
            EditorGUILayout.PropertyField(values.FindProperty("canon"));
            EditorGUILayout.PropertyField(values.FindProperty("transitionSeconds"));
            bool authored = (JcBlackBulletCameraCanon)values.FindProperty("canon").intValue == JcBlackBulletCameraCanon.Authored;
            if (authored) EditorGUILayout.PropertyField(values.FindProperty("authoredProfile"));
            else DrawPropertiesExcluding(values, "m_Script", "canon", "transitionSeconds", "authoredProfile", "perspectiveStrength");
            if (values.ApplyModifiedProperties()) controls.Reapply();
            if (authored)
            {
                DrawSlot(controls);
                if (controls.AuthoredDraft)
                {
                    var shots = new SerializedObject(controls.AuthoredDraft); shots.Update();
                    var shot = shots.FindProperty("skill" + (controls.editingSkill + 1)).FindPropertyRelative("targets").GetArrayElementAtIndex(controls.editingTarget);
                    EditorGUILayout.PropertyField(shot.FindPropertyRelative("position"), new GUIContent("촬영 기준 위치", "기존 은행 촬영 좌표입니다. 현재 전투의 진형 원점으로 자동 변환합니다."));
                    EditorGUILayout.PropertyField(shot.FindPropertyRelative("rotation"), new GUIContent("촬영 기준 회전"));
                    EditorGUILayout.PropertyField(shot.FindPropertyRelative("fieldOfView"), new GUIContent("촬영 기준 FOV"));
                    if (shots.ApplyModifiedProperties()) controls.Reapply();
                    if (GUILayout.Button("선택한 정본1 구도만 저장")) SaveShot(controls);
                }
                else EditorGUILayout.HelpBox("정본1 저장 프로필을 연결해 주세요.", MessageType.Error);
                if (GUILayout.Button("정본1 촬영 툴 열기")) JcBankSkillCameraWindow.Open();
                // 두 정본에 공통인 가시성 보호는 정본1에서도 편집할 수 있습니다.
                var safety = new SerializedObject(controls.Draft); safety.Update();
                foreach (string name in new[] { "edgeMarginPixels", "maximumFov", "uiInsets", "skill1ImpactRadius", "skill2ImpactRadius", "skill3ImpactRadius", "skill4ImpactRadius", "projectileRadius", "targetMotionMargin", "damageDigits" })
                    EditorGUILayout.PropertyField(safety.FindProperty(name));
                if (safety.ApplyModifiedProperties()) controls.Reapply();
            }
            EditorGUILayout.HelpBox("원근감은 하위 JC_Perspective에서 조절합니다. 정본1은 선택한 스킬·적 칸, 정본2는 전체 배치에 적용됩니다.", MessageType.None);
            EditorGUILayout.LabelField("현재 단계", controls.Phase);
            if (GUILayout.Button("정본 선택·공통 설정 저장"))
            {
                Undo.RecordObject(controls.profile, "JC 공통 카메라 저장");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(controls.Draft), controls.profile);
                EditorUtility.SetDirty(controls.profile); AssetDatabase.SaveAssetIfDirty(controls.profile);
            }
            if (GUILayout.Button("두 정본 저장값 다시 불러오기")) controls.ReloadProfile();
        }
        public static void SaveShot(JcBlackBulletCameraControls controls)
        {
            if (controls.EditingShot == null || !controls.Draft.authoredProfile) return;
            var source = controls.Draft.authoredProfile;
            Undo.RecordObject(source, "JC 정본1 선택 구도 저장");
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(controls.EditingShot), source.GetSlot(controls.editingSkill, controls.editingTarget));
            EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source);
        }
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
