using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 단독 규격, 260514 DH 정문 정책으로 일원화]
/// HeroUnion GO에 부착. PartyGridMover.GridEntered 이벤트를 구독해
/// HeroUnionUnit.IsInteractionCell(partyGrid) 매칭으로 방문 판정 → HQVisitState에 방문 파티 ID 등록.
/// 매프레임 폴링이 아니라 그리드 진입 이벤트 시점에만 재평가하므로 성능 부담이 없고 즉시 반응한다.
///
/// 변경 이력:
/// - 260513 신설: heroUnionGrid + targetGridOffsets 매칭 방식
/// - 260514 일원화: DH IsInteractionCell 호출로 변경. targetGridOffsets·gridManager·heroUnionGrid 계산 폐기.
///   사유: DH 5차 신본에서 2×2 건물 정문(하단 1줄) 진입 정책 도입. HI 방문 판정도 동일 위치로 통일하여
///   "정문 도착 = 방문중" 자연 흐름 + DH 정책 변경 자동 추종.
///
/// 향후 과제: 외부거점/적 건물 등도 방문 추적 필요. HQVisitState 일반화 + Detector 베이스 클래스 추출 트랙 별도.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(HeroUnionUnit))]
public class HeroUnionHQVisitDetector : MonoBehaviour
{
    [Header("Visitor Visual Indicator")]
    [Tooltip("본부 위에 띄울 방문 표시 GameObject. 방문 파티 있을 때 SetActive(true), 없을 때 (false). 비워두면 무시")]
    [SerializeField] private GameObject visitorIndicator;

    [Header("Debug")]
    [SerializeField] private bool logVisitChanges = false;

    // [JC 260618] HQVisitState 일반화: 본부는 고정 source 키로 자기 방문 파티만 갱신(거점 source와 분리).
    private HeroUnionUnit heroUnionUnit;
    private readonly List<PartyGridMover> subscribedMovers = new List<PartyGridMover>();

    // JC | 2026-11-10 | 메인 탐사씬 연출 연결. 방문 상태와 표시 종료 타이밍을 분리하고 기존 등록부의 파티만 구독합니다.
    private JcAssociationIndicatorMotion indicatorMotion;
    private PartyRegistry partyRegistry;
    private PartyGridMover registeredParty;
    private bool lastEligible, lastEnding;
    // JC | 2026-11-11 | 메인에서 주입한 등록부는 검색하지 않고, 기존 씬은 Optimize의 단일 파티 구독 갱신을 유지합니다.
    private bool useConfiguredPartyRegistry;

    public void ConfigureIndicator(GameObject indicator, JcAssociationIndicatorMotion motion, PartyRegistry registry)
    {
        visitorIndicator = indicator;
        indicatorMotion = motion;
        partyRegistry = registry;
        useConfiguredPartyRegistry = registry != null;
        if (partyRegistry != null) BindRegisteredParty();
    }

    private void LateUpdate()
    {
        if (!useConfiguredPartyRegistry && RefreshPlayerSubscription()) EvaluateAllVisits();
        // JC | 2026-11-10 | 참조/활성/패배 대기/엔딩 변경만 O(1)로 확인합니다. 이동 판정은 GridEntered 이벤트를 유지합니다.
        if (useConfiguredPartyRegistry && partyRegistry != null && registeredParty != partyRegistry.PlayerParty)
        {
            BindRegisteredParty();
            EvaluateAllVisits();
        }
        bool eligible = IsRegisteredPartyEligible();
        bool ending = DHGameEndState.IsEnding;
        if (eligible != lastEligible || ending != lastEnding)
        {
            lastEligible = eligible;
            lastEnding = ending;
            EvaluateAllVisits();
        }
    }

    private bool IsRegisteredPartyEligible() => registeredParty != null
        && registeredParty.gameObject.activeInHierarchy && !DefeatedPartyReturnController.IsPartyWaiting(registeredParty);

    private void BindRegisteredParty()
    {
        UnsubscribeAllMovers();
        registeredParty = partyRegistry.PlayerParty;
        if (registeredParty != null)
        {
            registeredParty.GridEntered += OnPartyGridEntered;
            subscribedMovers.Add(registeredParty);
        }
        lastEligible = IsRegisteredPartyEligible();
        lastEnding = DHGameEndState.IsEnding;
    }

    private void Awake()
    {
        heroUnionUnit = GetComponent<HeroUnionUnit>();
    }

    private void OnEnable()
    {
        if (useConfiguredPartyRegistry && partyRegistry != null) BindRegisteredParty();
        else RefreshPlayerSubscription();
        EvaluateAllVisits();
    }

