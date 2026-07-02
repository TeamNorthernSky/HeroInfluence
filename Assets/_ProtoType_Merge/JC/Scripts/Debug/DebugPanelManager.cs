using UnityEngine;

public class DebugPanelManager : MonoBehaviour
{
    public static DebugPanelManager Instance { get; private set; }

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
