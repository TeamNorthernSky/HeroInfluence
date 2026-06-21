using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 공방 모달 컨트롤러 (기획서 공방 레이아웃 V2: 1무기=1행).
/// 좌측 영웅 프로필, 우측 무기 3행(하급/중급/상급). 각 행 = [대표 아이콘] + [4 강화단계 칸(Lv2~5)].
///   - 대표 아이콘 클릭: 보유+미장착 → 즉시 장착 / 미보유+제작가능 → 제작 선택(진행으로 확정)
///   - 강화 칸 클릭: "다음 강화 가능 칸"만 선택 가능 → 진행 버튼으로 1단계 강화
///   - 비용(자금/수정)은 우하단 표시, 진행/취소는 하단.
/// 무기 스킬 계수 전투 반영은 보류(더미). 무기 스탯은 WeaponPersistentRepository 인스턴스 경유로 ingame 실반영. [JC 260616]
/// 비용 출처: H.I 자원 데이터 테이블 V1.4 협회-공방(제작/강화).
/// </summary>
[DisallowMultipleComponent]
public class WorkshopModalController : MonoBehaviour
{
    private enum SlotAction { None, Craft, Enhance }

    [Serializable]
    public class StageCell
    {
        public Button button;   // 강화 단계 칸 버튼
        public Image frame;      // 테두리 전용(빈/선택)
        public Image contentIcon; // [JC 260617] 단계별 무기 아이콘(weapon {tier} level {stageLevel})
        public GameObject finishedMark; // [JC 260617] 강화 완료 마크(아이콘 앞, 작게)
        public GameObject lockMark;     // [JC 260617] 잠금 마크(아이콘 앞, 작게)
    }

    [Serializable]
    public class WeaponRow
    {
        public GameObject rowRoot;       // 행 전체 토글(없으면 weaponButton 기준)
        public Button weaponButton;      // 대표 무기 아이콘 버튼(장착/제작 선택)
        public Image weaponIcon;         // 대표 무기 아이콘
        public GameObject usingMark;     // 장착중 마크
        public GameObject lockMark;      // 미보유 자물쇠
        public GameObject selectedFrame; // 제작 선택 프레임(대표)
        public StageCell[] stages = new StageCell[4]; // Lv2~5
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

    [Header("무기 행 (하급/중급/상급)")]
    [SerializeField] private List<WeaponRow> rows = new List<WeaponRow>();

    [Header("필요 자원 표시 (우하단)")]
    [SerializeField] private TextMeshProUGUI costMoneyText;
    [SerializeField] private TextMeshProUGUI costCrystalText;
    [SerializeField] private TextMeshProUGUI stateInfoText;

    [Header("영웅 선택 sub-modal")]
    [SerializeField] private GameObject heroSelectModalRoot;
    [SerializeField] private HeroListController heroSelectListController;
    [SerializeField] private Button btnHeroSelectClose;

    // [JC 260618] 무기 아이콘 경로는 HeroIconLibrary(SO)로 이관됨. tier 폴백 규약도 SO가 보유.

    [Header("강화 칸 프레임 스프라이트 (Resources)")]
    [SerializeField] private string cellEmptyPath    = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame";
    [SerializeField] private string cellSelectedPath = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame(Selected)";
    [SerializeField] private string cellLockedPath   = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame(Locked)";
    [SerializeField] private string cellFilledPath   = "UI_Sprite/UI_HQLobby/Popup/UI_,mark_finished";

    private int selectedUnitIndex = -1;
    private int selectedRow = -1;
    private SlotAction selectedAction = SlotAction.None;
    private int selectedTargetLevel = -1;
    private readonly List<int> boundWeapons = new List<int>();

    private WorkshopManager subWs;
    private EconomyManager subEco;
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    // [JC 260617] 제작/강화 완료 토스트(StateInfo 5초) — 연구소 방식.
    private const float CompletionDuration = 5f;
    private float completionMsgUntil;
    private Coroutine completionRoutine;

