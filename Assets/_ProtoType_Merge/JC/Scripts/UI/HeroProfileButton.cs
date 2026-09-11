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

    [Header("장착 코어명 (공방 2단계)")]
    [Tooltip("장착 중인 코어명. 카탈로그가 없거나 미장착이면 '—'.")]
    [SerializeField] private TMP_Text coreText;

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
    private HeroRankBadge rankBadge; // [KJ 260910] 파티 카드 랭크 배지(자식에서 캐시, SerializeField 추가 안 함)

    /// <summary>바인딩된 영웅 인덱스(출전 드래그 등 외부 결합용).</summary>
    public int UnitIndex => unitIndex;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClick);
        if (background == null) background = GetComponent<Image>();
        rankBadge = GetComponentInChildren<HeroRankBadge>(true);
    }

    public void Bind(int unitIndex, HeroInfoModal infoModal, UnitPersistentData unit, DHPlayerUnitTemplate template, bool inParty)
    {
        this.unitIndex = unitIndex;
        this.infoModal = infoModal;
        this.selectCallback = null;
        ApplyDisplay(unit, template, inParty);
    }

    public void BindForSelect(int unitIndex, Action<int> onSelected, UnitPersistentData unit, DHPlayerUnitTemplate template, bool inParty)
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

    private void ApplyDisplay(UnitPersistentData unit, DHPlayerUnitTemplate template, bool inParty)
    {
        string displayName = template != null && !string.IsNullOrWhiteSpace(template.UnitName)
            ? template.UnitName
            : (unit != null ? unit.UnitTemplateKey : $"unit_{unitIndex}");
        string displayClass = template != null ? template.ClassName : "-";
        int level = unit != null ? unit.Level : 1;

        if (nameText != null) nameText.text = displayName;
        if (classText != null) classText.text = displayClass;
        if (levelText != null) levelText.text = $"Lv {level}";
        if (rankBadge != null) rankBadge.SetLevel(level); // [KJ 260910] 랭크 배지
        // [KJ 260729] 공방 2단계 — 장착 코어명 표시.
        if (coreText != null) coreText.text = ResolveCoreName();

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

    /// <summary>[KJ 260729] 장착 코어명. 공방/카탈로그가 없거나 미장착이면 '—'.
    /// unitIndex는 Bind/BindForSelect에서 ApplyDisplay 호출 전에 대입된다.</summary>
    private string ResolveCoreName()
    {
        const string none = "—";
        var gm = GameManager.Instance;
        if (gm == null || gm.Workshop == null || unitIndex <= 0) return none;

        int weaponIndex = gm.Workshop.GetEquippedWeaponIndex(unitIndex);
        if (weaponIndex <= 0) return none;

        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null || !catalog.TryGetWeaponTemplate(weaponIndex, out DHWeaponTemplate wd) || wd == null) return none;
        return string.IsNullOrWhiteSpace(wd.WeaponName) ? none : wd.WeaponName;
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
        // 인스펙터 결선이 없으면 영속 모달(CommonUIManager.HeroInfoModal) 사용
        var modal = infoModal != null
            ? infoModal
            : (CommonUIManager.Instance != null ? CommonUIManager.Instance.HeroInfoModal : null);
        if (modal == null)
        {
            Debug.LogWarning($"[HeroProfileButton] HeroInfoModal 없음 (unitIndex={unitIndex})");
            return;
        }
        modal.Open(unitIndex);
    }
}
