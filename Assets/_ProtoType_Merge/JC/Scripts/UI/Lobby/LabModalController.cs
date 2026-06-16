using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 연구소 모달 컨트롤러. 영웅 선택 후, 보유 스킬을 슬롯에 표시하고
///   - 비장착 스킬 클릭 "즉시" 장착 변경 (LabManager.EquipSkill, 실결선)
///   - 슬롯 선택 후 진행 버튼으로 스킬 강화 (LabManager.TryUpgradeSkill, 비용 차감)
/// 을 수행한다. TrainingModalController 패턴 복제(영웅 선택 sub-modal = HeroListController selectionMode).
///
/// ※ 스킬 강화의 전투 계수 반영은 보류(더미) — [[feedback_seam_interface_policy]], LabManager 참조.
/// 슬롯 비주얼(아이콘 스프라이트 등)은 추후 공식 리소스 결선. 본 컨트롤러는 기능 로직 담당.
/// </summary>
[DisallowMultipleComponent]
public class LabModalController : MonoBehaviour
{
    [Serializable]
    public class SkillSlot
    {
        public Button button;
        public Image icon;
        public GameObject usingMark;       // 장착중 마크
        public GameObject selectedFrame;   // 업그레이드 선택 프레임
        public GameObject lockedOverlay;   // 사용 불가(선행조건 미충족) 프레임
        public GameObject finishedMark;    // 강화 완료(Max) 마크
        public GameObject lockMark;        // 미해금 자물쇠 마크
        public TextMeshProUGUI levelText;  // Lv. n
    }

    [Header("Modal_Lab 본체")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnConfirm; // 진행
    [SerializeField] private Button btnCancel;  // 취소

    [Header("영웅 영역")]
    [SerializeField] private Button btnHeroSlot;
    [SerializeField] private Image heroProfileImage;
    [SerializeField] private GameObject heroSilhouette;
    [SerializeField] private GameObject selectPromptGo;
    [SerializeField] private TextMeshProUGUI heroNameText;

    [Header("스킬 슬롯 (고정 배열, 씬 배치)")]
    [SerializeField] private List<SkillSlot> slots = new List<SkillSlot>();

    [Header("필요 자원 표시")]
    [SerializeField] private TextMeshProUGUI costMoneyText;
    [SerializeField] private TextMeshProUGUI costChipText;
    [SerializeField] private TextMeshProUGUI stateInfoText;

    [Header("스킬 아이콘 (임시 리소스, 순서 매핑 + 강화레벨)")]
    [Tooltip("{0}=종류(01~), {1}=강화레벨(1~5)")]
    [SerializeField] private string skillIconPathFormat = "UI_Sprite/UI_Icon/ClassSkill_temp/skill {0:00} level {1}";
    [Tooltip("준비된 스킬 아이콘 종류 수(슬롯 순서를 이 값으로 순환). 임시 2종.")]
    [SerializeField] private int skillIconVariants = 2;

    [Header("영웅 선택 sub-modal")]
    [SerializeField] private GameObject heroSelectModalRoot;
    [SerializeField] private HeroListController heroSelectListController;
    [SerializeField] private Button btnHeroSelectClose;

    private int selectedUnitIndex = -1;
    private int selectedSkillIndex = -1; // 업그레이드 대상으로 선택한 슬롯의 스킬
    private readonly List<SkillData> boundSkills = new List<SkillData>();

    private LabManager subLab;
    private EconomyManager subEco;
    private readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

    // 슬롯 순서(클래스 스킬 순서)를 준비된 종류 수로 순환(wraparound) + 강화레벨(1~5)로 아이콘 선택.
    private Sprite GetSkillIcon(int order, int level)
    {
        int variants = Mathf.Max(1, skillIconVariants);
        int v = (order % variants + variants) % variants + 1;
        int lv = Mathf.Clamp(level, 1, 5);
        string path = string.Format(skillIconPathFormat, v, lv);
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

        if (heroSelectListController != null)
        {
            heroSelectListController.SetSelectionMode(true);
            heroSelectListController.SetVisitingOnlyMode(true); // 본부 상주(방문) 파티 영웅만 연구소 사용
        }
    }

    private void OnEnable() { TrySubscribe(); Refresh(); }
    private void OnDisable() { Unsubscribe(); }
    private void Update() { if (subLab == null || subEco == null) TrySubscribe(); }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (subLab == null && gm.Lab != null)
        {
            subLab = gm.Lab;
            subLab.OnStateChanged += Refresh;
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
        if (subLab != null)
        {
            subLab.OnStateChanged -= Refresh;
            if (heroSelectListController != null) heroSelectListController.UnitSelected -= OnHeroSelected;
            subLab = null;
        }
        if (subEco != null) { subEco.OnResourceChanged -= OnResourceChanged; subEco = null; }
    }

    private void OnResourceChanged(ResourceType _, int __) => Refresh();

    public void CloseModal()
    {
        if (heroSelectModalRoot != null) heroSelectModalRoot.SetActive(false);
        if (modalRoot != null) modalRoot.SetActive(false);
    }

    // ─── 영웅 선택 ──────────────────────────────────────────
    private void OpenHeroSelect()
    {
        if (heroSelectModalRoot != null) heroSelectModalRoot.SetActive(true);
        if (heroSelectListController != null) heroSelectListController.Rebuild();
    }
    private void CloseHeroSelect() { if (heroSelectModalRoot != null) heroSelectModalRoot.SetActive(false); }

    private void OnHeroSelected(int unitIndex)
    {
        selectedUnitIndex = unitIndex;
        selectedSkillIndex = -1;
        CloseHeroSelect();
        Refresh();
    }

    // ─── 슬롯 상호작용 ──────────────────────────────────────
    private void OnSlotClicked(int slotIdx)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null) return;
        if (selectedUnitIndex < 0) return;
        if (slotIdx < 0 || slotIdx >= boundSkills.Count) return;

