using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-16 | 플레이 초안은 즉시 미리보기하고 명시적 저장만 본게임/테스트 공통 프로필에 반영합니다.
    [CustomEditor(typeof(JcBossLayoutController))]
    public sealed class JcBossLayoutControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var control = (JcBossLayoutController)target;
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("저장 대상", control.Source, typeof(JcBossLayoutProfile), false);
            if (!control.Draft) { EditorGUILayout.HelpBox(control.Status, MessageType.Info); return; }
            EditorGUILayout.HelpBox("보스전 전용입니다. 전체 이동에 캐릭터/오브젝트의 개별 이동을 더합니다. X는 좌우, Z는 전후이며 타일 단위입니다. 카메라·벽·기둥·바닥 타일·조명·창밖 풍경은 이동하지 않습니다.", MessageType.Info);
            EditorGUILayout.LabelField("타일 한 칸", control.TileSize.ToString("0.###") + " 월드 단위");
            var draft = new SerializedObject(control.Draft); draft.Update();
            DrawOffset(draft.FindProperty("allOffset"), "전체 배치");
            DrawOffset(draft.FindProperty("characterOffset"), "캐릭터·전투 그리드 추가 이동");
            DrawOffset(draft.FindProperty("objectOffset"), "책상·소품·카펫·바닥 문양 추가 이동");
            if (draft.ApplyModifiedProperties()) control.ApplyDraft();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(control.Status, control.Pending ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.HelpBox("스킬·턴 연출 중에는 종료 후 반영합니다. 플레이 종료 전에 저장 버튼을 눌러야 다음 실행에도 유지됩니다. 본게임 보스전과 보스 테스트씬이 같은 프로필을 사용합니다.", MessageType.Info);
            if (GUILayout.Button("현재 값을 프로필에 저장") && control.ApplyDraft())
            {
                Undo.RecordObject(control.Source, "JC 보스 배치 저장");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(control.Draft), control.Source);
                EditorUtility.SetDirty(control.Source); AssetDatabase.SaveAssetIfDirty(control.Source);
            }
            if (GUILayout.Button("저장값 다시 불러오기")) control.ReloadDraft();
            if (GUILayout.Button("오프셋 0으로 미리보기 (저장하지 않음)"))
            {
                Undo.RecordObject(control.Draft, "JC 보스 배치 오프셋 초기화");
                control.Draft.allOffset = control.Draft.characterOffset = control.Draft.objectOffset = Vector2.zero;
                control.ApplyDraft();
            }
            if (Application.isPlaying) Repaint();
        }
        static void DrawOffset(SerializedProperty property, string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var value = property.vector2Value;
            using (new EditorGUILayout.HorizontalScope())
            {
                float oldWidth = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 20;
                value.x = EditorGUILayout.FloatField("X", value.x); value.y = EditorGUILayout.FloatField("Z", value.y);
                EditorGUIUtility.labelWidth = oldWidth;
            }
            property.vector2Value = value;
        }
    }
}
