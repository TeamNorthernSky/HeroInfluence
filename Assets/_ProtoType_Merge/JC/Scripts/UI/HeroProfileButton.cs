using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보유 히어로 명단의 1명 프로필 버튼. 프리팹용. HeroListController가 동적 생성.
/// 기본 모드: 클릭 시 공용 HeroInfoModal.Open(unitIndex).
/// 선택 모드: BindForSelect로 외부 콜백 결합 시 클릭 → 콜백 호출 (홍보 영웅 선택 등).
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
    private Action<int> selectCallback;

    /// <summary>바인딩된 영웅 인덱스(출전 드래그 등 외부 결합용).</summary>
    public int UnitIndex => unitIndex;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClick);
    }

    public void Bind(int unitIndex, HeroInfoModal infoModal, UnitPersistentData unit, UnitData template, bool isVisiting)
    {
        this.unitIndex = unitIndex;
        this.infoModal = infoModal;
        this.selectCallback = null;
        ApplyDisplay(unit, template, isVisiting);
    }

    public void BindForSelect(int unitIndex, Action<int> onSelected, UnitPersistentData unit, UnitData template, bool isVisiting)
    {
        this.unitIndex = unitIndex;
        this.infoModal = null;
        this.selectCallback = onSelected;
        ApplyDisplay(unit, template, isVisiting);
    }

    private void ApplyDisplay(UnitPersistentData unit, UnitData template, bool isVisiting)
    {
        string displayName = template != null && !string.IsNullOrWhiteSpace(template.Name)
            ? template.Name
            : (unit != null ? unit.UnitTemplateKey : $"unit_{unitIndex}");
        string displayClass = template != null ? template.UnitType : "-";
        int level = unit != null ? unit.Level : 1;

        if (nameText != null) nameText.text = displayName;
        if (classText != null) classText.text = displayClass;
        if (levelText != null) levelText.text = $"Lv {level}";

        if (profileImage != null)
        {
            var sp = HeroProfileCatalog.GetByName(template != null ? template.Name : null);
            profileImage.gameObject.SetActive(true);
            profileImage.enabled = true;
            if (sp != null) profileImage.sprite = sp;
        }

        if (visitIndicator != null && visitIndicator.activeSelf != isVisiting)
            visitIndicator.SetActive(isVisiting);
    }

    private void OnClick()
    {
        if (selectCallback != null)
        {
            selectCallback.Invoke(unitIndex);
            return;
        }
        if (infoModal == null)
        {
            Debug.LogWarning($"[HeroProfileButton] infoModal 미바인딩 (unitIndex={unitIndex})");
            return;
        }
        infoModal.Open(unitIndex);
    }
}
