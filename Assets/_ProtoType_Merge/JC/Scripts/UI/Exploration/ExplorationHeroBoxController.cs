using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260615] 탐사씬 하단 HeroBox(BTN_Explor_HeroBtn_1~4) 컨트롤러. (탐사 기획 ③-2/③-3)
/// 선택된 파티의 멤버를 버튼에 바인딩: 프로필 / 이름 / 현재HP·최대HP / 현재IP·최대IP.
/// 버튼 클릭 → 영속 HeroInfoModal.Open.
/// [JC 260630] 파티 선택 탭(③-1) 코드 전면 제거 — PartyTabBar GO 삭제에 대응.
/// </summary>
[DisallowMultipleComponent]
public class ExplorationHeroBoxController : MonoBehaviour
{
    [Serializable]
    public class HeroSlot
    {
        public GameObject root;      // 버튼 GO (BTN_Explor_HeroBtn_n)
        public Button button;
        public Image profile;
        public TMP_Text nameText;
        public TMP_Text hpText;
        public Image hpFill;         // [KJ 260909] HP 게이지(Image Type=Filled). 미연결이면 무시
        public TMP_Text ipText;
        public GameObject ipMaxIcon;
        public JcInfluenceBadgeView influenceBadge;
    }

    [Header("멤버 슬롯 (BTN_Explor_HeroBtn 1~4)")]
    [SerializeField] private HeroSlot[] slots = new HeroSlot[4];

    [Header("파티")]
    [Tooltip("빈칸이면 selectedPartyIndex로 선택. 보통 빈칸")]
    [SerializeField] private string targetPartyId = "";

    private int selectedPartyIndex;
    // [JC 260625] slots 길이에 맞춰 Awake에서 할당(고정 4 → IndexOutOfRange 방지). slots 5+ 설정해도 안전.
    private int[] boundUnits = System.Array.Empty<int>();
    private GameObject[] ipMaxIcons = System.Array.Empty<GameObject>();
    private Image[] defeatedOverlays = System.Array.Empty<Image>();
    private GameObject[] selectionFrames = System.Array.Empty<GameObject>();
    private RectTransform[] licenseClickThrough = System.Array.Empty<RectTransform>();
    private ResourceGainNumberMotion[] ipMotions = System.Array.Empty<ResourceGainNumberMotion>();
    private int[] ipMotionUnits = System.Array.Empty<int>();
    private HeroInfoModal selectedLicense;
    private int selectedHero = -1;
    private EconomyManager subEco; // 갱신 트리거(자원/IP 변동) 용
    private float nextRefresh;     // [JC 260616] 스탯/HP/IP 변동 주기적 재반영 타이머

    private void Awake()
    {
        boundUnits = new int[slots != null ? slots.Length : 0];
        ipMaxIcons = new GameObject[boundUnits.Length];
        defeatedOverlays = new Image[boundUnits.Length];
        selectionFrames = new GameObject[boundUnits.Length];
        licenseClickThrough = new RectTransform[boundUnits.Length];
        ipMotions = new ResourceGainNumberMotion[boundUnits.Length];
        ipMotionUnits = new int[boundUnits.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            ipMotions[i] = new ResourceGainNumberMotion(animateColor: false,
                noticeIcon: () => Sprites.Load("NpcPreview/UI_icon_IP"));
            if (slots[i]?.root != null)
            {
                selectionFrames[i] = slots[i].root.transform.Find("ProfileHoverBorder")?.gameObject;
                licenseClickThrough[i] = slots[i].root.transform as RectTransform;
                foreach (var tooltip in slots[i].root.GetComponentsInChildren<WorldInfluenceBadgeTooltip>(true))
                    tooltip.BindOwner(this);
            }
            if (slots[i]?.root != null && slots[i].influenceBadge == null)
                slots[i].influenceBadge = slots[i].root.GetComponentInChildren<JcInfluenceBadgeView>(true);
            if (slots[i]?.root != null)
                foreach (var child in slots[i].root.GetComponentsInChildren<Transform>(true))
                    if (child.name == "UI_icon_max")
                    {
                        ipMaxIcons[i] = child.gameObject;
                        ipMaxIcons[i].SetActive(false);
                        break;
                    }
            int ci = i;
            // JC | 2026-11-04 | 전투·탐사 HUD의 마스크 안에서 아이콘 비율과 상태 표시를 유지합니다.
            defeatedOverlays[i] = CreateDefeatedOverlay(slots[i]);
            if (slots[i]?.button != null) slots[i].button.onClick.AddListener(() => OnSlotClicked(ci));
        }
    }

