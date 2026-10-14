using UnityEditor;
using UnityEngine;
namespace JC.EnvironmentDetail.Editor
{
    [CustomEditor(typeof(JcRoadDetailSettings))]
    public sealed class JcRoadDetailSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("생성 결과는 RoadDetailKit/Prefabs의 독립 부품입니다. 전투 코드 없이 다른 씬에 배치할 수 있습니다. Main_1-2 예시는 생성 시 함께 갱신됩니다. 콜라이더·탐사 통행 판정은 포함하지 않습니다.",MessageType.Info);
            using(new EditorGUI.DisabledScope(Application.isPlaying))
                if(GUILayout.Button(new GUIContent("부품 및 Main_1-2 예시 재생성", "이 설정 에셋의 값으로 생성 메시·재질·예시 배경을 갱신합니다. 탐사씬과 전투씬은 저장하지 않습니다.")))
                {try{JcRoadDetailBuilder.BuildWithSettings((JcRoadDetailSettings)target);}catch(System.Exception e){Debug.LogException(e);EditorUtility.DisplayDialog("디테일 생성 실패",e.Message,"확인");}}
        }
    }
}
