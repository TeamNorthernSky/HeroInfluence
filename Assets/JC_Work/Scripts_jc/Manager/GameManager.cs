using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public CurrencyManager Currency { get; private set; }
    public SceneLoader SceneLoader { get; private set; }
    public UIPrefabRegistry UIPrefabRegistry { get; private set; }
    public DebugManager Debug { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeManagers();
    }

    private void Update()
    {
        if (Debug != null && Input.GetKeyDown(Debug.ToggleKey))
        {
            Debug.TogglePanel();
        }
    }

    private void InitializeManagers()
    {
        Currency = GetComponentInChildren<CurrencyManager>();
        Currency.Initialize();

        SceneLoader = GetComponentInChildren<SceneLoader>();
        if (SceneLoader != null) SceneLoader.Initialize();

        UIPrefabRegistry = GetComponentInChildren<UIPrefabRegistry>(true);

        Debug = GetComponentInChildren<DebugManager>(true);
        if (Debug != null) Debug.Initialize();
    }
}
