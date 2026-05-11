using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보유 히어로 명단의 1명 프로필 버튼. 프리팹용. HeroListController가 동적 생성.
/// 클릭 시 공용 HeroInfoModal.Open(unitIndex) 호출.
/// </summary>
[DisallowMultipleComponent]
public class HeroProfileButton : MonoBehaviour
{
    [Header("UI Bindings")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text classText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image profileImage; // 추후 클래스별 스프라이트 연결

    [Header("Visit Indicator")]
    [Tooltip("On_Visit_Member_Indicator 배경. 본부 방문중 멤버이면 SetActive(true)")]
    [SerializeField] private GameObject visitIndicator;

    private int unitIndex;
    private HeroInfoModal infoModal;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClick);
    }

    public void Bind(int unitIndex, HeroInfoModal infoModal, UnitPersistentData unit, UnitData template, bool isVisiting)
    {
        this.unitIndex = unitIndex;
        this.infoModal = infoModal;

        string displayName = template != null && !string.IsNullOrWhiteSpace(template.Name)
            ? template.Name
            : (unit != null ? unit.UnitTemplateKey : $"unit_{unitIndex}");
        string displayClass = template != null ? template.UnitType : "-";
        int level = unit != null ? unit.Level : 1;

        if (nameText != null) nameText.text = displayName;
        if (classText != null) classText.text = displayClass;
        if (levelText != null) levelText.text = $"Lv {level}";

        if (visitIndicator != null && visitIndicator.activeSelf != isVisiting)
            visitIndicator.SetActive(isVisiting);
    }

    private void OnClick()
    {
        if (infoModal == null)
        {
            Debug.LogWarning($"[HeroProfileButton] infoModal 미바인딩 (unitIndex={unitIndex})");
            return;
        }
        infoModal.Open(unitIndex);
    }
}
