using UnityEditor;
using UnityEngine;

namespace JC.Indicators.EditorTools
{
    // JC | 2026-11-04 | 탐사 영역의 채움과 테두리를 부모 조절기에서 함께 관리해 하위 표시 설정이 저장값을 덮지 않게 합니다.
    [CustomEditor(typeof(JcTriggerAreaIndicator))]
    public sealed class JcTriggerAreaIndicatorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var indicator = (JcTriggerAreaIndicator)target;
            serializedObject.Update();
            Field(serializedObject.FindProperty("profile"), "저장 프로필");
            EditorGUILayout.HelpBox("플레이 중 아래 값을 바꾸면 즉시 반영됩니다. 플레이 종료 전에 ‘현재 값 저장’을 누르세요. 저장 전에는 원본 프로필이 바뀌지 않습니다. 같은 프로필을 참조하는 탐사씬에 저장값이 적용됩니다.", MessageType.Info);
            var settings = serializedObject.FindProperty("settings");
            var shape = settings.FindPropertyRelative("shape");
            EditorGUILayout.LabelField("테두리 형태 (월드 단위)", EditorStyles.boldLabel);
            Field(shape.FindPropertyRelative("padding"), "영역 안쪽 여백");
            Field(shape.FindPropertyRelative("cornerRadius"), "모서리 반지름 (안·바깥)");
            Field(shape.FindPropertyRelative("curveSegments"), "모서리 분할 수");
            Field(shape.FindPropertyRelative("outerWidth"), "바깥 보조선 폭");
            Field(shape.FindPropertyRelative("mainWidth"), "본체 테두리 폭");
            Field(shape.FindPropertyRelative("innerWidth"), "안쪽 보조선 폭");
            Field(shape.FindPropertyRelative("depth"), "입체 두께");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("본체 테두리", EditorStyles.boldLabel);
            Draw("intensity", "본체 테두리 인텐시티"); Draw("opacity", "본체 테두리 불투명도");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("바깥 보조선", EditorStyles.boldLabel);
            Draw("showOuter", "바깥 보조선 표시"); Draw("outerIntensity", "바깥 보조선 인텐시티"); Draw("outerOpacity", "바깥 보조선 불투명도");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("안쪽 보조선", EditorStyles.boldLabel);
            Draw("showInner", "안쪽 보조선 표시"); Draw("innerIntensity", "안쪽 보조선 인텐시티"); Draw("innerOpacity", "안쪽 보조선 불투명도");
            // JC | 2026-11-04 | 건물·인도 가림 배율을 부모의 임시 초안에서 각각 조절·저장합니다.
            EditorGUILayout.Space(); EditorGUILayout.LabelField("가려진 구간 표시", EditorStyles.boldLabel);
            Draw("buildingOccludedOpacity", "건물 가림 표시 불투명도"); Draw("sidewalkOccludedOpacity", "인도 가림 표시 불투명도");
            Draw("fillSidewalkOccludedOpacity", "채움 인도 가림 표시 불투명도");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("상하 운동", EditorStyles.boldLabel);
            Draw("minimumHeight", "최저 지면 여유"); Draw("bobAmplitude", "왕복 진폭"); Draw("bobPeriod", "왕복 주기 (초)");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("내부 채움", EditorStyles.boldLabel);
            Draw("useFill", "채움 표시"); Draw("fillUsesTriggerColor", "기존 트리거 색 사용");
            Draw("fillColor", "지정 채움 색"); Draw("fillIntensity", "채움 인텐시티"); Draw("fillOpacity", "채움 불투명도"); Draw("fillTexture", "채움 문양");
            EditorGUILayout.Space();
            bool wiring = EditorGUILayout.Foldout(SessionState.GetBool("JC.TriggerArea.Wiring", false), "씬 연결", true);
            SessionState.SetBool("JC.TriggerArea.Wiring", wiring);
            if (wiring)
            {
                Field(serializedObject.FindProperty("source"), "기존 영역 컨트롤러");
                Field(serializedObject.FindProperty("borderPrefab"), "전투 테두리 프리팹");
                Field(serializedObject.FindProperty("borderShader"), "전투 테두리 셰이더");
            }
            serializedObject.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(!indicator.Profile))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("저장값 다시 적용", "현재 초안을 버리고 저장 프로필 값을 다시 적용합니다. 씬을 저장하지 않습니다.")))
                { Undo.RecordObject(indicator, "탐사 트리거 저장값 적용"); indicator.LoadProfile(); EditorUtility.SetDirty(indicator); }
                if (GUILayout.Button(new GUIContent("현재 값 저장", "현재 초안을 탐사 프로필에 복사하고 해당 에셋만 디스크에 저장합니다. 씬과 전투 프로필은 저장하지 않습니다.")))
                {
                    Undo.RecordObject(indicator.Profile, "탐사 트리거 프로필 저장");
                    indicator.Profile.settings = indicator.Settings.Copy();
                    EditorUtility.SetDirty(indicator.Profile); AssetDatabase.SaveAssetIfDirty(indicator.Profile);
                }
            }
            if (!indicator.IsReady) EditorGUILayout.HelpBox("기존 컨트롤러·전투 테두리·셰이더·탐사 프로필 연결을 확인하세요.", MessageType.Warning);
            else EditorGUILayout.LabelField(Application.isPlaying ? "표시 영역 수: " + indicator.RegionCount : "플레이 진입 시 기존 셀 정보를 받아 표시합니다.", EditorStyles.miniLabel);
            void Draw(string property, string label) => Field(settings.FindPropertyRelative(property), label);
        }
        private static void Field(SerializedProperty value, string label) => EditorGUILayout.PropertyField(value, new GUIContent(label, value.tooltip), true);
    }
}
