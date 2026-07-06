using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 단독 규격] 본부·점령 거점 등 "상호작용 거점"에 방문 중인 파티 ID 집합. DontDestroyOnLoad Singleton.
/// [JC 260618 일반화] source별(본부="HQ" / 거점=progressKey 등) 방문 집합을 분리 보관하고, 외부에는 합집합을 노출한다.
///   → 본부와 거점이 서로의 방문 정보를 덮어쓰지 않는다(과거 SetVisitingParties 전체교체 충돌 해소).
///   읽는 쪽(시설 visitingOnly·출전 등)은 합집합만 보므로 거점 방문 파티도 자동으로 동일하게 다뤄진다.
/// 데이터 채움: HeroUnionHQVisitDetector(본부, source="HQ") + OutpostVisitIndicator(거점, source=거점 progressKey).
/// 데이터 읽음: HeroListController·SortieController·LobbyMemberPanelBinder 등 로비 UI.
/// 추후 DH 정식 구현 도입 시 폐기 가능.
/// </summary>
public class HQVisitState : MonoBehaviour
{
    private const string GameObjectName = "[HQVisitState]";

    public static HQVisitState Instance { get; private set; }

    /// <summary>본부(HeroUnion) 방문의 고정 source 키. 거점은 progressKey를 source로 사용.</summary>
    public const string SourceHQ = "HQ";

    // [JC 260618] source key → 그 source가 감지한 방문 파티 집합. 합집합(unionCache)이 외부 노출값.
    private readonly Dictionary<string, HashSet<string>> sourceToParties = new Dictionary<string, HashSet<string>>();
    private readonly HashSet<string> unionCache = new HashSet<string>();

    /// <summary>방문중 파티 ID 집합 (모든 source 합집합, 읽기 전용).</summary>
    public IReadOnlyCollection<string> VisitingPartyIds => unionCache;

    /// <summary>방문중인 파티가 1개 이상인지.</summary>
    public bool HasVisitingParty => unionCache.Count > 0;

    /// <summary>방문중 파티 변경 알림 (합집합이 실제로 바뀐 경우에만 발생).</summary>
    public event Action OnVisitingChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null) return;

        GameObject host = new GameObject(GameObjectName);
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<HQVisitState>();
    }

    /// <summary>특정 파티가 (어느 거점에든) 방문중인지 조회.</summary>
    public bool IsPartyVisiting(string partyId)
    {
        return !string.IsNullOrEmpty(partyId) && unionCache.Contains(partyId);
    }

    /// <summary>
    /// 특정 source(본부 "HQ" / 거점 progressKey 등)의 방문 파티 집합을 갱신한다. 다른 source는 보존.
    /// 빈 집합이면 해당 source를 제거한다. 합집합이 바뀌면 OnVisitingChanged.
    /// </summary>
    public void SetVisitingParties(string sourceKey, IEnumerable<string> partyIds)
    {
        if (string.IsNullOrEmpty(sourceKey)) return;

        HashSet<string> next = new HashSet<string>();
        if (partyIds != null)
        {
            foreach (var id in partyIds)
                if (!string.IsNullOrWhiteSpace(id)) next.Add(id);
        }

        if (next.Count == 0)
        {
            if (!sourceToParties.Remove(sourceKey)) return; // 이미 없으면 변화 없음
        }
        else
        {
            if (sourceToParties.TryGetValue(sourceKey, out var cur) && cur.SetEquals(next)) return; // 변화 없음
            sourceToParties[sourceKey] = next;
        }

        RebuildUnion();
    }

    /// <summary>특정 source의 방문 정보만 제거(다른 source 보존).</summary>
    public void ClearSource(string sourceKey)
    {
        if (string.IsNullOrEmpty(sourceKey)) return;
        if (sourceToParties.Remove(sourceKey)) RebuildUnion();
    }

    /// <summary>모든 source의 방문 정보를 비움(새 게임/리셋 등).</summary>
    public void ClearVisitingParties()
    {
        if (sourceToParties.Count == 0 && unionCache.Count == 0) return;
        sourceToParties.Clear();
        RebuildUnion();
    }

    private void RebuildUnion()
    {
        bool changed = false;

        // 변경 여부 판정을 위해 기존 합집합과 비교.
        HashSet<string> rebuilt = new HashSet<string>();
        foreach (var kv in sourceToParties)
            foreach (var id in kv.Value)
                rebuilt.Add(id);

        if (!rebuilt.SetEquals(unionCache))
        {
            unionCache.Clear();
            foreach (var id in rebuilt) unionCache.Add(id);
            changed = true;
        }

        if (changed) OnVisitingChanged?.Invoke();
    }
}
