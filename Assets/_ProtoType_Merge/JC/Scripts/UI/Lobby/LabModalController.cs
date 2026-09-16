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

    [Header("강화 단계 프레임 스프라이트 (Resources)")]
    [SerializeField] private string cellEmptyPath    = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame";
    [SerializeField] private string cellSelectedPath = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame(Selected)";
    [SerializeField] private string cellLockedPath   = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame(Locked)";
    [SerializeField] private string cellFinishedPath = "UI_Sprite/UI_HQLobby/Popup/UI_,mark_finished";

    // [JC 260618] 스킬 아이콘 경로/변형 수는 HeroIconLibrary(SO)로 이관됨. 폴백 규약도 SO가 보유.

    [Header("툴팁 문구")]
    [SerializeField] private string lockedStageTip = "연구소 업그레이드 필요";
    [SerializeField] private string notLearnedTip  = "아직 습득하지 않은 스킬";

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
    private readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private Sprite Load(string p)
    {
        if (string.IsNullOrEmpty(p)) return null;
        if (!cache.TryGetValue(p, out var s)) { s = Resources.Load<Sprite>(p); cache[p] = s; }
        return s;
    }

    private Sprite GetSkillIcon(int order, int level)
    {
        // [JC 260618] 아이콘 해석을 HeroIconLibrary로 이관. 정체성 키=skillIndex, order는 폴백(시각 동등)용.
        int skillIndex = (order >= 0 && order < boundSkills.Count && boundSkills[order] != null)
            ? boundSkills[order].NumericSkillId : -1;
        return Sprites.Icon.ClassSkill(skillIndex, level, order);
    }

    private void EnsureFifthEnhancementStage()
    {
        foreach (var row in rows)
        {
            if (row?.stages == null || row.stages.Length != 4 || row.stages[3]?.root == null) continue;
            var source = row.stages[3];
            var clone = Instantiate(source.root, source.root.transform.parent);
            clone.name = source.root.name + "_Plus5";
            var extra = new StageCell
            {
                root = clone,
                frame = CloneReference(source.frame, source.root.transform, clone.transform),
                contentIcon = CloneReference(source.contentIcon, source.root.transform, clone.transform),
                finishedMark = CloneObject(source.finishedMark, source.root.transform, clone.transform),
                lockMark = CloneObject(source.lockMark, source.root.transform, clone.transform),
                clickHandler = CloneReference(source.clickHandler, source.root.transform, clone.transform),
                tooltip = CloneReference(source.tooltip, source.root.transform, clone.transform)
            };
            Array.Resize(ref row.stages, 5);
            row.stages[4] = extra;
            // 기존 게이지가 사용하던 가로 영역 안에 기본+5개의 칸을 균등 배치합니다.
            var first = row.firstLevelGauge != null ? row.firstLevelGauge.rectTransform : row.stages[0].root.transform as RectTransform;
            var last = source.root.transform as RectTransform;
            if (first != null && last != null && first.parent == last.parent && first.parent.GetComponent<LayoutGroup>() == null)
            {
                float left = first.anchoredPosition.x - first.rect.width * first.pivot.x;
                float right = last.anchoredPosition.x + last.rect.width * (1f - last.pivot.x);
                int count = row.firstLevelGauge != null ? 6 : 5;
                float stride = (right - left) / count;
                var rects = new List<RectTransform>();
                if (row.firstLevelGauge != null) rects.Add(first);
                foreach (var stage in row.stages) rects.Add(stage.root.transform as RectTransform);
                for (int i = 0; i < rects.Count; i++)
                {
                    var rect = rects[i];
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, stride - 2f));
                    rect.anchoredPosition = new Vector2(left + stride * (i + rect.pivot.x), rect.anchoredPosition.y);
                }
            }
        }
    }

    private static Transform CloneTransform(Transform value, Transform source, Transform clone)
    {
        if (value == null) return null;
        if (value == source) return clone;
        var names = new List<string>();
        for (var t = value; t != null && t != source; t = t.parent) names.Insert(0, t.name);
        return clone.Find(string.Join("/", names));
    }
    private static T CloneReference<T>(T value, Transform source, Transform clone) where T : Component
    {
        var t = value != null ? CloneTransform(value.transform, source, clone) : null;
        return t != null ? t.GetComponent<T>() : null;
    }
    private static GameObject CloneObject(GameObject value, Transform source, Transform clone)
    {
        var t = value != null ? CloneTransform(value.transform, source, clone) : null;
        return t != null ? t.gameObject : null;
    }

    private void Awake()
    {
        EnsureFifthEnhancementStage();
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnCancel != null) btnCancel.onClick.AddListener(OnCancel);
        if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirm);

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

    // ─── rep 스킬 클릭 → 장착 변경 ──────────────────────────────
    private void OnSkillClicked(int rowIndex)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || selectedUnitIndex < 0) return;
        if (rowIndex < 0 || rowIndex >= boundSkills.Count) return;
        var skill = boundSkills[rowIndex];
        if (!gm.Lab.IsSkillLearned(selectedUnitIndex, skill)) return; // 미습득은 장착 불가
        if (gm.Lab.GetEquippedSkillIndex(selectedUnitIndex) != skill.NumericSkillId)
            gm.Lab.EquipSkill(selectedUnitIndex, skill.NumericSkillId);
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
        if (!gm.Economy.Spend(ResourceType.Money, money)) return;
        if (!gm.Economy.Spend(ResourceType.Chip, chip)) { gm.Economy.Add(ResourceType.Money, money); return; }
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

    /// <summary>[JC 260617] 스킬 설명에 레벨별 계수 치환(효과타입별). 공격(0)=배율("1.2배"), 그 외=원문 수치.</summary>
    // [JC 260619] 본문은 공유 헬퍼 ClassSkillTooltipText로 이관(HeroInfo 모달과 공용).
    private static string EffectText(DHClassSkillTemplate s, int level) => ClassSkillTooltipText.BuildDesc(s, level);

    /// <summary>[JC 260617] 리치 스킬 툴팁 호버 콜백. stageIndex -1=rep(1레벨 정보), 0~3=단계(현재↔다음 비교).</summary>
    public void OnSkillHover(int rowIndex, int stageIndex, bool enter)
    {
        if (!enter) { if (SkillTooltip.Instance != null) SkillTooltip.Instance.Hide(); return; }
        var gm = GameManager.Instance;
        if (gm == null || gm.Lab == null || SkillTooltip.Instance == null) return;
        if (selectedUnitIndex < 0 || rowIndex < 0 || rowIndex >= boundSkills.Count || rowIndex >= rows.Count) return;
        var skill = boundSkills[rowIndex];
        var row = rows[rowIndex];
        int level = Mathf.Max(1, gm.Lab.IsSkillLearned(selectedUnitIndex, skill) ? gm.Lab.GetSkillLevel(selectedUnitIndex, skill.NumericSkillId) : 1);

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
            int level = learned ? lab.GetSkillLevel(selectedUnitIndex, skill.NumericSkillId) : 1;
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
                        && lab.GetNextUpgradeCost(selectedUnitIndex, boundSkills[selRow].NumericSkillId, out _, out _);
        int reqM = -1, reqC = -1;
        if (showCost) lab.GetNextUpgradeCost(selectedUnitIndex, boundSkills[selRow].NumericSkillId, out reqM, out reqC);
        if (costMoneyText != null) costMoneyText.text = showCost ? $"{reqM:N0}" : "—";
        if (costChipText != null) costChipText.text = showCost ? $"{reqC:N0}" : "—";

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
