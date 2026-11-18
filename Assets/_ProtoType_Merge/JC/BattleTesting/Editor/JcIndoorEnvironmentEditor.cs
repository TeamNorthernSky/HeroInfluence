using UnityEditor;
using UnityEngine;
namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-09 | 은행/보스 모두 같은 조절 위치와 저장 흐름을 사용하며 기존 전문 편집기를 재사용합니다.
    [CustomEditor(typeof(JcIndoorEnvironment))]
    public sealed class JcIndoorEnvironmentEditor : UnityEditor.Editor
    {
        UnityEditor.Editor lighting,materials,toon,contact;
        // JC | 2026-11-17 | 유리 조절을 긴 조명 항목 아래 숨기지 않고 첫 펼침 항목으로 노출합니다.
        bool lightOpen=true,materialOpen=true,commonOpen=true,toonOpen,contactOpen;
        void OnDisable() { DestroyImmediate(lighting); DestroyImmediate(materials); DestroyImmediate(toon); DestroyImmediate(contact); }
        public override void OnInspectorGUI()
        {
            var control=(JcIndoorEnvironment)target;
            // JC | 2026-11-17 | 에디트 조절기도 전문 인스펙터와 동일한 초안/저장 흐름을 사용합니다.
            if(!control.Draft) { EditorGUILayout.HelpBox("은행/보스 테스트 씬의 JC_IndoorEnvironment (에디트)에서 조절할 수 있습니다.",MessageType.Info); return; }
            if(control.PeriodProfiles)
            {
                using(new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("시간대별 저장 묶음",control.PeriodProfiles,typeof(JcIndoorPeriodProfiles),false);
                if(Application.isPlaying) control.FollowTime=EditorGUILayout.Toggle("전투 시간대 자동 적용",control.FollowTime);
                int period=GUILayout.Toolbar(control.PeriodIndex,JcIndoorPeriodProfiles.Names);
                if(period!=control.PeriodIndex) JcIndoorEditPreview.SelectPeriod(control,period);
                // JC | 2026-11-18 | 공통 재질로 분리한 보스와 기존 은행의 저장 범위를 구별해 안내합니다.
                EditorGUILayout.HelpBox(control.IsBoss
                    ? "아래 버튼은 현재 시간대의 조명·유리·음영 설정을 저장합니다. 공통 실내 표면은 별도 저장합니다. 시간대를 바꿔도 미저장 초안은 유지됩니다."
                    : "아래 버튼은 현재 시간대의 재질·조명·음영 설정을 모두 저장합니다. 시간대를 바꿔도 미저장 초안은 유지됩니다.",MessageType.Info);
                if(GUILayout.Button(control.IsBoss ? "현재 시간대 설정 저장 · 공통 표면 제외" : "현재 시간대 전체 설정을 프로필에 저장")) SavePeriod(control);
                if(GUILayout.Button("현재 시간대 저장값 다시 적용")) ReloadPeriod(control);
            }
            EditorGUILayout.HelpBox(control.IsBoss ? "보스 테스트와 실제 BE490의 공유 설정입니다. 은행 프로필과 독립적입니다." : "BankTestScene과 실제 은행 전투의 공유 설정입니다. 보스 프로필과 독립적입니다.",MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var serialized=new SerializedObject(control.Draft); serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("characterLighting"));
            if(control.IsBoss)
            {
                foreach(var field in new[]{"ssaoEnabled","ssaoIntensity","ssaoRadius","ssaoDirectLightingStrength"})
                    EditorGUILayout.PropertyField(serialized.FindProperty(field));
            }
            if(serialized.ApplyModifiedProperties()) control.ApplyDraft();
            materialOpen=EditorGUILayout.Foldout(materialOpen,control.IsBoss ? "선택 시간대 · 유리와 하이라이트" : "재질 · 표면",true);
            // JC | 2026-11-18 | 보스 공통 표면을 시간대 유리·조명 저장 영역에서 분리합니다. 은행 UI는 기존 경로를 유지합니다.
            if(control.IsBoss && control.BossMaterials) {
                if(materialOpen) {
                    CreateCachedEditor(control.BossMaterials,null,ref materials);
                    ((JcBossBackgroundMaterialsEditor)materials).DrawGlass();
                }
                commonOpen=EditorGUILayout.Foldout(commonOpen,"공통 실내 재질 · 모든 시간대",true);
                if(commonOpen) JcBossBackgroundMaterialsEditor.DrawSurfaces(control.BossMaterials);
            } else if(materialOpen) Draw(control.BankMaterials,ref materials);
            lightOpen=EditorGUILayout.Foldout(lightOpen,"조명 · 환경",true);
            if(lightOpen) Draw(control.Bank ? (Object)control.Bank : control.Boss,ref lighting);
            toonOpen=EditorGUILayout.Foldout(toonOpen,"툰 명암",true);
            if(toonOpen) Draw(control.Toon,ref toon);
            contactOpen=EditorGUILayout.Foldout(contactOpen,"접점 음영",true);
            if(contactOpen) Draw(control.Contact,ref contact);
            // JC | 2026-11-17 | 에디트 모드의 조절 변경도 Scene/Game 뷰 렌더에 바로 전달합니다.
            if(EditorGUI.EndChangeCheck()) { EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); }
            EditorGUILayout.HelpBox("조절은 즉시 반영됩니다. 각 항목의 현재 값 저장 버튼으로 해당 프로필을 저장하세요. 저장하지 않은 초안은 Play 전환·스크립트 재컴파일 때 폐기됩니다. 명암 방식과 주변 음영은 조명 프로필에 함께 저장됩니다.",MessageType.None);
        }
        // JC | 2026-11-18 | 현재 시간대 저장/재로드는 공통 표면을 제외하며 나머지 시간대와 씬은 저장하지 않습니다.
        public static void SavePeriod(JcIndoorEnvironment control)
        {
            foreach(var pair in control.DraftPairs(false))
            {
                Undo.RecordObject(pair.source,"실내 시간대 전체 저장");
                var name=pair.source.name;var flags=pair.source.hideFlags;
                if(pair.draft is Material) JcIndoorEnvironment.CopyDraft(pair.draft,pair.source);
                else EditorUtility.CopySerialized(pair.draft,pair.source);
                pair.source.name=name;pair.source.hideFlags=flags;
                EditorUtility.SetDirty(pair.source);AssetDatabase.SaveAssetIfDirty(pair.source);
            }
            control.RememberDrafts();
        }
        public static void ReloadPeriod(JcIndoorEnvironment control)
        {
            foreach(var pair in control.DraftPairs(false)) JcIndoorEnvironment.CopyDraft(pair.source,pair.draft);
            control.RememberDrafts();control.ApplyAllDrafts();
            EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();
        }
        static void Draw(Object item,ref UnityEditor.Editor editor)
        { if(!item)return; CreateCachedEditor(item,null,ref editor); editor.OnInspectorGUI(); }
    }
}
