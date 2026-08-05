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
///
/// [JC 260703 · dim 일원화] 자체 dim 제거 → 공유 dim(ModalManager, Modal_Sortie.wantsDim=1) 단일화.
/// 공유 dim은 범용이라 외부 요소를 dim 위로 못 올린다. 그래서 편성 로스터/편성완료 버튼을 Modal_Sortie
/// 자식(=Layer_Modals, dim 위)에 복제로 두고, 원본(로비 로스터 패널/출전 열기버튼)은 모달 중 비활성한다.
/// 이 복제+원본비활성 구조는 의도된 트레이드오프(범용 ModalManager 단순성 유지) — 최적화로 걷어내지 말 것.
/// 모달 로스터는 표시전용 LobbyRosterView(파티 4명 고정 + 드래그-추가 dormant). [[project_sortie_layer_refactor]]
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

    [Header("한글 기본 폰트 (런타임 생성 텍스트용. 비우면 슬롯 라벨에서 복사)")]
    [SerializeField] private TMP_FontAsset hangulFont; // = NotoSansKR-Regular SDF

    [Header("새 파티(무-상주) 모드 상태 안내")]
    [SerializeField] private string singlePartyStateMessage = "현재 버전에서는 1개 파티만 생성할 수 있습니다.";

    [Header("로스터(편성 selection) — dormant. 현재 모달 로스터는 표시전용 LobbyRosterView 사용")]
    [Tooltip("[JC 260703] 편성 드래그-추가 기획 재활성 시 HeroListController(selection)를 여기 결선. 현재는 null.")]
    [SerializeField] private HeroListController rosterController;

    [Header("[JC 260703] 원본(로비) 토글 — 모달 열림 동안 비활성(모달이 자체 복제본 표시)")]
    [Tooltip("로비 상시 로스터 패널(PNL_Lobby_CurrentParty). 타 번들(UI_PartyPanel) → 런타임 레지스트리 폴백.")]
    [SerializeField] private Transform rosterPanelRoot;
    [Tooltip("출전 열기 버튼(BTN_HQLobby_Go). 같은 UI_Sortie 번들 → 직접 결선.")]
    [SerializeField] private Transform sortieButtonRoot;
    [Tooltip("[JC 260703] 편성완료(저장+닫기) 버튼. Modal_Sortie 자식(공유 dim 위). 클릭→TryClose.")]
    [SerializeField] private Button confirmButton;

    [Header("진형 파티 ID")]
    [SerializeField] private string targetPartyId = "";

    // [JC 260628] 기획: 파티원 4명 고정. 진형(위치) 편집은 허용, 인원 추가/제거 트리거만 차단.
    // 추가/제거 인프라(드래그·클릭·PlaceHero 등)는 보존 — 추후 기획 변경 시 이 값만 true로.
    [Header("파티 인원 변동 허용 (기획: 4명 고정 → false)")]
    [SerializeField] private bool allowPartySizeChange = false;

    [Header("슬롯 색 (드래그 강조)")]
    [SerializeField] private Color slotNormalColor = new Color(0.10f, 0.16f, 0.32f, 0.7f);
    [SerializeField] private Color slotHighlightColor = new Color(0.35f, 1f, 0.45f, 0.85f);

    private readonly List<int> formation = new List<int>(); // 슬롯 순서대로 unitIndex (0=빈칸)

    // 로스터(영웅창) 임시 전환 상태 복원용 (dormant selection 로스터용)
    private bool rosterEngaged;
    private bool rosterOrigSelectionMode;
    private bool rosterOrigVisitingOnly;

    // [JC 260703] 자체 dim 제거 — 공유 dim(ModalManager, Modal_Sortie.wantsDim=1)이 대체.

    // 드래그 상태
    private int draggedUnitIndex = -1;
    private int draggedSourceSlot = -1; // -1=로스터 출처, >=0=진형 슬롯 출처
    private int hoveredSlot = -1;
    private RectTransform ghost;
    private TMP_Text ghostLabel;
    private Canvas rootCanvas;

    private void Awake()
    {
        // [JC 260616] X 닫기도 빈 파티 게이트 경유 (멤버 0명이면 닫기 차단)
        if (btnClose != null) btnClose.onClick.AddListener(() => TryClose());
        if (confirmButton != null) confirmButton.onClick.AddListener(() => TryClose()); // [JC 260703] 편성완료 = 저장(OnDisable)+닫기
        EnsureStateText();

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
        // [JC 260629] 번들 분리 — 크로스번들 참조를 레지스트리에서 폴백 해석.
        // 자기 자신 등록은 SortieRegistrar(상시 active 번들 루트)가 담당 — Modal_Sortie는 비활성 시작.
        if (rosterController == null) rosterController = LobbyUIRegistry.Roster; // dormant selection 로스터(현재 null)
        // [JC 260703] rosterPanelRoot(로비 PNL)는 PartyPanelRegistrar가 씬 로드 시 RosterPanelRoot로 등록 → 그것만 사용.
        //  ※ LobbyRosterRoot는 폴백으로 쓰지 않는다: 모달 자신의 LobbyRosterView도 OnEnable에 LobbyRosterRoot를 자기로
        //    덮으므로, 폴백 시 '모달 자신의 로스터'를 비활성화할 위험이 있다.
        if (rosterPanelRoot == null && LobbyUIRegistry.RosterPanelRoot != null) rosterPanelRoot = LobbyUIRegistry.RosterPanelRoot;
        // [JC 260703] sortieButtonRoot는 UI_Sortie 번들 내부(BTN_HQLobby_Go) 직접 결선 — 레지스트리 폴백 불필요(GoButton 슬롯 제거됨).

        // [JC 260703] 모달 열림 = 로비 원본(로스터 패널/열기버튼) 비활성. 모달은 자체 복제본(LobbyRosterView/편성완료 버튼)을
        // Modal_Sortie 자식(공유 dim 위)으로 표시한다. 범용 ModalManager 공유 dim은 외부요소를 위로 못 올리므로
        // '복제본을 dim 위로' 대신 '원본을 비활성'으로 통일. [[project_sortie_layer_refactor]]
        if (rosterPanelRoot != null) rosterPanelRoot.gameObject.SetActive(false);
        if (sortieButtonRoot != null) sortieButtonRoot.gameObject.SetActive(false);

        LoadFromRepository();
        EngageRoster();
        Refresh();
    }

    private void OnDisable()
    {
        SaveFormation();
        DisengageRoster();
        // [JC 260703] 모달 닫힘 = 로비 원본 복귀(비활성 역토글). LobbyRosterView가 OnEnable에 순서 재읽기.
        if (rosterPanelRoot != null) rosterPanelRoot.gameObject.SetActive(true);
        if (sortieButtonRoot != null) sortieButtonRoot.gameObject.SetActive(true);
        CancelDrag();
    }

    public void CloseModal() { if (modalRoot != null) modalRoot.SetActive(false); }

    // ─── 로스터(영웅창) 전환 ─────────────────────────────────
    // [JC 260703] dormant: 편성 드래그-추가 기획 재활성 시 rosterController(HeroListController selection)를 결선하면 동작.
    // 현재 모달 로스터는 표시전용 LobbyRosterView라 rosterController=null → 이 메서드는 no-op.
    // (자체 dim/끌어올리기는 제거 — 공유 dim + 원본 비활성 방식으로 통일)
    private void EngageRoster()
    {
        if (rosterController == null || rosterEngaged) return;
        rosterEngaged = true;
        rosterOrigSelectionMode = rosterController.GetSelectionMode();
        rosterOrigVisitingOnly = rosterController.GetVisitingOnlyMode();
        rosterController.SetSelectionMode(true);
        // [JC 260616] 출전 로스터 = 본부 상주(방문 파티 + 무소속)만. 탐사 나간 파티 멤버 제외.
        rosterController.SetVisitingOnlyMode(true);
        rosterController.UnitSelected += OnRosterClicked;
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
            rosterController.SetVisitingOnlyMode(rosterOrigVisitingOnly);
            rosterController.Rebuild(); // 일반 동작(영웅 정보)로 복귀
        }
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
        if (!allowPartySizeChange) return; // [JC 260628] 파티원 고정: 추가/해제 토글 차단
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
        if (!allowPartySizeChange) return; // [JC 260628] 파티원 고정: 클릭 해제 차단
        if (slotIdx < 0 || slotIdx >= formation.Count) return;
        if (formation[slotIdx] != 0) { formation[slotIdx] = 0; Refresh(); }
    }

    // ─── 드래그 앤 드롭 (SortieDragItem에서 호출) ────────────
    // 로스터 영웅 드래그 시작 (SortieDragItem)
    public void BeginDrag(int unitIndex, PointerEventData e)
    {
        if (!allowPartySizeChange) return; // [JC 260628] 파티원 고정: 영웅창→진형 추가 드래그 차단 (슬롯 간 이동은 BeginSlotDrag)
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
                if (draggedSourceSlot >= 0) MoveOrSwap(draggedSourceSlot, hoveredSlot); // 진형 내 이동/교환 — 항상 허용
                else if (allowPartySizeChange) PlaceHero(draggedUnitIndex, hoveredSlot); // 로스터 → 배치(추가) — 파티원 고정 시 차단
            }
            else if (draggedSourceSlot >= 0 && allowPartySizeChange)
            {
                formation[draggedSourceSlot] = 0; // 진형 밖으로 드래그 → 해제 — 파티원 고정 시 차단(제자리 복귀)
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
    // [JC 260628] 진형 grid ↔ 전투 슬롯 매핑은 공용 PartyFormation 규약 사용(일원화).
    private void LoadFromRepository()
    {
        for (int i = 0; i < formation.Count; i++) formation[i] = 0;
        var repo = PartyPersistentRepository.Instance;
        if (repo == null) return;
        string partyId = ResolvePartyId(repo);
        // [JC 260616] 상주(방문) 파티가 없으면 진형을 로드하지 않는다 → 탐사 나간 파티 진형이 보이지 않음
        if (string.IsNullOrWhiteSpace(partyId)) return;
        if (!repo.TryGetParty(partyId, out var party) || party == null) return;
        var src = party.UnitIndices;
        var slots = party.UnitSlots;
        // [JC 260628] 저장된 전투 슬롯(1-base) 기준으로 진형 grid에 복원(SaveFormation 역매핑).
        // 슬롯 정보가 없으면 위치+1을 슬롯으로 간주(레거시 폴백).
        for (int i = 0; i < src.Count; i++)
        {
            int unit = src[i];
            if (unit <= 0) continue;
            int slot = (slots != null && i < slots.Count) ? slots[i] : (i + 1);
            int grid = PartyFormation.SlotToGrid(slot);
            if (grid >= 0 && grid < formation.Count) formation[grid] = unit;
        }
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
        string partyId = ResolvePartyId(repo);
        // [JC 260616] 상주 파티가 없으면 저장하지 않는다(빈 파티/새 파티 자동 생성 차단)
        if (string.IsNullOrWhiteSpace(partyId)) return;
        int last = -1;
        for (int i = 0; i < formation.Count; i++) if (formation[i] > 0) last = i;
        // [JC 260616] 멤버 0명이면 빈 파티를 등록하지 않는다(닫기 게이트로 도달 불가하나 방어적 차단)
        if (last < 0) return;
        var ordered = new List<int>();
        var unitSlots = new List<int>();
        for (int i = 0; i <= last; i++)
        {
            ordered.Add(formation[i]);  // 내부 빈칸(0) 보존, 후미 빈칸은 절삭
            // [JC 260628] 진형 grid → 전투 슬롯. 전투 규약(슬롯1-3=후열, 4-6=전열)에 맞춰
            // 전열 프레임(grid0-2)→슬롯4-6, 후열(grid3-5)→슬롯1-3. LoadFromRepository와 역대칭.
            unitSlots.Add(PartyFormation.GridToSlot(i));
        }
        repo.RegisterOrUpdateParty(partyId, ordered, unitSlots);
    }

    // [JC 260616] 출전 메뉴는 "본부 상주(방문중) 파티"만 다룬다. 폴백으로 Parties[0]을 잡던
    // 이전 동작은 탐사 나간 파티 진형을 그대로 노출하던 버그였으므로 제거.
    private string ResolvePartyId(PartyPersistentRepository repo)
    {
        if (!string.IsNullOrWhiteSpace(targetPartyId) && IsPartyVisiting(targetPartyId))
            return targetPartyId;
        return ResolveResidentPartyId();
    }

    private static bool IsPartyVisiting(string id)
    {
        var v = HQVisitState.Instance;
        return v != null && v.IsPartyVisiting(id);
    }

    /// <summary>본부에 상주(방문중)인 첫 파티 ID. 없으면 null. 현재 버전은 파티 1개 한정이라 사실상 유일.</summary>
    public static string ResolveResidentPartyId()
    {
        var partyRepo = PartyPersistentRepository.Instance;
        var visit = HQVisitState.Instance;
        if (partyRepo == null || visit == null) return null;
        for (int p = 0; p < partyRepo.Parties.Count; p++)
        {
            var party = partyRepo.Parties[p];
            if (party == null) continue;
            if (visit.IsPartyVisiting(party.PartyId)) return party.PartyId;
        }
        return null;
    }

    /// <summary>출전 가능 영웅 수 = 상주 파티 멤버 + 무소속(어느 파티에도 없는 본부 잔류). 출전 버튼 활성/메시지 분기에 사용.</summary>
    public static int CountDeployableHeroes()
    {
        var unitRepo = PersistentUnitRepository.Instance;
        if (unitRepo == null) return 0;
        var partyRepo = PartyPersistentRepository.Instance;

        var allMembers = new HashSet<int>();
        if (partyRepo != null)
            for (int p = 0; p < partyRepo.Parties.Count; p++)
            {
                var party = partyRepo.Parties[p];
                if (party == null) continue;
                for (int u = 0; u < party.UnitIndices.Count; u++)
                    if (party.UnitIndices[u] > 0) allMembers.Add(party.UnitIndices[u]);
            }

        int count = 0;
        // 상주 파티 멤버
        string resident = ResolveResidentPartyId();
        if (resident != null && partyRepo != null && partyRepo.TryGetParty(resident, out var rp) && rp != null)
            for (int u = 0; u < rp.UnitIndices.Count; u++)
                if (rp.UnitIndices[u] > 0) count++;
        // 무소속(어느 파티에도 편성되지 않은 영웅)
        for (int u = 0; u < unitRepo.Units.Count; u++)
        {
            var unit = unitRepo.Units[u];
            if (unit == null) continue;
            if (!allMembers.Contains(unit.UnitIndex)) count++;
        }
        return count;
    }

    /// <summary>본부 상주(방문중) 파티가 있는지. 없으면 출전=새 파티 생성에 해당(현재 버전 차단 대상).</summary>
    public bool HasResidentParty => !string.IsNullOrEmpty(ResolveResidentPartyId());

    /// <summary>현재 진형에 편성된 인원 수(외부 게이트 판정용).</summary>
    public int FilledCount => CountFilled();

    /// <summary>외부(출전 버튼 게이트)에서 상태 메시지(붉은색)를 띄운다.</summary>
    public void ShowStateMessage(string msg) => ShowWarning(msg);

    /// <summary>외부(출전 버튼 게이트)에서 닫기 요청. 상주 파티가 있을 때 멤버 0명이면 닫지 않고 false 반환.</summary>
    public bool TryClose()
    {
        // [JC 260703] 편성완료/취소(X) 닫기 게이트 일원화. 구 SortieEntryGate의 재클릭-닫기 분기를 여기로 이관
        //             (열기버튼은 이제 열기 전용 + 모달 중 비활성).
        if (!HasResidentParty)
        {
            // 무-상주(새 파티 편성) 모드: 1명 이상 편성 시 "1개 파티만" 안내(닫지 않음).
            // 0명이면 저장 no-op이라 자유 닫기(취소). ※ 파티 4명 고정 중엔 추가 불가라 사실상 항상 0명.
            if (CountFilled() > 0) { ShowWarning(singlePartyStateMessage); return false; }
            CloseModal();
            return true;
        }
        // [JC 260616] 상주 파티가 있을 때 빈 파티(0명) 닫기(=출전) 차단.
        if (CountFilled() == 0)
        {
            ShowWarning("최소 1명 이상 편성해야 합니다.");
            return false;
        }
        CloseModal();
        return true;
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
            if (slot.icon != null)
            {
                // [JC 260628] 프로필 이미지 = unitIndex 기준 SpriteLibrary 포트레이트(텍스트 단독 → 이미지+텍스트)
                slot.icon.sprite = filled ? Sprites.Portrait.HeroByUnit(formation[i]) : null;
                slot.icon.enabled = filled && slot.icon.sprite != null;
            }
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

        // [JC 260616] 무-상주(새 파티 생성) 모드는 진입 시점부터 추가생성 불가 안내를 상시 표시
        if (!HasResidentParty)
            ShowWarning(singlePartyStateMessage);
        else if (count == 0)
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

    private void ShowState(string msg) => ShowState(msg, Color.white);

    private void ShowState(string msg, Color color)
    {
        EnsureStateText();
        if (stateInfoText == null) return;
        stateInfoText.gameObject.SetActive(true);
        stateInfoText.color = color;
        stateInfoText.text = msg;
    }

    // [JC 260616] 빈 파티 닫기 차단 등 경고는 붉은색으로 강조
    private void ShowWarning(string msg) => ShowState(msg, new Color(0.95f, 0.27f, 0.27f, 1f));

    // [JC 260616] stateInfoText 미바인딩 시 모달 하단에 런타임 안내/경고 텍스트 생성
    private void EnsureStateText()
    {
        if (stateInfoText != null) return;
        Transform parent = modalRoot != null ? modalRoot.transform : transform;
        var go = new GameObject("SortieStateText", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 24f);
        rt.sizeDelta = new Vector2(560f, 44f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 20f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var f = ResolveHangulFont();
        if (f != null) tmp.font = f;
        rt.SetAsLastSibling();
        stateInfoText = tmp;
    }

    // [JC 260616] 런타임 생성 텍스트용 한글 기본 폰트(NotoSansKR-Regular SDF). 미바인딩이면 슬롯 라벨에서 복사.
    private TMP_FontAsset ResolveHangulFont()
    {
        if (hangulFont != null) return hangulFont;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null && slots[i].label != null && slots[i].label.font != null) return slots[i].label.font;
        return null;
    }

    private static string ResolveHeroName(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.TryGetUnit(unitIndex, out var unit) || unit == null)
            return $"#{unitIndex}";
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null && catalog.TryGetPlayerUnitTemplate(unit.UnitTemplateKey, out var template) && template != null
            && !string.IsNullOrWhiteSpace(template.UnitName))
            return template.UnitName;
        return string.IsNullOrWhiteSpace(unit.UnitTemplateKey) ? $"#{unitIndex}" : unit.UnitTemplateKey;
    }
}
