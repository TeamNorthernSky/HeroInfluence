using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    [CustomEditor(typeof(JcBossCamera))]
    public sealed class JcBossCameraEditor : UnityEditor.Editor
    {

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
            // JC | 2026-11-16 | 경계와 편집 미리보기 분기를 없애 저장값과 실제 기본뷰가 일치하도록 합니다.
            EditorGUILayout.HelpBox("보스 스킬 연출은 이 기본뷰를 기준으로 계산합니다. 무대 경계에 의한 자동 보정은 없습니다. 스킬 중 기본뷰 수정은 복귀 후 적용됩니다.", MessageType.Info);
            // JC | 2026-11-16 | 저장 기준을 읽기 전용으로 안내합니다. 배치 저장/카메라 저장이 기준을 자동 재설정하지 않도록 보존합니다.
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector2Field("카메라 기준 배치 (타일)", settings.Draft.layoutReference);
                EditorGUILayout.Vector3Field("기본·스킬 공통 이동량", settings.LayoutOffset);
            }
            EditorGUILayout.HelpBox("JC_BossLayout의 전체+캐릭터 이동 차이만큼 기본 카메라가 먼저 이동하고, 스킬 연출은 그 기본뷰를 사용합니다. 오브젝트 전용 이동은 제외합니다. 배치 저장 후 재실행해도 같은 구도를 유지합니다.", MessageType.Info);
            if (draft.ApplyModifiedProperties()) settings.ApplyDraft();
            using (new EditorGUI.DisabledScope(!settings.CanCaptureBase))
                if (GUILayout.Button("현재 화면에서 기본 구도 가져오기")) settings.CaptureBase();
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
                settings.ApplyDraft();
            }
        }

        static void Copy(JcBossCameraProfile from, JcBossCameraProfile to)
        {
            to.viewA = from.viewA.Copy(); to.viewB = from.viewB.Copy();
            // JC | 2026-11-16 | 프로필 저장/불러오기에서 고정된 배치 기준을 함께 보존합니다.
            to.layoutReference = from.layoutReference;
        }
    }
}
