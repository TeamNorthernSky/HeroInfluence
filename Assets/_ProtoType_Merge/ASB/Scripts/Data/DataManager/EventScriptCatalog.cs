using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이벤트 스크립트 한 구역(zone)의 SO 참조 묶음.
/// 메인/서브 테이블은 로드 시 해당 구역의 저장공간 하나로 통합된다.
/// </summary>
[System.Serializable]
public class EventScriptZone
{
    [Tooltip("구역 식별자. Chat_ID/Branch_ID 맨 앞 자리와 맞추는 것을 권장 (예: 1구역→1, 4구역→4)")]
    public int zoneId;

    [Tooltip("인스펙터 식별용 라벨 (예: \"1구역\", \"4구역\"). 로직엔 영향 없음")]
    public string zoneLabel;

    public ChatDBEventDataTable   chatMainTable;
    public ChatDBEventDataTable   chatSubTable;
    public BranchDBEventDataTable branchMainTable;
    public BranchDBEventDataTable branchSubTable;

    [Header("Event Battle Tables")]
    public EnemyGroupDataTable enemyBattleGroupTable;
    public EnemyUnit1SectorDataTable enemyBattleUnitTable;
}

/// <summary>
/// 이벤트 스크립트(분기형 대사) 데이터를 SO에서 로드해 구역별 Dictionary로 캐싱하는 카탈로그.
/// 저장공간은 구역(zone)별로 분리하고, 각 구역 안에서 메인/서브는 하나로 통합한다.
/// 순수 데이터 조회까지만 담당하며, 대사 진행 상태는 이 카탈로그를 쓰는 쪽 책임이다.
/// </summary>
[DisallowMultipleComponent]
public class EventScriptCatalog : MonoBehaviour
{
    public static EventScriptCatalog Instance { get; private set; }

    [Header("Event Script Zones")]
    [SerializeField] private List<EventScriptZone> zones = new List<EventScriptZone>();

    [Header("Reward Tables")]
    [SerializeField] private MainSubEventRewardDataTable mainSubEventRewardTable;
    [SerializeField] private WorldEventRewardDataTable worldEventRewardTable;

    [Header("Settings")]
    [SerializeField] private bool loadOnAwake       = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    // 구역별 저장공간. 각 구역 안에서 메인/서브는 통합된다.
    // zoneId → (Chat_ID → 대사 노드)
    private readonly Dictionary<int, Dictionary<int, ChatDBEventData>> chatByZone
        = new Dictionary<int, Dictionary<int, ChatDBEventData>>();
    // zoneId → (Branch_ID → 선택지 리스트, Selection_Index 오름차순 정렬)
    private readonly Dictionary<int, Dictionary<int, List<BranchDBEventData>>> branchByZone
        = new Dictionary<int, Dictionary<int, List<BranchDBEventData>>>();
    private readonly Dictionary<int, Dictionary<string, EnemyGroupData>> battleGroupByZone
        = new Dictionary<int, Dictionary<string, EnemyGroupData>>();
    private readonly Dictionary<int, Dictionary<string, EnemyUnit1SectorData>> battleUnitByZone
        = new Dictionary<int, Dictionary<string, EnemyUnit1SectorData>>();
    private readonly Dictionary<int, MainSubEventRewardData> mainSubRewardLookup
        = new Dictionary<int, MainSubEventRewardData>();
    private readonly Dictionary<int, WorldEventRewardData> worldRewardLookup
        = new Dictionary<int, WorldEventRewardData>();

    private bool isLoaded;

    public bool IsLoaded => isLoaded;

