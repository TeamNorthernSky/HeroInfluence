using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class LobbyDepartmentButtonBinder : MonoBehaviour
{
    [SerializeField] private HQDepartment department;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private GameObject targetModalRoot;

    [Header("LV 표시 (해금 후만 표시)")]
    [SerializeField] private GameObject lvBgRoot;
    [SerializeField] private TextMeshProUGUI levelText;

    private Button button;
    private HQStateManager subscribedHQ;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (subscribedHQ == null)
        {
            TrySubscribe();
            Refresh();
        }
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;
        if (subscribedHQ != null) return;
        subscribedHQ = gm.HQ;
        subscribedHQ.OnStateChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (subscribedHQ != null)
        {
            subscribedHQ.OnStateChanged -= Refresh;
            subscribedHQ = null;
        }
    }

    private bool IsLocked()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return true;
        return gm.HQ.GetLevel(department) <= 0;
    }

    private void Refresh()
    {
        bool locked = IsLocked();
        if (lockedOverlay != null) lockedOverlay.SetActive(locked);
        if (button != null) button.interactable = !locked;

        int level = GameManager.Instance != null && GameManager.Instance.HQ != null
            ? GameManager.Instance.HQ.GetLevel(department)
            : 0;
        bool activated = level >= 1;
        if (lvBgRoot != null) lvBgRoot.SetActive(activated);
        if (levelText != null && activated) levelText.text = $"Lv. {level}";
        // Hover는 ButtonEffectController(별도 IPointerEnter/Exit 핸들러)가 처리하므로 interactable과 무관
    }

    private void HandleClick()
    {
        // Button.interactable=false면 본 콜백이 호출되지 않음. 안전망으로 한 번 더 가드.
        if (IsLocked()) return;
        if (targetModalRoot != null) targetModalRoot.SetActive(true);
    }
}