        int skillIndex = boundSkills[slotIdx].skillIndex;
        int equipped = gm.Lab.GetEquippedSkillIndex(selectedUnitIndex);

        if (skillIndex != equipped)
        {
            // 비장착 스킬 클릭 → 즉시 장착 변경
            gm.Lab.EquipSkill(selectedUnitIndex, skillIndex);
            selectedSkillIndex = -1;
        }
        else
        {
            // 장착중 스킬 클릭 → 업그레이드 대상 선택 토글
            selectedSkillIndex = (selectedSkillIndex == skillIndex) ? -1 : skillIndex;
        }
        Refresh();
    }

    private void OnCancel()
    {
        if (selectedSkillIndex >= 0) { selectedSkillIndex = -1; Refresh(); }
        else CloseModal();
    }

    private void OnConfirm()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || gm.Economy == null) return;
        if (selectedUnitIndex < 0 || selectedSkillIndex < 0) return;
        if (!gm.Lab.CanUpgradeSkill(selectedUnitIndex, selectedSkillIndex)) return;
        if (!gm.Lab.GetNextUpgradeCost(selectedUnitIndex, selectedSkillIndex, out int money, out int chip)) return;
        if (!gm.Economy.Has(ResourceType.Money, money) || !gm.Economy.Has(ResourceType.Chip, chip)) return;

        if (!gm.Economy.Spend(ResourceType.Money, money)) return;
        if (!gm.Economy.Spend(ResourceType.Chip, chip)) { gm.Economy.Add(ResourceType.Money, money); return; }

        if (!gm.Lab.TryUpgradeSkill(selectedUnitIndex, selectedSkillIndex))
        {
            gm.Economy.Add(ResourceType.Money, money);
            gm.Economy.Add(ResourceType.Chip, chip);
            return;
        }
        Refresh();
    }

    // ─── Refresh ────────────────────────────────────────────
    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || gm.Economy == null) return;

        bool unlocked = gm.Lab.IsUnlocked();
        bool hasSelection = selectedUnitIndex >= 0;

        if (heroSilhouette != null) heroSilhouette.SetActive(false); // 빈 프로필은 기본(00) 이미지로 대체
        if (heroProfileImage != null)
        {
            heroProfileImage.gameObject.SetActive(true);
            heroProfileImage.enabled = true;
            heroProfileImage.sprite = hasSelection
                ? HeroProfileCatalog.GetByUnitIndex(selectedUnitIndex)
                : HeroProfileCatalog.Default;
        }
        if (selectPromptGo != null) selectPromptGo.SetActive(!hasSelection);

        boundSkills.Clear();
        if (hasSelection)
            boundSkills.AddRange(gm.Lab.GetLearnedSkills(selectedUnitIndex));
        if (heroNameText != null)
            heroNameText.text = hasSelection
                ? (gm.Lab.TryResolveClass(selectedUnitIndex, out string cn, out _) ? cn : "—") : "";

        int equipped = hasSelection ? gm.Lab.GetEquippedSkillIndex(selectedUnitIndex) : 0;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;
            bool active = i < boundSkills.Count;
            if (slot.button != null) slot.button.gameObject.SetActive(active);
            if (!active) continue;

            SkillData s = boundSkills[i];
            int level = gm.Lab.GetSkillLevel(selectedUnitIndex, s.skillIndex);
            bool isEquipped = s.skillIndex == equipped;
            bool isSelected = s.skillIndex == selectedSkillIndex;
            bool canUpgrade = gm.Lab.CanUpgradeSkill(selectedUnitIndex, s.skillIndex);
            bool maxed = level >= LabManager.MaxSkillLevel;

            if (slot.usingMark != null) slot.usingMark.SetActive(isEquipped);
            if (slot.selectedFrame != null) slot.selectedFrame.SetActive(isSelected);
            if (slot.lockedOverlay != null) slot.lockedOverlay.SetActive(!unlocked);
            if (slot.lockMark != null) slot.lockMark.SetActive(!unlocked);
            if (slot.finishedMark != null) slot.finishedMark.SetActive(maxed);
            if (slot.levelText != null) slot.levelText.text = $"Lv.{level}";
            if (slot.icon != null) { var sp = GetSkillIcon(i, level); slot.icon.sprite = sp; slot.icon.enabled = sp != null; }
        }

        // 필요 자원
        bool showCost = hasSelection && selectedSkillIndex >= 0
                        && gm.Lab.GetNextUpgradeCost(selectedUnitIndex, selectedSkillIndex, out int m, out int c);
        int reqM = 0, reqC = 0;
        if (showCost) gm.Lab.GetNextUpgradeCost(selectedUnitIndex, selectedSkillIndex, out reqM, out reqC);
        if (costMoneyText != null) costMoneyText.text = showCost ? $"{reqM:N0}" : "—";
        if (costChipText != null) costChipText.text = showCost ? $"{reqC:N0}" : "—";

        // 진행 버튼
        bool canConfirm = false;
        if (hasSelection && selectedSkillIndex >= 0 && gm.Lab.CanUpgradeSkill(selectedUnitIndex, selectedSkillIndex)
            && gm.Lab.GetNextUpgradeCost(selectedUnitIndex, selectedSkillIndex, out int nm, out int nc))
        {
            canConfirm = gm.Economy.Has(ResourceType.Money, nm) && gm.Economy.Has(ResourceType.Chip, nc);
        }
        if (btnConfirm != null) btnConfirm.interactable = canConfirm;

        if (stateInfoText != null)
        {
            string msg = null;
            if (!unlocked) msg = "연구소 기능이 활성화되지 않았습니다.";
            else if (!hasSelection) msg = "영웅을 선택해 주세요.";
            stateInfoText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
            if (!string.IsNullOrEmpty(msg)) stateInfoText.text = msg;
        }
    }
}
