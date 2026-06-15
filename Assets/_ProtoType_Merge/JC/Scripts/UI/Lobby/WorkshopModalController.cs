using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 공방 모달 컨트롤러. 영웅 선택 후 클래스 무기(하급/중급/상급)를 슬롯에 표시하고
///   - 비장착 보유 무기 클릭 "즉시" 장착 (WorkshopManager.EquipWeapon, 실결선)
///   - 미보유·제작가능 무기 선택 후 진행 → 제작+자동장착 (TryCraft)
///   - 장착중 무기 선택 후 진행 → 강화 (TryEnhance, 스탯 실반영)
/// 을 수행한다. LabModalController와 동형. 무기 스킬 계수 강화의 전투 반영은 보류(더미).
/// 비용 출처: H.I 자원 데이터 테이블 V1.4 협회-공방(제작/강화). 소모 = 자금 + 수정.
/// </summary>
[DisallowMultipleComponent]
public class WorkshopModalController : MonoBehaviour
{
    private enum SlotAction { None, Craft, Enhance }

    [Serializable]
    public class WeaponSlot
    {
        public Button button;
        public Image icon;
        public GameObject usingMark;      // 장착중 마크
        public GameObject selectedFrame;  // 선택 프레임
        public GameObject lockedOverlay;  // 사용 불가(미해금/제작 선행조건 미충족) 프레임
        public GameObject finishedMark;   // 강화 완료(Max)
        public GameObject lockMark;       // 미보유 자물쇠 마크
        public TextMeshProUGUI levelText; // Lv.n / 미보유 표시
    }

