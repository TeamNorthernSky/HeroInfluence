using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 출전(Sortie) 모달 컨트롤러. (기획서 협회(본부,출전) ④, V3.0)
///
/// 구조:
///  - 좌측 진형 2×3(전열 0~2 / 후열 3~5) + "(현재 인원수)/최대 N인"
///  - 우측 전열/후열 효과 안내(플레이스홀더)
///  - 로스터 = 로비 우측 영웅창(공유 HeroListController). 출전 모달이 열리면 영웅창을
///    "출전 모드"로 전환(좌클릭→추가, 드래그→진형 배치)하고 모달 위로 올려 활성 유지.
///    닫으면 원복(영웅 정보 동작 복귀).
///  - 진형 슬롯 좌/우클릭 → 해제 (SortieSlotInput).
///  - 닫기(X) → 진형 데이터 PartyPersistentRepository에 저장(OnDisable).
///
/// ※ 최대 인원(maxOnField)은 인스펙터 가변. 현재 영속 데이터 파티=4명 기준 4. 추후 DH가 파티를
///   3명으로 줄이면 이 값만 3으로 변경.
/// </summary>
[DisallowMultipleComponent]
public class SortieController : MonoBehaviour
{
    [Serializable]
    public class FormationSlot
    {
        public Button button;
        public Image icon;
        public GameObject emptyState;
        public GameObject filledState;
        public TMP_Text label;
    }

    [Header("Modal_Sortie 본체")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button btnClose; // X (닫기 = 저장)

    [Header("진형 슬롯 — 전열 3 + 후열 3 순서")]
    [SerializeField] private List<FormationSlot> slots = new List<FormationSlot>();

    [Header("인원수 표시")]
    [SerializeField] private TMP_Text countText;
    [SerializeField] private int maxOnField = 4;   // 인스펙터 가변
    [SerializeField] private TextMeshProUGUI stateInfoText;

    [Header("로스터 = 로비 우측 영웅창 (공유)")]
    [SerializeField] private HeroListController rosterController;
    [Tooltip("출전 모달 활성 동안 모달 위로 올릴 영웅창 루트(PNL_Lobby_CurrentParty)")]
    [SerializeField] private Transform rosterPanelRoot;
    [Tooltip("출전 모달 활성 동안 모달 위로 올릴 출전 버튼(재클릭으로 닫기). 보통 BTN_HQLobby_Go")]
    [SerializeField] private Transform sortieButtonRoot;

    [Header("진형 파티 ID")]
    [SerializeField] private string targetPartyId = "";

    [Header("슬롯 색 (드래그 강조)")]
    [SerializeField] private Color slotNormalColor = new Color(0.10f, 0.16f, 0.32f, 0.7f);
    [SerializeField] private Color slotHighlightColor = new Color(0.35f, 1f, 0.45f, 0.85f);

    private readonly List<int> formation = new List<int>(); // 슬롯 순서대로 unitIndex (0=빈칸)

    // 로스터(영웅창) 임시 전환 상태 복원용
    private bool rosterEngaged;
    private bool rosterOrigSelectionMode;
    private int rosterOrigSibling = -1;
    private int sortieBtnOrigSibling = -1;

    // 드래그 상태
    private int draggedUnitIndex = -1;
    private int draggedSourceSlot = -1; // -1=로스터 출처, >=0=진형 슬롯 출처
    private int hoveredSlot = -1;
    private RectTransform ghost;
    private TMP_Text ghostLabel;
    private Canvas rootCanvas;

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);

