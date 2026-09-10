#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

/// <summary>Windows 에디터 전용 입력 소유권·포커스·OS 커서 제한. 재생 자체는 중단하지 않는다.</summary>
[InitializeOnLoad]
public static class JcPlayInputController
{
    public const string ToggleId = "JC/플레이 입력/편집 전환";
    public const string ConfineId = "JC/플레이 입력/커서 가두기";
    public const string AutoId = "JC/플레이 입력/자동 편집 전환";
    private static readonly string Prefix = "JC.PlayInput." + Application.dataPath + ".";
    private static readonly Type GameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly PropertyInfo TargetRect = GameViewType?.GetProperty("targetInView", Flags);
    private static readonly PropertyInfo ViewRect = GameViewType?.GetProperty("viewInWindow", Flags);
    private static readonly FieldInfo GlobalEvent = typeof(EditorApplication).GetField("globalEventHandler", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly EditorApplication.CallbackFunction KeyHandler = HandleEditorKeys;
    private static readonly JcPointerSession session = new JcPointerSession();
    private static EditorWindow game;
    private static EditorWindow previousEditor;
    private static Rect displayPixels;
    private static Vector2 cursorPixels;
    private static bool geometryValid;
    private static bool clipOwned;
    private static bool mouseWasDown;
    private static bool shortcutWasDown;
    private static KeyCode heldShortcut;
    private static double lastShortcut = -1;
    private static double resumeAfter;
    private static bool restoreMaximized;
    private static bool pendingPlay;
    private static bool pendingCenter;
    private static bool pendingCursorHidePermission;
    private static double layoutReadyAfter;
    private static string problem = "";
    private static readonly uint ProcessId = (uint)Process.GetCurrentProcess().Id;

    public static bool Supported => Application.platform == RuntimePlatform.WindowsEditor;
    public static bool Confine
    {
        get => EditorPrefs.GetBool(Prefix + "Confine", false);
        set { EditorPrefs.SetBool(Prefix + "Confine", value); if (!value) ReleaseClip(); }
    }
    public static bool Automatic
    {
        get => EditorPrefs.GetBool(Prefix + "Automatic", true);
        set { EditorPrefs.SetBool(Prefix + "Automatic", value); if (!value && session.State == JcPointerSession.Mode.AutomaticEdit) EnterEdit(false); }
    }
    public static float ReturnDelay
    {
        get => EditorPrefs.GetFloat(Prefix + "ReturnDelay", .3f);
        set => EditorPrefs.SetFloat(Prefix + "ReturnDelay", Mathf.Max(0, value));
    }
    public static float Hysteresis
    {
        get => EditorPrefs.GetFloat(Prefix + "Hysteresis", 20);
        set => EditorPrefs.SetFloat(Prefix + "Hysteresis", Mathf.Max(0, value));
    }
    public static string Status => !Supported ? "Windows 에디터 전용" : !EditorApplication.isPlaying ? "재생 대기"
        : EditorApplication.isPaused ? "재생 일시정지" : session.State == JcPointerSession.Mode.Playing ? "플레이 조작"
        : session.State == JcPointerSession.Mode.AutomaticEdit ? (session.ReturnSince >= 0 ? "자동 복귀 대기 중" : "거리 초과 · 자동 편집") : "수동 편집 · 자동 복귀 없음";
    public static string Problem => problem;
    public static Rect DisplayPixels => displayPixels;
    public static JcPointerSession.Mode State => session.State;

    static JcPlayInputController()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += PlayStateChanged;
        InstallKeyHandler();
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.quitting += Shutdown;
        EditorApplication.pauseStateChanged += state => { if (state == PauseState.Paused) EnterEdit(false); };
    }

