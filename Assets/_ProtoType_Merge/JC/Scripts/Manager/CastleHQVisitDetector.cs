using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 단독 규격, 260514 DH 정문 정책으로 일원화]
/// Castle GO에 부착. PartyGridMover.GridEntered 이벤트를 구독해
/// CastleUnit.IsInteractionCell(partyGrid) 매칭으로 방문 판정 → HQVisitState에 방문 파티 ID 등록.
/// 매프레임 폴링이 아니라 그리드 진입 이벤트 시점에만 재평가하므로 성능 부담이 없고 즉시 반응한다.
///
/// 변경 이력:
/// - 260513 신설: castleGrid + targetGridOffsets 매칭 방식
/// - 260514 일원화: DH IsInteractionCell 호출로 변경. targetGridOffsets·gridManager·castleGrid 계산 폐기.
///   사유: DH 5차 신본에서 2×2 건물 정문(하단 1줄) 진입 정책 도입. HI 방문 판정도 동일 위치로 통일하여
///   "정문 도착 = 방문중" 자연 흐름 + DH 정책 변경 자동 추종.
///
/// 향후 과제: 외부거점/적 건물 등도 방문 추적 필요. HQVisitState 일반화 + Detector 베이스 클래스 추출 트랙 별도.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CastleUnit))]
public class CastleHQVisitDetector : MonoBehaviour
{
    [Header("Visitor Visual Indicator")]
    [Tooltip("본부 위에 띄울 방문 표시 GameObject. 방문 파티 있을 때 SetActive(true), 없을 때 (false). 비워두면 무시")]
    [SerializeField] private GameObject visitorIndicator;

    [Header("Debug")]
    [SerializeField] private bool logVisitChanges = false;

    private CastleUnit castleUnit;
    private readonly List<PartyGridMover> subscribedMovers = new List<PartyGridMover>();

    private void Awake()
    {
        castleUnit = GetComponent<CastleUnit>();
    }

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

        if (castleUnit == null) castleUnit = GetComponent<CastleUnit>();
        if (castleUnit == null)
        {
            state.ClearVisitingParties();
            ApplyVisitorIndicator(false);
            return;
        }

        var detected = new HashSet<string>();

        for (int i = 0; i < subscribedMovers.Count; i++)
        {
            var mover = subscribedMovers[i];
            if (mover == null) continue;

            // [JC 260514 일원화] DH 정문 정책(IsInteractionCell)에 위임. 본부의 진입 셀 = 방문 위치.
            if (!castleUnit.IsInteractionCell(mover.GetCurrentGrid())) continue;

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

    private void ApplyVisitorIndicator(bool hasVisitor)
    {
        if (visitorIndicator == null) return;
        if (visitorIndicator.activeSelf != hasVisitor)
            visitorIndicator.SetActive(hasVisitor);
    }
}
