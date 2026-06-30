using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>[JC 260630] 거점 점령 알림 모달(JC). OutpostNoticeRequested 구독→렌더, OK/ESC/외곽=닫기. 항상-활성 번들 루트에 부착.</summary>
[DisallowMultipleComponent]
public class OutpostNoticeModalController : MonoBehaviour
{
    [SerializeField] private GameObject root;        // 모달 본체(Modal 동반, 비활성 시작)
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Image buildingImage;
    [SerializeField] private Image resourceImage;
    [SerializeField] private Button okButton;

    private void Awake()
    {
        if (okButton != null) okButton.onClick.AddListener(Close);
        if (root != null) root.SetActive(false);
    }
    private void OnEnable()  => ExplorationModalEvents.OutpostNoticeRequested += Show;
    private void OnDisable() => ExplorationModalEvents.OutpostNoticeRequested -= Show;

    private void Show(OutpostNoticeRequest r)
    {
        if (r == null) return;
        if (titleText != null) titleText.text = r.title;
        if (descriptionText != null) descriptionText.text = r.description;
        if (amountText != null) amountText.text = r.amount.ToString();
        Apply(buildingImage, r.buildingSprite);
        Apply(resourceImage, r.resourceSprite);
        if (root != null) { root.transform.SetAsLastSibling(); root.SetActive(true); }
    }
    private void Close() { if (root != null) root.SetActive(false); }
    private static void Apply(Image img, Sprite s) { if (img == null) return; img.sprite = s; img.enabled = s != null; }
}
