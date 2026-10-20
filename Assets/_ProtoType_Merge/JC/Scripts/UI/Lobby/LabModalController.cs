using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 연구소 모달 컨트롤러 — [JC 260617] 공방형 stage-select 모델로 재작성(샘플 레이아웃).
/// 좌측 영웅 프로필, 중앙 스킬 행 × 강화단계 셀(ClassSkill 아이콘 + 상태 프레임).
///   - 스킬 행 좌클릭 → 다음 단계의 2자원(자금+메달) 비용 표시 → [진행] 으로 1단계 강화.
///   - 취소 / 행 외 모달 영역 클릭 → 선택 해제.
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
        public Image cardImage;
        public TMP_Text nameText;
        public TMP_Text levelText;
        public Image firstLevelGauge;

        // Optional compact-card presentation; legacy prefabs retain the stage-icon layout.
        public void RefreshCard(string skillName, int level, bool learned, bool enhanceable, bool selected,
            Sprite normal, Sprite highlighted, Sprite empty, Sprite filled)
        {
            if (cardImage == null) return;
            cardImage.sprite = selected ? highlighted : normal;
            if (nameText != null) { nameText.text = skillName; nameText.color = selected ? Color.white : Color.black; }
            if (levelText != null) { levelText.text = learned ? (level <= 1 ? "기본" : $"+{level - 1}") : "미습득"; levelText.color = selected ? Color.white : Color.black; }
            if (firstLevelGauge != null)
            {
                firstLevelGauge.gameObject.SetActive(enhanceable);
                firstLevelGauge.sprite = learned ? filled : empty;
            }
            if (stages == null) return;
            for (int k = 0; k < stages.Length; k++)
            {
                var cell = stages[k];
                if (cell == null) continue;
                if (cell.root != null) cell.root.SetActive(enhanceable);
                if (cell.frame != null)
                {
                    cell.frame.sprite = learned && level >= k + 2 ? filled : empty;
                    cell.frame.enabled = true;
                }
                if (cell.contentIcon != null) cell.contentIcon.enabled = false;
                if (cell.finishedMark != null) cell.finishedMark.SetActive(false);
                if (cell.lockMark != null) cell.lockMark.SetActive(false);
            }
        }
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
    [SerializeField] private Image heroProfileImage;
    [SerializeField] private GameObject heroSilhouette;
    [SerializeField] private GameObject selectPromptGo;

    [Header("스킬 행")]
    [SerializeField] private List<SkillRow> rows = new List<SkillRow>();

    [Header("비용(자금+메달) / 안내")]
    [SerializeField] private TMP_Text costMoneyText;
    [SerializeField] private TMP_Text costChipText;
    [SerializeField] private TMP_Text stateInfoText;
    private TMP_Text requiredLabLevelText;

    // [JC 260618] 스킬 아이콘 경로/변형 수는 HeroIconLibrary(SO)로 이관됨. 폴백 규약도 SO가 보유.

    [Header("훈련실과 동일한 카드 표시")]
    [SerializeField] private Sprite cardNormalSprite;
    [SerializeField] private Sprite cardSelectedSprite;
    [SerializeField] private Sprite gaugeEmptySprite;
    [SerializeField] private Sprite gaugeFilledSprite;

    private const float CompletionDuration = 5f;
    private float completionMsgUntil;
    private Coroutine completionRoutine;

    private int selectedUnitIndex = -1;
    private int selRow = -1;
    private int selStageLevel = -1;
    private readonly List<DHClassSkillTemplate> boundSkills = new List<DHClassSkillTemplate>();

    private LabManager subLab;
    private EconomyManager subEco;
    private Sprite GetSkillIcon(int order, int level)
    {
        // [JC 260618] 아이콘 해석을 HeroIconLibrary로 이관. 정체성 키=skillIndex, order는 폴백(시각 동등)용.
        int skillIndex = (order >= 0 && order < boundSkills.Count && boundSkills[order] != null)
            ? boundSkills[order].NumericSkillId : -1;
        return Sprites.Icon.ClassSkill(skillIndex, level, order);
    }

    private void Awake()
    {
        BindRequiredLabLevelText();
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnCancel != null) btnCancel.onClick.AddListener(OnCancel);
        if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirm);

        for (int r = 0; r < rows.Count; r++)
        {
            int cr = r;
            var row = rows[r];
            if (row?.rowRoot != null)
            {
                var equippedView = row.rowRoot.GetComponent<LabEquippedRowView>();
                if (equippedView != null) equippedView.SelectionManaged = true;
                var rowButton = row.rowRoot.GetComponent<Button>();
                if (rowButton == null) rowButton = row.rowRoot.AddComponent<Button>();
                rowButton.transition = Selectable.Transition.None;
                rowButton.targetGraphic = row.cardImage;
                if (row.cardImage != null) row.cardImage.raycastTarget = true;
                rowButton.onClick.AddListener(() => OnRowClicked(cr));
                foreach (var hover in row.rowRoot.GetComponentsInChildren<LabSkillHover>(true))
                    if (hover.gameObject != row.rowRoot) hover.enabled = false;
                var rowHover = row.rowRoot.GetComponent<LabSkillHover>();
                if (rowHover == null) rowHover = row.rowRoot.AddComponent<LabSkillHover>();
                rowHover.Bind(this, cr, -1);
                rowHover.enabled = true;
            }
            if (row?.skillButton != null)
            {
                // 자식 버튼이 행 클릭을 가로채지 않도록 합니다.
                if (row.skillButton.gameObject != row.rowRoot) row.skillButton.enabled = false;
            }
            if (row?.stages != null)
                for (int k = 0; k < row.stages.Length; k++)
                {
                    if (row.stages[k]?.clickHandler != null) row.stages[k].clickHandler.enabled = false;
                }
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
        // [KJ 260728] 파티 패널을 선택 모드로 전환. 서브모달 대신 우측 로스터에서 영웅을 고른다.
        var roster = LobbyUIRegistry.RosterView;
        if (roster != null) roster.BeginSelection(OnHeroSelected);
        else Debug.LogWarning("[LabModalController] LobbyUIRegistry.RosterView 없음 — 영웅 선택 불가.");
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
            subLab = null;
        }
        if (subEco != null) { subEco.OnResourceChanged -= OnResourceChanged; subEco = null; }
    }

    private void OnResourceChanged(ResourceType _, int __) => Refresh();

    public void CloseModal()
    {
        if (modalRoot != null) modalRoot.SetActive(false);
    }

    private void OnHeroSelected(int unitIndex)
    {
        selectedUnitIndex = unitIndex;
        EnsureDefaultEquipped(unitIndex); // [JC 260617] 기본 장착 스킬 미지정(CurrentSkillIndex=0)이면 첫 습득 스킬 자동 장착
        ClearSelection();
        // [KJ 260728] 파티 패널 하이라이트 동기화. 서브모달 폐기로 CloseHeroSelect 호출 제거.
        if (LobbyUIRegistry.RosterView != null) LobbyUIRegistry.RosterView.SetSelected(unitIndex);
        Refresh();
    }

    /// <summary>[JC 260617] 영웅 생성 시 currentSkillIndex=0(기본 스킬 미지정)이라 진입 직후 UsingMark가 안 뜨는 문제 보정.
    /// 장착 스킬이 없거나 미습득이면 첫 습득 스킬(최저 acquireLevel)을 자동 장착해 영속 기록·표시.</summary>
    private void EnsureDefaultEquipped(int unitIndex)
    {
        // [JC 260619] 로직을 LabManager로 이관(게임 시작 시 전역 자동장착과 공용). 여기선 위임.
        var gm = GameManager.Instance;
        if (gm != null && gm.Lab != null) gm.Lab.EnsureDefaultEquipped(unitIndex);
    }

    // ─── 행 클릭 → 현재 스킬의 다음 강화 단계 선택 ───────────────
    private void OnRowClicked(int rowIndex)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || selectedUnitIndex < 0) return;
        if (rowIndex < 0 || rowIndex >= boundSkills.Count) return;
        var skill = boundSkills[rowIndex];
        if (!gm.Lab.IsSkillLearned(selectedUnitIndex, skill)) return;
        // 레벨이 부족해도 다음 강화 비용과 필요 레벨은 확인할 수 있다.
        if (!gm.Lab.GetNextUpgradeCost(selectedUnitIndex, skill.NumericSkillId, out _, out _)) return;
        selRow = rowIndex;
        selStageLevel = gm.Lab.GetSkillLevel(selectedUnitIndex, skill.NumericSkillId) + 1;
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
        int level = gm.Lab.GetSkillLevel(selectedUnitIndex, skill.NumericSkillId);
        int stageLevel = stageIndex + 2; // 셀0→레벨2 … 셀3→레벨5
        if (level + 1 != stageLevel) return;
        if (!gm.Lab.CanUpgradeSkill(selectedUnitIndex, skill.NumericSkillId)) return;
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
        if (!gm.Lab.CanUpgradeSkill(selectedUnitIndex, skill.NumericSkillId)) return;
        if (gm.Lab.GetSkillLevel(selectedUnitIndex, skill.NumericSkillId) + 1 != selStageLevel) return;
        if (!gm.Lab.GetNextUpgradeCost(selectedUnitIndex, skill.NumericSkillId, out int money, out int chip)) return;
        if (!gm.Economy.Has(ResourceType.Money, money) || !gm.Economy.Has(ResourceType.Chip, chip)) return;
        if (money > 0 && !gm.Economy.Spend(ResourceType.Money, money)) return;
        if (chip > 0 && !gm.Economy.Spend(ResourceType.Chip, chip)) { gm.Economy.Add(ResourceType.Money, money); return; }
        if (!gm.Lab.TryUpgradeSkill(selectedUnitIndex, skill.NumericSkillId))
        {
            gm.Economy.Add(ResourceType.Money, money);
            gm.Economy.Add(ResourceType.Chip, chip);
            return;
        }
        int reached = gm.Lab.GetSkillLevel(selectedUnitIndex, skill.NumericSkillId);
        ShowCompletion($"{SkillName(skill)} +{reached - 1} 강화 완료!");
        ClearSelection();
        Refresh();
    }

    private static string SkillName(DHClassSkillTemplate s) => (s != null && !string.IsNullOrWhiteSpace(s.SkillName)) ? s.SkillName : "스킬";

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

    /// <summary>선택 영웅의 능력치와 해당 스킬 레벨로 설명 수치를 계산한다.</summary>
    // [JC 260619] 본문은 공유 헬퍼 ClassSkillTooltipText로 이관(HeroInfo 모달과 공용).
    private string EffectText(DHClassSkillTemplate s, int level) => ClassSkillTooltipText.BuildDesc(s, level, unitIndex: selectedUnitIndex);

    /// <summary>행 호버 시 해당 스킬의 현재 단계와 다음 단계를 비교한다. 최고 단계/미습득은 단일 정보.</summary>
    public void OnSkillHover(int rowIndex, int stageIndex, bool enter)
    {
        if (!enter) { if (SkillTooltip.Instance != null) SkillTooltip.Instance.Hide(); return; }
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || SkillTooltip.Instance == null) return;
        if (selectedUnitIndex < 0 || rowIndex < 0 || rowIndex >= boundSkills.Count || rowIndex >= rows.Count) return;
        var skill = boundSkills[rowIndex];
        var row = rows[rowIndex];
        bool learned = gm.Lab.IsSkillLearned(selectedUnitIndex, skill);
        int level = Mathf.Max(1, learned ? gm.Lab.GetSkillLevel(selectedUnitIndex, skill.NumericSkillId) : 1);
        var rt = row.rowRoot != null ? row.rowRoot.transform as RectTransform : null;

        if (!learned || level >= LabManager.MaxSkillLevel)
        {
            string label = !learned ? "미습득" : "최고 단계";
            SkillTooltip.Instance.ShowInfo(GetSkillIcon(rowIndex, level), $"{SkillName(skill)} ({label})", EffectText(skill, level), rt);
        }
        else
        {
            int cur = level, next = level + 1;
            SkillTooltip.Instance.ShowCompare(
                GetSkillIcon(rowIndex, cur),  $"{SkillName(skill)} {(cur == 1 ? "기본" : "+" + (cur - 1))}",  EffectText(skill, cur),
                GetSkillIcon(rowIndex, next), $"{SkillName(skill)} +{next - 1}", EffectText(skill, next), rt);
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
    private void BindRequiredLabLevelText()
    {
        if (requiredLabLevelText != null) return;
        Transform searchRoot = modalRoot != null ? modalRoot.transform : transform;
        foreach (var text in searchRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name != "WarningMessage" && text.name != "WarningMessageBox") continue;
            requiredLabLevelText = text;
            requiredLabLevelText.text = "";
            requiredLabLevelText.color = new Color(1f, 0.35f, 0.35f);
            requiredLabLevelText.raycastTarget = false;
            requiredLabLevelText.gameObject.SetActive(false);
            break;
        }
    }

    private void UpdateRequiredLabLevelText(int requiredLevel, int currentLevel)
    {
        if (requiredLabLevelText == null) return;
        bool levelTooLow = requiredLevel > 0 && currentLevel < requiredLevel;
        requiredLabLevelText.gameObject.SetActive(levelTooLow);
        requiredLabLevelText.text = levelTooLow ? $"Lv.{requiredLevel}이 필요합니다" : "";
    }

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
            int level = learned ? lab.GetSkillLevel(selectedUnitIndex, skill.NumericSkillId) : 0;
            bool isEquipped = learned && skill.NumericSkillId == equipped;

            if (row.skillIcon != null) { var sp = GetSkillIcon(r, level); row.skillIcon.sprite = sp; row.skillIcon.enabled = sp != null; }
            if (row.usingMark != null) row.usingMark.SetActive(isEquipped);
            if (row.lockMark != null) row.lockMark.SetActive(!learned);
            // 툴팁은 SkillTooltip(리치)이 호버 콜백으로 표시 — 여기선 문자열 SetContent 안 함

            // 모든 현행 히어로 스킬은 리스크 확률을 +5까지 강화합니다.
            bool enhanceable = true;
            if (row.cardImage != null)
            {
                row.RefreshCard(SkillName(skill), level, learned, enhanceable, selRow == r,
                    cardNormalSprite, cardSelectedSprite, gaugeEmptySprite, gaugeFilledSprite);
                continue;
            }
            if (row.stages == null) continue;
            for (int k = 0; k < row.stages.Length; k++)
            {
                var cell = row.stages[k];
                if (cell == null) continue;
                if (cell.root != null) cell.root.SetActive(enhanceable);
                if (!enhanceable) continue;
                int stageLevel = k + 2;
                bool filled = learned && level >= stageLevel;          // 이미 강화한 단계
                bool isNext = learned && level + 1 == stageLevel && lab.CanUpgradeSkill(selectedUnitIndex, skill.NumericSkillId);
                bool stageLocked = !filled && !isNext;                 // 강화 불가(잠금)
                // 기존 셀 이미지는 프리팹 값을 유지. 선택 강조는 행 카드에서 처리한다.
                if (cell.contentIcon != null) { var sp = GetSkillIcon(r, stageLevel); cell.contentIcon.sprite = sp; cell.contentIcon.enabled = sp != null; }
                if (cell.finishedMark != null) cell.finishedMark.SetActive(filled);          // 이미 강화 오버레이
                if (cell.lockMark != null) cell.lockMark.SetActive(stageLocked);              // 잠금 오버레이
                // 단계 툴팁은 SkillTooltip(리치 비교)이 호버 콜백으로 표시
            }
        }

        // 비용(자금+메달)
        bool showCost = hasHero && selRow >= 0 && selRow < boundSkills.Count
                        && lab.GetNextUpgradeCost(selectedUnitIndex, boundSkills[selRow].NumericSkillId, out _, out _);
        int reqM = -1, reqC = -1;
        if (showCost) lab.GetNextUpgradeCost(selectedUnitIndex, boundSkills[selRow].NumericSkillId, out reqM, out reqC);
        if (costMoneyText != null) costMoneyText.text = showCost ? $"{reqM:N0}" : "—";
        if (costChipText != null) costChipText.text = showCost ? $"{reqC:N0}" : "—";

        int requiredLevel = showCost && AssociationCosts.TryLab(selStageLevel, out var rule)
            ? rule.RequiredLabLevel : 0;
        UpdateRequiredLabLevelText(requiredLevel, lab.GetDepartmentLevel());

        bool canConfirm = showCost && gm.Economy.Has(ResourceType.Money, reqM) && gm.Economy.Has(ResourceType.Chip, reqC)
                          && lab.CanUpgradeSkill(selectedUnitIndex, boundSkills[selRow].NumericSkillId);
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
