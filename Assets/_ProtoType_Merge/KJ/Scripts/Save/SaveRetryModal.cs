using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [KJ 260708] 자동 저장 실패 시 재시도 팝업 (Modal_SaveRetry.prefab 루트에 부착).
/// TitleScene TutorialPopup 복제 기반. GameSaveService.SaveToSlot 실패 → Show(slot)로 소환.
/// 예: 팝업을 닫고 재저장(재실패 시 Show가 같은 인스턴스를 재활성 → Modal.pausesGame이 다시 pause).
/// 아니요/x: 그냥 닫기. Modal(pausesGame=true)가 활성 중 일시정지, 비활성 순간 재개를 담당.
/// </summary>
[DisallowMultipleComponent]
public class SaveRetryModal : MonoBehaviour
{
    private const string PrefabPath = "UI/Modal_SaveRetry"; // Assets/Resources/UI/Modal_SaveRetry.prefab

    [SerializeField] private Button retryButton; // Accept Button ("예")
    [SerializeField] private Button denyButton;  // Deny Button ("아니요")
    [SerializeField] private Button closeButton; // Cancle Button ("x") — 아니요와 동일 동작

    private static SaveRetryModal current;
    private int slotIndex;

    /// <summary>저장 실패 팝업 소환. 이미 떠 있으면 재활성만(중복 생성 방지).</summary>
    public static void Show(int slotIndex)
    {
        if (current != null)
        {
            current.slotIndex = slotIndex;
            if (!current.gameObject.activeSelf) current.gameObject.SetActive(true);
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[SaveRetryModal] 프리팹 없음: Resources/{PrefabPath}");
            return;
        }

        // [KJ 260708] 프리팹이 자체 Canvas(Overlay, order 500) 보유 — 씬 캔버스 구조에 독립적.
        // (최상위 씬 캔버스에 얹는 초기 방식은 탐사씬에서 GameEndCanvas(order 1000)에 붙는 부작용이 있었음)
        GameObject go = Instantiate(prefab);
        current = go.GetComponent<SaveRetryModal>();
        if (current != null) current.slotIndex = slotIndex;
    }

    private void Awake()
    {
        if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
        if (denyButton != null) denyButton.onClick.AddListener(Close);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnRetry()
    {
        // 먼저 닫아서(비활성 → 즉시 게임 재개) 재실패 시 Show()가 같은 인스턴스를 되살리게 함 — 겹침 방지.
        gameObject.SetActive(false);
        bool ok = GameSaveService.SaveToSlot(slotIndex);
        if (ok) Destroy(gameObject);
    }

    private void Close()
    {
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}
