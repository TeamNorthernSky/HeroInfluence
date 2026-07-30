using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 공방 모달 컨트롤러 (코어 5종 카드 레이아웃).
/// [KJ 260729] 2단계 — WorkshopManager 실데이터 연결.
///   - 카드 5장은 계정 소유 상태를 표시한다. 영웅 선택과 무관하게 항상 보이고 제작·강화가 가능하다.
///   - 주 버튼: 미보유=제작, 보유=강화. 미해금·레벨 미달·자원 부족·최대 레벨이면 비활성 + 사유 노출.
///   - 「영웅 선택」 버튼: 영웅 미선택이면 안내(비활성), 선택되면 「장착」으로 전환.
///   - 자원 차감은 여기서 한다(LabModalController.OnConfirm과 같은 패턴). 실패 시 환불한다.
/// 설계: docs/superpowers/specs/2026-07-29-workshop-phase2-design.md
/// </summary>
[DisallowMultipleComponent]
public class WorkshopModalController : MonoBehaviour
{
    public const int CoreCount = 5;

    [Serializable]
    public class CoreCard
    {
        public Button button;
        public Image icon;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI levelText;
        public GameObject selectedFrame;
        public GameObject lockMark;
    }

    [Header("Modal_Workshop 본체")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button btnClose;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("영웅 영역")]
    [SerializeField] private Image heroProfileImage;
    [SerializeField] private GameObject selectPromptGo;
    [Tooltip("HeroSlot/EquippedCoreInfo — 선택 영웅이 장착한 코어의 아이콘·이름")]
    [SerializeField] private Image equippedCoreIcon;
    [SerializeField] private TextMeshProUGUI equippedCoreNameText;

    [Header("코어 카드 5장")]
    [SerializeField] private List<CoreCard> cards = new List<CoreCard>();

    [Header("아이콘·이름·설명 (인스펙터 직결, 카탈로그 폴백용)")]
    [SerializeField] private Sprite[] coreIcons = new Sprite[CoreCount];
    [SerializeField] private string[] coreNames = new string[CoreCount]
        { "기본 코어", "광역 코어", "방어 코어", "회복 코어", "위압 코어" };
    [Tooltip("카탈로그(DHCsvTemplateCatalog)에 설명이 있으면 그 값으로 덮어쓴다.")]
    [SerializeField] private string[] coreDescs = new string[CoreCount];

    [Header("상세 패널")]
    [SerializeField] private TextMeshProUGUI detailTitleText;
    [SerializeField] private TextMeshProUGUI detailDescText;
    [SerializeField] private TextMeshProUGUI sharedLevelText;

    [Header("하단 액션 바")]
    [SerializeField] private TextMeshProUGUI condText;
    [SerializeField] private GameObject condBadge;
    [SerializeField] private TextMeshProUGUI costMoneyText;
    [SerializeField] private GameObject goldBadge;
    [SerializeField] private TextMeshProUGUI costCrystalText;
    [SerializeField] private GameObject crystalBadge;
    [SerializeField] private Button btnConfirm;
    [SerializeField] private TextMeshProUGUI btnConfirmLabel;
    [SerializeField] private Button btnHeroSelect;
    [SerializeField] private TextMeshProUGUI btnHeroSelectLabel;

    private const string NoValue = "—";

    private int selectedCore = -1;      // 0~4, 미선택 -1
    private int selectedUnitIndex = -1;

