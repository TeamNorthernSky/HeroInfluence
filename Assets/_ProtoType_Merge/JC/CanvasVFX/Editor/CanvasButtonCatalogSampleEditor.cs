using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CanvasButtonCatalogSample))]
public sealed class CanvasButtonCatalogSampleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var sample = (CanvasButtonCatalogSample)target;
        EditorGUILayout.HelpBox("효과 자체는 기존 컴포넌트가 담당합니다. 이 컴포넌트는 예제 설명과 입력 상태만 관리합니다. Edit 모드에서 아래 기존 컴포넌트 값을 조절하고 씬을 저장하세요. Play 중 변경값은 자동 저장되지 않습니다.", MessageType.Info);
        DrawDefaultInspector();
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
            if (GUILayout.Button(new GUIContent("미리보기 초기화", "플레이 중 위치·배율·선택을 초기화하고 스윕을 다시 시작합니다. 에셋은 저장하지 않습니다.")))
                sample.ResetPreview();
        if (GUILayout.Button(new GUIContent("효과 참조 복사", "효과 ID, 설명, 기존 코드·재질 경로를 클립보드에 복사합니다. 다른 에이전트에게 적용을 요청할 때 사용하세요.")))
            EditorGUIUtility.systemCopyBuffer = sample.effectId + " / " + sample.displayName + "\n" + sample.description + "\n" + sample.references;
    }
}
