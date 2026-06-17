using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 연구소 모달 컨트롤러 — [JC 260617] 공방형 stage-select 모델로 재작성(샘플 레이아웃).
/// 좌측 영웅 프로필, 중앙 스킬 행 × 강화단계 셀(ClassSkill 아이콘 + 상태 프레임).
///   - 스킬 rep 아이콘 좌클릭: 습득 스킬이면 즉시 장착 변경(EquipSkill).
///   - 다음 강화 가능 단계 셀 좌클릭 → 2자원(자금+메달) 비용 표시 → [진행] 으로 1단계 강화.
///   - 단계 우클릭 / 단계 외 모달 영역 클릭 → 선택 해제.
///   - 분류(스킬)·단계 롤오버 시 LobbyTooltip. 강화 완료 시 StateInfo 토스트(5초).
/// 백엔드 = LabManager(+DH PersistentUnitRepository.SetSkillLevel 전투 동기). 비용=인스펙터(LabManager).
/// </summary>
[DisallowMultipleComponent]
public class LabModalController : MonoBehaviour
{
    [Serializable]
    public class StageCell
    {
        public GameObject root;
        public Image frame;                     // 상태 프레임(빈/선택/잠금)
        public Image contentIcon;               // 단계 아이콘(ClassSkill)
        public GameObject finishedMark;         // 완료 마크
        public GameObject lockMark;             // 미해금 자물쇠
        public LabStageCell clickHandler;
        public LobbyTooltipTrigger tooltip;
    }

    [Serializable]
    public class SkillRow
    {
        public GameObject rowRoot;
        public Button skillButton;              // rep 스킬 아이콘(장착 변경)
        public Image skillIcon;
        public GameObject usingMark;            // 장착중 마크
        public GameObject lockMark;             // 미습득 자물쇠
        public LobbyTooltipTrigger skillTooltip;
        public StageCell[] stages = new StageCell[4]; // 레벨 2~5
    }

