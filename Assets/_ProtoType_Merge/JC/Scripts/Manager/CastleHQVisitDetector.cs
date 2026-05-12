using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 단독 규격] DHScene Castle GO에 부착. PartyGridMover.GridEntered 이벤트를 구독해
/// Castle 그리드 + 오프셋 매칭을 평가하고 HQVisitState에 방문 파티 ID를 등록한다.
/// 매프레임 폴링이 아니라 그리드 진입 이벤트 시점에만 재평가하므로 성능 부담이 없고 즉시 반응한다.
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

    [Header("Visitor Visual Indicator")]
    [Tooltip("본부 위에 띄울 방문 표시 GameObject. 방문 파티 있을 때 SetActive(true), 없을 때 (false). 비워두면 무시")]
    [SerializeField] private GameObject visitorIndicator;

    [Header("Debug")]
    [SerializeField] private bool logVisitChanges = false;

    private readonly List<PartyGridMover> subscribedMovers = new List<PartyGridMover>();

    private void OnEnable()
    {
        SubscribeAllMovers();
        EvaluateAllVisits();
    }

    private void OnDisable()
    {
        // [JC 수정 260512] HQVisitState.ClearVisitingParties() 호출 제거.
        //   사유: DHScene unload 시 visiting 정보가 클리어되면 로비에서 방문중 멤버 녹색 표시가 사라지는 회귀.
        //   HQVisitState는 DontDestroyOnLoad이고 다른 씬(로비)에서도 사용. DHScene 재진입 시 OnEnable의
        //   EvaluateAllVisits가 현재 mover 위치 기반으로 재평가하므로 stale 정보 위험 없음.
        UnsubscribeAllMovers();
        ApplyVisitorIndicator(false);
    }

    /// <summary>외부에서 즉시 재평가 강제 (게이트씬 부트스트랩 등).</summary>
    public void ReevaluateNow()
    {
        EvaluateAllVisits();
    }

    private void SubscribeAllMovers()
    {
        UnsubscribeAllMovers();
        var movers = FindObjectsByType<PartyGridMover>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < movers.Length; i++)
        {
            var m = movers[i];
            if (m == null) continue;
            m.GridEntered += OnPartyGridEntered;
            subscribedMovers.Add(m);
        }
    }

    private void UnsubscribeAllMovers()
    {
        for (int i = 0; i < subscribedMovers.Count; i++)
        {
            var m = subscribedMovers[i];
            if (m != null) m.GridEntered -= OnPartyGridEntered;
        }
        subscribedMovers.Clear();
    }

    private void OnPartyGridEntered(Vector2Int _)
    {
        EvaluateAllVisits();
    }

    private void EvaluateAllVisits()
    {
        HQVisitState state = HQVisitState.Instance;
        if (state == null) return;

        if (gridManager == null)
        {
            state.ClearVisitingParties();
            ApplyVisitorIndicator(false);
            return;
        }

        Vector2Int castleGrid = gridManager.WorldToGrid(transform.position);
        var detected = new HashSet<string>();

        for (int i = 0; i < subscribedMovers.Count; i++)
        {
            var mover = subscribedMovers[i];
            if (mover == null) continue;

            if (!MatchesAnyOffset(mover.GetCurrentGrid(), castleGrid)) continue;

            PartyIdentity identity = mover.GetComponent<PartyIdentity>();
            string partyId = identity != null ? identity.PartyId : mover.gameObject.name;
            if (!string.IsNullOrWhiteSpace(partyId))
                detected.Add(partyId);
        }

        int prevCount = state.VisitingPartyIds.Count;
        state.SetVisitingParties(detected);
        ApplyVisitorIndicator(state.HasVisitingParty);

        if (logVisitChanges && prevCount != state.VisitingPartyIds.Count)
            Debug.Log($"[CastleHQVisitDetector] VisitingParties count: {prevCount} → {state.VisitingPartyIds.Count} [{string.Join(",", detected)}]");
    }

    private bool MatchesAnyOffset(Vector2Int partyGrid, Vector2Int castleGrid)
    {
        for (int o = 0; o < targetGridOffsets.Count; o++)
            if (partyGrid == castleGrid + targetGridOffsets[o]) return true;
        return false;
    }

    private void ApplyVisitorIndicator(bool hasVisitor)
    {
        if (visitorIndicator == null) return;
        if (visitorIndicator.activeSelf != hasVisitor)
            visitorIndicator.SetActive(hasVisitor);
    }
}
