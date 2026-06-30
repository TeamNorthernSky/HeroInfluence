using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>[JC 260630] 맵이벤트 결정 모달(JC). MapEventRequested 구독→렌더, Yes=실행위임/No·ESC·외곽=무시. 항상-활성 번들 루트에 부착.</summary>
[DisallowMultipleComponent]
public class MapEventDecisionModalController : MonoBehaviour
{
    [SerializeField] private GameObject root;          // Modal 동반, 비활성 시작
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text effectAmountText;
    [SerializeField] private TMP_Text costAmountText;
    [SerializeField] private Image resourceIcon;
    [SerializeField] private Image effectIcon;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private MapEventModalRequest current;

    private void Awake()
    {
        if (yesButton != null) yesButton.onClick.AddListener(OnYes);
        if (noButton != null) noButton.onClick.AddListener(Close);
        if (root != null) root.SetActive(false);
    }
    private void OnEnable()  => ExplorationModalEvents.MapEventRequested += Show;
    private void OnDisable()
    {
        ExplorationModalEvents.MapEventRequested -= Show;
        ExplorationModalEvents.MapEventModalActive = false;
    }

    // 모달 활성 상태를 허브 플래그에 동기화(버튼·ESC·외곽클릭 모든 닫기 경로 커버). 거점/본부 더블클릭·우클릭 가드가 조회.
    private void Update()
    {
        ExplorationModalEvents.MapEventModalActive = root != null && root.activeSelf;
    }

    private void Show(MapEventModalRequest r)
    {
        if (r == null) return;
        current = r;
        if (descriptionText != null) descriptionText.text = r.description;
        if (effectAmountText != null) effectAmountText.text = r.effectAmountText;
        if (costAmountText != null) costAmountText.text = r.costAmountText;
        Apply(resourceIcon, r.resourceIcon);
        Apply(effectIcon, r.effectIcon);
        if (yesButton != null) yesButton.interactable = r.canAfford;
        if (root != null) { root.transform.SetAsLastSibling(); root.SetActive(true); }
    }

    private void OnYes()
    {
        if (current == null || current.onConfirm == null || current.onConfirm()) Close();
    }
    private void Close() { if (root != null) root.SetActive(false); current = null; }
    private static void Apply(Image img, Sprite s) { if (img == null) return; img.sprite = s; img.enabled = s != null; }
}
