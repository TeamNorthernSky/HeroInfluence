using UnityEngine;

// [JC 260629] 영속 공용 UI 호스트 (GameManager 분해 조각 2).
// HeroInfo/HeroStatus 모달 + SystemMenuModal + EventSystem을 GameManager에서 분리해 소유.
// 매니저 컴포넌트는 항상-활성 루트에 둔다(모달은 self-deactivate → 구동자는 항상-활성 호스트에, 조각1 교훈).
public class CommonUIManager : MonoBehaviour
{
    public static CommonUIManager Instance { get; private set; }

    public HeroInfoModal HeroInfoModal { get; private set; }
    public SettingsModalController SettingsModal { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>("CommonUIManager");
        if (prefab == null) return; // 프리팹 분리(Task 2) 전 중간 상태 → 무동작
        var go = Object.Instantiate(prefab);
        go.name = "CommonUIManager";
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
        Initialize();
    }

    private void Initialize()
    {
        // [JC 260629] Modal_HeroStatus(미사용) 제거 — HeroInfo 단일.
        HeroInfoModal = GetComponentInChildren<HeroInfoModal>(true);
        // [KJ 260708] Settings 모달(Modal_Settings)도 HeroInfo와 동일하게 항상-활성 호스트에서 조회.
        SettingsModal = GetComponentInChildren<SettingsModalController>(true);
    }
}