    private void OnDisable()
    {
        // [JC 수정 260512] HQVisitState.ClearVisitingParties() 호출 제거.
        //   사유: DHScene unload 시 visiting 정보가 클리어되면 로비에서 방문중 멤버 녹색 표시가 사라지는 회귀.
        //   HQVisitState는 DontDestroyOnLoad이고 다른 씬(로비)에서도 사용. DHScene 재진입 시 OnEnable의
        //   EvaluateAllVisits가 현재 mover 위치 기반으로 재평가하므로 stale 정보 위험 없음.
        UnsubscribeAllMovers();
        // JC | 2026-11-10 | 씬 종료/건물 비활성화 때는 사라지는 연출을 기다리지 않고 즉시 정리합니다.
        if (indicatorMotion != null) indicatorMotion.SetVisible(false, true);
        else ApplyVisitorIndicator(false);
    }

    /// <summary>외부에서 즉시 재평가 강제 (게이트씬 부트스트랩 등).</summary>
    public void ReevaluateNow()
    {
        EvaluateAllVisits();
    }

    // JC | 2026-11-10 | 기존 단일 파티가 등록되기 전에도 탐색은 가능하게 유지하되 정상 등록 후에는 참조 읽기만 수행합니다.
    private bool RefreshPlayerSubscription()
    {
        if (partyRegistry == null || !partyRegistry.gameObject.activeInHierarchy)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();
        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party == null) party = FindFirstObjectByType<PartyGridMover>();
        if (party != null && !party.gameObject.activeInHierarchy) party = null;
        if (party == null ? subscribedMovers.Count == 0 : subscribedMovers.Count == 1 && subscribedMovers[0] == party)
            return false;

        UnsubscribeAllMovers();
        registeredParty = party;
        if (party != null)
        {
            party.GridEntered += OnPartyGridEntered;
            subscribedMovers.Add(party);
        }
        return true;
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

        if (DHGameEndState.IsEnding)
        {
            state.ClearSource(GetSourceKey());
            ApplyVisitorIndicator(false);
            return;
        }

        if (heroUnionUnit == null) heroUnionUnit = GetComponent<HeroUnionUnit>();
        if (heroUnionUnit == null)
        {
            state.ClearSource(GetSourceKey());
            ApplyVisitorIndicator(false);
            return;
        }

        // JC | 2026-11-11 | Optimize의 호출별 임시 집합 대여를 유지하여 방문 알림 중 재진입해도 연출 판정 결과가 섞이지 않게 합니다.
        using var detectedLease = UnityEngine.Pool.HashSetPool<string>.Get(out var detected);

        for (int i = 0; i < subscribedMovers.Count; i++)
        {
            var mover = subscribedMovers[i];
            if (mover == null) continue;
            if (!mover.gameObject.activeInHierarchy) continue;
            if (DefeatedPartyReturnController.IsPartyWaiting(mover)) continue;

            // [JC 260514 일원화] DH 정문 정책(IsInteractionCell)에 위임. 본부의 진입 셀 = 방문 위치.
            if (!heroUnionUnit.IsInteractionCell(mover.GetCurrentGrid())) continue;

            PartyIdentity identity = mover.GetComponent<PartyIdentity>();
            string partyId = identity != null ? identity.PartyId : mover.gameObject.name;
            if (!string.IsNullOrWhiteSpace(partyId))
                detected.Add(partyId);
        }

        int prevCount = state.VisitingPartyIds.Count;
        state.SetVisitingParties(GetSourceKey(), detected);
        // JC | 2026-11-10 | 메인 협회 연출은 이 협회의 방문 결과만 사용합니다. 거점 방문의 합집합은 저장/로비 기능에 그대로 둡니다.
        ApplyVisitorIndicator(indicatorMotion != null ? detected.Count > 0 : state.HasVisitingParty);

        if (logVisitChanges && prevCount != state.VisitingPartyIds.Count)
            Debug.Log($"[HeroUnionHQVisitDetector] VisitingParties count: {prevCount} → {state.VisitingPartyIds.Count} [{string.Join(",", detected)}]");
    }

    private string GetSourceKey()
    {
        if (heroUnionUnit == null)
            heroUnionUnit = GetComponent<HeroUnionUnit>();

        if (heroUnionUnit != null)
            return heroUnionUnit.GetProgressKey();

        return HQVisitState.SourceHQ;
    }

    private void ApplyVisitorIndicator(bool hasVisitor)
    {
        if (visitorIndicator == null) return;
        if (indicatorMotion != null)
        {
            indicatorMotion.SetVisible(hasVisitor);
            return;
        }
        if (visitorIndicator.activeSelf != hasVisitor)
            visitorIndicator.SetActive(hasVisitor);
    }
}