        for (int i = 0; i < slots.Count; i++)
        {
            formation.Add(0);
            var slot = slots[i];
            if (slot != null && slot.button != null)
            {
                var input = slot.button.GetComponent<SortieSlotInput>();
                if (input == null) input = slot.button.gameObject.AddComponent<SortieSlotInput>();
                input.index = i;
                input.owner = this;

                var drag = slot.button.GetComponent<SortieSlotDrag>();
                if (drag == null) drag = slot.button.gameObject.AddComponent<SortieSlotDrag>();
                drag.index = i;
                drag.owner = this;
            }
        }

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
    }

    private void OnEnable()
    {
        LoadFromRepository();
        EngageRoster();
        Refresh();
    }

    private void OnDisable()
    {
        SaveFormation();
        DisengageRoster();
        CancelDrag();
    }

    public void CloseModal() { if (modalRoot != null) modalRoot.SetActive(false); }

    // ─── 로스터(영웅창) 전환 ─────────────────────────────────
    private void EngageRoster()
    {
        if (rosterController == null || rosterEngaged) return;
        rosterEngaged = true;
        rosterOrigSelectionMode = rosterController.GetSelectionMode();
        rosterController.SetSelectionMode(true);
        rosterController.UnitSelected += OnRosterClicked;
        if (rosterPanelRoot != null)
        {
            rosterOrigSibling = rosterPanelRoot.GetSiblingIndex();
            rosterPanelRoot.SetAsLastSibling(); // 모달 dim 위로 올려 활성 유지
        }
        if (sortieButtonRoot != null)
        {
            sortieBtnOrigSibling = sortieButtonRoot.GetSiblingIndex();
            sortieButtonRoot.SetAsLastSibling(); // 출전 버튼 재클릭(닫기) 가능하도록 위로
        }
        rosterController.Rebuild();
        AttachRosterDragItems();
    }

    private void DisengageRoster()
    {
        if (!rosterEngaged) return;
        rosterEngaged = false;
        if (rosterController != null)
        {
            rosterController.UnitSelected -= OnRosterClicked;
            rosterController.SetSelectionMode(rosterOrigSelectionMode);
            rosterController.Rebuild(); // 일반 동작(영웅 정보)로 복귀
        }
        if (rosterPanelRoot != null && rosterOrigSibling >= 0)
            rosterPanelRoot.SetSiblingIndex(rosterOrigSibling);
        if (sortieButtonRoot != null && sortieBtnOrigSibling >= 0)
            sortieButtonRoot.SetSiblingIndex(sortieBtnOrigSibling);
    }

    private void AttachRosterDragItems()
    {
        if (rosterController == null) return;
        var items = rosterController.GetComponentsInChildren<HeroProfileButton>(true);
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (item == null) continue;
            var d = item.GetComponent<SortieDragItem>();
            if (d == null) d = item.gameObject.AddComponent<SortieDragItem>();
            d.unitIndex = item.UnitIndex;
            d.owner = this;
        }
    }

    // ─── 클릭 추가 / 해제 ────────────────────────────────────
    private void OnRosterClicked(int unitIndex)
    {
        if (unitIndex <= 0) return;
        // 이미 배치된 영웅이면 배치 취소(토글)
        int existing = formation.IndexOf(unitIndex);
        if (existing >= 0) { formation[existing] = 0; Refresh(); return; }
        if (CountFilled() >= maxOnField) { ShowState($"최대 {maxOnField}명까지 편성할 수 있습니다."); return; }
        int empty = formation.IndexOf(0);
        if (empty < 0) { ShowState("진형이 가득 찼습니다."); return; }
        formation[empty] = unitIndex;
        Refresh();
    }

    /// <summary>좌/우클릭 해제 (SortieSlotInput에서 호출).</summary>
    public void RemoveSlot(int slotIdx)
    {
        if (slotIdx < 0 || slotIdx >= formation.Count) return;
        if (formation[slotIdx] != 0) { formation[slotIdx] = 0; Refresh(); }
    }

    // ─── 드래그 앤 드롭 (SortieDragItem에서 호출) ────────────
    // 로스터 영웅 드래그 시작 (SortieDragItem)
    public void BeginDrag(int unitIndex, PointerEventData e)
    {
        BeginDragInternal(unitIndex, -1, e);
    }

    // 진형 슬롯 영웅 드래그 시작 (SortieSlotDrag) — 비어 있으면 무시
    public void BeginSlotDrag(int slotIndex, PointerEventData e)
    {
        if (slotIndex < 0 || slotIndex >= formation.Count || formation[slotIndex] <= 0) return;
        BeginDragInternal(formation[slotIndex], slotIndex, e);
    }

    private void BeginDragInternal(int unitIndex, int sourceSlot, PointerEventData e)
    {
        draggedUnitIndex = unitIndex;
        draggedSourceSlot = sourceSlot;
        EnsureGhost();
        if (ghost != null)
        {
            ghost.gameObject.SetActive(true);
            ghost.SetAsLastSibling();
            if (ghostLabel != null) ghostLabel.text = ResolveHeroName(unitIndex);
        }
        UpdateDrag(e);
    }

    public void UpdateDrag(PointerEventData e)
    {
        if (draggedUnitIndex <= 0 || e == null) return;
        if (ghost != null) ghost.position = e.position;
        hoveredSlot = FindSlotUnder(e.position);
        UpdateSlotColors();
    }

    public void EndDrag(PointerEventData e)
    {
        if (draggedUnitIndex > 0)
        {
            if (hoveredSlot >= 0)
            {
                if (draggedSourceSlot >= 0) MoveOrSwap(draggedSourceSlot, hoveredSlot); // 진형 내 이동/교환
                else PlaceHero(draggedUnitIndex, hoveredSlot);                          // 로스터 → 배치
            }
            else if (draggedSourceSlot >= 0)
            {
                formation[draggedSourceSlot] = 0; // 진형 밖으로 드래그 → 배치 취소
            }
        }
        CancelDrag();
        Refresh();
    }

    /// <summary>진형 슬롯 간 이동/교환. 대상이 점유돼 있으면 서로 위치 교환.</summary>
    private void MoveOrSwap(int sourceSlot, int targetSlot)
    {
        if (sourceSlot == targetSlot) return;
        int a = formation[sourceSlot];
        int b = formation[targetSlot];
        formation[targetSlot] = a;
        formation[sourceSlot] = b; // b==0이면 단순 이동, 아니면 교환
    }

    private void CancelDrag()
    {
        draggedUnitIndex = -1;
        draggedSourceSlot = -1;
        hoveredSlot = -1;
        if (ghost != null) ghost.gameObject.SetActive(false);
        UpdateSlotColors();
    }

    private void PlaceHero(int unitIndex, int slotIdx)
    {
        if (slotIdx < 0 || slotIdx >= formation.Count) return;
        bool alreadyIn = formation.Contains(unitIndex);
        bool targetOccupied = formation[slotIdx] != 0;
        if (!alreadyIn && !targetOccupied && CountFilled() >= maxOnField)
        {
            ShowState($"최대 {maxOnField}명까지 편성할 수 있습니다.");
            return;
        }
        for (int i = 0; i < formation.Count; i++)
            if (formation[i] == unitIndex) formation[i] = 0; // 이동(중복 제거)
        formation[slotIdx] = unitIndex; // 점유 셀이면 교체
    }

    private int FindSlotUnder(Vector2 screenPos)
    {
        Camera cam = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? rootCanvas.worldCamera : null;
        for (int i = 0; i < slots.Count; i++)
        {
            var b = slots[i] != null ? slots[i].button : null;
            if (b == null || !b.gameObject.activeInHierarchy) continue;
            var rt = b.transform as RectTransform;
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam)) return i;
        }
        return -1;
    }

    private void UpdateSlotColors()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var b = slots[i] != null ? slots[i].button : null;
            if (b == null) continue;
            var img = b.GetComponent<Image>();
            if (img == null) continue;
            bool hi = (draggedUnitIndex > 0 && i == hoveredSlot);
            img.color = hi ? slotHighlightColor : slotNormalColor;
        }
    }

    private void EnsureGhost()
    {
        if (ghost != null || rootCanvas == null) return;
        var go = new GameObject("SortieDragGhost", typeof(RectTransform), typeof(Image));
        ghost = go.GetComponent<RectTransform>();
        ghost.SetParent(rootCanvas.transform, false);
        ghost.sizeDelta = new Vector2(130, 44);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.22f, 0.45f, 0.92f);
        img.raycastTarget = false;
        var tgo = new GameObject("Text", typeof(TextMeshProUGUI));
        var trt = tgo.GetComponent<RectTransform>();
        trt.SetParent(ghost, false);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(4, 2); trt.offsetMax = new Vector2(-4, -2);
        ghostLabel = tgo.GetComponent<TextMeshProUGUI>();
        ghostLabel.fontSize = 16; ghostLabel.color = Color.white;
        ghostLabel.alignment = TextAlignmentOptions.Center; ghostLabel.raycastTarget = false;
        // 한글 폰트: 슬롯 라벨 폰트를 복사(없으면 기본)
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null && slots[i].label != null && slots[i].label.font != null)
            { ghostLabel.font = slots[i].label.font; break; }
        ghost.gameObject.SetActive(false);
    }

    // ─── 저장/로드 ───────────────────────────────────────────
    private void LoadFromRepository()
    {
        for (int i = 0; i < formation.Count; i++) formation[i] = 0;
        var repo = PartyPersistentRepository.Instance;
        if (repo == null) return;
        if (!repo.TryGetParty(ResolvePartyId(repo), out var party) || party == null) return;
        var src = party.UnitIndices;
        for (int i = 0; i < formation.Count && i < src.Count; i++)
            formation[i] = src[i];
    }

    /// <summary>
    /// 진형을 저장한다. 전열(0~2)/후열(3~5) 위치 보존을 위해 빈칸(0)을 포함한 배열(unitIndices)과
    /// 각 원소의 슬롯 번호(unitSlots)를 함께 저장한다. DH PartyPersistentRepository가 unitSlots를
    /// 지원하고, CombatEncounterManager.BuildPartyCombatSlotUnitIndices가 unitSlots로 6칸 위치를
    /// 보존하므로 전열/후열이 전투까지 도달한다. [[project_sortie_formation_seam]]
    /// </summary>
    private void SaveFormation()
    {
        var repo = PartyPersistentRepository.Instance;
        if (repo == null) return;
        int last = -1;
        for (int i = 0; i < formation.Count; i++) if (formation[i] > 0) last = i;
        var ordered = new List<int>();
        var unitSlots = new List<int>();
        for (int i = 0; i <= last; i++)
        {
            ordered.Add(formation[i]);  // 내부 빈칸(0) 보존, 후미 빈칸은 절삭
            // DH는 슬롯을 1-base(1~6)로 기대. + 진형 UI 좌열(0~2)=후열, 우열(3~5)=전열을
            // 전투 슬롯 전/후열 그룹에 맞추기 위해 그룹 교환: 좌열→슬롯4~6, 우열→슬롯1~3 (상중하 순서 유지)
            unitSlots.Add(i < 3 ? i + 4 : i - 2);
        }
        repo.RegisterOrUpdateParty(ResolvePartyId(repo), ordered, unitSlots);
    }

    private string ResolvePartyId(PartyPersistentRepository repo)
    {
        if (!string.IsNullOrWhiteSpace(targetPartyId)) return targetPartyId;
        if (repo.Parties != null && repo.Parties.Count > 0) return repo.Parties[0].PartyId;
        return "player_party";
    }

    // ─── Refresh ────────────────────────────────────────────
    private void Refresh()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;
            bool filled = i < formation.Count && formation[i] > 0;
            if (slot.emptyState != null) slot.emptyState.SetActive(!filled);
            if (slot.filledState != null) slot.filledState.SetActive(filled);
            if (slot.icon != null) slot.icon.enabled = filled;
            if (slot.label != null) slot.label.text = filled ? ResolveHeroName(formation[i]) : string.Empty;
        }

        int count = CountFilled();
        if (countText != null) countText.text = $"{count} / 최대 {maxOnField}인";

        if (rosterEngaged && rosterController != null)
        {
            rosterController.Rebuild();
            AttachRosterDragItems();
        }
        UpdateSlotColors();

        if (stateInfoText != null && count == 0)
            ShowState("영웅창에서 영웅을 선택해 편성하세요.");
        else if (stateInfoText != null)
            stateInfoText.gameObject.SetActive(false);
    }

    private int CountFilled()
    {
        int c = 0;
        for (int i = 0; i < formation.Count; i++) if (formation[i] > 0) c++;
        return c;
    }

    private void ShowState(string msg)
    {
        if (stateInfoText == null) return;
        stateInfoText.gameObject.SetActive(true);
        stateInfoText.text = msg;
    }

    private static string ResolveHeroName(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.TryGetUnit(unitIndex, out var unit) || unit == null)
            return $"#{unitIndex}";
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null && catalog.TryGetPlayerTemplate(unit.UnitTemplateKey, out var template) && template != null
            && !string.IsNullOrWhiteSpace(template.Name))
            return template.Name;
        return string.IsNullOrWhiteSpace(unit.UnitTemplateKey) ? $"#{unitIndex}" : unit.UnitTemplateKey;
    }
}
