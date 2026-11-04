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
            draft.SetFloat(Face, EditorGUILayout.Slider("정면 불투명도", draft.GetFloat(Face), 0f, 1f));
            draft.SetFloat(Edge, EditorGUILayout.Slider("비스듬한 각도 불투명도", draft.GetFloat(Edge), 0f, 1f));
            if (GUILayout.Button("현재 유리 값을 프로필에 저장"))
            {
                var profile = settings.GlassProfile;
                Undo.RecordObject(profile, "보스 홀 유리 설정 저장");
                CopyGlass(draft, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
            if (GUILayout.Button("유리 프로필 불러오기 · 즉시 적용")) CopyGlass(settings.GlassProfile, draft);
        }

        static void CopyGlass(Material source, Material destination)
        {
            destination.SetColor(Tint, source.GetColor(Tint));
            destination.SetFloat(Face, source.GetFloat(Face));
            destination.SetFloat(Edge, source.GetFloat(Edge));
        }
    }
}
