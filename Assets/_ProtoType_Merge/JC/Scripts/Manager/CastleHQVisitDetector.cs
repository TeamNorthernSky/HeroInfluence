using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 단독 규격] DHScene Castle GO에 부착. 매 폴링마다 Castle 그리드 + 오프셋 좌표에 있는
/// PartyGridMover를 탐색해 HQVisitState에 본부 방문 중인 파티 ID를 등록.
/// DH 정식 구현(Castle/그리드 시스템)이 도입되면 본 컴포넌트는 폐기 후보.
/// </summary>
[DisallowMultipleComponent]
public class CastleHQVisitDetector : MonoBehaviour
{
    [Header("Grid Dependency")]
    [Tooltip("DH의 GridManager 참조. World→Grid 좌표 변환에 사용")]
    [SerializeField] private GridManager gridManager;

    [Header("Detection")]
    [Tooltip("Castle 그리드 좌표에서의 오프셋 후보들. 본부 앞 좌·우 등 여러 그리드를 방문 판정에 포함. 기본 (0,-2)·(1,-2)")]
    [SerializeField] private List<Vector2Int> targetGridOffsets = new List<Vector2Int> { new Vector2Int(0, -2), new Vector2Int(1, -2) };

    [Tooltip("폴링 주기(초). 파티 이동 빈도 따라 조정")]
    [SerializeField] private float pollInterval = 0.2f;

    [Header("Visitor Visual Indicator")]
    [Tooltip("본부 위에 띄울 방문 표시 GameObject. 방문 파티 있을 때 SetActive(true), 없을 때 (false). 비워두면 무시")]
    [SerializeField] private GameObject visitorIndicator;

    [Header("Debug")]
    [SerializeField] private bool logVisitChanges = false;

    private float nextPollTime;
    private PartyGridMover[] cachedMovers;
    private float nextMoverRefreshTime;

    private void OnEnable()
    {
        nextPollTime = 0f;
        nextMoverRefreshTime = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextPollTime) return;
        nextPollTime = Time.unscaledTime + Mathf.Max(0.05f, pollInterval);

        Poll();
    }

    /// <summary>외부에서 즉시 폴링 강제 (게이트씬 부트스트랩 등). 폴링 타이머 무시.</summary>
    public void PollImmediately()
    {
        nextPollTime = 0f;
        Poll();
    }

    private void Poll()
    {
        HQVisitState state = HQVisitState.Instance;
        if (state == null) return;

        if (gridManager == null)
        {
            state.ClearVisitingParties();
            ApplyVisitorIndicator(false);
            return;
        }

        if (cachedMovers == null || Time.unscaledTime >= nextMoverRefreshTime)
        {
            cachedMovers = FindObjectsByType<PartyGridMover>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            nextMoverRefreshTime = Time.unscaledTime + 1f; // 파티 추가/제거 빈도가 낮으므로 1초
        }

        Vector2Int castleGrid = gridManager.WorldToGrid(transform.position);

        var detected = new HashSet<string>();
        for (int i = 0; i < cachedMovers.Length; i++)
        {
            PartyGridMover mover = cachedMovers[i];
            if (mover == null) continue;

            Vector2Int partyGrid = mover.GetCurrentGrid();
            bool matched = false;
            for (int o = 0; o < targetGridOffsets.Count; o++)
            {
                if (partyGrid == castleGrid + targetGridOffsets[o]) { matched = true; break; }
            }
            if (!matched) continue;

            PartyIdentity identity = mover.GetComponent<PartyIdentity>();
            string partyId = identity != null ? identity.PartyId : mover.gameObject.name;
            if (!string.IsNullOrWhiteSpace(partyId))
                detected.Add(partyId);
        }

        int prevCount = state.VisitingPartyIds.Count;
        state.SetVisitingParties(detected);

        ApplyVisitorIndicator(state.HasVisitingParty);

        if (logVisitChanges && (prevCount != state.VisitingPartyIds.Count || detected.Count != prevCount))
            Debug.Log($"[CastleHQVisitDetector] VisitingParties count: {prevCount} → {state.VisitingPartyIds.Count} [{string.Join(",", detected)}]");
    }

    private void ApplyVisitorIndicator(bool hasVisitor)
    {
        if (visitorIndicator == null) return;
        if (visitorIndicator.activeSelf != hasVisitor)
            visitorIndicator.SetActive(hasVisitor);
    }

    private void OnDisable()
    {
        ApplyVisitorIndicator(false);
    }
}