    private Sprite Load(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!spriteCache.TryGetValue(path, out var s)) { s = Resources.Load<Sprite>(path); spriteCache[path] = s; }
        return s;
    }

    // [JC 260618] 아이콘 해석을 HeroIconLibrary로 이관(키=weaponIndex, tier 폴백은 SO가 처리).
    private Sprite GetWeaponIcon(int weaponIndex, int level)
    {
        return HeroIcons.GetWeaponIcon(weaponIndex, level);
    }

    // [JC 260621] 공방 표시 규약: 레벨1 = 무기 아이콘(무기스킬 1레벨 자리를 대체·생략), 레벨2~5 = 무기스킬 아이콘.
    //  무기스킬 1레벨 아이콘 자체는 예비 인프라로 존재하나 공방 UI는 결선하지 않음(HeroInfo 모달에서만 실사용).
    private Sprite WorkshopStageIcon(int weaponIndex, int level)
    {
        if (level <= 1) return GetWeaponIcon(weaponIndex, level);
        int wsi = WeaponSkillIndexOf(weaponIndex);
        if (wsi > 0) return HeroIcons.GetWeaponSkillIcon(wsi, level);
        return GetWeaponIcon(weaponIndex, level); // wd 조회 실패 시 폴백
    }

    private static int WeaponSkillIndexOf(int weaponIndex)
    {
        var catalog = DHCsvTemplateCatalog.Instance;
        return (catalog != null && catalog.TryGetWeapon(weaponIndex, out var wd) && wd != null) ? wd.WeaponSkillIndex : 0;
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
            if (row?.weaponButton != null)
            {
                row.weaponButton.onClick.AddListener(() => OnWeaponClicked(cr));
                BindHover(row.weaponButton.gameObject, cr, -1); // 대표 호버(현재 레벨 정보)
            }
            if (row?.stages != null)
            {
                for (int k = 0; k < row.stages.Length; k++)
                {
                    int ck = k;
                    if (row.stages[k]?.button != null)
                    {
                        row.stages[k].button.onClick.AddListener(() => OnStageClicked(cr, ck));
                        BindHover(row.stages[k].button.gameObject, cr, ck); // 단계 호버(Lv 비교)
                    }
                }
            }
        }

