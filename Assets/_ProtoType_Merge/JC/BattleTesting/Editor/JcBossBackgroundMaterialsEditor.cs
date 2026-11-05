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
            if (!draft) return;
            EditorGUILayout.LabelField("보스 홀 유리", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("저장 대상", settings.GlassProfile, typeof(Material), false);
            EditorGUILayout.HelpBox("현재 전투에서 바로 확인할 수 있습니다. 저장하면 다음 보스전에도 적용됩니다.", MessageType.Info);
            Color color = draft.GetColor(Tint);
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            if (saturation > 0f && value > 0f) lastHue = hue;
            EditorGUI.BeginChangeCheck();
            var picked = EditorGUILayout.ColorField(new GUIContent("유리 색상"), color, true, false, false);
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

        static void CopyGlass(Material source, Material destination)
        {
            destination.SetColor(Tint, source.GetColor(Tint));
            destination.SetFloat(Face, source.GetFloat(Face));
            destination.SetFloat(Edge, source.GetFloat(Edge));
            foreach(var name in new[]{"_Thickness","_IOR","_RefractionStrength","_Absorption","_ReflectionStrength","_ReflectionRoughness"})
                destination.SetFloat(name,source.GetFloat(name));
        }

        static void FloatControl(Material draft,string property,string label,float min,float max)
        { draft.SetFloat(property,EditorGUILayout.Slider(label,draft.GetFloat(property),min,max)); }

        // JC | 2026-11-05 | 바닥 줄눈과 표면별 약한 반사를 동일한 초안/명시적 저장 방식으로 제공합니다.
        static void DrawSurfaces(JcBossBackgroundMaterials settings)
        {
            if (!settings.SurfaceDraft) return;
            EditorGUILayout.Space(); EditorGUILayout.LabelField("실내 표면 · 큼직한 바닥 줄눈",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("저장 대상",settings.SurfaceSource,typeof(JcBossSurfaceProfile),false);
            var serialized = new SerializedObject(settings.SurfaceDraft); serialized.Update();
            foreach(var field in new[]{"tileSize","jointWidth","jointContrast","jointNormal","tileVariation","surfaces"})
                EditorGUILayout.PropertyField(serialized.FindProperty(field),true);
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
    }
}
