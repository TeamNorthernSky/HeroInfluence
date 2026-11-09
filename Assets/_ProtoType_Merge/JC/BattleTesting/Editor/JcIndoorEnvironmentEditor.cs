using UnityEditor;
using UnityEngine;
namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-09 | 은행/보스 모두 같은 조절 위치와 저장 흐름을 사용하며 기존 전문 편집기를 재사용합니다.
    [CustomEditor(typeof(JcIndoorEnvironment))]
    public sealed class JcIndoorEnvironmentEditor : UnityEditor.Editor
    {
        UnityEditor.Editor lighting,materials,toon,contact;
        bool lightOpen=true,materialOpen,toonOpen,contactOpen;
        void OnDisable() { DestroyImmediate(lighting); DestroyImmediate(materials); DestroyImmediate(toon); DestroyImmediate(contact); }
        public override void OnInspectorGUI()
        {
            var control=(JcIndoorEnvironment)target;
            if(!control.Draft) { EditorGUILayout.HelpBox("실내 전투 진입 후 초기화됩니다.",MessageType.Info); return; }
            EditorGUILayout.HelpBox(control.IsBoss ? "보스 테스트와 실제 BE490의 공유 설정입니다. 은행 프로필과 독립적입니다." : "BankTestScene과 실제 은행 전투의 공유 설정입니다. 보스 프로필과 독립적입니다.",MessageType.Info);
            var serialized=new SerializedObject(control.Draft); serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("characterLighting"));
            if(control.IsBoss)
            {
                foreach(var field in new[]{"ssaoEnabled","ssaoIntensity","ssaoRadius","ssaoDirectLightingStrength"})
                    EditorGUILayout.PropertyField(serialized.FindProperty(field));
            }
            if(serialized.ApplyModifiedProperties()) control.ApplyDraft();
            lightOpen=EditorGUILayout.Foldout(lightOpen,"조명 · 환경",true);
            if(lightOpen) Draw(control.Bank ? (Object)control.Bank : control.Boss,ref lighting);
            materialOpen=EditorGUILayout.Foldout(materialOpen,"재질 · 표면"+(control.IsBoss?" · 유리":""),true);
            if(materialOpen) Draw(control.BankMaterials ? (Object)control.BankMaterials : control.BossMaterials,ref materials);
            toonOpen=EditorGUILayout.Foldout(toonOpen,"툰 명암",true);
            if(toonOpen) Draw(control.Toon,ref toon);
            contactOpen=EditorGUILayout.Foldout(contactOpen,"접점 음영",true);
            if(contactOpen) Draw(control.Contact,ref contact);
            EditorGUILayout.HelpBox("조절은 즉시 반영됩니다. 각 항목의 현재 값 저장 버튼으로 해당 프로필을 저장한 뒤 Play를 종료하세요. 명암 방식과 주변 음영은 조명 프로필에 함께 저장됩니다.",MessageType.None);
        }
        static void Draw(Object item,ref UnityEditor.Editor editor)
        { if(!item)return; CreateCachedEditor(item,null,ref editor); editor.OnInspectorGUI(); }
    }
}