    // ─────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (loadOnAwake)
            Reload();
    }

    // ─────────────────────────────────────────────────────────
    // Public API (전부 zoneId를 받는 순수 조회)
    // ─────────────────────────────────────────────────────────

    /// <summary>구역 내 Chat_ID로 대사 노드를 조회한다.</summary>
    public bool TryGetChat(int zoneId, int chatId, out ChatDBEventData chat)
    {
        EnsureLoaded();
        chat = null;
        if (chatId <= 0) return false;
        return chatByZone.TryGetValue(zoneId, out Dictionary<int, ChatDBEventData> lookup)
            && lookup.TryGetValue(chatId, out chat);
    }

    /// <summary>현재 노드의 Next_Chat_ID를 따라 다음 노드를 조회한다. (Next_Chat_ID가 0이면 종료 → false)</summary>
    public bool TryGetNextChat(int zoneId, int chatId, out ChatDBEventData next)
    {
        next = null;
        if (!TryGetChat(zoneId, chatId, out ChatDBEventData current) || current == null)
            return false;

        if (current.Next_Chat_ID <= 0)
            return false;

        return TryGetChat(zoneId, current.Next_Chat_ID, out next);
    }

    /// <summary>이 노드가 선택지 분기를 가지는지 여부.</summary>
    public bool HasBranch(ChatDBEventData chat)
    {
        return chat != null && chat.Branch_Group_ID != 0;
    }

    /// <summary>구역 내 Branch_Group_ID(= Branch_ID)에 해당하는 선택지 목록을 조회한다.</summary>
    public bool TryGetBranchOptions(int zoneId, int branchGroupId,
                                    out IReadOnlyList<BranchDBEventData> options)
    {
        EnsureLoaded();
        options = null;
        if (branchGroupId == 0) return false;

        if (branchByZone.TryGetValue(zoneId, out Dictionary<int, List<BranchDBEventData>> lookup) &&
            lookup.TryGetValue(branchGroupId, out List<BranchDBEventData> list))
        {
            options = list;
            return true;
        }
        return false;
    }

    /// <summary>선택지(Selection_Index)를 고른 결과의 Target_Talk_ID 노드를 조회한다.</summary>
    public bool TryResolveBranchTarget(int zoneId, int branchGroupId,
                                       string selectionIndex, out ChatDBEventData target)
    {
        target = null;
        if (string.IsNullOrWhiteSpace(selectionIndex)) return false;
        if (!TryGetBranchOptions(zoneId, branchGroupId, out IReadOnlyList<BranchDBEventData> options))
            return false;

        string normalized = selectionIndex.Trim();
        for (int i = 0; i < options.Count; i++)
        {
            BranchDBEventData option = options[i];
            if (option == null) continue;
            if (!string.Equals(option.Selection_Index?.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase))
                continue;

            // TODO(trigger): Trigger_Type/Trigger_Value 조건 판정은 미정. 지금은 조건 무관 그대로 해석한다.

            // Target_Talk_ID == 0 은 "이 선택지는 대화 종료" → 대상 없음(false).
            // 잘못된 ID는 데이터 오류이며, 조회 실패 시 안전하게 false 반환한다.
            if (option.Target_Talk_ID <= 0)
                return false;

            return TryGetChat(zoneId, option.Target_Talk_ID, out target);
        }
        return false;
    }

    /// <summary>디버그/에디터용: 해당 구역의 전체 대사 노드.</summary>
    public IReadOnlyList<ChatDBEventData> GetAllChats(int zoneId)
    {
        EnsureLoaded();
        return chatByZone.TryGetValue(zoneId, out Dictionary<int, ChatDBEventData> lookup)
            ? new List<ChatDBEventData>(lookup.Values)
            : new List<ChatDBEventData>();
    }

    /// <summary>디버그/UI용: 로드된 구역 ID 목록.</summary>
    public IReadOnlyList<int> GetZoneIds()
    {
        EnsureLoaded();
        return new List<int>(chatByZone.Keys);
    }

    public bool TryGetBattleEnemyGroup(int zoneId, string groupKey, out EnemyGroupData group)
    {
        EnsureLoaded();
        group = null;

        string normalizedKey = NormalizeBattleGroupKey(groupKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return false;

        return battleGroupByZone.TryGetValue(zoneId, out Dictionary<string, EnemyGroupData> lookup)
            && lookup.TryGetValue(normalizedKey, out group);
    }

    public bool TryGetBattleEnemyUnit(int zoneId, string unitKey, out EnemyUnit1SectorData unit)
    {
        EnsureLoaded();
        unit = null;

        string normalizedKey = NormalizeBattleUnitKey(unitKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return false;

        return battleUnitByZone.TryGetValue(zoneId, out Dictionary<string, EnemyUnit1SectorData> lookup)
            && lookup.TryGetValue(normalizedKey, out unit);
    }

    public bool TryGetMainSubEventReward(int rewardId, out MainSubEventRewardData reward)
    {
        EnsureLoaded();
        reward = null;
        if (rewardId <= 0)
            return false;

        return mainSubRewardLookup.TryGetValue(rewardId, out reward);
    }

    public bool TryGetWorldEventReward(int rewardId, out WorldEventRewardData reward)
    {
        EnsureLoaded();
        reward = null;
        if (rewardId <= 0)
            return false;

        return worldRewardLookup.TryGetValue(rewardId, out reward);
    }

    public bool TryGetAnyEventReward(int rewardId, out MainSubEventRewardData mainSubReward, out WorldEventRewardData worldReward)
    {
        mainSubReward = null;
        worldReward = null;

        if (TryGetMainSubEventReward(rewardId, out mainSubReward))
            return true;

        return TryGetWorldEventReward(rewardId, out worldReward);
    }

    // ─────────────────────────────────────────────────────────
    // 로드 / 캐싱
    // ─────────────────────────────────────────────────────────

    [ContextMenu("Reload")]
    public void Reload()
    {
        ClearCache();

        for (int i = 0; i < zones.Count; i++)
        {
            EventScriptZone zone = zones[i];
            if (zone == null) continue;
            LoadZone(zone);
        }

        LoadMainSubRewardTable(mainSubEventRewardTable, mainSubRewardLookup, "Global", "Reward/MainSub");
        LoadWorldRewardTable(worldEventRewardTable, worldRewardLookup, "Global", "Reward/World");

        isLoaded = true;
        LogCatalogSummary();
    }

    private void LoadZone(EventScriptZone zone)
    {
        string label = string.IsNullOrWhiteSpace(zone.zoneLabel) ? $"zone {zone.zoneId}" : zone.zoneLabel;

        // 구역 저장공간 확보 (같은 zoneId가 여러 번 나오면 같은 공간에 통합된다)
        if (!chatByZone.TryGetValue(zone.zoneId, out Dictionary<int, ChatDBEventData> chatLookup))
            chatByZone[zone.zoneId] = chatLookup = new Dictionary<int, ChatDBEventData>();
        if (!branchByZone.TryGetValue(zone.zoneId, out Dictionary<int, List<BranchDBEventData>> branchLookup))
            branchByZone[zone.zoneId] = branchLookup = new Dictionary<int, List<BranchDBEventData>>();
        if (!battleGroupByZone.TryGetValue(zone.zoneId, out Dictionary<string, EnemyGroupData> battleGroupLookup))
            battleGroupByZone[zone.zoneId] = battleGroupLookup = new Dictionary<string, EnemyGroupData>();
        if (!battleUnitByZone.TryGetValue(zone.zoneId, out Dictionary<string, EnemyUnit1SectorData> battleUnitLookup))
            battleUnitByZone[zone.zoneId] = battleUnitLookup = new Dictionary<string, EnemyUnit1SectorData>();

        // 메인/서브를 같은 저장공간으로 통합
        LoadChatTable(zone.chatMainTable, chatLookup, label, "Chat/Main");
        LoadChatTable(zone.chatSubTable,  chatLookup, label, "Chat/Sub");
        LoadBranchTable(zone.branchMainTable, branchLookup, label, "Branch/Main");
        LoadBranchTable(zone.branchSubTable,  branchLookup, label, "Branch/Sub");
        LoadBattleGroupTable(zone.enemyBattleGroupTable, battleGroupLookup, label, "Battle/EnemyGroup");
        LoadBattleUnitTable(zone.enemyBattleUnitTable, battleUnitLookup, label, "Battle/EnemyUnit");

        // 그룹별로 Selection_Index 오름차순 정렬. ("1", "1A", "2" …)
        foreach (var options in branchLookup.Values)
        {
            options.Sort((a, b) => string.Compare(
                a?.Selection_Index, b?.Selection_Index, System.StringComparison.Ordinal));
        }
    }

    private void LoadChatTable(ChatDBEventDataTable table, Dictionary<int, ChatDBEventData> lookup,
                               string zoneLabel, string source)
    {
        if (table == null)
        {
            Debug.LogWarning($"[EventScriptCatalog] {zoneLabel} {source} 테이블이 할당되지 않았습니다.", this);
            return;
        }

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ChatDBEventData row = table.DataList[i];
            if (row == null) continue;

            if (lookup.ContainsKey(row.Chat_ID))
            {
                Debug.LogWarning($"[EventScriptCatalog] {zoneLabel} 중복 Chat_ID {row.Chat_ID}({source}) 건너뜀.", this);
                continue;
            }
            lookup.Add(row.Chat_ID, row);
        }
    }

    private void LoadBranchTable(BranchDBEventDataTable table, Dictionary<int, List<BranchDBEventData>> lookup,
                                 string zoneLabel, string source)
    {
        if (table == null)
        {
            Debug.LogWarning($"[EventScriptCatalog] {zoneLabel} {source} 테이블이 할당되지 않았습니다.", this);
            return;
        }

        // Branch_ID로 선택지들을 묶는다. (정렬은 LoadZone에서 구역 로드 완료 후 일괄 수행)
        for (int i = 0; i < table.DataList.Count; i++)
        {
            BranchDBEventData row = table.DataList[i];
            if (row == null) continue;

            if (!lookup.TryGetValue(row.Branch_ID, out List<BranchDBEventData> options))
                lookup[row.Branch_ID] = options = new List<BranchDBEventData>();

            options.Add(row);
        }
    }

    private void LoadBattleGroupTable(EnemyGroupDataTable table, Dictionary<string, EnemyGroupData> lookup,
                                      string zoneLabel, string source)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            EnemyGroupData row = table.DataList[i];
            if (row == null) continue;

            string key = NormalizeBattleGroupKey(row.EnemyIndex);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (lookup.ContainsKey(key))
            {
                Debug.LogWarning($"[EventScriptCatalog] {zoneLabel} duplicate battle group key '{key}'({source}) skipped.", this);
                continue;
            }

            lookup.Add(key, row);
        }
    }

    private void LoadBattleUnitTable(EnemyUnit1SectorDataTable table, Dictionary<string, EnemyUnit1SectorData> lookup,
                                     string zoneLabel, string source)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            EnemyUnit1SectorData row = table.DataList[i];
            if (row == null) continue;

            AddBattleUnitAlias(lookup, row, row.EnemyIndex, zoneLabel, source);
            AddBattleUnitAlias(lookup, row, ExtractNumericBattleUnitKey(row.EnemyIndex), zoneLabel, source);
        }
    }

    private void LoadMainSubRewardTable(MainSubEventRewardDataTable table, Dictionary<int, MainSubEventRewardData> lookup,
                                        string zoneLabel, string source)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            MainSubEventRewardData row = table.DataList[i];
            if (row == null || row.reward_id <= 0)
                continue;

            if (lookup.ContainsKey(row.reward_id))
            {
                Debug.LogWarning($"[EventScriptCatalog] {zoneLabel} duplicate main/sub reward id '{row.reward_id}'({source}) skipped.", this);
                continue;
            }

            lookup.Add(row.reward_id, row);
        }
    }

    private void LoadWorldRewardTable(WorldEventRewardDataTable table, Dictionary<int, WorldEventRewardData> lookup,
                                      string zoneLabel, string source)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            WorldEventRewardData row = table.DataList[i];
            if (row == null || row.reward_id <= 0)
                continue;

            if (lookup.ContainsKey(row.reward_id))
            {
                Debug.LogWarning($"[EventScriptCatalog] {zoneLabel} duplicate world reward id '{row.reward_id}'({source}) skipped.", this);
                continue;
            }

            lookup.Add(row.reward_id, row);
        }
    }

    private void AddBattleUnitAlias(Dictionary<string, EnemyUnit1SectorData> lookup, EnemyUnit1SectorData row,
                                    string rawKey, string zoneLabel, string source)
    {
        string key = NormalizeBattleUnitKey(rawKey);
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (lookup.TryGetValue(key, out EnemyUnit1SectorData existing))
        {
            if (!ReferenceEquals(existing, row))
                Debug.LogWarning($"[EventScriptCatalog] {zoneLabel} duplicate battle unit key '{key}'({source}) skipped.", this);
            return;
        }

        lookup.Add(key, row);
    }

    private void LogCatalogSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[EventScriptCatalog] Loaded zones={chatByZone.Count}");
        foreach (var pair in chatByZone)
        {
            int zoneId = pair.Key;
            int chatCount = pair.Value.Count;
            int branchCount = branchByZone.TryGetValue(zoneId, out var b) ? b.Count : 0;
            int battleGroupCount = battleGroupByZone.TryGetValue(zoneId, out var bg) ? bg.Count : 0;
            int battleUnitCount = battleUnitByZone.TryGetValue(zoneId, out var bu) ? bu.Count : 0;
            sb.Append($" | zone {zoneId}: Chat {chatCount}, Branch {branchCount}, BattleGroup {battleGroupCount}, BattleUnit {battleUnitCount}");
        }
        sb.Append($" | Rewards: MainSub {mainSubRewardLookup.Count}, World {worldRewardLookup.Count}");
        Debug.Log(sb.ToString(), this);
    }

    private void LogSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[EventScriptCatalog] 로드 완료 — 구역 {chatByZone.Count}개");
        foreach (var pair in chatByZone)
        {
            int zoneId = pair.Key;
            int chatCount = pair.Value.Count;
            int branchCount = branchByZone.TryGetValue(zoneId, out var b) ? b.Count : 0;
            sb.Append($" | zone {zoneId}: Chat {chatCount}, Branch {branchCount} 그룹");
        }
        Debug.Log(sb.ToString(), this);
    }

    // ─────────────────────────────────────────────────────────
    // 공통 유틸
    // ─────────────────────────────────────────────────────────

    private void ClearCache()
    {
        chatByZone.Clear();
        branchByZone.Clear();
        battleGroupByZone.Clear();
        battleUnitByZone.Clear();
        mainSubRewardLookup.Clear();
        worldRewardLookup.Clear();
        isLoaded = false;
    }

    private void EnsureLoaded()
    {
        if (!isLoaded) Reload();
    }

    private static string NormalizeBattleGroupKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeBattleUnitKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string ExtractNumericBattleUnitKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var digits = new System.Text.StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsDigit(value[i]))
                digits.Append(value[i]);
        }

        return digits.Length > 0 ? digits.ToString() : string.Empty;
    }
}
