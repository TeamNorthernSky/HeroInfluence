using UnityEngine;
using TMPro;

public class DebugPanelManager : MonoBehaviour
{
    // JC | 2026-11-09 | 개발자 UI는 게임 HUD와 독립된 Noto 참조를 사용합니다. Resources 프리팹의 직접 참조로 빌드 포함도 보장합니다.
    [Tooltip("개발자 도구 기본 글꼴(NotoSansKR). 누락되면 사용 가능한 글꼴을 자동 선택합니다.")]
    [SerializeField] private TMP_FontAsset developerFont;
    [Tooltip("개발자용 OnGUI 표시의 NotoSansKR 원본 글꼴입니다. 누락되면 사용 가능한 글꼴을 자동 선택합니다.")]
    [SerializeField] private Font developerSourceFont;
    private static DebugPanelManager fontConfiguration;
    private static Font builtInFont;

    private static DebugPanelManager FontConfiguration
    {
        get
        {
            if (fontConfiguration == null)
            {
                var prefab = Resources.Load<GameObject>("DebugPanelManager");
                if (prefab != null) fontConfiguration = prefab.GetComponent<DebugPanelManager>();
            }
            return fontConfiguration;
        }
    }

    // JC | 2026-11-09 | Noto 부재 시 자동 대체는 사용자 승인 사항입니다. 씬의 UI 이름/폰트 이름 일치를 필수 조건으로 삼지 않습니다.
    public static TMP_FontAsset ResolveDeveloperFont(TMP_FontAsset existing = null)
    {
        var configuration = FontConfiguration;
        if (UsableFont(configuration != null ? configuration.developerFont : null)) return configuration.developerFont;
        var loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        var selected = SelectAvailableFont(loaded, existing, TMP_Settings.defaultFontAsset);
        if (selected != null) return selected;
        // JC | 2026-11-09 | 아직 로드된 TMP 글꼴이 없으면 빌드에 포함된 Resources 글꼴까지 검색합니다.
        // LegacyRuntime 원본은 TMP 변환에 실패하므로 변환에 의존하지 않고 기존 TMP 에셋을 직접 사용합니다.
        return SelectAvailableFont(Resources.LoadAll<TMP_FontAsset>(string.Empty), null, null);
    }

    private static bool UsableFont(TMP_FontAsset candidate) => candidate != null && candidate.material != null && candidate.atlasTexture != null;

    private static TMP_FontAsset SelectAvailableFont(TMP_FontAsset[] loaded, TMP_FontAsset existing, TMP_FontAsset defaultFont)
    {
        foreach (var candidate in loaded)
            if (UsableFont(candidate) && candidate.name.IndexOf("NotoSans", System.StringComparison.OrdinalIgnoreCase) >= 0) return candidate;
        if (UsableFont(existing)) return existing;
        if (UsableFont(defaultFont)) return defaultFont;
        foreach (var candidate in loaded) if (UsableFont(candidate)) return candidate;
        return null;
    }

    // JC | 2026-11-09 | IMGUI도 같은 Noto 원본을 우선합니다. 폰트가 없을 때에는 기존 표시/로드된 폰트/내장 폰트 순으로 연결합니다.
    public static Font ResolveDeveloperSourceFont(Font existing = null)
    {
        var configuration = FontConfiguration;
        if (configuration != null && configuration.developerSourceFont != null) return configuration.developerSourceFont;
        var loaded = Resources.FindObjectsOfTypeAll<Font>();
        foreach (var candidate in loaded)
            if (candidate != null && candidate.name.IndexOf("NotoSans", System.StringComparison.OrdinalIgnoreCase) >= 0) return candidate;
        if (existing != null) return existing;
        foreach (var candidate in loaded) if (candidate != null) return candidate;
        if (builtInFont == null) builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return builtInFont;
    }

    // JC | 2026-11-09 | OnGUI 한 번의 표시 범위에서만 글꼴을 바꾸고 반드시 복원하여 일반 UI에 전파하지 않습니다.
    public readonly struct DeveloperGuiFontScope : System.IDisposable
    {
        private readonly GUISkin skin;
        private readonly Font previous;
        public DeveloperGuiFontScope(GUISkin target)
        {
            skin = target;
            previous = skin.font;
            skin.font = ResolveDeveloperSourceFont(previous);
        }
        public void Dispose() { if (skin != null) skin.font = previous; }
    }

    public static DebugPanelManager Instance { get; private set; }