    private WorkshopManager subWorkshop;
    private EconomyManager subEco;

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);

        if (btnConfirm != null)
        {
            btnConfirm.onClick.AddListener(OnPrimaryClicked);
            if (btnConfirmLabel == null) btnConfirmLabel = btnConfirm.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        if (btnHeroSelect != null)
        {
            btnHeroSelect.onClick.AddListener(OnEquipClicked);
            if (btnHeroSelectLabel == null) btnHeroSelectLabel = btnHeroSelect.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        for (int i = 0; i < cards.Count; i++)
        {
            int ci = i;
            if (cards[i]?.button != null) cards[i].button.onClick.AddListener(() => OnCardClicked(ci));
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
        var roster = LobbyUIRegistry.RosterView;
        if (roster != null) roster.BeginSelection(OnHeroSelected);
        else Debug.LogWarning("[WorkshopModalController] LobbyUIRegistry.RosterView 없음 — 영웅 선택 불가.");
        if (selectedCore < 0) selectedCore = 0;   // 진입 시 첫 카드 선택
        // [KJ 260729] 우클릭 해제를 없앤 대신 진입 시 기본 유닛(블래스터)을 선택해 둔다.
        if (selectedUnitIndex < 0)
        {
            int defaultUnit = RosterOrdering.ResolveDefaultUnit();
            if (defaultUnit >= 0) OnHeroSelected(defaultUnit);
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (LobbyUIRegistry.RosterView != null) LobbyUIRegistry.RosterView.EndSelection();
        selectedUnitIndex = -1;
        Unsubscribe();
    }

    // 매니저 생성 타이밍이 모달 활성화보다 늦을 수 있어 구독을 재시도한다(LabModalController와 같은 방식).
    private void Update() { if (subWorkshop == null || subEco == null) TrySubscribe(); }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (subWorkshop == null && gm.Workshop != null)
        {
            subWorkshop = gm.Workshop;
            subWorkshop.OnStateChanged += Refresh;
        }
        if (subEco == null && gm.Economy != null)
        {
            subEco = gm.Economy;
            subEco.OnResourceChanged += OnResourceChanged;
        }
        Refresh();
    }

    private void Unsubscribe()
    {
        if (subWorkshop != null) { subWorkshop.OnStateChanged -= Refresh; subWorkshop = null; }
        if (subEco != null) { subEco.OnResourceChanged -= OnResourceChanged; subEco = null; }
    }

    private void OnResourceChanged(ResourceType _, int __) => Refresh();

    public void CloseModal()
    {
        if (modalRoot != null) modalRoot.SetActive(false);
    }

    private void OnCardClicked(int index)
    {
        selectedCore = index;
        Refresh();
    }

    private void OnHeroSelected(int unitIndex)
    {
        selectedUnitIndex = unitIndex;
        // 장착 인스턴스가 계정 소유 집합에 없으면 코어 1로 정정(강화 공유 보장).
        var gm = GameManager.Instance;
        if (gm != null && gm.Workshop != null) gm.Workshop.EnsureDefaultEquipped(unitIndex);
        if (LobbyUIRegistry.RosterView != null) LobbyUIRegistry.RosterView.SetSelected(unitIndex);
        Refresh();
    }

    // ─── 카탈로그 폴백 해석 ────────────────────────────────────
    private static bool TryGetWeaponData(int weaponIndex, out WeaponData data)
    {
        data = null;
        var cat = DHCsvTemplateCatalog.Instance;
        return cat != null && cat.TryGetWeapon(weaponIndex, out data) && data != null;
    }

    private string CoreName(int i)
    {
        if (TryGetWeaponData(i + 1, out var wd) && !string.IsNullOrWhiteSpace(wd.WeaponName))
            return wd.WeaponName;
        return (coreNames != null && i >= 0 && i < coreNames.Length && !string.IsNullOrWhiteSpace(coreNames[i]))
               ? coreNames[i] : $"코어 {i + 1}";
    }

    private string CoreDesc(int i)
    {
        if (TryGetWeaponData(i + 1, out var wd) && !string.IsNullOrWhiteSpace(wd.WeaponDescription))
            return wd.WeaponDescription;
        return (coreDescs != null && i >= 0 && i < coreDescs.Length && !string.IsNullOrWhiteSpace(coreDescs[i]))
               ? coreDescs[i] : "설명 준비 중";
    }

    private Sprite CoreIcon(int i)
        => (coreIcons != null && i >= 0 && i < coreIcons.Length) ? coreIcons[i] : null;

    // ─── 표시 ──────────────────────────────────────────────────
    private void Refresh()
    {
        var gm = GameManager.Instance;
        var ws = gm != null ? gm.Workshop : null;
        var eco = gm != null ? gm.Economy : null;

        int deptLevel = ws != null ? ws.GetDepartmentLevel() : 0;
        bool unlocked = ws != null && ws.IsUnlocked();
        bool hasHero = selectedUnitIndex >= 0;

        if (titleText != null) titleText.text = $"공방 Lv.{deptLevel}";
        if (selectPromptGo != null) selectPromptGo.SetActive(!hasHero);
        if (heroProfileImage != null)
        {
            heroProfileImage.enabled = true;
            heroProfileImage.sprite = hasHero
                ? HeroProfileCatalog.GetByUnitIndex(selectedUnitIndex)
                : HeroProfileCatalog.Default;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            int wi = i + 1;
            bool owned = ws != null && ws.IsOwned(wi);
            int level = owned ? Mathf.Max(WorkshopManager.BaseWeaponLevel, ws.GetWeaponLevel(wi)) : 0;

            if (c.icon != null) { var sp = CoreIcon(i); c.icon.sprite = sp; c.icon.enabled = sp != null; }
            if (c.nameText != null) c.nameText.text = CoreName(i);
            if (c.levelText != null) c.levelText.text = owned ? $"Lv.{level}" : NoValue;
            if (c.selectedFrame != null) c.selectedFrame.SetActive(i == selectedCore);
            if (c.lockMark != null) c.lockMark.SetActive(!owned);
        }

        bool valid = selectedCore >= 0 && selectedCore < cards.Count;
        int sel = valid ? selectedCore + 1 : 0;
        bool selOwned = valid && ws != null && ws.IsOwned(sel);
        int selLevel = selOwned ? Mathf.Max(WorkshopManager.BaseWeaponLevel, ws.GetWeaponLevel(sel)) : 0;

        if (detailTitleText != null) detailTitleText.text = valid ? CoreName(selectedCore) : "-";
        if (detailDescText != null) detailDescText.text = valid ? CoreDesc(selectedCore) : string.Empty;
        if (sharedLevelText != null) sharedLevelText.text = selOwned ? $"Lv.{selLevel}" : NoValue;

        ApplyActionBar(ws, eco, valid, sel, selOwned, selLevel, deptLevel, unlocked);
        ApplyEquipButton(ws, hasHero, sel, selOwned);
        ApplyEquippedCoreInfo(ws, hasHero);
    }

    /// <summary>EquippedCoreInfo — 선택 영웅이 장착한 코어의 아이콘·이름.
    /// 미장착 상태는 두지 않는다. EnsureDefaultEquipped가 기본 코어를 보장하므로
    /// 조회가 비면(매니저 준비 전 등) 기본 코어로 표시한다.</summary>
    private void ApplyEquippedCoreInfo(WorkshopManager ws, bool hasHero)
    {
        int wi = (hasHero && ws != null) ? ws.GetEquippedWeaponIndex(selectedUnitIndex) : 0;
        if (wi < WorkshopManager.MinWeaponIndex || wi > WorkshopManager.MaxWeaponIndex)
            wi = WorkshopManager.DefaultWeaponIndex;

        if (equippedCoreIcon != null)
        {
            Sprite sp = CoreIcon(wi - 1);
            equippedCoreIcon.sprite = sp;
            equippedCoreIcon.enabled = sp != null;
        }
        if (equippedCoreNameText != null)
            equippedCoreNameText.text = CoreName(wi - 1);
    }

    /// <summary>주 버튼 라벨·활성 여부와 조건·비용 표시를 한 번에 결정한다.</summary>
    private void ApplyActionBar(WorkshopManager ws, EconomyManager eco,
        bool valid, int weaponIndex, bool owned, int level, int deptLevel, bool unlocked)
    {
        string primary = owned ? "강화" : "제작";
        string cond;
        bool costKnown = false;
        bool levelShort = false;
        bool enabled = false;
        int reqLevel = -1, money = 0, crystal = 0;

        if (ws == null || !valid)
        {
            cond = "공방 정보를 불러올 수 없습니다.";
        }
        else if (!unlocked)
        {
            cond = "공방이 아직 해금되지 않았습니다.";
        }
        else if (owned && level >= WorkshopManager.MaxWeaponLevel)
        {
            primary = "최대 강화";
            cond = $"현재 공방 Lv.{deptLevel} / 최대 레벨 도달";
        }
        else
        {
            costKnown = owned
                ? ws.GetEnhanceCost(weaponIndex, level, out reqLevel, out money, out crystal)
                : ws.GetCraftCost(weaponIndex, out reqLevel, out money, out crystal);

            if (!costKnown)
            {
                cond = $"현재 공방 Lv.{deptLevel} / 비용 정보 없음";
            }
            else
            {
                cond = $"현재 공방 Lv.{deptLevel} / 필요 Lv.{reqLevel}";
                levelShort = deptLevel < reqLevel;
                if (levelShort)
                {
                    primary = $"공방 Lv.{reqLevel} 필요";
                }
                else
                {
                    bool canPay = eco != null
                                  && eco.Has(ResourceType.Money, money)
                                  && eco.Has(ResourceType.Crystal, crystal);
                    enabled = canPay && (owned ? ws.CanEnhance(weaponIndex) : ws.CanCraft(weaponIndex));
                }
            }
        }

        bool lackMoney = costKnown && (eco == null || !eco.Has(ResourceType.Money, money));
        bool lackCrystal = costKnown && (eco == null || !eco.Has(ResourceType.Crystal, crystal));

        if (condText != null) condText.text = cond;
        if (condBadge != null) condBadge.SetActive(levelShort);
        if (costMoneyText != null) costMoneyText.text = costKnown ? $"{money:N0}" : NoValue;
        if (goldBadge != null) goldBadge.SetActive(lackMoney);
        if (costCrystalText != null) costCrystalText.text = costKnown ? $"{crystal:N0}" : NoValue;
        if (crystalBadge != null) crystalBadge.SetActive(lackCrystal);
        if (btnConfirmLabel != null) btnConfirmLabel.text = primary;
        if (btnConfirm != null) btnConfirm.interactable = enabled;
    }

    /// <summary>영웅 미선택이면 「영웅 선택」 안내(비활성), 선택되면 「장착」/「장착중」.</summary>
    private void ApplyEquipButton(WorkshopManager ws, bool hasHero, int weaponIndex, bool owned)
    {
        if (!hasHero)
        {
            if (btnHeroSelectLabel != null) btnHeroSelectLabel.text = "영웅 선택";
            if (btnHeroSelect != null) btnHeroSelect.interactable = false;
            return;
        }

        bool alreadyEquipped = ws != null && ws.GetEquippedWeaponIndex(selectedUnitIndex) == weaponIndex;
        if (btnHeroSelectLabel != null) btnHeroSelectLabel.text = alreadyEquipped ? "장착중" : "장착";
        if (btnHeroSelect != null) btnHeroSelect.interactable = owned && !alreadyEquipped;
    }

    // ─── 실행 ──────────────────────────────────────────────────
    /// <summary>주 버튼 — 미보유면 제작, 보유면 강화 1단계. 자원 차감·환불을 여기서 처리한다.</summary>
    private void OnPrimaryClicked()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Workshop == null || gm.Economy == null) return;
        if (selectedCore < 0 || selectedCore >= cards.Count) return;

        var ws = gm.Workshop;
        var eco = gm.Economy;
        int wi = selectedCore + 1;
        bool owned = ws.IsOwned(wi);
        int level = owned ? ws.GetWeaponLevel(wi) : 0;

        int money, crystal;
        bool costKnown = owned
            ? ws.GetEnhanceCost(wi, level, out _, out money, out crystal)
            : ws.GetCraftCost(wi, out _, out money, out crystal);
        if (!costKnown) return;

        if (!(owned ? ws.CanEnhance(wi) : ws.CanCraft(wi))) return;
        if (!eco.Has(ResourceType.Money, money) || !eco.Has(ResourceType.Crystal, crystal)) return;

        if (money > 0 && !eco.Spend(ResourceType.Money, money)) return;
        if (crystal > 0 && !eco.Spend(ResourceType.Crystal, crystal))
        {
            if (money > 0) eco.Add(ResourceType.Money, money);
            return;
        }

        bool ok = owned ? ws.TryEnhance(wi) : ws.TryCraft(wi);
        if (!ok)
        {
            if (money > 0) eco.Add(ResourceType.Money, money);
            if (crystal > 0) eco.Add(ResourceType.Crystal, crystal);
            Debug.LogWarning($"[WorkshopModalController] {(owned ? "강화" : "제작")} 실패 — 자원 환불 (weaponIndex={wi})");
            return;
        }
        Refresh();
    }

    /// <summary>「장착」 버튼 — 선택 영웅에게 선택 코어를 장착한다.</summary>
    private void OnEquipClicked()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Workshop == null) return;
        if (selectedUnitIndex < 0) return;
        if (selectedCore < 0 || selectedCore >= cards.Count) return;

        if (!gm.Workshop.EquipWeapon(selectedUnitIndex, selectedCore + 1)) return;

        // [KJ 260729] 로스터는 WorkshopManager.OnStateChanged를 구독하지 않으므로 직접 재생성해
        //   카드의 장착 코어명을 갱신한다. Rebuild 말미에 선택 하이라이트가 복원된다.
        var roster = LobbyUIRegistry.RosterView;
        if (roster != null)
        {
            roster.Rebuild();
            roster.SetSelected(selectedUnitIndex);
        }
        Refresh();
    }
}