    [Header("Modal_Lab 본체")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnConfirm;   // 진행
    [SerializeField] private Button btnCancel;    // 취소

    [Header("영웅 영역")]
    [SerializeField] private Button btnHeroSlot;
    [SerializeField] private Image heroProfileImage;
    [SerializeField] private GameObject heroSilhouette;
    [SerializeField] private GameObject selectPromptGo;

    [Header("스킬 행")]
    [SerializeField] private List<SkillRow> rows = new List<SkillRow>();

    [Header("비용(자금+메달) / 안내")]
    [SerializeField] private TMP_Text costMoneyText;
    [SerializeField] private TMP_Text costChipText;
    [SerializeField] private TMP_Text stateInfoText;

    [Header("강화 단계 프레임 스프라이트 (Resources)")]
    [SerializeField] private string cellEmptyPath    = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame";
    [SerializeField] private string cellSelectedPath = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame(Selected)";
    [SerializeField] private string cellLockedPath   = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame(Locked)";
    [SerializeField] private string cellFinishedPath = "UI_Sprite/UI_HQLobby/Popup/UI_,mark_finished";

    [Header("스킬 아이콘 (ClassSkill_temp, 순서 매핑 + 레벨)")]
    [Tooltip("{0}=종류(01~), {1}=레벨(1~5)")]
    [SerializeField] private string skillIconPathFormat = "UI_Sprite/UI_Icon/ClassSkill_temp/skill {0:00} level {1}";
    [SerializeField] private int skillIconVariants = 2;

    [Header("툴팁 문구")]
    [SerializeField] private string lockedStageTip = "연구소 업그레이드 필요";
    [SerializeField] private string notLearnedTip  = "아직 습득하지 않은 스킬";

    [Header("영웅 선택 sub-modal")]
    [SerializeField] private GameObject heroSelectModalRoot;
    [SerializeField] private HeroListController heroSelectListController;
    [SerializeField] private Button btnHeroSelectClose;

    private const float CompletionDuration = 5f;
    private float completionMsgUntil;
    private Coroutine completionRoutine;

    private int selectedUnitIndex = -1;
    private int selRow = -1;
    private int selStageLevel = -1;
    private readonly List<SkillData> boundSkills = new List<SkillData>();

    private LabManager subLab;
    private EconomyManager subEco;
    private readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private Sprite Load(string p)
    {
        if (string.IsNullOrEmpty(p)) return null;
        if (!cache.TryGetValue(p, out var s)) { s = Resources.Load<Sprite>(p); cache[p] = s; }
        return s;
    }

    private Sprite GetSkillIcon(int order, int level)
    {
        int variants = Mathf.Max(1, skillIconVariants);
        int v = (order % variants + variants) % variants + 1;
        int lv = Mathf.Clamp(level, 1, 5);
        return Load(string.Format(skillIconPathFormat, v, lv));
    }

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnCancel != null) btnCancel.onClick.AddListener(OnCancel);
        if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirm);
        if (btnHeroSlot != null) btnHeroSlot.onClick.AddListener(OpenHeroSelect);
        if (btnHeroSelectClose != null) btnHeroSelectClose.onClick.AddListener(CloseHeroSelect);

        for (int r = 0; r < rows.Count; r++)
        {
            int cr = r;
            var row = rows[r];
            if (row?.skillButton != null)
            {
                row.skillButton.onClick.AddListener(() => OnSkillClicked(cr));
                var rh = row.skillButton.GetComponent<LabSkillHover>(); if (rh != null) rh.Bind(this, cr, -1);
            }
            if (row?.stages != null)
                for (int k = 0; k < row.stages.Length; k++)
                {
                    if (row.stages[k]?.clickHandler != null) row.stages[k].clickHandler.Bind(this, cr, k);
                    var sr = row.stages[k]?.root != null ? row.stages[k].root.GetComponent<LabSkillHover>() : null;
                    if (sr != null) sr.Bind(this, cr, k);
                }
        }

        if (heroSelectListController != null)
        {
            heroSelectListController.SetSelectionMode(true);
            heroSelectListController.SetVisitingOnlyMode(true);
        }
    }

    private void OnEnable() { TrySubscribe(); Refresh(); }
    private void OnDisable()
    {
        selectedUnitIndex = -1;
        ClearSelection();
        completionMsgUntil = 0f;
        if (completionRoutine != null) { StopCoroutine(completionRoutine); completionRoutine = null; }
        Unsubscribe();
    }
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

    private void OpenHeroSelect()
    {
        if (heroSelectModalRoot != null) heroSelectModalRoot.SetActive(true);
        if (heroSelectListController != null) heroSelectListController.Rebuild();
    }
    private void CloseHeroSelect() { if (heroSelectModalRoot != null) heroSelectModalRoot.SetActive(false); }
    private void OnHeroSelected(int unitIndex)
    {
        selectedUnitIndex = unitIndex;
        EnsureDefaultEquipped(unitIndex); // [JC 260617] 기본 장착 스킬 미지정(CurrentSkillIndex=0)이면 첫 습득 스킬 자동 장착
        ClearSelection();
        CloseHeroSelect();
        Refresh();
    }

    /// <summary>[JC 260617] 영웅 생성 시 currentSkillIndex=0(기본 스킬 미지정)이라 진입 직후 UsingMark가 안 뜨는 문제 보정.
    /// 장착 스킬이 없거나 미습득이면 첫 습득 스킬(최저 acquireLevel)을 자동 장착해 영속 기록·표시.</summary>
    private void EnsureDefaultEquipped(int unitIndex)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || unitIndex < 0) return;
        var learned = gm.Lab.GetLearnedSkills(unitIndex);
        if (learned == null || learned.Count == 0) return;
        int eq = gm.Lab.GetEquippedSkillIndex(unitIndex);
        bool valid = false;
        if (eq != 0)
            for (int i = 0; i < learned.Count; i++)
                if (learned[i] != null && learned[i].skillIndex == eq) { valid = true; break; }
        if (!valid) gm.Lab.EquipSkill(unitIndex, learned[0].skillIndex);
    }

    // ─── rep 스킬 클릭 → 장착 변경 ──────────────────────────────
    private void OnSkillClicked(int rowIndex)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || selectedUnitIndex < 0) return;
        if (rowIndex < 0 || rowIndex >= boundSkills.Count) return;
        var skill = boundSkills[rowIndex];
        if (!gm.Lab.IsSkillLearned(selectedUnitIndex, skill)) return; // 미습득은 장착 불가
        if (gm.Lab.GetEquippedSkillIndex(selectedUnitIndex) != skill.skillIndex)
            gm.Lab.EquipSkill(selectedUnitIndex, skill.skillIndex);
        ClearSelection();
        Refresh();
    }

    // ─── 단계 셀 선택/해제 ──────────────────────────────────────
    public void OnStageCellClicked(int rowIndex, int stageIndex, bool isRight)
    {
        if (isRight) { ClearSelection(); Refresh(); return; }
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || selectedUnitIndex < 0) return;
        if (rowIndex < 0 || rowIndex >= boundSkills.Count) return;
        var skill = boundSkills[rowIndex];
        if (!gm.Lab.IsSkillLearned(selectedUnitIndex, skill)) return;
        int level = gm.Lab.GetSkillLevel(selectedUnitIndex, skill.skillIndex);
        int stageLevel = stageIndex + 2; // 셀0→레벨2 … 셀3→레벨5
        if (level + 1 != stageLevel) return;
        if (!gm.Lab.CanUpgradeSkill(selectedUnitIndex, skill.skillIndex)) return;
        if (selRow == rowIndex && selStageLevel == stageLevel) ClearSelection();
        else { selRow = rowIndex; selStageLevel = stageLevel; }
        Refresh();
    }

    public void OnDeselectAreaClicked() { if (selRow >= 0) { ClearSelection(); Refresh(); } }
    private void ClearSelection() { selRow = -1; selStageLevel = -1; }

    private void OnCancel()
    {
        if (selRow >= 0) { ClearSelection(); Refresh(); }
        else CloseModal();
    }

    private void OnConfirm()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || gm.Economy == null) return;
        if (selectedUnitIndex < 0 || selRow < 0 || selRow >= boundSkills.Count) return;
        var skill = boundSkills[selRow];
        if (!gm.Lab.CanUpgradeSkill(selectedUnitIndex, skill.skillIndex)) return;
        if (gm.Lab.GetSkillLevel(selectedUnitIndex, skill.skillIndex) + 1 != selStageLevel) return;
        if (!gm.Lab.GetNextUpgradeCost(selectedUnitIndex, skill.skillIndex, out int money, out int chip)) return;
        if (!gm.Economy.Has(ResourceType.Money, money) || !gm.Economy.Has(ResourceType.Chip, chip)) return;
        if (!gm.Economy.Spend(ResourceType.Money, money)) return;
        if (!gm.Economy.Spend(ResourceType.Chip, chip)) { gm.Economy.Add(ResourceType.Money, money); return; }
        if (!gm.Lab.TryUpgradeSkill(selectedUnitIndex, skill.skillIndex))
        {
            gm.Economy.Add(ResourceType.Money, money);
            gm.Economy.Add(ResourceType.Chip, chip);
            return;
        }
        int reached = gm.Lab.GetSkillLevel(selectedUnitIndex, skill.skillIndex);
        ShowCompletion($"{SkillName(skill)} {reached}단계 강화 완료!");
        ClearSelection();
        Refresh();
    }

    private static string SkillName(SkillData s) => (s != null && !string.IsNullOrWhiteSpace(s.skillName)) ? s.skillName : "스킬";

    private static float SkillValueAt(int skillIndex, int level)
    {
        var cat = DHCsvTemplateCatalog.Instance;
        return cat != null ? cat.GetClassSkillValueAtLevel(skillIndex, level) : 0f;
    }

    private static float SkillSubValueAt(int skillIndex, int level)
    {
        var cat = DHCsvTemplateCatalog.Instance;
        return cat != null ? cat.GetClassSkillSubValueAtLevel(skillIndex, level) : 0f;
    }

    /// <summary>[JC 260617] 스킬 설명에 레벨별 계수 치환(효과타입별). 공격(0)=배율("1.2배"), 그 외=원문 수치.</summary>
    private static string EffectText(SkillData s, int level)
    {
        if (s == null) return "";
        string d = s.description ?? "";
        float v = SkillValueAt(s.skillIndex, level);
        float sub = SkillSubValueAt(s.skillIndex, level);
        bool atk = s.classSkillEffect == 0;
        string vStr = atk ? $"기본 피해량 × {v:0.##}" : $"{v:0.##}";
        string subStr = atk ? $"기본 피해량 × {sub:0.##}" : $"{sub:0.##}";
        return d.Replace("{ClassSkillValue}", vStr).Replace("{ClassSkillSubValue}", subStr);
    }

    /// <summary>[JC 260617] 리치 스킬 툴팁 호버 콜백. stageIndex -1=rep(1레벨 정보), 0~3=단계(현재↔다음 비교).</summary>
    public void OnSkillHover(int rowIndex, int stageIndex, bool enter)
    {
        if (!enter) { if (SkillTooltip.Instance != null) SkillTooltip.Instance.Hide(); return; }
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || SkillTooltip.Instance == null) return;
        if (selectedUnitIndex < 0 || rowIndex < 0 || rowIndex >= boundSkills.Count || rowIndex >= rows.Count) return;
        var skill = boundSkills[rowIndex];
        var row = rows[rowIndex];
        int level = Mathf.Max(1, gm.Lab.IsSkillLearned(selectedUnitIndex, skill) ? gm.Lab.GetSkillLevel(selectedUnitIndex, skill.skillIndex) : 1);

        if (stageIndex < 0)
        {
            var rt = row.skillButton != null ? row.skillButton.transform as RectTransform : null;
            SkillTooltip.Instance.ShowInfo(GetSkillIcon(rowIndex, level), SkillName(skill), EffectText(skill, level), rt);
        }
        else
        {
            int stageLevel = stageIndex + 2; // 셀0→Lv2
            int cur = stageLevel - 1, next = stageLevel;
            var cellRoot = (row.stages != null && stageIndex < row.stages.Length && row.stages[stageIndex] != null) ? row.stages[stageIndex].root : null;
            var rt = cellRoot != null ? cellRoot.transform as RectTransform : null;
            SkillTooltip.Instance.ShowCompare(
                GetSkillIcon(rowIndex, cur),  $"{SkillName(skill)} Lv.{cur}",  EffectText(skill, cur),
                GetSkillIcon(rowIndex, next), $"{SkillName(skill)} Lv.{next}", EffectText(skill, next), rt);
        }
    }

    private void ShowCompletion(string msg)
    {
        if (stateInfoText != null) { stateInfoText.gameObject.SetActive(true); stateInfoText.text = msg; }
        completionMsgUntil = Time.unscaledTime + CompletionDuration;
        if (completionRoutine != null) StopCoroutine(completionRoutine);
        completionRoutine = StartCoroutine(CompletionExpireRoutine());
    }

    private IEnumerator CompletionExpireRoutine()
    {
        yield return new WaitForSecondsRealtime(CompletionDuration);
        completionMsgUntil = 0f;
        completionRoutine = null;
        Refresh();
    }

    // ─── Refresh ────────────────────────────────────────────
    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || gm.Economy == null) return;
        var lab = gm.Lab;

        bool unlocked = lab.IsUnlocked();
        bool hasHero = selectedUnitIndex >= 0;

        if (heroSilhouette != null) heroSilhouette.SetActive(!hasHero);
        if (heroProfileImage != null)
        {
            heroProfileImage.enabled = true;
            heroProfileImage.sprite = hasHero ? HeroProfileCatalog.GetByUnitIndex(selectedUnitIndex) : HeroProfileCatalog.Default;
        }
        if (selectPromptGo != null) selectPromptGo.SetActive(!hasHero);

        boundSkills.Clear();
        if (hasHero) boundSkills.AddRange(lab.GetClassSkills(selectedUnitIndex));
        int equipped = hasHero ? lab.GetEquippedSkillIndex(selectedUnitIndex) : 0;

        Sprite sEmpty = Load(cellEmptyPath), sSel = Load(cellSelectedPath), sLock = Load(cellLockedPath), sFin = Load(cellFinishedPath);

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row == null) continue;
            bool active = r < boundSkills.Count;
            if (row.rowRoot != null) row.rowRoot.SetActive(active);
            else if (row.skillButton != null) row.skillButton.gameObject.SetActive(active);
            if (!active) continue;

            var skill = boundSkills[r];
            bool learned = lab.IsSkillLearned(selectedUnitIndex, skill);
            int level = learned ? lab.GetSkillLevel(selectedUnitIndex, skill.skillIndex) : 1;
            bool isEquipped = learned && skill.skillIndex == equipped;

            if (row.skillIcon != null) { var sp = GetSkillIcon(r, level); row.skillIcon.sprite = sp; row.skillIcon.enabled = sp != null; }
            if (row.usingMark != null) row.usingMark.SetActive(isEquipped);
            if (row.lockMark != null) row.lockMark.SetActive(!learned);
            // 툴팁은 SkillTooltip(리치)이 호버 콜백으로 표시 — 여기선 문자열 SetContent 안 함

            // [JC 260617] 무강화 스킬 → 단계셀 숨김, rep(1레벨)만 표시.
            //   판정: 계수가 레벨에 따라 "변하는가"(Lv1≠Lv5). 회복/부활은 전 레벨 평탄(고정값)이라 강화X.
            //   (구판 'Lv2>0'은 4010힐 0.3·4050광역힐 0.1·4070기적 0.2처럼 평탄한 고정값을 강화로 오판했음)
            bool enhanceable = !Mathf.Approximately(SkillValueAt(skill.skillIndex, 1), SkillValueAt(skill.skillIndex, 5))
                            || !Mathf.Approximately(SkillSubValueAt(skill.skillIndex, 1), SkillSubValueAt(skill.skillIndex, 5));
            if (row.stages == null) continue;
            for (int k = 0; k < row.stages.Length; k++)
            {
                var cell = row.stages[k];
                if (cell == null) continue;
                if (cell.root != null) cell.root.SetActive(enhanceable);
                if (!enhanceable) continue;
                int stageLevel = k + 2;
                bool filled = learned && level >= stageLevel;          // 이미 강화한 단계
                bool isNext = learned && level + 1 == stageLevel && lab.CanUpgradeSkill(selectedUnitIndex, skill.skillIndex);
                bool stageLocked = !filled && !isNext;                 // 강화 불가(잠금)
                bool isSel = selRow == r && selStageLevel == stageLevel;

                // 프레임 = 박스(선택 시 강조). 상태(완료/잠금)는 오버레이 마크로 표시.
                if (cell.frame != null) { var fs = isSel ? sSel : sEmpty; cell.frame.sprite = fs; cell.frame.enabled = fs != null; }
                if (cell.contentIcon != null) { var sp = GetSkillIcon(r, stageLevel); cell.contentIcon.sprite = sp; cell.contentIcon.enabled = sp != null; }
                if (cell.finishedMark != null) cell.finishedMark.SetActive(filled);          // 이미 강화 오버레이
                if (cell.lockMark != null) cell.lockMark.SetActive(stageLocked);              // 잠금 오버레이
                // 단계 툴팁은 SkillTooltip(리치 비교)이 호버 콜백으로 표시
            }
        }

        // 비용(자금+메달)
        bool showCost = hasHero && selRow >= 0 && selRow < boundSkills.Count
                        && lab.GetNextUpgradeCost(selectedUnitIndex, boundSkills[selRow].skillIndex, out _, out _);
        int reqM = -1, reqC = -1;
        if (showCost) lab.GetNextUpgradeCost(selectedUnitIndex, boundSkills[selRow].skillIndex, out reqM, out reqC);
        if (costMoneyText != null) costMoneyText.text = showCost ? $"{reqM:N0}" : "—";
        if (costChipText != null) costChipText.text = showCost ? $"{reqC:N0}" : "—";

        bool canConfirm = showCost && gm.Economy.Has(ResourceType.Money, reqM) && gm.Economy.Has(ResourceType.Chip, reqC)
                          && lab.CanUpgradeSkill(selectedUnitIndex, boundSkills[selRow].skillIndex);
        if (btnConfirm != null) btnConfirm.interactable = canConfirm;

        if (stateInfoText != null)
        {
            if (Time.unscaledTime < completionMsgUntil)
            {
                stateInfoText.gameObject.SetActive(true); // 완료 토스트 유지
            }
            else
            {
                string msg = null;
                if (!unlocked) msg = "연구소 기능이 활성화되지 않았습니다.";
                else if (!hasHero) msg = "영웅을 선택해 주세요.";
                stateInfoText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
                if (!string.IsNullOrEmpty(msg)) stateInfoText.text = msg;
            }
        }
    }
}
