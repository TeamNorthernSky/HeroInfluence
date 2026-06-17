using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 트레이닝 모달 컨트롤러 — [JC 260617] 공방형 stage-select 모델로 재작성.
/// 좌측 영웅 프로필, 중앙 2분류행(공격력/생명력) × 강화단계 셀(아이콘+상태프레임).
///   - 다음 강화 가능 단계 셀만 좌클릭으로 선택 → 비용 표시 → 하단 [진행] 으로 1단계 강화.
///   - 단계 우클릭 또는 단계 아이콘 외 모달 영역 클릭 → 선택 해제.
///   - 분류/단계 롤오버 시 LobbyTooltip 표시.
/// 백엔드 = TrainingManager(영웅 BaseStats 영구 가산). 단계 수치/비용은 TrainingManager 인스펙터 값 사용.
/// </summary>
[DisallowMultipleComponent]
public class TrainingModalController : MonoBehaviour
{
    [Serializable]
    public class StageCell
    {
        public GameObject root;                 // 셀 토글(없으면 frame 기준)
        public Image frame;                     // 상태별 프레임(빈/선택/잠금)
        public Image contentIcon;               // 단계 내용 아이콘(선택)
        public GameObject finishedMark;         // 완료 마크
        public GameObject lockMark;             // 미해금 자물쇠
        public TrainingStageCell clickHandler;  // 좌클릭 선택 / 우클릭 해제
        public LobbyTooltipTrigger tooltip;     // 단계 롤오버 툴팁
    }

    [Serializable]
    public class StatRow
    {
        public TrainingStat stat;
        public Image categoryIcon;              // 분류 아이콘(공격력/생명력)
        public LobbyTooltipTrigger categoryTooltip;
        public StageCell[] stages = new StageCell[3];
    }

