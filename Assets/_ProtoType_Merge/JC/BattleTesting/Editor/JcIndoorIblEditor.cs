using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-12 | 두 전문 조명 인스펙터에서 동일한 초안 UI와 검증 안내를 사용합니다.
    public static class JcIndoorIblEditor
    {
        public static void Draw(SerializedObject serialized, JcIndoorIblSettings settings, bool bank)
        {
            var property=serialized.FindProperty("ibl");
            EditorGUILayout.PropertyField(property,new GUIContent("실내 전용 IBL","실내 환경광과 반사를 조절하고 아래의 기존 조명 저장 버튼으로 보존합니다."),false);
            if(!property.isExpanded) return;
            EditorGUI.indentLevel++;
            foreach(var name in new[]{"enabled","environment","diffuseIntensity","specularIntensity","tint","rotation"})
                EditorGUILayout.PropertyField(property.FindPropertyRelative(name));
            if(bank) EditorGUILayout.PropertyField(property.FindPropertyRelative("bankProbeBlend"));
            EditorGUI.indentLevel--;
            if(settings != null && settings.enabled && (!settings.environment || !settings.environment.Ready))
                EditorGUILayout.HelpBox("구워진 환경 데이터가 없습니다. IBL은 적용되지 않으며 기존 조명 방식을 유지합니다. 환경 에셋에서 데이터를 구운 뒤 연결하세요.",MessageType.Warning);
            EditorGUILayout.HelpBox("켜면 기존 단색 환경광 대신 전용 IBL을 사용합니다. 직접 라이트와 창밖 환경은 유지합니다. 캐릭터의 '부드러운 명암' 방식은 기존 연출을 유지하며, '실내 실제 조명' 방식에서 IBL을 받습니다.",MessageType.None);
        }
    }

    [CustomEditor(typeof(JcIndoorIblAsset))]
    public sealed class JcIndoorIblAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var item=(JcIndoorIblAsset)target;
            EditorGUILayout.HelpBox(item.Ready ? "구운 HDR 반사와 확산광 데이터가 있습니다. 원본 변경 후 다시 구워주세요." : "환경 데이터를 먼저 구워야 전투에서 사용할 수 있습니다.",MessageType.Info);
            // JC | 2026-11-12 | 플레이 중 자산 재생성을 막고 조절기는 밝기·회전·환경 선택만 즉시 반영합니다.
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if(GUILayout.Button(new GUIContent("환경 데이터 굽기","편집 모드에서 원본을 확산광 SH와 거칠기별 HDR 반사로 변환하여 이 에셋에 저장합니다. 연결된 전투 모두에 영향을 줍니다.")))
                {
                    try { JcIndoorIblBaker.Bake(item); }
                    catch(System.Exception ex) { Debug.LogError("실내 IBL 베이크 실패: "+ex.Message,item); }
                }
        }
    }
}
