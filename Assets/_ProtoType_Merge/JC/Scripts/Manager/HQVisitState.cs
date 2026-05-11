using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 단독 규격] 본부 방문 중인 파티 ID 집합. DontDestroyOnLoad Singleton.
/// 다중 파티 동시 방문 지원 (1·2 파티 동시 방문 가능 케이스 대응).
/// 데이터 채움: CastleHQVisitDetector(탐사씬, Castle GO 부착)가 거리 검사로 갱신.
/// 데이터 읽음: LobbyMenuController, HeroListController, HeroProfileButton 등 로비 UI.
/// 추후 DH 정식 구현 도입 시 폐기 가능.
/// </summary>
public class HQVisitState : MonoBehaviour
{
    private const string GameObjectName = "[HQVisitState]";

    public static HQVisitState Instance { get; private set; }

    private readonly HashSet<string> visitingPartyIds = new HashSet<string>();

    /// <summary>방문중 파티 ID 집합 (읽기 전용).</summary>
    public IReadOnlyCollection<string> VisitingPartyIds => visitingPartyIds;

    /// <summary>방문중인 파티가 1개 이상인지.</summary>
    public bool HasVisitingParty => visitingPartyIds.Count > 0;

    /// <summary>방문중 파티 변경 알림 (추가/제거/Clear 무관 일괄 단일 이벤트).</summary>
    public event Action OnVisitingChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null) return;

        GameObject host = new GameObject(GameObjectName);
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<HQVisitState>();
    }

    /// <summary>특정 파티가 방문중인지 조회.</summary>
    public bool IsPartyVisiting(string partyId)
    {
        return !string.IsNullOrEmpty(partyId) && visitingPartyIds.Contains(partyId);
    }

    /// <summary>방문중 파티 ID 집합 전체 갱신. 이전 집합과 다르면 OnVisitingChanged 발생.</summary>
    public void SetVisitingParties(IEnumerable<string> partyIds)
    {
        var next = new HashSet<string>();
        if (partyIds != null)
        {
            foreach (var id in partyIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    next.Add(id);
            }
        }

        if (next.SetEquals(visitingPartyIds))
            return;

        visitingPartyIds.Clear();
        foreach (var id in next)
            visitingPartyIds.Add(id);

        OnVisitingChanged?.Invoke();
    }

    /// <summary>방문 집합을 비움.</summary>
    public void ClearVisitingParties()
    {
        if (visitingPartyIds.Count == 0) return;
        visitingPartyIds.Clear();
        OnVisitingChanged?.Invoke();
    }
}
