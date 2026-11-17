using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    [CustomEditor(typeof(JcBossBackgroundMaterials))]
    public sealed class JcBossBackgroundMaterialsEditor : UnityEditor.Editor
    {
        static readonly int Tint = Shader.PropertyToID("_Tint");
        static readonly int Face = Shader.PropertyToID("_FaceAlpha");
        static readonly int Edge = Shader.PropertyToID("_EdgeAlpha");
        float lastHue;

        public override void OnInspectorGUI()
        {
            var settings = (JcBossBackgroundMaterials)target;
            var draft = settings.GlassDraft;
            // JC | 2026-11-17 | 미초기화 상태도 빈 인스펙터 대신 실제 사용 위치를 안내합니다.
            if (!draft)
            {
                EditorGUILayout.HelpBox("보스 전투 진입 후 JC_IndoorEnvironment 또는 하위 JC_Materials에서 유리를 조절할 수 있습니다.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("보스 홀 유리", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("저장 대상", settings.GlassProfile, typeof(Material), false);
            EditorGUILayout.HelpBox("현재 전투의 초안에 즉시 반영됩니다. 아래 저장 버튼을 눌러야 Play 종료 후와 다음 보스전에도 유지됩니다. 불투명도 0은 투과 유리, 1은 배경을 가리는 틴트색입니다.", MessageType.Info);
            // JC | 2026-11-17 | 색의 흡수량과 별도로 배경 투과를 가리는 불투명도를 조절합니다.
            FloatControl(draft, "_Opacity", "불투명도", 0, 1);
            Color color = draft.GetColor(Tint);
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            if (saturation > 0f && value > 0f) lastHue = hue;
            EditorGUI.BeginChangeCheck();
            var picked = EditorGUILayout.ColorField(new GUIContent("유리 틴트"), color, true, false, false);
            if (EditorGUI.EndChangeCheck())
            {
                draft.SetColor(Tint, picked);
                Color.RGBToHSV(picked, out lastHue, out saturation, out value);
            }
            EditorGUI.BeginChangeCheck();
            lastHue = EditorGUILayout.Slider("색상 H (도)", lastHue * 360f, 0f, 360f) / 360f;
            saturation = EditorGUILayout.Slider("채도 S", saturation, 0f, 1f);
            value = EditorGUILayout.Slider("명도 V", value, 0f, 1f);
            if (EditorGUI.EndChangeCheck()) draft.SetColor(Tint, Color.HSVToRGB(lastHue, saturation, value));
            // JC | 2026-11-05 | 유리의 실제 두께와 굴절/투과색/반사를 분리해 조절하며 원본 프로필은 저장 버튼으로만 변경합니다.
            draft.SetFloat(Face, EditorGUILayout.Slider("정면 착색량", draft.GetFloat(Face), 0f, 1f));
            draft.SetFloat(Edge, EditorGUILayout.Slider("비스듬한 각도 착색량", draft.GetFloat(Edge), 0f, 1f));
            FloatControl(draft,"_Thickness","유리 두께",.005f,.3f);
            FloatControl(draft,"_IOR","굴절률",1,2);
            FloatControl(draft,"_RefractionStrength","굴절 표현 강도",0,3);
            FloatControl(draft,"_Absorption","착색 강도",0,8);
            FloatControl(draft,"_ReflectionStrength","표면 반사 강도",0,1);
            FloatControl(draft,"_ReflectionRoughness","표면 반사 거칠기",0,1);
            // JC | 2026-11-17 | 직접광의 광택과 환경 반사를 구별해 저장하고 시간대별 유리에도 같은 항목을 제공합니다.
            EditorGUILayout.Space();EditorGUILayout.LabelField("광택 · 직접광 하이라이트",EditorStyles.boldLabel);
            FloatControl(draft,"_GlossStrength","광택 강도",0,4);
            FloatControl(draft,"_GlossSmoothness","광택 선명도",0,1);
            draft.SetColor("_GlossTint",EditorGUILayout.ColorField("광택 색상",draft.GetColor("_GlossTint")));
            settings.ApplyGlass();
            if (GUILayout.Button("현재 유리 값을 프로필에 저장"))
            {
                var profile = settings.GlassProfile;
                Undo.RecordObject(profile, "보스 홀 유리 설정 저장");
                CopyGlass(draft, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
            if (GUILayout.Button("유리 프로필 불러오기 · 즉시 적용")) { CopyGlass(settings.GlassProfile, draft); settings.ApplyGlass(); }
            DrawSurfaces(settings);
        }

        // JC | 2026-11-17 | 에디트/플레이/시간대 캐시가 같은 유리 속성 목록으로 저장·불러오기합니다.
        static void CopyGlass(Material source, Material destination) => JcBossBackgroundMaterials.CopyGlassValues(source,destination);

        static void FloatControl(Material draft,string property,string label,float min,float max)
        { draft.SetFloat(property,EditorGUILayout.Slider(label,draft.GetFloat(property),min,max)); }

        // JC | 2026-11-05 | 바닥 줄눈과 표면별 약한 반사를 동일한 초안/명시적 저장 방식으로 제공합니다.
        static void DrawSurfaces(JcBossBackgroundMaterials settings)
        {
            if (!settings.SurfaceDraft) return;
            EditorGUILayout.Space(); EditorGUILayout.LabelField("실내 표면 · 큼직한 바닥 줄눈",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("저장 대상",settings.SurfaceSource,typeof(JcBossSurfaceProfile),false);
            var serialized = new SerializedObject(settings.SurfaceDraft); serialized.Update();
            foreach(var field in new[]{"tileSize","tileOffset","jointWidth","jointContrast","jointNormal","tileVariation"})
                EditorGUILayout.PropertyField(serialized.FindProperty(field),true);
            // JC | 2026-11-17 | 전체 복원도 저장된 현재 시간대 초안만 수정하고 원본 프로필은 저장 버튼으로 반영합니다.
            if (GUILayout.Button("실내 표면 색상 모두 기본값으로 되돌리기"))
            {
                var list = serialized.FindProperty("surfaces");
                for (int i = 0; i < list.arraySize; i++)
                {
                    var item = list.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("color").colorValue = item.FindPropertyRelative("defaultColor").colorValue;
                }
            }
            DrawSurfaceList(serialized.FindProperty("surfaces"));
            if(serialized.ApplyModifiedProperties())settings.ApplySurfaces();
            if(GUILayout.Button("현재 실내 표면 값을 프로필에 저장"))
            {
                Undo.RecordObject(settings.SurfaceSource,"보스 실내 표면 저장");
                var name=settings.SurfaceSource.name;
                EditorUtility.CopySerialized(settings.SurfaceDraft,settings.SurfaceSource);
                settings.SurfaceSource.name=name;settings.SurfaceSource.hideFlags=HideFlags.None;
                EditorUtility.SetDirty(settings.SurfaceSource);AssetDatabase.SaveAssetIfDirty(settings.SurfaceSource);
            }
            if(GUILayout.Button("실내 표면 저장값 다시 적용"))settings.ReloadSurfaces();
            EditorGUILayout.HelpBox("유리와 실내 표면은 각각 저장합니다. 두 프로필 모두 테스트와 실제 BE490에 적용됩니다. 줄눈은 얕은 표면 표현이며 바닥 높이·전투 셀은 바꾸지 않습니다.",MessageType.Info);
        }

        // JC | 2026-11-05 | 숫자 항목에 가려졌던 분리 카펫과 바닥 전용 설정을 대상 이름으로 바로 찾게 표시합니다.
        static void DrawSurfaceList(SerializedProperty surfaces)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("표면별 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(surfaces.FindPropertyRelative("Array.size"), new GUIContent("설정 수"));
            for (int i = 0; i < surfaces.arraySize; i++)
            {
                var item = surfaces.GetArrayElementAtIndex(i);
                var source = item.FindPropertyRelative("source").objectReferenceValue as Material;
                string material = source ? source.name : "재질 미지정";
                string title = material;
                switch (material)
                {
                    case "DeskCarpet": title = "책상 아래 붉은 카펫 · DeskCarpet"; break;
                    case "WovenEmblem": title = "중앙 카펫 · WovenEmblem"; break;
                    case "FurnitureLacquer": title = "책상 · FurnitureLacquer"; break;
                    case "HallStone": title = item.FindPropertyRelative("rendererPath").stringValue == "FinalStage/Floor"
                        ? "홀 바닥 · HallStone" : item.FindPropertyRelative("rendererPath").stringValue == "FinalStage/Pillar"
                        ? "기둥 석재 · HallStone" : "홀 석재 · HallStone"; break; // JC | 2026-11-17 | 기둥 전용 색을 바닥과 구별합니다.
                    case "WarmStone": title = "웜스톤 · WarmStone"; break;
                }
                item.isExpanded = EditorGUILayout.Foldout(item.isExpanded, title, true);
                if (!item.isExpanded) continue;
                using (new EditorGUI.IndentLevelScope())
                {
                    // JC | 2026-11-17 | 색 변경과 기본색 복원을 같은 행 가까이에 제공하며 기준색은 편집하지 못하게 숨깁니다.
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("color"));
                    if (GUILayout.Button("이 표면 색상 기본값으로 되돌리기"))
                        item.FindPropertyRelative("color").colorValue = item.FindPropertyRelative("defaultColor").colorValue;
                    foreach (var field in new[] { "source", "rendererPath", "additionalRendererPaths", "colorOnly", "floorTiles", "smoothness", "specular", "variation" })
                        EditorGUILayout.PropertyField(item.FindPropertyRelative(field), true);
                }
            }
        }
    }
}