    // 백쿼트 활성화와 닫기 시퀀스가 사용하는 실제 디버그 창 상태를 공유한다.
    public static bool IsDebugPanelActive => Instance != null && Instance.isActiveAndEnabled &&
        Instance.panelController != null && Instance.panelController.IsCanvasActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return; // 이미 존재(분리 전엔 GameManager 자식 Awake가 설정)
        var prefab = Resources.Load<GameObject>("DebugPanelManager");
        if (prefab == null) return;   // 프리팹 분리 전 중간 상태 → 무동작
        var go = Object.Instantiate(prefab);
        go.name = "DebugPanelManager";
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject); // 루트일 때만(자식 시 경고 회피)
        Initialize();
    }

    private void Update() => Tick();

    [Header("활성화 시퀀스 (백쿼트 N연타)")]
    [SerializeField] private KeyCode activationKey = KeyCode.BackQuote;
    [SerializeField] private int activationCount = 5;
    [SerializeField] private float activationWindow = 1.5f;

    [Header("비활성화 시퀀스 (Shift 홀드 + Tab N회)")]
    [SerializeField] private KeyCode deactivationKey = KeyCode.Tab;
    [SerializeField] private int deactivationCount = 2;
    [SerializeField] private float deactivationWindow = 1f;

    private DebugPanelController panelController;

    private int armedActivationCount;
    private float activationTimer;
    private int armedDeactivationCount;
    private float deactivationTimer;

    public void Initialize()
    {
        panelController = GetComponentInChildren<DebugPanelController>(true);
        if (panelController != null)
        {
            panelController.Initialize();
        }
    }

    public void TogglePanel()
    {
        if (panelController != null) panelController.Toggle();
    }

    public void Tick()
    {
        if (panelController == null) return;

        if (panelController.IsCanvasActive)
        {
            // [JC 260619] Shift+숫자 치트는 디버그 패널이 열려 있을 때만 작동(백쿼트 5연타로 활성화한 상태).
            UpdateGlobalShortcuts();
            UpdateDeactivationSequence();
            UpdateInputToggle();
        }
        else
        {
            UpdateActivationSequence();
        }
    }

    private void UpdateGlobalShortcuts()
    {
        // 입력창 활성 중에는 글로벌 숏컷 무시 (텍스트 입력 우선)
        if (panelController.IsInputModeActive) return;

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (!shiftHeld) return;

        if (Input.GetKeyDown(KeyCode.Alpha4)) RunCheat("Tetra Anax");
        else if (Input.GetKeyDown(KeyCode.Alpha3)) RunCheat("Ain Soph Aur");
        else if (Input.GetKeyDown(KeyCode.Alpha1)) RunCheat("Logos");
        else if (Input.GetKeyDown(KeyCode.Alpha6)) RunCheat("Malkuth");
        else if (Input.GetKeyDown(KeyCode.Alpha7)) RunCheat("Istaroth");
        else if (Input.GetKeyDown(KeyCode.Alpha8)) RunCheat("Asmoday");
    }

    private static void RunCheat(string command)
    {
        CheatCommandRegistry.TryExecute(command, out string result);
        UnityEngine.Debug.Log($"[Cheat] {result}");
    }

    private void UpdateActivationSequence()
    {
        if (armedActivationCount > 0)
        {
            activationTimer -= Time.unscaledDeltaTime;
            if (activationTimer <= 0f) armedActivationCount = 0;
        }

        bool backquoteDown = Input.GetKeyDown(activationKey);

        // 시퀀스 진행 중 백쿼트 외 다른 키/마우스 입력 → 즉시 무효화
        if (armedActivationCount > 0 && !backquoteDown && Input.anyKeyDown)
        {
            armedActivationCount = 0;
            return;
        }

        if (!backquoteDown) return;

        if (armedActivationCount == 0)
        {
            armedActivationCount = 1;
            activationTimer = activationWindow;
            return;
        }

        armedActivationCount++;
        if (armedActivationCount >= activationCount)
        {
            armedActivationCount = 0;
            panelController.Toggle(); // 활성화
        }
    }

    private void UpdateDeactivationSequence()
    {
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (!shiftHeld)
        {
            armedDeactivationCount = 0;
            return;
        }

        if (armedDeactivationCount > 0)
        {
            deactivationTimer -= Time.unscaledDeltaTime;
            if (deactivationTimer <= 0f) armedDeactivationCount = 0;
        }

        if (!Input.GetKeyDown(deactivationKey)) return;

        if (armedDeactivationCount == 0)
        {
            armedDeactivationCount = 1;
            deactivationTimer = deactivationWindow;
            return;
        }

        armedDeactivationCount++;
        if (armedDeactivationCount >= deactivationCount)
        {
            armedDeactivationCount = 0;
            panelController.Toggle(); // 비활성 (Toggle 내부에서 입력 모드도 종료)
        }
    }

    private void UpdateInputToggle()
    {
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (!ctrlHeld || !shiftHeld) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (panelController.IsInputModeActive)
                panelController.HideInputBar();
            else
                panelController.ShowInputBar();
        }
    }
}