        if (heroSelectListController != null)
        {
            heroSelectListController.SetSelectionMode(true);
            heroSelectListController.SetVisitingOnlyMode(true); // 본부 상주(방문) 파티 영웅만 공방 사용
        }
    }

    private void OnEnable() { TrySubscribe(); Refresh(); }
    private void OnDisable()
    {
        Unsubscribe();
        if (completionRoutine != null) { StopCoroutine(completionRoutine); completionRoutine = null; }
        completionMsgUntil = 0f;
    }
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
        var gm = GameManager.Instance;
        if (gm != null && gm.Workshop != null) gm.Workshop.EnsureDefaultEquipped(unitIndex); // 기본 무기 장착표시 보장
        ClearSelection();
        CloseHeroSelect();
        Refresh();
    }

    private void ClearSelection() { selectedRow = -1; selectedAction = SlotAction.None; selectedTargetLevel = -1; }

    // ─── 대표 무기 아이콘 클릭 ─────────────────────────────────
    private void OnWeaponClicked(int r)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Workshop == null) return;
        if (selectedUnitIndex < 0 || r < 0 || r >= boundWeapons.Count) return;

        int w = boundWeapons[r];
        var ws = gm.Workshop;
        bool owned = ws.IsOwned(selectedUnitIndex, w);
        int equipped = ws.GetEquippedWeaponIndex(selectedUnitIndex);

        if (!owned)
        {
            if (ws.CanCraft(selectedUnitIndex, w)) { selectedRow = r; selectedAction = SlotAction.Craft; selectedTargetLevel = -1; }
            else ClearSelection();
        }
        else if (w != equipped)
        {
            ws.EquipWeapon(selectedUnitIndex, w); // 즉시 장착
            ClearSelection();
        }
        else
        {
            ClearSelection(); // 장착중 대표 클릭 → 선택 해제
        }
        Refresh();
    }

    // ─── 강화 단계 칸 클릭 ─────────────────────────────────────
    private void OnStageClicked(int r, int k)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Workshop == null) return;
        if (selectedUnitIndex < 0 || r < 0 || r >= boundWeapons.Count) return;

        int w = boundWeapons[r];
        var ws = gm.Workshop;
        if (!ws.IsOwned(selectedUnitIndex, w)) return;

        int level = ws.GetWeaponLevel(selectedUnitIndex, w);
        if (level >= WorkshopManager.MaxWeaponLevel) return;
        int stageLevel = k + 2;          // 칸 k → 도달 레벨(2~5)
        if (level + 1 != stageLevel) return; // 다음 강화 가능 칸만 선택

        // 토글
        if (selectedAction == SlotAction.Enhance && selectedRow == r && selectedTargetLevel == stageLevel)
            ClearSelection();
        else { selectedRow = r; selectedAction = SlotAction.Enhance; selectedTargetLevel = stageLevel; }
        Refresh();
    }

    private void OnCancel()
    {
        if (selectedAction != SlotAction.None) { ClearSelection(); Refresh(); }
        else CloseModal();
    }

    private void OnConfirm()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Workshop == null || gm.Economy == null) return;
        if (selectedUnitIndex < 0 || selectedRow < 0 || selectedRow >= boundWeapons.Count) return;

        int w = boundWeapons[selectedRow];
        var ws = gm.Workshop;
        int money, crystal;

        if (selectedAction == SlotAction.Craft)
        {
            if (!ws.CanCraft(selectedUnitIndex, w)) return;
            if (!ws.GetCraftCost(w, out _, out money, out crystal)) return;
        }
        else if (selectedAction == SlotAction.Enhance)
        {
            if (!ws.CanEnhance(selectedUnitIndex, w)) return;
            int level = ws.GetWeaponLevel(selectedUnitIndex, w);
            if (!ws.GetEnhanceCost(w, level, out _, out money, out crystal)) return;
        }
        else return;

        if (!gm.Economy.Has(ResourceType.Money, money) || !gm.Economy.Has(ResourceType.Crystal, crystal)) return;
        if (!gm.Economy.Spend(ResourceType.Money, money)) return;
        if (!gm.Economy.Spend(ResourceType.Crystal, crystal)) { gm.Economy.Add(ResourceType.Money, money); return; }

        SlotAction action = selectedAction;
        bool ok = action == SlotAction.Craft
            ? ws.TryCraft(selectedUnitIndex, w)
            : ws.TryEnhance(selectedUnitIndex, w);
        if (!ok)
        {
            gm.Economy.Add(ResourceType.Money, money);
            gm.Economy.Add(ResourceType.Crystal, crystal);
            return;
        }
        // [JC 260617] 완료 토스트
        string wname = WeaponDisplayName(w);
        if (action == SlotAction.Craft) ShowCompletion($"{wname} 제작 완료!");
        else ShowCompletion($"{wname} {ws.GetWeaponLevel(selectedUnitIndex, w)}단계 강화 완료!");
        ClearSelection();
        Refresh();
    }

    private static string WeaponDisplayName(int weaponIndex)
    {
        var cat = DHCsvTemplateCatalog.Instance;
        if (cat != null && cat.TryGetWeapon(weaponIndex, out var wd) && wd != null && !string.IsNullOrWhiteSpace(wd.WeaponName))
            return wd.WeaponName;
        return "무기";
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

    // ─── Refresh ───────────────────────────────────────────────
    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Workshop == null || gm.Economy == null) return;
        var ws = gm.Workshop;

        bool unlocked = ws.IsUnlocked();
        bool hasSelection = selectedUnitIndex >= 0;

        // 영웅 영역
        if (heroSilhouette != null) heroSilhouette.SetActive(false);
        if (heroProfileImage != null)
        {
            heroProfileImage.gameObject.SetActive(true);
            heroProfileImage.enabled = true;
            heroProfileImage.sprite = hasSelection
                ? HeroProfileCatalog.GetByUnitIndex(selectedUnitIndex)
                : HeroProfileCatalog.Default;
        }
        if (selectPromptGo != null) selectPromptGo.SetActive(!hasSelection);
        // 타이틀: 영웅/클래스명이 아니라 항상 "공방 Lv.n" 표시.
        if (heroNameText != null)
            heroNameText.text = $"공방 Lv.{ws.GetDepartmentLevel()}";

        boundWeapons.Clear();
        if (hasSelection) boundWeapons.AddRange(ws.GetClassWeaponIndices(selectedUnitIndex));
        int equipped = hasSelection ? ws.GetEquippedWeaponIndex(selectedUnitIndex) : 0;

        Sprite sEmpty = Load(cellEmptyPath), sSel = Load(cellSelectedPath); // frame = 테두리 전용. 완료/잠금은 오버레이 마크.

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row == null) continue;
            bool active = r < boundWeapons.Count;
            if (row.rowRoot != null) row.rowRoot.SetActive(active);
            else if (row.weaponButton != null) row.weaponButton.gameObject.SetActive(active);
            if (!active) continue;

            int w = boundWeapons[r];
            bool owned = ws.IsOwned(selectedUnitIndex, w);
            int level = ws.GetWeaponLevel(selectedUnitIndex, w);
            bool isEquipped = owned && w == equipped;

            if (row.weaponIcon != null) { var sp = GetWeaponIcon(w, level); row.weaponIcon.sprite = sp; row.weaponIcon.enabled = sp != null; }
            if (row.usingMark != null) row.usingMark.SetActive(isEquipped);
            if (row.lockMark != null) row.lockMark.SetActive(!owned);
            if (row.selectedFrame != null) row.selectedFrame.SetActive(selectedAction == SlotAction.Craft && selectedRow == r);

            if (row.stages != null)
            {
                for (int k = 0; k < row.stages.Length; k++)
                {
                    var cell = row.stages[k];
                    if (cell == null) continue;
                    int stageLevel = k + 2;
                    bool filled = owned && level >= stageLevel;
                    bool isNext = owned && level + 1 == stageLevel && level < WorkshopManager.MaxWeaponLevel;
                    bool isSel = selectedAction == SlotAction.Enhance && selectedRow == r && selectedTargetLevel == stageLevel;
                    bool stageLocked = !filled && !isNext; // 강화 불가 단계(미보유 포함)
                    Sprite fs = isSel ? sSel : sEmpty; // 테두리 전용
                    if (cell.frame != null) { cell.frame.sprite = fs; cell.frame.enabled = fs != null; }
                    if (cell.contentIcon != null) { var ic = WorkshopStageIcon(w, stageLevel); cell.contentIcon.sprite = ic; cell.contentIcon.enabled = ic != null; }
                    if (cell.finishedMark != null) cell.finishedMark.SetActive(filled);   // 완료(아이콘 앞)
                    if (cell.lockMark != null) cell.lockMark.SetActive(stageLocked);       // 잠금(아이콘 앞)
                    if (cell.button != null) cell.button.interactable = isNext;
                }
            }
        }

        // 비용 (우하단)
        int reqM = 0, reqC = 0; bool showCost = false;
        if (hasSelection && selectedRow >= 0 && selectedRow < boundWeapons.Count && selectedAction != SlotAction.None)
        {
            int w = boundWeapons[selectedRow];
            if (selectedAction == SlotAction.Craft) showCost = ws.GetCraftCost(w, out _, out reqM, out reqC);
            else { int lv = ws.GetWeaponLevel(selectedUnitIndex, w); showCost = ws.GetEnhanceCost(w, lv, out _, out reqM, out reqC); }
        }
        if (costMoneyText != null) costMoneyText.text = showCost ? $"{reqM:N0}" : "—";
        if (costCrystalText != null) costCrystalText.text = showCost ? $"{reqC:N0}" : "—";

        bool canConfirm = showCost
                          && gm.Economy.Has(ResourceType.Money, reqM)
                          && gm.Economy.Has(ResourceType.Crystal, reqC);
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
                if (!unlocked) msg = "공방 기능이 활성화되지 않았습니다.";
                else if (!hasSelection) msg = "영웅을 선택해 주세요.";
                stateInfoText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
                if (!string.IsNullOrEmpty(msg)) stateInfoText.text = msg;
            }
        }
    }

    // ─── 리치 툴팁 (스킬 + 스탯) ───────────────────────────────
    private void BindHover(GameObject go, int row, int stage)
    {
        if (go == null) return;
        var h = go.GetComponent<WorkshopWeaponHover>();
        if (h == null) h = go.AddComponent<WorkshopWeaponHover>();
        h.Bind(this, row, stage);
    }

    /// <summary>무기 행 호버. stage -1=대표(현재 레벨 정보), 0~3=강화 단계셀(이전↔도달 레벨 비교).</summary>
    public void OnWeaponHover(int rowIndex, int stageIndex, bool enter)
    {
        if (SkillTooltip.Instance == null) return;
        if (!enter) { SkillTooltip.Instance.Hide(); return; }

        var gm = GameManager.Instance;
        if (gm == null || gm.Workshop == null) return;
        if (selectedUnitIndex < 0 || rowIndex < 0 || rowIndex >= boundWeapons.Count) return;
        if (rowIndex >= rows.Count || rows[rowIndex] == null) return;

        int w = boundWeapons[rowIndex];
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null || !catalog.TryGetWeapon(w, out var wd) || wd == null) return;
        int curLevel = Mathf.Max(WorkshopManager.BaseWeaponLevel, gm.Workshop.GetWeaponLevel(selectedUnitIndex, w));

        if (stageIndex < 0)
        {
            var rt = rows[rowIndex].weaponButton != null ? rows[rowIndex].weaponButton.transform as RectTransform : null;
            SkillTooltip.Instance.ShowInfo(GetWeaponIcon(w, curLevel), wd.WeaponName, BuildLevelDesc(wd, w, curLevel), rt);
        }
        else
        {
            int toLevel = Mathf.Clamp(stageIndex + 2, 2, WorkshopManager.MaxWeaponLevel);
            int fromLevel = toLevel - 1;
            var cell = rows[rowIndex].stages != null && stageIndex < rows[rowIndex].stages.Length ? rows[rowIndex].stages[stageIndex] : null;
            if (cell == null) return;
            var rt = (cell.frame != null ? cell.frame.transform : (cell.button != null ? cell.button.transform : null)) as RectTransform;
            SkillTooltip.Instance.ShowCompare(
                WorkshopStageIcon(w, fromLevel), $"Lv.{fromLevel}", BuildLevelDesc(wd, w, fromLevel),
                WorkshopStageIcon(w, toLevel),   $"Lv.{toLevel}",   BuildLevelDesc(wd, w, toLevel), rt);
        }
    }

    /// <summary>무기 스킬 효과 + 스탯 보너스를 레벨별로 조합한 설명 텍스트(무기 시트 V6.0 기준).
    /// [JC 260619] 본문은 공유 헬퍼 WeaponTooltipText로 이관(HeroInfo 모달과 공용).</summary>
    private string BuildLevelDesc(WeaponData wd, int weaponIndex, int level)
        => WeaponTooltipText.BuildWeaponLevelDesc(wd, weaponIndex, level);
}