    public static Image CreateDefeatedOverlay(HeroSlot slot)
    {
        var source = slot?.root != null ? slot.root.GetComponent<Image>() : null;
        if (source == null) return null;
        var existing = source.transform.Find("DefeatedOverlay");
        if (existing != null && existing.TryGetComponent<Image>(out var overlayImage)) return overlayImage;
        var cover = new GameObject("DefeatedOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)cover.transform;
        rect.SetParent(slot.root.transform, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var overlay = cover.GetComponent<Image>();
        overlay.sprite = source.sprite; overlay.type = source.type; overlay.preserveAspect = source.preserveAspect;
        overlay.material = Resources.Load<Material>("UI/DefeatedCardOverlay");
        overlay.color = new Color(.5f, .5f, .5f, .65f);
        overlay.raycastTarget = true;
        cover.SetActive(false);
        return overlay;
    }

    private void OnEnable() { Refresh(); }

    private void Update()
    {
        foreach (var motion in ipMotions) motion.Advance(Time.unscaledDeltaTime);
        RefreshSelection();
        if (GameManager.Instance == null) return;
        if (boundUnits.Length == 0) return;
        // 영속 매니저가 늦게 준비되면 즉시 보강
        if (boundUnits[0] == 0) { Refresh(); return; }
        // [JC 260616] HP/스탯/IP 변동(맵 이벤트·트레이닝 등)은 통지 이벤트가 없어 주기적으로 재반영
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.3f;
        Refresh();
    }

    private PartyPersistentData ResolveParty()
    {
        var repo = PartyPersistentRepository.Instance;
        if (repo == null || repo.Parties.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(targetPartyId))
            for (int p = 0; p < repo.Parties.Count; p++)
                if (repo.Parties[p] != null && repo.Parties[p].PartyId == targetPartyId) return repo.Parties[p];
        int idx = Mathf.Clamp(selectedPartyIndex, 0, repo.Parties.Count - 1);
        return repo.Parties[idx];
    }

    public void Refresh()
    {
        var party = ResolveParty();
        var unitRepo = PersistentUnitRepository.Instance;
        var catalog = DHCsvTemplateCatalog.Instance;

        // [JC 260628] 전열→후열 순(PartyFormation 공용 규약)으로 표시 — 전력평가·전투결과와 일치
        var members = party != null
            ? PartyFormation.OrderFrontFirst(party.UnitIndices, party.UnitSlots)
            : new List<int>();

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;
            bool active = i < members.Count;
            if (slot.root != null) slot.root.SetActive(active);
            boundUnits[i] = active ? members[i] : 0;
            if (!active)
            {
                ipMotions[i].Reset();
                ipMotionUnits[i] = 0;
                if (ipMaxIcons[i] != null) ipMaxIcons[i].SetActive(false);
                continue;
            }

            int unitIndex = members[i];
            UnitPersistentData unit = null;
            DHPlayerUnitTemplate template = null;
            if (unitRepo != null) unitRepo.TryGetUnit(unitIndex, out unit);
            if (slot.influenceBadge != null) slot.influenceBadge.SetUnit(unit);
            ApplySlotAvailability(i, unit);
            if (unit != null && catalog != null && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey))
                catalog.TryGetPlayerUnitTemplate(unit.UnitTemplateKey, out template);

            // [KJ 261008] 탐사 카드도 현재 영웅 레벨에 맞춰 랭크 배지를 갱신한다.
            var rankBadge = slot.root != null ? slot.root.GetComponentInChildren<HeroRankBadge>(true) : null;
            if (rankBadge != null)
                rankBadge.SetRank(unit != null ? UnitRankLookup.GetRank(unit.Level) : UnitRankLookup.Unknown);

            if (slot.profile != null)
            {
                // [JC 260621] 포트레이트 = unitIndex(=HeroIndex) 기준 PortraitLibrary 해석.
                var sp = HeroProfileCatalog.GetByUnitIndex(unitIndex);
                slot.profile.enabled = sp != null;
                if (sp != null) slot.profile.sprite = sp;
            }
            if (slot.nameText != null)
                slot.nameText.text = template != null && !string.IsNullOrWhiteSpace(template.UnitName)
                    ? template.UnitName : (unit != null ? unit.UnitTemplateKey : "-");
            {
                float cur = unit != null ? unit.CurrentHp : 0f;
                float max = unit != null ? unit.IngameStats.HP : 0f; // [JC 260616] 표시는 인게임 스탯(베이스는 내부 연산용)
                if (slot.hpText != null) slot.hpText.text = $"{cur:F0}/{max:F0}";
                // [KJ 260909] hpText 미연결(게이지만 쓰는 카드)에서도 게이지가 갱신되도록 값 계산을 블록 밖으로 분리
                if (slot.hpFill != null) slot.hpFill.fillAmount = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
            }
            if (ipMaxIcons[i] != null)
                ipMaxIcons[i].SetActive(unit != null && IsIpAtMax(unit.CurrentInfluence, unit.IngameStats.Influence));
            if (slot.ipText != null)
            {
                // [JC 260616] IP 표기 = 유닛 CurrentInfluence/IngameStats.Influence 일원화(PublicityManager 의존 제거)
                float ip = unit != null ? unit.CurrentInfluence : 0f;
                UpdateIpNumber(i, unitIndex, ip);
            }
        }
    }

