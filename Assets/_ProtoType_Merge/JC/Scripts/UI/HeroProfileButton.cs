using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 보유 히어로 명단의 1명 프로필 버튼. 프리팹용. HeroListController가 동적 생성.
/// 기본 모드: 클릭 시 공용 HeroInfoModal.Open(unitIndex).
/// 선택 모드: BindForSelect로 외부 콜백 결합 시 클릭 → 콜백 호출 (홍보/연구/공방 영웅 선택 등).
/// 깃발(partyMark): 파티 편성된 영웅 표시(방문 여부 무관). 상태 프레임: 일반/호버/선택됨.
/// </summary>
[DisallowMultipleComponent]
public class HeroProfileButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Bindings")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text classText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image profileImage;

    [Header("파티 편성 표시 (깃발)")]
    [Tooltip("파티에 편성된 영웅이면 표시(방문 여부 무관). UI_box_heroList_particiatingMark")]
    [FormerlySerializedAs("visitIndicator")]
    [SerializeField] private GameObject partyMark;

    [Header("항목 상태 프레임 (협회 기능 수행 시 일반/호버/선택)")]
    [SerializeField] private Image background;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;
    [SerializeField] private Sprite selectedSprite;

    private int unitIndex;
    private HeroInfoModal infoModal;
    private Action<int> selectCallback;
    private bool selected;
    private bool hovering;

    /// <summary>바인딩된 영웅 인덱스(출전 드래그 등 외부 결합용).</summary>
    public int UnitIndex => unitIndex;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClick);
        if (background == null) background = GetComponent<Image>();
    }

    public void Bind(int unitIndex, HeroInfoModal infoModal, UnitPersistentData unit, UnitData template, bool inParty)
    {
        this.unitIndex = unitIndex;
        this.infoModal = infoModal;
        this.selectCallback = null;
        ApplyDisplay(unit, template, inParty);
    }

    public void BindForSelect(int unitIndex, Action<int> onSelected, UnitPersistentData unit, UnitData template, bool inParty)
    {
        this.unitIndex = unitIndex;
        this.infoModal = null;
        this.selectCallback = onSelected;
        ApplyDisplay(unit, template, inParty);
    }

    /// <summary>선택됨 상태 토글(HeroListController가 단일 선택 관리).</summary>
    public void SetSelected(bool on)
    {
        selected = on;
        ApplyFrame();
    }

    private void ApplyDisplay(UnitPersistentData unit, UnitData template, bool inParty)
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
            // [JC 260621] 포트레이트 = unitIndex(=HeroIndex) 기준 PortraitLibrary 해석.
            var sp = HeroProfileCatalog.GetByUnitIndex(unitIndex);
            profileImage.gameObject.SetActive(true);
            profileImage.enabled = true;
            if (sp != null) profileImage.sprite = sp;
        }

        // 깃발: 파티 편성 여부(방문 무관)
        if (partyMark != null && partyMark.activeSelf != inParty)
            partyMark.SetActive(inParty);

        selected = false;
        hovering = false;
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (background == null) return;
        Sprite s = normalSprite;
        if (selected && selectedSprite != null) s = selectedSprite;
        else if (hovering && selectCallback != null && hoverSprite != null) s = hoverSprite;
        if (s != null) background.sprite = s;
    }

    public void OnPointerEnter(PointerEventData eventData) { hovering = true; ApplyFrame(); }
    public void OnPointerExit(PointerEventData eventData) { hovering = false; ApplyFrame(); }

    private void OnClick()
    {
        if (selectCallback != null)
        {
            selectCallback.Invoke(unitIndex);
            return;
        }
        // 인스펙터 결선이 없으면 영속 모달(GameManager.HeroInfoModal) 사용
        var modal = infoModal != null
            ? infoModal
            : (GameManager.Instance != null ? GameManager.Instance.HeroInfoModal : null);
        if (modal == null)
        {
            Debug.LogWarning($"[HeroProfileButton] HeroInfoModal 없음 (unitIndex={unitIndex})");
            return;
        }
        modal.Open(unitIndex);
    }
}