    [Header("Modal_Training 본체")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnConfirm;   // 진행
    [SerializeField] private Button btnCancel;    // 취소
    [SerializeField] private TMP_Text titleText;  // "훈련실 Lv.N"

    [Header("영웅 영역")]
    [SerializeField] private Button btnHeroSlot;
    [SerializeField] private Image heroProfileImage;
    [SerializeField] private GameObject heroSilhouette;
    [SerializeField] private GameObject selectPromptGo;

    [Header("스탯 분류 행 (공격력/생명력)")]
    [SerializeField] private List<StatRow> rows = new List<StatRow>();

    [Header("비용 표시 / 안내")]
    [SerializeField] private TMP_Text costMoneyText;
    [SerializeField] private TMP_Text stateInfoText;

    [Header("강화 단계 프레임 스프라이트 (Resources)")]
    [SerializeField] private string cellEmptyPath    = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame";
    [SerializeField] private string cellSelectedPath = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame(Selected)";
    [SerializeField] private string cellLockedPath   = "UI_Sprite/UI_HQLobby/Popup/UI_box_Frame(Locked)";
    [SerializeField] private string cellFinishedPath = "UI_Sprite/UI_HQLobby/Popup/UI_,mark_finished";

    [Header("분류 아이콘 스프라이트 (Resources)")]
    [SerializeField] private string atkIconPath = "UI_Sprite/UI_Icon/Status/UI_icon_ATK";
    [SerializeField] private string hpIconPath  = "UI_Sprite/UI_Icon/Status/UI_icon_HP";

    [Header("툴팁 문구")]
    [SerializeField] private string atkCategoryTip = "공격력을 강화합니다.";
    [SerializeField] private string hpCategoryTip  = "생명력을 강화합니다.";
    [SerializeField] private string lockedStageTip = "훈련실 업그레이드 필요";

    [Header("영웅 선택 sub-modal")]
    [SerializeField] private GameObject heroSelectModalRoot;
    [SerializeField] private HeroListController heroSelectListController;
    [SerializeField] private Button btnHeroSelectClose;

    private int selectedUnitIndex = -1;
    private int selRow = -1;        // 선택된 행 인덱스
    private int selStageLevel = -1; // 선택된 도달 레벨(1~MaxTrainingLevel)

    private TrainingManager subTM;
    private EconomyManager subEco;
    private readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // [JC 260617] 강화 완료 토스트(StateInfo 5초 유지)
    private const float CompletionDuration = 5f;
    private float completionMsgUntil;       // 이 시각(unscaledTime)까지 완료 메시지 유지
    private Coroutine completionRoutine;

    private Sprite Load(string p)
    {
        if (string.IsNullOrEmpty(p)) return null;
        if (!cache.TryGetValue(p, out var s)) { s = Resources.Load<Sprite>(p); cache[p] = s; }
        return s;
    }

    private static string StatName(TrainingStat s) => s == TrainingStat.Attack ? "공격력" : "생명력";

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnCancel != null) btnCancel.onClick.AddListener(OnCancel);
        if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirm);
        if (btnHeroSlot != null) btnHeroSlot.onClick.AddListener(OpenHeroSelect);
        if (btnHeroSelectClose != null) btnHeroSelectClose.onClick.AddListener(CloseHeroSelect);

        // 분류 아이콘 + 분류 툴팁 고정 세팅
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row == null) continue;
            if (row.categoryIcon != null)
            {
                var sp = Load(row.stat == TrainingStat.Attack ? atkIconPath : hpIconPath);
                row.categoryIcon.sprite = sp; row.categoryIcon.enabled = sp != null;
            }
            if (row.categoryTooltip != null)
                row.categoryTooltip.SetContent(row.stat == TrainingStat.Attack ? atkCategoryTip : hpCategoryTip);
            if (row.stages != null)
                for (int k = 0; k < row.stages.Length; k++)
                    if (row.stages[k]?.clickHandler != null) row.stages[k].clickHandler.Bind(this, r, k);
        }

        if (heroSelectListController != null)
        {
            heroSelectListController.SetSelectionMode(true);
            heroSelectListController.SetVisitingOnlyMode(true); // 본부 상주 파티 + 무소속만
        }
    }

    private void OnEnable() { TrySubscribe(); Refresh(); }
    private void OnDisable()
    {
        // [JC 260617] 닫힐 때(모든 경로) 선택 영웅·완료 토스트 초기화 → 재진입 시 미선택 상태
        selectedUnitIndex = -1;
        ClearSelection();
        completionMsgUntil = 0f;
        if (completionRoutine != null) { StopCoroutine(completionRoutine); completionRoutine = null; }
        Unsubscribe();
    }
    private void Update() { if (subTM == null || subEco == null) TrySubscribe(); }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (subTM == null && gm.Training != null)
        {
            subTM = gm.Training;
            subTM.OnStateChanged += Refresh;
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
        if (subTM != null)
        {
            subTM.OnStateChanged -= Refresh;
            if (heroSelectListController != null) heroSelectListController.UnitSelected -= OnHeroSelected;
            subTM = null;
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
        ClearSelection();
        CloseHeroSelect();
        Refresh();
    }

    // ─── 단계 선택/해제 (핸들러 콜백) ───────────────────────────
    public void OnStageCellClicked(int rowIndex, int stageIndex, bool isRight)
    {
        if (isRight) { ClearSelection(); Refresh(); return; }
        var gm = GameManager.Instance;
        if (gm == null || gm.Training == null || selectedUnitIndex < 0) return;
        if (rowIndex < 0 || rowIndex >= rows.Count) return;
        var stat = rows[rowIndex].stat;
        int level = gm.Training.GetLevel(selectedUnitIndex, stat);
        int maxLv = gm.Training.GetMaxTrainableLevel();
        int stageLevel = stageIndex + 1;
        // 다음 강화 가능 단계만 선택 가능
        if (!(level + 1 == stageLevel && level < maxLv)) return;
        // 토글
        if (selRow == rowIndex && selStageLevel == stageLevel) ClearSelection();
        else { selRow = rowIndex; selStageLevel = stageLevel; }
        Refresh();
    }

    public void OnDeselectAreaClicked()
    {
        if (selRow >= 0) { ClearSelection(); Refresh(); }
    }

    private void ClearSelection() { selRow = -1; selStageLevel = -1; }

    private void OnCancel()
    {
        if (selRow >= 0) { ClearSelection(); Refresh(); }
        else CloseModal();
    }

    private void OnConfirm()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Training == null || gm.Economy == null) return;
        if (selectedUnitIndex < 0 || selRow < 0 || selRow >= rows.Count) return;
        var stat = rows[selRow].stat;
        if (!gm.Training.CanTrain(selectedUnitIndex, stat)) return;
        if (gm.Training.GetLevel(selectedUnitIndex, stat) + 1 != selStageLevel) return;

        int cost = gm.Training.GetNextCost(selectedUnitIndex, stat);
        if (cost < 0) return;
        if (!gm.Economy.Has(ResourceType.Money, cost)) return;
        if (!gm.Economy.Spend(ResourceType.Money, cost)) return;
        if (!gm.Training.TryTrain(selectedUnitIndex, stat))
        {
            gm.Economy.Add(ResourceType.Money, cost); // 롤백
            return;
        }
        int reached = gm.Training.GetLevel(selectedUnitIndex, stat);
        ShowCompletion($"{StatName(stat)} {reached}단계 강화 완료!");
        ClearSelection();
        Refresh();
    }

    /// <summary>강화 완료 토스트를 StateInfo에 표시(5초 유지). 진행 중 새로 호출되면 덮어쓰고 타이머 리셋.</summary>
    private void ShowCompletion(string msg)
    {
        if (stateInfoText != null)
        {
            stateInfoText.gameObject.SetActive(true);
            stateInfoText.text = msg;
        }
        completionMsgUntil = Time.unscaledTime + CompletionDuration;
        if (completionRoutine != null) StopCoroutine(completionRoutine);
        completionRoutine = StartCoroutine(CompletionExpireRoutine());
    }

    private IEnumerator CompletionExpireRoutine()
    {
        yield return new WaitForSecondsRealtime(CompletionDuration);
        completionMsgUntil = 0f;
        completionRoutine = null;
        Refresh(); // 정상 안내 상태로 복귀(또는 숨김)
    }

    // ─── Refresh ────────────────────────────────────────────
    private void Refresh()
    {
        // [JC 260617] 타이틀(훈련실 Lv.N)은 Title 오브젝트의 DeptLevelLabel(department=Training)이 담당 — 여기서 안 건드림.
        var gm = GameManager.Instance;
        if (gm == null || gm.Training == null || gm.Economy == null) return;
        var tm = gm.Training;

        bool unlocked = tm.IsUnlocked();
        bool hasHero = selectedUnitIndex >= 0;
        int maxLv = tm.GetMaxTrainableLevel();

        // 영웅 영역
        if (heroSilhouette != null) heroSilhouette.SetActive(!hasHero);
        if (heroProfileImage != null)
        {
            heroProfileImage.enabled = true;
            heroProfileImage.sprite = hasHero ? HeroProfileCatalog.GetByUnitIndex(selectedUnitIndex) : HeroProfileCatalog.Default;
        }
        if (selectPromptGo != null) selectPromptGo.SetActive(!hasHero);

        Sprite sEmpty = Load(cellEmptyPath), sSel = Load(cellSelectedPath), sLock = Load(cellLockedPath), sFin = Load(cellFinishedPath);

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row == null) continue;
            int level = hasHero ? tm.GetLevel(selectedUnitIndex, row.stat) : 0;
            float[] gains = ToArray(row.stat == TrainingStat.Attack ? tm.AtkGainPerLevel : tm.HpGainPerLevel);

            if (row.stages == null) continue;
            for (int k = 0; k < row.stages.Length; k++)
            {
                var cell = row.stages[k];
                if (cell == null) continue;
                int stageLevel = k + 1;
                bool filled = hasHero && level >= stageLevel;
                bool isNext = hasHero && level + 1 == stageLevel && level < maxLv;
                bool deptLocked = stageLevel > maxLv; // 부서 레벨 부족
                bool isSel = selRow == r && selStageLevel == stageLevel;

                Sprite fs = isSel ? sSel : (filled ? sFin : (deptLocked ? sLock : sEmpty));
                if (cell.frame != null) { cell.frame.sprite = fs; cell.frame.enabled = fs != null; }
                if (cell.finishedMark != null) cell.finishedMark.SetActive(filled && !isSel);
                if (cell.lockMark != null) cell.lockMark.SetActive(deptLocked);

                // 단계 툴팁
                if (cell.tooltip != null)
                {
                    float g = (k < gains.Length) ? gains[k] : 0f;
                    string tip = deptLocked ? lockedStageTip : $"{StatName(row.stat)} +{g:0}";
                    cell.tooltip.SetContent(tip);
                }
            }
        }

        // 비용
        bool showCost = hasHero && selRow >= 0 && selRow < rows.Count;
        int reqM = -1;
        if (showCost) reqM = tm.GetNextCost(selectedUnitIndex, rows[selRow].stat);
        if (costMoneyText != null) costMoneyText.text = (showCost && reqM >= 0) ? $"{reqM:N0}" : "—";

        bool canConfirm = showCost && reqM >= 0 && gm.Economy.Has(ResourceType.Money, reqM)
                          && tm.CanTrain(selectedUnitIndex, rows[selRow].stat);
        if (btnConfirm != null) btnConfirm.interactable = canConfirm;

        if (stateInfoText != null)
        {
            if (Time.unscaledTime < completionMsgUntil)
            {
                // [JC 260617] 강화 완료 토스트 유지(텍스트는 ShowCompletion이 세팅) — 안내문구로 덮지 않음
                stateInfoText.gameObject.SetActive(true);
            }
            else
            {
                string msg = null;
                if (!unlocked) msg = "트레이닝 기능이 활성화되지 않았습니다.";
                else if (!hasHero) msg = "영웅을 선택해 주세요.";
                stateInfoText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
                if (!string.IsNullOrEmpty(msg)) stateInfoText.text = msg;
            }
        }
    }

    private static float[] ToArray(IReadOnlyList<float> src)
    {
        if (src == null) return Array.Empty<float>();
        var a = new float[src.Count];
        for (int i = 0; i < src.Count; i++) a[i] = src[i];
        return a;
    }
}