    [Header("Modal_Workshop 본체")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnCancel;

    [Header("영웅 영역")]
    [SerializeField] private Button btnHeroSlot;
    [SerializeField] private Image heroProfileImage;
    [SerializeField] private GameObject heroSilhouette;
    [SerializeField] private GameObject selectPromptGo;
    [SerializeField] private TextMeshProUGUI heroNameText;

    [Header("무기 슬롯 (고정 배열, 씬 배치)")]
    [SerializeField] private List<WeaponSlot> slots = new List<WeaponSlot>();

    [Header("필요 자원 표시")]
    [SerializeField] private TextMeshProUGUI costMoneyText;
    [SerializeField] private TextMeshProUGUI costCrystalText;
    [SerializeField] private TextMeshProUGUI stateInfoText;

    [Header("무기 아이콘 (임시 리소스, tier 매핑 + 강화레벨)")]
    [Tooltip("{0}=tier(1~3), {1}=강화레벨(1~5)")]
    [SerializeField] private string weaponIconPathFormat = "UI_Sprite/UI_Icon/Weapon_temp/weapon {0:00} level {1}";

    [Header("영웅 선택 sub-modal")]
    [SerializeField] private GameObject heroSelectModalRoot;
    [SerializeField] private HeroListController heroSelectListController;
    [SerializeField] private Button btnHeroSelectClose;

    private int selectedUnitIndex = -1;
    private int selectedWeaponIndex = -1;
    private SlotAction selectedAction = SlotAction.None;
    private readonly List<int> boundWeapons = new List<int>();

    private WorkshopManager subWs;
    private EconomyManager subEco;
    private readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

    // 무기 tier(1~3) + 강화레벨(1~5)로 아이콘 선택. 미보유(level 0)는 level 1 아이콘으로 표시.
    private Sprite GetWeaponIcon(int weaponIndex, int level)
    {
        int tier = Mathf.Clamp(WorkshopManager.TierOf(weaponIndex), 1, 3);
        int lv = Mathf.Clamp(level < 1 ? 1 : level, 1, 5);
        string path = string.Format(weaponIconPathFormat, tier, lv);
        if (!iconCache.TryGetValue(path, out var s)) { s = Resources.Load<Sprite>(path); iconCache[path] = s; }
        return s;
    }

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnCancel != null) btnCancel.onClick.AddListener(OnCancel);
        if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirm);
        if (btnHeroSlot != null) btnHeroSlot.onClick.AddListener(OpenHeroSelect);
        if (btnHeroSelectClose != null) btnHeroSelectClose.onClick.AddListener(CloseHeroSelect);

        for (int i = 0; i < slots.Count; i++)
        {
            int captured = i;
            if (slots[i]?.button != null)
                slots[i].button.onClick.AddListener(() => OnSlotClicked(captured));
        }

        if (heroSelectListController != null) heroSelectListController.SetSelectionMode(true);
    }

    private void OnEnable() { TrySubscribe(); Refresh(); }
    private void OnDisable() { Unsubscribe(); }
    private void Update() { if (subWs == null || subEco == null) TrySubscribe(); }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (subWs == null && gm.Workshop != null)
        {
            subWs = gm.Workshop;
            subWs.OnStateChanged += Refresh;
            if (heroSelectListController != null) heroSelectListController.UnitSelected += OnHeroSelected;
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
        if (subWs != null)
        {
            subWs.OnStateChanged -= Refresh;
            if (heroSelectListController != null) heroSelectListController.UnitSelected -= OnHeroSelected;
            subWs = null;
        }
        if (subEco != null) { subEco.OnResourceChanged -= OnResourceChanged; subEco = null; }
    }

    private void OnResourceChanged(ResourceType _, int __) => Refresh();

    public void CloseModal()
    {
        if (heroSelectModalRoot != null) heroSelectModalRoot.SetActive(false);
        if (modalRoot != null) modalRoot.SetActive(false);
    }

    private void OpenHeroSelect()
    {
        if (heroSelectModalRoot != null) heroSelectModalRoot.SetActive(true);
        if (heroSelectListController != null) heroSelectListController.Rebuild();
    }
    private void CloseHeroSelect() { if (heroSelectModalRoot != null) heroSelectModalRoot.SetActive(false); }

    private void OnHeroSelected(int unitIndex)
    {
        selectedUnitIndex = unitIndex;
        ClearSelection();
        CloseHeroSelect();
        Refresh();
    }

    private void ClearSelection() { selectedWeaponIndex = -1; selectedAction = SlotAction.None; }

    private void OnSlotClicked(int slotIdx)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Workshop == null) return;
        if (selectedUnitIndex < 0) return;
        if (slotIdx < 0 || slotIdx >= boundWeapons.Count) return;

        int w = boundWeapons[slotIdx];
        var ws = gm.Workshop;
        bool owned = ws.IsOwned(selectedUnitIndex, w);
        int equipped = ws.GetEquippedWeaponIndex(selectedUnitIndex);

        if (!owned)
        {
            if (ws.CanCraft(selectedUnitIndex, w)) Select(w, SlotAction.Craft);
            else ClearSelection();
        }
        else if (w != equipped)
        {
            ws.EquipWeapon(selectedUnitIndex, w); // 즉시 장착
            ClearSelection();
        }
        else
        {
            // 장착중 무기 → 강화 선택 토글
            if (selectedWeaponIndex == w && selectedAction == SlotAction.Enhance) ClearSelection();
            else Select(w, SlotAction.Enhance);
        }
        Refresh();
    }

    private void Select(int weaponIndex, SlotAction action) { selectedWeaponIndex = weaponIndex; selectedAction = action; }

    private void OnCancel()
    {
        if (selectedAction != SlotAction.None) { ClearSelection(); Refresh(); }
        else CloseModal();
    }

    private void OnConfirm()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Workshop == null || gm.Economy == null) return;
        if (selectedUnitIndex < 0 || selectedWeaponIndex < 0 || selectedAction == SlotAction.None) return;

        int money, crystal;
        bool ok;
        if (selectedAction == SlotAction.Craft)
        {
            if (!gm.Workshop.CanCraft(selectedUnitIndex, selectedWeaponIndex)) return;
            if (!gm.Workshop.GetCraftCost(selectedWeaponIndex, out _, out money, out crystal)) return;
        }
        else
        {
            if (!gm.Workshop.CanEnhance(selectedUnitIndex, selectedWeaponIndex)) return;
            int level = gm.Workshop.GetWeaponLevel(selectedUnitIndex, selectedWeaponIndex);
            if (!gm.Workshop.GetEnhanceCost(selectedWeaponIndex, level, out _, out money, out crystal)) return;
        }

        if (!gm.Economy.Has(ResourceType.Money, money) || !gm.Economy.Has(ResourceType.Crystal, crystal)) return;
        if (!gm.Economy.Spend(ResourceType.Money, money)) return;
        if (!gm.Economy.Spend(ResourceType.Crystal, crystal)) { gm.Economy.Add(ResourceType.Money, money); return; }

        ok = selectedAction == SlotAction.Craft
            ? gm.Workshop.TryCraft(selectedUnitIndex, selectedWeaponIndex)
            : gm.Workshop.TryEnhance(selectedUnitIndex, selectedWeaponIndex);

        if (!ok)
        {
            gm.Economy.Add(ResourceType.Money, money);
            gm.Economy.Add(ResourceType.Crystal, crystal);
            return;
        }
        ClearSelection();
        Refresh();
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Workshop == null || gm.Economy == null) return;
        var ws = gm.Workshop;

        bool unlocked = ws.IsUnlocked();
        bool hasSelection = selectedUnitIndex >= 0;

        if (heroSilhouette != null) heroSilhouette.SetActive(!hasSelection);
        if (heroProfileImage != null) heroProfileImage.enabled = hasSelection;
        if (selectPromptGo != null) selectPromptGo.SetActive(!hasSelection);
        if (heroNameText != null)
            heroNameText.text = hasSelection
                ? (ws.TryResolveClass(selectedUnitIndex, out string c, out _) ? c : "—") : "";

        boundWeapons.Clear();
        if (hasSelection) boundWeapons.AddRange(ws.GetClassWeaponIndices(selectedUnitIndex));
        int equipped = hasSelection ? ws.GetEquippedWeaponIndex(selectedUnitIndex) : 0;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;
            bool active = i < boundWeapons.Count;
            if (slot.button != null) slot.button.gameObject.SetActive(active);
            if (!active) continue;

            int w = boundWeapons[i];
            bool owned = ws.IsOwned(selectedUnitIndex, w);
            int level = ws.GetWeaponLevel(selectedUnitIndex, w);
            bool isEquipped = owned && w == equipped;
            bool isSelected = w == selectedWeaponIndex;
            bool maxed = owned && level >= WorkshopManager.MaxWeaponLevel;
            // 잠금 표시: 미해금이거나, 미보유+제작불가
            bool locked = !unlocked || (!owned && !ws.CanCraft(selectedUnitIndex, w));

            if (slot.usingMark != null) slot.usingMark.SetActive(isEquipped);
            if (slot.selectedFrame != null) slot.selectedFrame.SetActive(isSelected);
            if (slot.lockedOverlay != null) slot.lockedOverlay.SetActive(locked);
            if (slot.lockMark != null) slot.lockMark.SetActive(!owned);
            if (slot.finishedMark != null) slot.finishedMark.SetActive(maxed);
            if (slot.levelText != null) slot.levelText.text = owned ? $"Lv.{level}" : "미보유";
            if (slot.icon != null) { var sp = GetWeaponIcon(w, level); slot.icon.sprite = sp; slot.icon.enabled = sp != null; }
        }

        // 필요 자원
        int reqM = 0, reqC = 0; bool showCost = false;
        if (hasSelection && selectedWeaponIndex >= 0 && selectedAction != SlotAction.None)
        {
            if (selectedAction == SlotAction.Craft)
                showCost = ws.GetCraftCost(selectedWeaponIndex, out _, out reqM, out reqC);
            else
            {
                int lv = ws.GetWeaponLevel(selectedUnitIndex, selectedWeaponIndex);
                showCost = ws.GetEnhanceCost(selectedWeaponIndex, lv, out _, out reqM, out reqC);
            }
        }
        if (costMoneyText != null) costMoneyText.text = showCost ? $"{reqM:N0}" : "—";
        if (costCrystalText != null) costCrystalText.text = showCost ? $"{reqC:N0}" : "—";

        bool canConfirm = showCost
                          && gm.Economy.Has(ResourceType.Money, reqM)
                          && gm.Economy.Has(ResourceType.Crystal, reqC);
        if (btnConfirm != null) btnConfirm.interactable = canConfirm;

        if (stateInfoText != null)
        {
            string msg = null;
            if (!unlocked) msg = "공방 기능이 활성화되지 않았습니다.";
            else if (!hasSelection) msg = "영웅을 선택해 주세요.";
            stateInfoText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
            if (!string.IsNullOrEmpty(msg)) stateInfoText.text = msg;
        }
    }
}