    public IEnumerable<TMP_Text> GetIpRewardLabels()
    {
        var repo = PersistentUnitRepository.Instance;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i]?.ipText != null && slots[i].ipText.gameObject.activeInHierarchy &&
                boundUnits[i] > 0 && repo != null && repo.TryGetUnit(boundUnits[i], out var unit) &&
                unit.CurrentInfluence < unit.IngameStats.Influence)
                yield return slots[i].ipText;
    }

    private void UpdateIpNumber(int slotIndex, int unitIndex, float ip)
    {
        if (ipMotionUnits[slotIndex] != unitIndex)
        {
            ipMotions[slotIndex].Reset();
            ipMotionUnits[slotIndex] = unitIndex;
        }
        ipMotions[slotIndex].Set(slots[slotIndex].ipText, Mathf.RoundToInt(ip));
    }

    public RectTransform GetInfluenceTooltipAnchor(string heroKey, RectTransform fallback)
    {
        // 저스티스/루미나는 첫 슬롯 높이, 네코밍/블랙 불릿은 두 번째 슬롯 높이를 공유합니다.
        int index = heroKey == "10001" || heroKey == "10002" ? 0 :
            heroKey == "10003" || heroKey == "10004" ? 1 : -1;
        if (index < 0 || slots == null || index >= slots.Length || slots[index]?.root == null) return fallback;
        var anchor = slots[index].root.transform.Find("InfluenceTooltipAnchor") as RectTransform;
        return anchor != null ? anchor : fallback;
    }

    private static bool IsIpAtMax(float current, float maximum)
        => maximum > 0f && current >= maximum;

    private void ApplySlotAvailability(int i, UnitPersistentData unit)
    {
        bool defeated = unit != null && (unit.IsIncapacitated || unit.CurrentHp <= 0f);
        if (slots[i].button != null) slots[i].button.interactable = unit != null && !defeated;
        if (defeatedOverlays[i] != null)
        {
            defeatedOverlays[i].gameObject.SetActive(defeated);
            if (defeated) defeatedOverlays[i].transform.SetAsLastSibling();
        }
    }

    private void OnSlotClicked(int i)
    {
        if (i < 0 || i >= boundUnits.Length || boundUnits[i] <= 0) return;
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.TryGetUnit(boundUnits[i], out var unit) ||
            unit.IsIncapacitated || unit.CurrentHp <= 0f) return;
        var cui = CommonUIManager.Instance;
        if (cui == null) return;
        // [JC 260619] 탐사 멤버 클릭 → 로비와 동일 레이아웃(Modal_HeroInfo)으로 통일. HeroStatusModal 우선선택 폐기.
        var modal = cui.HeroInfoModal;
        if (modal != null)
        {
            modal.Open(boundUnits[i], dimBackground: false, clickThrough: licenseClickThrough);
            selectedLicense = modal;
            selectedHero = boundUnits[i];
            RefreshSelection();
        }
    }

    private void RefreshSelection()
    {
        if (selectedLicense == null || !selectedLicense.gameObject.activeInHierarchy ||
            selectedLicense.CurrentUnitIndex != selectedHero)
        {
            selectedLicense = null;
            selectedHero = -1;
        }
        for (int i = 0; i < selectionFrames.Length; i++)
        {
            bool selected = selectedHero > 0 && boundUnits[i] == selectedHero;
            if (selectionFrames[i] != null && selectionFrames[i].activeSelf != selected)
                selectionFrames[i].SetActive(selected);
        }
    }

    private void OnDisable()
    {
        foreach (var motion in ipMotions) motion.Reset();
        selectedLicense = null;
        selectedHero = -1;
        RefreshSelection();
    }
}
