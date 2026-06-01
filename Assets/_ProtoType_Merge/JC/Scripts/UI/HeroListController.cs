using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 보유 히어로 명단 패널. ScrollRect와 같은 GameObject에 부착.
/// Persistent Unit Repository 순회 → 파티 등록 순으로 정렬 → HeroProfileButton 프리팹 동적 생성.
/// 1버튼 단위 스냅 (vertical 기준).
///
/// 인스펙터 구성:
/// - 본 컴포넌트는 ScrollRect와 같은 GO에 부착
/// - Content RectTransform 지정 (ScrollRect.content와 동일 GO)
/// - Content에 VerticalLayoutGroup 부착 권장 (자동 배치용). ContentSizeFitter는 사용 금지(메모리 정책)
/// - itemHeight + itemSpacing는 본 컴포넌트가 수동 계산해 Content.sizeDelta.y 갱신
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScrollRect))]
public class HeroListController : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("Refs")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private HeroProfileButton itemPrefab;
    [SerializeField] private HeroInfoModal infoModal;

    [Header("Selection Mode (옵션 — 영웅 선택 모달용)")]
    [Tooltip("true면 클릭 시 infoModal.Open 대신 OnUnitSelected 이벤트만 발화")]
    [SerializeField] private bool selectionMode;
    [SerializeField] private UnityEvent<int> onUnitSelected;

    public event Action<int> UnitSelected; // 코드 결합용

    public void SetSelectionMode(bool enabled)
    {
        selectionMode = enabled;
    }

    [Header("Layout (Content 크기 수동 계산용)")]
    [Tooltip("아이템 1개 높이. 프리팹의 실제 RectTransform 높이와 일치시킬 것")]
    [SerializeField] private float itemHeight = 100f;
    [Tooltip("아이템 간 간격. VerticalLayoutGroup.spacing 값과 동기화 권장")]
    [SerializeField] private float itemSpacing = 5f;

    [Tooltip("화면에 한 번에 보이는 카드 수. 스냅 단위 계산에 사용 (예: 4명 보이고 나머지 스크롤)")]
    [SerializeField] private int maxVisibleItems = 4;

    [Header("Snap")]
    [SerializeField] private bool enableSnap = true;
    [Tooltip("스냅 보정 부드러운 이동 시간(초)")]
    [SerializeField] private float snapDuration = 0.2f;
    [Tooltip("휠·트랙패드 등 비드래그 입력 후 스냅까지 idle 대기 시간(초)")]
    [SerializeField] private float idleSnapDelay = 0.15f;

    [Header("Wheel Scroll")]
    [Tooltip("ScrollRect.scrollSensitivity 적용. 기본 Unity 값 1.0은 휠 1노치당 1단위라 매우 느림. 30~50 권장")]
    [SerializeField] private float scrollSensitivity = 30f;

    private readonly List<HeroProfileButton> spawnedItems = new List<HeroProfileButton>();
    private Coroutine snapRoutine;
    private bool isDragging;
    private float lastValueChangeTime = -1f;
    private bool ignoreNextValueChange;

    private void Awake()
    {
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
        if (content == null && scrollRect != null) content = scrollRect.content;
        if (scrollRect != null)
        {
            scrollRect.scrollSensitivity = scrollSensitivity;
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }
    }

    private void OnDestroy()
    {
        if (scrollRect != null)
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDisable()
    {
        ClearItems();
    }

    private void OnScrollValueChanged(Vector2 normalizedPosition)
    {
        if (ignoreNextValueChange)
        {
            ignoreNextValueChange = false;
            return;
        }
        lastValueChangeTime = Time.unscaledTime;
    }

    private void Update()
    {
        if (!enableSnap) return;
        if (isDragging) return;
        if (lastValueChangeTime < 0f) return;
        if (Time.unscaledTime - lastValueChangeTime < Mathf.Max(0.05f, idleSnapDelay)) return;

        lastValueChangeTime = -1f;
        SnapToNearest();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (itemPrefab == null || content == null)
        {
            Debug.LogWarning("[HeroListController] itemPrefab / content 미바인딩");
            return;
        }

        ClearItems();

        PersistentUnitRepository repo = PersistentUnitRepository.Instance;
        if (repo == null) return;

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        HQVisitState visitState = HQVisitState.Instance;
        List<int> orderedUnits = ResolveOrderedUnits(repo, visitState);

        for (int i = 0; i < orderedUnits.Count; i++)
        {
            int unitIndex = orderedUnits[i];
            if (!repo.TryGetUnit(unitIndex, out UnitPersistentData unit) || unit == null) continue;

            UnitData template = null;
            if (catalog != null && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey))
                catalog.TryGetPlayerTemplate(unit.UnitTemplateKey, out template);

            bool isVisiting = IsUnitVisiting(unitIndex, repo, visitState);

            HeroProfileButton item = Instantiate(itemPrefab, content);
            item.gameObject.SetActive(true);
            if (selectionMode)
                item.BindForSelect(unitIndex, InvokeSelection, unit, template, isVisiting);
            else
                item.Bind(unitIndex, infoModal, unit, template, isVisiting);
            spawnedItems.Add(item);
        }

        UpdateContentSize();

        if (scrollRect != null)
        {
            ignoreNextValueChange = true;  // Rebuild에 의한 위치 강제 변경은 idle 감지에서 제외
            scrollRect.verticalNormalizedPosition = 1f; // 최상단에서 시작
        }
        lastValueChangeTime = -1f;
    }

    private void InvokeSelection(int unitIndex)
    {
        UnitSelected?.Invoke(unitIndex);
        onUnitSelected?.Invoke(unitIndex);
    }

    // [JC 수정 260512] 머지 사이클: 파티 책임이 PartyPersistentRepository로 이관됨
    private static List<int> ResolveOrderedUnits(PersistentUnitRepository repo, HQVisitState visitState)
    {
        List<int> ordered = new List<int>();
        HashSet<int> seen = new HashSet<int>();
        PartyPersistentRepository partyRepo = PartyPersistentRepository.Instance;

        // 1) 방문중 파티의 멤버 (파티 레지스트리 순)
        if (partyRepo != null && visitState != null && visitState.HasVisitingParty)
        {
            for (int p = 0; p < partyRepo.Parties.Count; p++)
            {
                PartyPersistentData party = partyRepo.Parties[p];
                if (party == null) continue;
                if (!visitState.IsPartyVisiting(party.PartyId)) continue;

                AppendPartyUnits(party, repo, ordered, seen);
            }
        }

        // 2) 비방문 파티 멤버 (파티 등록 순)
        if (partyRepo != null)
        {
            for (int p = 0; p < partyRepo.Parties.Count; p++)
            {
                PartyPersistentData party = partyRepo.Parties[p];
                if (party == null) continue;
                if (visitState != null && visitState.IsPartyVisiting(party.PartyId)) continue;

                AppendPartyUnits(party, repo, ordered, seen);
            }
        }

        // 3) 무소속 유닛 (등록 순, 추후 고용 흐름)
        for (int u = 0; u < repo.Units.Count; u++)
        {
            UnitPersistentData unit = repo.Units[u];
            if (unit == null || seen.Contains(unit.UnitIndex)) continue;
            ordered.Add(unit.UnitIndex);
            seen.Add(unit.UnitIndex);
        }

        return ordered;
    }

    private static void AppendPartyUnits(PartyPersistentData party, PersistentUnitRepository repo, List<int> ordered, HashSet<int> seen)
    {
        for (int u = 0; u < party.UnitIndices.Count; u++)
        {
            int unitIndex = party.UnitIndices[u];
            if (unitIndex <= 0 || seen.Contains(unitIndex)) continue;
            if (!repo.ContainsUnit(unitIndex)) continue;
            ordered.Add(unitIndex);
            seen.Add(unitIndex);
        }
    }

    // [JC 수정 260512] 머지 사이클: 파티 책임이 PartyPersistentRepository로 이관됨
    private static bool IsUnitVisiting(int unitIndex, PersistentUnitRepository repo, HQVisitState visitState)
    {
        if (visitState == null || !visitState.HasVisitingParty) return false;
        PartyPersistentRepository partyRepo = PartyPersistentRepository.Instance;
        if (partyRepo == null) return false;
        for (int p = 0; p < partyRepo.Parties.Count; p++)
        {
            PartyPersistentData party = partyRepo.Parties[p];
            if (party == null) continue;
            if (!visitState.IsPartyVisiting(party.PartyId)) continue;
            for (int u = 0; u < party.UnitIndices.Count; u++)
            {
                if (party.UnitIndices[u] == unitIndex) return true;
            }
        }
        return false;
    }

    private void UpdateContentSize()
    {
        if (content == null) return;
        int count = spawnedItems.Count;
        float total = count > 0
            ? count * itemHeight + Mathf.Max(0, count - 1) * itemSpacing
            : 0f;

        Vector2 size = content.sizeDelta;
        size.y = total;
        content.sizeDelta = size;
    }

    private void ClearItems()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (spawnedItems[i] != null)
            {
                // 부모에서 즉시 분리 → VerticalLayoutGroup이 이번 프레임 자식 계산에서 제외
                spawnedItems[i].transform.SetParent(null, false);
                Destroy(spawnedItems[i].gameObject);
            }
        }
        spawnedItems.Clear();
    }

    // --- Snap / Drag ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        lastValueChangeTime = -1f;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (!enableSnap) return;
        SnapToNearest();
    }

    [ContextMenu("Snap To Nearest")]
    private void SnapToNearest()
    {
        if (scrollRect == null) return;
        int count = spawnedItems.Count;
        int scrollSteps = count - Mathf.Max(1, maxVisibleItems);
        if (scrollSteps <= 0) return;  // 보이는 수보다 적거나 같으면 스크롤 불필요

        float step = 1f / scrollSteps;
        float current = scrollRect.verticalNormalizedPosition;
        int nearestIndex = Mathf.RoundToInt((1f - current) / step);
        nearestIndex = Mathf.Clamp(nearestIndex, 0, scrollSteps);
        float target = 1f - (nearestIndex * step);

        if (snapRoutine != null) StopCoroutine(snapRoutine);
        snapRoutine = StartCoroutine(SmoothSnap(target));
    }

    private IEnumerator SmoothSnap(float target)
    {
        float duration = Mathf.Max(0.05f, snapDuration);
        float start = scrollRect.verticalNormalizedPosition;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            scrollRect.verticalNormalizedPosition = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        scrollRect.verticalNormalizedPosition = target;
        snapRoutine = null;
    }
}
