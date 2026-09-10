#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class JcPlayInputWindow : EditorWindow
{
    [MenuItem("JC/마우스 스크롤-커서 제어")]
    public static void Open()
    {
        var window = GetWindow<JcPlayInputWindow>("마우스 스크롤-커서 제어");
        window.minSize = new Vector2(310, 65);
        window.Show();
    }
    private Vector2 scroll;
    [SerializeField] private bool collapsed;
    [SerializeField] private float expandedHeight = 450;
    private void OnEnable() { titleContent = new GUIContent("마우스 스크롤-커서 제어"); minSize = new Vector2(310, 65); }
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        bool wasCollapsed = collapsed;
        JcPlayInputGui.Draw(ref collapsed);
        EditorGUILayout.EndScrollView();
        if (wasCollapsed != collapsed && !docked)
        {
            Rect bounds = position;
            if (collapsed) expandedHeight = Mathf.Max(200, bounds.height);
            bounds.height = collapsed ? 65 : expandedHeight;
            position = bounds;
        }
    }
    private void OnInspectorUpdate() => Repaint();
}

[Overlay(typeof(SceneView), "jc-play-input", "마우스 스크롤-커서 제어", true)]
public sealed class JcPlayInputOverlay : Overlay
{
    [SerializeField] private bool collapsed;
    public override VisualElement CreatePanelContent()
    {
        var root = new ScrollView { style = { width = 330, maxHeight = 460, paddingLeft = 8, paddingRight = 8,
            paddingTop = 6, paddingBottom = 6, backgroundColor = new Color(.16f, .16f, .16f, .98f) } };
        root.Add(new IMGUIContainer(() => JcPlayInputGui.Draw(ref collapsed)));
        return root;
    }
}

internal static class JcPlayInputGui
{
    public static void Draw(ref bool collapsed)
    {
        collapsed = GUILayout.Toggle(collapsed, new GUIContent(collapsed ? "설정 펼치기 ▾" : "설정 최소화 ▴", "설정 내용을 접거나 펼칩니다. 조작 상태와 단축키 동작은 유지됩니다."), "Button");
        EditorGUILayout.LabelField(JcPlayInputController.Status, EditorStyles.boldLabel);
        if (collapsed) return;
        EditorGUILayout.HelpBox("재생은 유지하고 게임 입력만 전환합니다. 수동 편집에서는 자동 복귀하지 않습니다.", MessageType.None);
        if (EditorApplication.isPlaying && EditorApplication.isPaused)
            EditorGUILayout.HelpBox("Unity가 일시정지 상태입니다. 상단 Pause를 해제한 후 플레이 조작으로 복귀할 수 있습니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || EditorApplication.isPaused || !JcPlayInputController.Supported))
        {
            if (GUILayout.Button(new GUIContent("편집 ↔ 플레이 조작  Ctrl+[", "편집 시 조작을 멈추고 최대화를 잠시 해제합니다. 수동 복귀 시 최대화를 복원하고 Game 뷰 중앙으로 커서를 이동합니다. Unity Pause 중에는 먼저 일시정지를 해제해야 합니다.")))
                JcPlayInputController.ToggleMode();
        }
        using (new EditorGUI.DisabledScope(!JcPlayInputController.Supported))
        {
            JcPlayInputController.Confine = EditorGUILayout.Toggle(new GUIContent("커서 가두기  Ctrl+]", "기본 OFF. 플레이 조작 중 실제 게임 표시 영역 안에 가둡니다. 편집·포커스 상실·재생 종료 시 해제합니다. 플레이 종료 시 기본값으로 복귀합니다."), JcPlayInputController.Confine);
            using (new EditorGUI.DisabledScope(JcPlayInputController.Confine))
                JcPlayInputController.Automatic = EditorGUILayout.Toggle(new GUIContent("자동 편집 전환  Ctrl+\\", "기본 ON. Game 뷰 최대화 중에는 설정과 무관하게 자동 전환/복귀가 작동하지 않습니다. 외부 거리 초과 시 편집, 복귀 영역에 머무르면 자동 복귀합니다. 수동 편집/다른 창 클릭 시 자동 복귀하지 않습니다. 플레이 종료 시 기본값으로 복귀합니다."), JcPlayInputController.Automatic);
            if (JcPlayInputController.Confine)
                EditorGUILayout.HelpBox("가두기 중에는 화면 밖에 나갈 수 없어 자동 전환을 일시 중지합니다. 화면 경계 속도로 이동합니다.", MessageType.Info);
            JcPlayInputController.ReturnDelay = EditorGUILayout.FloatField(new GUIContent("자동 복귀 대기(초)", "기본 0.3초. 복귀 영역에 연속으로 머문 시간입니다. 이탈·클릭·텍스트 편집 시 취소합니다. 0이면 즉시 복귀. 플레이 종료 시 기본값으로 복귀합니다."), JcPlayInputController.ReturnDelay);
            JcPlayInputController.Hysteresis = EditorGUILayout.FloatField(new GUIContent("복귀 여유 거리(px)", "기본 20 표시 픽셀. 외부 200px에서 나갔다면 180px 안으로 들어와야 복귀 대기를 시작합니다. 0이면 이탈과 복귀 경계가 같습니다. 플레이 종료 시 기본값으로 복귀합니다."), JcPlayInputController.Hysteresis);
        }
        if (!string.IsNullOrEmpty(JcPlayInputController.Problem)) EditorGUILayout.HelpBox(JcPlayInputController.Problem, MessageType.Warning);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("공용 스크롤 설정", EditorStyles.boldLabel);
        var profile = AssetDatabase.LoadAssetAtPath<JcPointerProfile>("Assets/Resources/JcPointerProfile.asset");
        if (profile == null) { EditorGUILayout.HelpBox("Assets/Resources/JcPointerProfile.asset이 필요합니다.", MessageType.Warning); return; }
        var serialized = new SerializedObject(profile);
        serialized.Update();
        Field(serialized, "innerPixels", "안쪽 영역(px)");
        Field(serialized, "outerPixels", "바깥 거리(px)");
        Field(serialized, "boundarySpeed", "화면 경계 속도");
        Field(serialized, "maximumSpeed", "최대 속도");
        Field(serialized, "acceleration", "시작·전환 응답률");
        Field(serialized, "normalCursorSize", "기본 커서 기준 크기");
        Field(serialized, "scrollCursorSize", "스크롤 커서 기준 크기");
        serialized.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("속도 단위: 월드 단위/초. 플레이 중 변경은 종료 시 시작 전 값으로 복귀합니다. 영구 저장은 재생 종료 후 가능합니다.", MessageType.None);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button(new GUIContent("공용 설정 저장", "재생 종료 상태에서 현재 프로필을 저장합니다. 모든 탐사 카메라와 빌드에 적용되며 씬은 저장하지 않습니다."))) AssetDatabase.SaveAssetIfDirty(profile);
    }
    private static void Field(SerializedObject serialized, string name, string label)
    {
        var property = serialized.FindProperty(name);
        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
    }
}
#endif