    [Shortcut(ToggleId, typeof(JcPlayInputWindow), KeyCode.LeftBracket, ShortcutModifiers.Action)]
    private static void ToggleShortcut() { if (AcceptShortcut()) ToggleMode(); }
    [Shortcut(ConfineId, typeof(JcPlayInputWindow), KeyCode.RightBracket, ShortcutModifiers.Action)]
    private static void ConfineShortcut() { if (AcceptShortcut()) Confine = !Confine; }
    [Shortcut(AutoId, typeof(JcPlayInputWindow), KeyCode.Backslash, ShortcutModifiers.Action)]
    private static void AutoShortcut() { if (AcceptShortcut()) Automatic = !Automatic; }

    private static bool AcceptShortcut()
    {
        if (!Supported || !EditorApplication.isPlaying || EditorApplication.isCompiling || TextEditing()) return false;
        double now = EditorApplication.timeSinceStartup;
        if (now - lastShortcut < .15) return false;
        lastShortcut = now;
        return true;
    }

    private static void InstallKeyHandler()
    {
        // Unity 2022.3의 ShortcutIntegration보다 먼저 재생 전용 키를 소비한다.
        // 기본 Grid 바인딩/사용자 프로필을 수정하지 않고 재생 종료 시 그대로 통과시킨다.
        if (GlobalEvent == null) return;
        var existing = GlobalEvent.GetValue(null) as EditorApplication.CallbackFunction;
        existing -= KeyHandler;
        GlobalEvent.SetValue(null, KeyHandler + existing);
    }

    private static void HandleEditorKeys()
    {
        Event e = Event.current;
        if (e != null && e.type == EventType.KeyUp && e.keyCode == heldShortcut) heldShortcut = KeyCode.None;
        if (e == null || e.type != EventType.KeyDown || !EditorApplication.isPlaying || !Supported || TextEditing()) return;
        bool toggle = EventBinding(e, ToggleId), confine = EventBinding(e, ConfineId), auto = EventBinding(e, AutoId);
        if (!toggle && !confine && !auto) return;
        e.Use();
        if (heldShortcut == e.keyCode) return;
        heldShortcut = e.keyCode;
        if (!AcceptShortcut()) return;
        if (toggle) ToggleMode();
        else if (confine) Confine = !Confine;
        else Automatic = !Automatic;
    }

    private static bool EventBinding(Event e, string id)
    {
        var sequence = ShortcutManager.instance.GetShortcutBinding(id).keyCombinationSequence.ToArray();
        if (sequence.Length != 1) return false;
        var combo = sequence[0];
        return e.keyCode == combo.keyCode && e.control == ((combo.modifiers & ShortcutModifiers.Action) != 0)
            && e.shift == ((combo.modifiers & ShortcutModifiers.Shift) != 0) && e.alt == ((combo.modifiers & ShortcutModifiers.Alt) != 0);
    }

    public static void ToggleMode()
    {
        if (!EditorApplication.isPlaying || !Supported) return;
        if (session.State == JcPointerSession.Mode.Playing) EnterEdit(false);
        else EnterPlay(true);
    }

