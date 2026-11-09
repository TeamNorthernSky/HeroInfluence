using UnityEditor;
using UnityEngine;
namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-09 | 은행/보스에서 재사용하므로 안내는 현재 연결된 프로필 범위로 표시합니다.
    [CustomEditor(typeof(JcBankToonController))]
    public sealed class JcBankToonControllerEditor : UnityEditor.Editor
    {
        void OnEnable() => Undo.undoRedoPerformed += Reapply;
        void OnDisable() => Undo.undoRedoPerformed -= Reapply;
        void Reapply() { if(target)((JcBankToonController)target).ApplyDraft(); }
        public override void OnInspectorGUI()
        {
            var c=(JcBankToonController)target;
            using(new EditorGUI.DisabledScope(true))EditorGUILayout.ObjectField(new GUIContent("공유 저장 프로필","현재 전투의 본게임과 테스트가 공유하며 다른 전투와는 독립적입니다."),c.Source,typeof(JcBankToonProfile),false);
            if(!c.Draft)return;
            EditorGUILayout.HelpBox("툰 명암을 조절합니다. 후면 보조광은 JC_IndoorEnvironment의 조명 · 환경 목록에서 조절하세요. 유지할 값은 플레이 종료 전에 저장하세요.",MessageType.Info);
            using(var so=new SerializedObject(c.Draft)) {
                so.Update();
                foreach(var name in new[]{"toonEnabled","background","characters","strength","steps","softness","shadowTint"})EditorGUILayout.PropertyField(so.FindProperty(name));
                if(so.ApplyModifiedProperties())c.ApplyDraft();
            }
            if(GUILayout.Button(new GUIContent("현재 툰 값을 공유 프로필에 저장","현재 툰 초안만 저장하여 현재 전투의 본게임과 테스트에 적용합니다. 조명값은 저장하지 않습니다."))) {
                Undo.RecordObject(c.Source,"은행 툰 저장");string name=c.Source.name;var flags=c.Source.hideFlags;EditorUtility.CopySerialized(c.Draft,c.Source);c.Source.name=name;c.Source.hideFlags=flags;EditorUtility.SetDirty(c.Source);AssetDatabase.SaveAssetIfDirty(c.Source);
            }
            if(GUILayout.Button(new GUIContent("저장값 다시 적용","저장하지 않은 초안을 버리고 공유 프로필을 다시 읽습니다.")))c.ReloadProfile();
        }
    }
}