    private static void PlayStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode) CaptureInitialProfile();
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            JcPointerInput.EditorManaged = Supported;
            FindGame();
            if (game != null && game.hasFocus) EnterPlay(false);
        }
        else if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode) Shutdown();
        if (change == PlayModeStateChange.EnteredEditMode) RestoreInitialSettings();
    }

    private static void CaptureInitialProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<JcPointerProfile>("Assets/Resources/JcPointerProfile.asset");
        if (profile == null) return;
        SessionState.SetString(Prefix + "InitialProfile", EditorJsonUtility.ToJson(profile));
        SessionState.SetBool(Prefix + "ProfileWasDirty", EditorUtility.IsDirty(profile));
    }

    private static void RestoreInitialSettings()
    {
        Confine = false;
        Automatic = true;
        ReturnDelay = .3f;
        Hysteresis = 20;
        string initial = SessionState.GetString(Prefix + "InitialProfile", "");
        var profile = AssetDatabase.LoadAssetAtPath<JcPointerProfile>("Assets/Resources/JcPointerProfile.asset");
        if (profile != null && !string.IsNullOrEmpty(initial))
        {
            EditorJsonUtility.FromJsonOverwrite(initial, profile);
            if (SessionState.GetBool(Prefix + "ProfileWasDirty", false)) EditorUtility.SetDirty(profile);
            else EditorUtility.ClearDirty(profile);
        }
        SessionState.EraseString(Prefix + "InitialProfile");
        SessionState.EraseBool(Prefix + "ProfileWasDirty");
    }

    private static void FindGame()
    {
        if (GameViewType == null) return;
        var focused = EditorWindow.focusedWindow;
        if (focused != null && focused.GetType() == GameViewType) game = focused;
        if (game == null) game = Resources.FindObjectsOfTypeAll(GameViewType).OfType<EditorWindow>().FirstOrDefault();
    }

    // GameView 내부의 실제 표시 영역을 사용한다. 해상도 여백/툴바/줌을 제외한 표시 픽셀 좌표.
    public static bool RefreshGeometry()
    {
        FindGame();
        geometryValid = false;
        if (game == null || TargetRect == null || ViewRect == null) { problem = "Game 뷰 표시 영역을 찾을 수 없습니다."; return false; }
        try
        {
            Rect view = (Rect)ViewRect.GetValue(game);
            Rect target = (Rect)TargetRect.GetValue(game);
            Rect local = Rect.MinMaxRect(Mathf.Max(0, target.xMin), Mathf.Max(0, target.yMin),
                Mathf.Min(view.width, target.xMax), Mathf.Min(view.height, target.yMax));
            float scale = EditorGUIUtility.pixelsPerPoint;
            Vector2 origin = game.position.position + view.position + local.position;
            displayPixels = new Rect(origin * scale, local.size * scale);
            geometryValid = local.width > 1 && local.height > 1 && origin.x > -20000 && origin.y > -20000;
            problem = geometryValid ? "" : "Game 뷰가 최소화되었거나 표시 영역이 유효하지 않습니다.";
        }
        catch (Exception e) { problem = "Game 뷰 좌표 확인 실패: " + e.Message; }
        return geometryValid;
    }

    private static bool EditorForeground()
    {
        IntPtr foreground = Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(foreground, out uint pid);
        return pid == ProcessId && !Native.IsIconic(foreground);
    }

    private static bool TextEditing()
    {
        if (EditorGUIUtility.editingTextField || GUIUtility.hotControl != 0) return true;
        var element = EditorWindow.focusedWindow?.rootVisualElement?.focusController?.focusedElement as UnityEngine.UIElements.VisualElement;
        for (; element != null; element = element.parent)
            if (element.GetType().Name.IndexOf("TextInput", StringComparison.OrdinalIgnoreCase) >= 0 || element is UnityEngine.UIElements.TextField) return true;
        return false;
    }

    private static void Tick()
    {
        if (!Supported) return;
        int heldVk = Native.VirtualKey(heldShortcut);
        if (heldVk != 0 && !Native.Down(heldVk)) heldShortcut = KeyCode.None;
        JcPointerInput.EditorManaged = true;
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling)
        { JcPointerInput.EditorPlayInput = false; ReleaseClip(); return; }
        if (!EditorForeground()) { EnterEdit(false, false); return; }
        // 최대화 복원은 지연된 Unity 레이아웃 갱신을 거친 뒤 새 표시 영역으로 진입한다.
        if (pendingPlay)
        {
            JcPointerInput.EditorPlayInput = false;
            ReleaseClip();
            if (EditorApplication.timeSinceStartup >= layoutReadyAfter)
            {
                bool center = pendingCenter;
                pendingPlay = false;
                EnterPlay(center);
            }
            return;
        }
        if (!RefreshGeometry() || !Native.GetCursorPos(out var pos)) { EnterEdit(false, false); return; }
        cursorPixels = new Vector2(pos.x, pos.y);
        bool inside = displayPixels.Contains(cursorPixels);
        bool mouseDown = Native.Down(1) || Native.Down(2) || Native.Down(4);
        bool clicked = mouseDown && !mouseWasDown;
        mouseWasDown = mouseDown;
        var focused = EditorWindow.focusedWindow;
        if (focused != null && focused != game && focused is not JcPlayInputWindow) previousEditor = focused;

        // Game 뷰에서 Unity 단축키 처리가 꺼진 경우에도 같은 등록 바인딩으로 동작한다.
        if (focused == game) PollGameShortcuts();
        else shortcutWasDown = false;
        // 단축키 안에서 포커스/레이아웃이 바뀔 수 있으므로 이전 스냅샷을 재사용하지 않는다.
        if (pendingPlay) return;
        focused = EditorWindow.focusedWindow;
        if (!RefreshGeometry() || !Native.GetCursorPos(out pos)) { EnterEdit(false, false); return; }
        cursorPixels = new Vector2(pos.x, pos.y);
        inside = displayPixels.Contains(cursorPixels);

        double now = EditorApplication.timeSinceStartup;
        if (clicked)
        {
            if (inside && (EditorWindow.mouseOverWindow == game || focused == game))
            {
                if (session.State != JcPointerSession.Mode.Playing) EnterPlay(false);
            }
            else EnterEdit(false, false);
        }
        if (session.State == JcPointerSession.Mode.Playing && now >= resumeAfter && focused != game)
            EnterEdit(false, false);

        float outside = Mathf.Max(0, displayPixels.xMin - cursorPixels.x, cursorPixels.x - displayPixels.xMax,
            displayPixels.yMin - cursorPixels.y, cursorPixels.y - displayPixels.yMax);
        var before = session.State;
        bool canReturn = !mouseDown && !TextEditing() && now >= resumeAfter;
        // 최대화 중에는 토글 저장값과 무관하게 거리 전환/자동 복귀를 모두 차단한다.
        bool automaticAllowed = Automatic && game != null && !game.maximized;
        bool returned = session.Tick(outside, JcPointerProfile.Current.outerPixels, Hysteresis, now, ReturnDelay, automaticAllowed, Confine, canReturn);
        if (before == JcPointerSession.Mode.Playing && session.State == JcPointerSession.Mode.AutomaticEdit)
        {
            ReleaseClip();
            JcPointerInput.EditorPlayInput = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            FocusEditor();
        }
        if (returned) EnterPlay(false);

        JcPointerInput.EditorPosition = new Vector2(cursorPixels.x - displayPixels.xMin, displayPixels.yMax - cursorPixels.y);
        JcPointerInput.EditorSize = displayPixels.size;
        JcPointerInput.EditorPlayInput = !pendingPlay && session.State == JcPointerSession.Mode.Playing && now >= resumeAfter;
        // GameView는 보통 첫 클릭 후에만 숨김을 허용한다. 입력 전환이 끝나고 화면 안에
        // 들어왔을 때 한 번 허용하며, 실제 숨김/화면 밖 복원은 기존 UI 커서가 담당한다.
        // 잠금 허용은 건드리지 않아 커서 가두기 설정과 분리한다.
        if (pendingCursorHidePermission && JcPointerInput.CanControl && inside && game.hasFocus)
        {
            Unsupported.SetAllowCursorHide(true);
            pendingCursorHidePermission = false;
        }
        if (JcPointerInput.EditorPlayInput && Confine) ApplyClip(); else ReleaseClip();
    }

    public static void EnterPlay(bool center)
    {
        if (!Supported || !EditorApplication.isPlaying || EditorApplication.isPaused || !EditorForeground()) return;
        FindGame();
        if (game == null) return;
        if (restoreMaximized)
        {
            restoreMaximized = false;
            game.Focus();
            game.maximized = true;
            pendingPlay = true;
            pendingCenter = center;
            layoutReadyAfter = EditorApplication.timeSinceStartup + .15;
            JcPointerInput.EditorPlayInput = false;
            ReleaseClip();
            return;
        }
        if (!RefreshGeometry()) return;
        game.Focus();
        if (center)
        {
            Vector2 point = displayPixels.center;
            if (!Native.SetCursorPos(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y)))
            { problem = "커서 중앙 이동에 실패했습니다."; EnterEdit(false, false); return; }
            cursorPixels = point;
        }
        session.Play();
        pendingCursorHidePermission = true;
        resumeAfter = EditorApplication.timeSinceStartup + .06;
        JcPointerInput.SuppressTransitionInput();
    }

    public static void EnterEdit(bool automatic, bool focusEditor = true)
    {
        if (automatic && game != null && game.maximized) return;
        pendingPlay = false;
        pendingCursorHidePermission = false;
        session.Edit(automatic);
        JcPointerInput.EditorPlayInput = false;
        ReleaseClip();
        if (Application.isPlaying)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.visible = true;
        }
        if (focusEditor) FocusEditor();
    }

    private static void FocusEditor()
    {
        // 최대화된 Game 뒤의 숨겨진 탭은 Focus()만으로 활성화할 수 없다.
        if (game != null && game.maximized)
        {
            restoreMaximized = true;
            game.maximized = false;
        }
        var destination = previousEditor != null ? previousEditor : SceneView.lastActiveSceneView;
        if (destination != null) destination.Focus();
    }

    private static void PollGameShortcuts()
    {
        bool toggle = BindingDown(ToggleId), confine = BindingDown(ConfineId), auto = BindingDown(AutoId);
        bool any = toggle || confine || auto;
        if (any && !shortcutWasDown && AcceptShortcut())
        {
            if (toggle) ToggleMode();
            else if (confine) Confine = !Confine;
            else Automatic = !Automatic;
        }
        shortcutWasDown = any;
    }

    private static bool BindingDown(string id)
    {
        var sequence = ShortcutManager.instance.GetShortcutBinding(id).keyCombinationSequence.ToArray();
        if (sequence.Length != 1) return false;
        var combo = sequence[0];
        int vk = Native.VirtualKey(combo.keyCode);
        if (vk == 0 || !Native.Down(vk)) return false;
        bool control = Native.Down(0x11), shift = Native.Down(0x10), alt = Native.Down(0x12);
        return control == ((combo.modifiers & ShortcutModifiers.Action) != 0)
            && shift == ((combo.modifiers & ShortcutModifiers.Shift) != 0)
            && alt == ((combo.modifiers & ShortcutModifiers.Alt) != 0);
    }

    private static void ApplyClip()
    {
        var rect = new Native.RECT { left = Mathf.CeilToInt(displayPixels.xMin), top = Mathf.CeilToInt(displayPixels.yMin),
            right = Mathf.FloorToInt(displayPixels.xMax), bottom = Mathf.FloorToInt(displayPixels.yMax) };
        if (Native.ClipCursor(ref rect)) clipOwned = true;
        else { problem = "커서 가두기에 실패했습니다."; Confine = false; }
    }
    private static void ReleaseClip()
    {
        if (!Supported || !clipOwned) return;
        Native.UnclipCursor(IntPtr.Zero);
        clipOwned = false;
    }
    private static void Shutdown()
    {
        restoreMaximized = false;
        pendingPlay = false;
        heldShortcut = KeyCode.None;
        EnterEdit(false, false);
        JcPointerInput.EditorManaged = false;
    }
    private static void BeforeReload()
    {
        Shutdown();
        if (GlobalEvent != null)
        {
            var handler = GlobalEvent.GetValue(null) as EditorApplication.CallbackFunction;
            handler -= KeyHandler;
            GlobalEvent.SetValue(null, handler);
        }
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int left, top, right, bottom; }
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr handle);
        [DllImport("user32.dll")] public static extern bool ClipCursor(ref RECT rect);
        [DllImport("user32.dll", EntryPoint = "ClipCursor")] public static extern bool UnclipCursor(IntPtr rect);
        public static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
        public static int VirtualKey(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z) return (int)key - (int)KeyCode.A + 65;
            if (key >= KeyCode.F1 && key <= KeyCode.F12) return (int)key - (int)KeyCode.F1 + 112;
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return (int)key;
            switch (key) { case KeyCode.LeftBracket: return 0xDB; case KeyCode.RightBracket: return 0xDD;
                case KeyCode.Backslash: return 0xDC; default: return 0; }
        }
    }
}
#endif
