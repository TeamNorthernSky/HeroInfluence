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
    private readonly Dictionary<int, Dictionary<int, DHEventChatTemplate>> chatTemplateByZone
        = new Dictionary<int, Dictionary<int, DHEventChatTemplate>>();
    // zoneId → (Branch_ID → 선택지 리스트, Selection_Index 오름차순 정렬)
    private readonly Dictionary<int, Dictionary<int, List<BranchDBEventData>>> branchByZone
        = new Dictionary<int, Dictionary<int, List<BranchDBEventData>>>();
    private readonly Dictionary<int, Dictionary<int, List<DHEventBranchTemplate>>> branchTemplateByZone
        = new Dictionary<int, Dictionary<int, List<DHEventBranchTemplate>>>();
    private readonly Dictionary<int, Dictionary<string, EnemyGroupData>> battleGroupByZone
        = new Dictionary<int, Dictionary<string, EnemyGroupData>>();
    private readonly Dictionary<int, Dictionary<string, DHEventBattleGroupTemplate>> battleGroupTemplateByZone
        = new Dictionary<int, Dictionary<string, DHEventBattleGroupTemplate>>();
    private readonly Dictionary<int, Dictionary<string, EnemyUnit1SectorData>> battleUnitByZone
        = new Dictionary<int, Dictionary<string, EnemyUnit1SectorData>>();
    private readonly Dictionary<int, Dictionary<string, DHEventBattleUnitTemplate>> battleUnitTemplateByZone
        = new Dictionary<int, Dictionary<string, DHEventBattleUnitTemplate>>();
    private readonly Dictionary<int, MainSubEventRewardData> mainSubRewardLookup
        = new Dictionary<int, MainSubEventRewardData>();
    private readonly Dictionary<int, WorldEventRewardData> worldRewardLookup
        = new Dictionary<int, WorldEventRewardData>();
    private readonly Dictionary<int, DHEventRewardTemplate> rewardTemplateLookup
        = new Dictionary<int, DHEventRewardTemplate>();

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

    public bool TryGetChatTemplate(int zoneId, int chatId, out DHEventChatTemplate chat)
    {
        EnsureLoaded();
        chat = null;
        if (chatId <= 0) return false;
        return chatTemplateByZone.TryGetValue(zoneId, out Dictionary<int, DHEventChatTemplate> lookup)
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

    public bool TryGetNextChatTemplate(int zoneId, int chatId, out DHEventChatTemplate next)
    {
        next = null;
        if (!TryGetChatTemplate(zoneId, chatId, out DHEventChatTemplate current) || current == null)
            return false;

        if (current.NextChatId <= 0)
            return false;

        return TryGetChatTemplate(zoneId, current.NextChatId, out next);
    }

    /// <summary>이 노드가 선택지 분기를 가지는지 여부.</summary>
    public bool HasBranch(ChatDBEventData chat)
    {
        return chat != null && chat.Branch_Group_ID != 0;
    }

    public bool HasBranch(DHEventChatTemplate chat)
    {
        return chat != null && chat.BranchGroupId != 0;
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

    public bool TryGetBranchOptionTemplates(int zoneId, int branchGroupId,
                                            out IReadOnlyList<DHEventBranchTemplate> options)
    {
        EnsureLoaded();
        options = null;
        if (branchGroupId == 0) return false;

        if (branchTemplateByZone.TryGetValue(zoneId, out Dictionary<int, List<DHEventBranchTemplate>> lookup) &&
            lookup.TryGetValue(branchGroupId, out List<DHEventBranchTemplate> list))
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

    public IReadOnlyList<DHEventChatTemplate> GetAllChatTemplates(int zoneId)
    {
        EnsureLoaded();
        return chatTemplateByZone.TryGetValue(zoneId, out Dictionary<int, DHEventChatTemplate> lookup)
            ? new List<DHEventChatTemplate>(lookup.Values)
            : new List<DHEventChatTemplate>();
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

    public bool TryGetBattleEnemyGroupTemplate(int zoneId, string groupKey, out DHEventBattleGroupTemplate group)
    {
        EnsureLoaded();
        group = null;

        string normalizedKey = NormalizeBattleGroupKey(groupKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return false;

        return battleGroupTemplateByZone.TryGetValue(zoneId, out Dictionary<string, DHEventBattleGroupTemplate> lookup)
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

    public bool TryGetBattleEnemyUnitTemplate(int zoneId, string unitKey, out DHEventBattleUnitTemplate unit)
    {
        EnsureLoaded();
        unit = null;

        string normalizedKey = NormalizeBattleUnitKey(unitKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return false;

        return battleUnitTemplateByZone.TryGetValue(zoneId, out Dictionary<string, DHEventBattleUnitTemplate> lookup)
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

    public bool TryGetEventRewardTemplate(int rewardId, out DHEventRewardTemplate reward)
    {
        EnsureLoaded();
        reward = null;
        if (rewardId <= 0)
            return false;

        return rewardTemplateLookup.TryGetValue(rewardId, out reward);
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

        LoadMainSubRewardTable(mainSubEventRewardTable, mainSubRewardLookup, rewardTemplateLookup, "Global", "Reward/MainSub");
        LoadWorldRewardTable(worldEventRewardTable, worldRewardLookup, rewardTemplateLookup, "Global", "Reward/World");

        isLoaded = true;
        LogCatalogSummary();
    }

    private void LoadZone(EventScriptZone zone)
    {
        string label = string.IsNullOrWhiteSpace(zone.zoneLabel) ? $"zone {zone.zoneId}" : zone.zoneLabel;

        // 구역 저장공간 확보 (같은 zoneId가 여러 번 나오면 같은 공간에 통합된다)
        if (!chatByZone.TryGetValue(zone.zoneId, out Dictionary<int, ChatDBEventData> chatLookup))
            chatByZone[zone.zoneId] = chatLookup = new Dictionary<int, ChatDBEventData>();
        if (!chatTemplateByZone.TryGetValue(zone.zoneId, out Dictionary<int, DHEventChatTemplate> chatTemplateLookup))
            chatTemplateByZone[zone.zoneId] = chatTemplateLookup = new Dictionary<int, DHEventChatTemplate>();
        if (!branchByZone.TryGetValue(zone.zoneId, out Dictionary<int, List<BranchDBEventData>> branchLookup))
            branchByZone[zone.zoneId] = branchLookup = new Dictionary<int, List<BranchDBEventData>>();
        if (!branchTemplateByZone.TryGetValue(zone.zoneId, out Dictionary<int, List<DHEventBranchTemplate>> branchTemplateLookup))
            branchTemplateByZone[zone.zoneId] = branchTemplateLookup = new Dictionary<int, List<DHEventBranchTemplate>>();
        if (!battleGroupByZone.TryGetValue(zone.zoneId, out Dictionary<string, EnemyGroupData> battleGroupLookup))
            battleGroupByZone[zone.zoneId] = battleGroupLookup = new Dictionary<string, EnemyGroupData>();
        if (!battleGroupTemplateByZone.TryGetValue(zone.zoneId, out Dictionary<string, DHEventBattleGroupTemplate> battleGroupTemplateLookup))
            battleGroupTemplateByZone[zone.zoneId] = battleGroupTemplateLookup = new Dictionary<string, DHEventBattleGroupTemplate>();
        if (!battleUnitByZone.TryGetValue(zone.zoneId, out Dictionary<string, EnemyUnit1SectorData> battleUnitLookup))
            battleUnitByZone[zone.zoneId] = battleUnitLookup = new Dictionary<string, EnemyUnit1SectorData>();
        if (!battleUnitTemplateByZone.TryGetValue(zone.zoneId, out Dictionary<string, DHEventBattleUnitTemplate> battleUnitTemplateLookup))
            battleUnitTemplateByZone[zone.zoneId] = battleUnitTemplateLookup = new Dictionary<string, DHEventBattleUnitTemplate>();

        // 메인/서브를 같은 저장공간으로 통합
        LoadChatTable(zone.chatMainTable, chatLookup, chatTemplateLookup, label, "Chat/Main");
        LoadChatTable(zone.chatSubTable,  chatLookup, chatTemplateLookup, label, "Chat/Sub");
        LoadBranchTable(zone.branchMainTable, branchLookup, branchTemplateLookup, label, "Branch/Main");
        LoadBranchTable(zone.branchSubTable,  branchLookup, branchTemplateLookup, label, "Branch/Sub");
        LoadBattleGroupTable(zone.enemyBattleGroupTable, battleGroupLookup, battleGroupTemplateLookup, label, "Battle/EnemyGroup");
        LoadBattleUnitTable(zone.enemyBattleUnitTable, battleUnitLookup, battleUnitTemplateLookup, label, "Battle/EnemyUnit");

        // 그룹별로 Selection_Index 오름차순 정렬. ("1", "1A", "2" …)
        foreach (var options in branchLookup.Values)
        {
            options.Sort((a, b) => string.Compare(
                a?.Selection_Index, b?.Selection_Index, System.StringComparison.Ordinal));
        }
        foreach (var options in branchTemplateLookup.Values)
        {
            options.Sort((a, b) => string.Compare(
                a?.SelectionIndex, b?.SelectionIndex, System.StringComparison.Ordinal));
        }
    }

    private void LoadChatTable(ChatDBEventDataTable table,
                               Dictionary<int, ChatDBEventData> lookup,
                               Dictionary<int, DHEventChatTemplate> templateLookup,
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
            if (templateLookup != null)
                templateLookup[row.Chat_ID] = ConvertChatTemplate(row);
        }
    }

    private void LoadBranchTable(BranchDBEventDataTable table,
                                 Dictionary<int, List<BranchDBEventData>> lookup,
                                 Dictionary<int, List<DHEventBranchTemplate>> templateLookup,
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

            if (templateLookup != null)
            {
                if (!templateLookup.TryGetValue(row.Branch_ID, out List<DHEventBranchTemplate> templateOptions))
                    templateLookup[row.Branch_ID] = templateOptions = new List<DHEventBranchTemplate>();

                templateOptions.Add(ConvertBranchTemplate(row));
            }
        }
    }

    private static DHEventChatTemplate ConvertChatTemplate(ChatDBEventData row)
    {
        if (row == null)
            return null;

        return new DHEventChatTemplate(
            row.Chat_ID,
            row.Chat_Type,
            row.Char_Name,
            row.Char_Profile,
            row.Message_Text,
            row.IMG_Res,
            row.Next_Chat_ID,
            row.Branch_Group_ID);
    }

    private static DHEventBranchTemplate ConvertBranchTemplate(BranchDBEventData row)
    {
        if (row == null)
            return null;

        return new DHEventBranchTemplate(
            row.Branch_ID,
            row.Selection_Index,
            row.Trigger_Type,
            row.Trigger_Value,
            row.Selection_Text,
            row.Target_Talk_ID,
            row.Trigger_Effect,
            row.비고);
    }

    private void LoadBattleGroupTable(
                                      EnemyGroupDataTable table,
                                      Dictionary<string, EnemyGroupData> lookup,
                                      Dictionary<string, DHEventBattleGroupTemplate> templateLookup,
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

            DHEventBattleGroupTemplate template = ConvertBattleGroupTemplate(row, key, zoneLabel, source);
            if (template != null)
                templateLookup.Add(key, template);
        }
    }

    private void LoadBattleUnitTable(
                                     EnemyUnit1SectorDataTable table,
                                     Dictionary<string, EnemyUnit1SectorData> lookup,
                                     Dictionary<string, DHEventBattleUnitTemplate> templateLookup,
                                     string zoneLabel, string source)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            EnemyUnit1SectorData row = table.DataList[i];
            if (row == null) continue;

            AddBattleUnitAlias(lookup, templateLookup, row, row.EnemyIndex, zoneLabel, source);
            AddBattleUnitAlias(lookup, templateLookup, row, ExtractNumericBattleUnitKey(row.EnemyIndex), zoneLabel, source);
        }
    }

    private DHEventBattleGroupTemplate ConvertBattleGroupTemplate(
        EnemyGroupData row,
        string battleKey,
        string zoneLabel,
        string source)
    {
        if (row == null)
            return null;

        var members = new List<DHEventBattleGroupMember>();
        TryAddBattleGroupMember(members, row.Enemy1, row.Enemy1Slot);
        TryAddBattleGroupMember(members, row.Enemy2, row.Enemy2Slot);
        TryAddBattleGroupMember(members, row.Enemy3, row.Enemy3Slot);
        TryAddBattleGroupMember(members, row.Enemy4, row.Enemy4Slot);
        TryAddBattleGroupMember(members, row.Enemy5, row.Enemy5Slot);
        TryAddBattleGroupMember(members, row.Enemy6, row.Enemy6Slot);

        if (members.Count == 0)
        {
            Debug.LogWarning($"[EventScriptCatalog] {zoneLabel} battle group '{battleKey}' has no valid members({source}) skipped.", this);
            return null;
        }

        return new DHEventBattleGroupTemplate(
            battleKey,
            row.EnemyGroupName,
            row.MinLevel,
            row.MaxLevel,
            members);
    }

    private static void TryAddBattleGroupMember(List<DHEventBattleGroupMember> members, string unitKey, int combatSlot)
    {
        string normalizedUnitKey = NormalizeBattleUnitKey(unitKey);
        if (string.IsNullOrWhiteSpace(normalizedUnitKey) || combatSlot <= 0)
            return;

        members.Add(new DHEventBattleGroupMember(normalizedUnitKey, combatSlot));
    }

    private void LoadMainSubRewardTable(
                                        MainSubEventRewardDataTable table,
                                        Dictionary<int, MainSubEventRewardData> lookup,
                                        Dictionary<int, DHEventRewardTemplate> templateLookup,
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
            AddRewardTemplate(
                templateLookup,
                ConvertMainSubRewardTemplate(row),
                row.reward_id,
                zoneLabel,
                source);
        }
    }

    private void LoadWorldRewardTable(
                                      WorldEventRewardDataTable table,
                                      Dictionary<int, WorldEventRewardData> lookup,
                                      Dictionary<int, DHEventRewardTemplate> templateLookup,
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
            AddRewardTemplate(
                templateLookup,
                ConvertWorldRewardTemplate(row),
                row.reward_id,
                zoneLabel,
                source);
        }
    }

    private void AddRewardTemplate(
        Dictionary<int, DHEventRewardTemplate> lookup,
        DHEventRewardTemplate template,
        int rewardId,
        string zoneLabel,
        string source)
    {
        if (lookup == null || template == null || rewardId <= 0)
            return;

        if (lookup.ContainsKey(rewardId))
        {
            Debug.LogWarning($"[EventScriptCatalog] {zoneLabel} duplicate reward template id '{rewardId}'({source}) skipped.", this);
            return;
        }

        lookup.Add(rewardId, template);
    }

    private static DHEventRewardTemplate ConvertMainSubRewardTemplate(MainSubEventRewardData row)
    {
        if (row == null || row.reward_id <= 0)
            return null;

        return new DHEventRewardTemplate(
            row.reward_id,
            row.reward_name,
            DHEventRewardSourceType.MainSub,
            row.money,
            row.medal,
            row.gem,
            row.supply,
            row.EXP,
            BuildCharacterRewards(
                row.Rumina_MaxHP, row.Rumina_ATK, row.Rumina_DEF, row.Rumina_IP, row.Rumina_Heal,
                row.Justice_MaxHP, row.Justice_ATK, row.Justice_Def, row.Justice_IP, row.Justice_Heal,
                row.BlackBullet_MaxHP, row.BlackBullet_ATK, row.BlackBullet_Def, row.BlackBullet_IP, row.BlackBullet_Heal,
                row.Nekoming_MaxHP, row.Nekoming_ATK, row.Nekoming_Def, row.Nekoming_IP, row.Nekoming_Heal));
    }

    private static DHEventRewardTemplate ConvertWorldRewardTemplate(WorldEventRewardData row)
    {
        if (row == null || row.reward_id <= 0)
            return null;

        return new DHEventRewardTemplate(
            row.reward_id,
            string.Empty,
            DHEventRewardSourceType.World,
            row.money,
            row.medal,
            row.gem,
            0,
            0,
            BuildCharacterRewards(
                row.Rumina_MaxHP, row.Rumina_ATK, row.Rumina_DEF, row.Rumina_IP, row.Rumina_Heal,
                row.Justice_MaxHP, row.Justice_ATK, row.Justice_Def, row.Justice_IP, row.Justice_Heal,
                row.BlackBullet_MaxHP, row.BlackBullet_ATK, row.BlackBullet_Def, row.BlackBullet_IP, row.BlackBullet_Heal,
                row.Nekoming_MaxHP, row.Nekoming_ATK, row.Nekoming_Def, row.Nekoming_IP, row.Nekoming_Heal));
    }

    private static List<DHEventCharacterRewardEntry> BuildCharacterRewards(
        int ruminaMaxHp, int ruminaAtk, int ruminaDef, int ruminaIp, int ruminaHeal,
        int justiceMaxHp, int justiceAtk, int justiceDef, int justiceIp, int justiceHeal,
        int blackBulletMaxHp, int blackBulletAtk, int blackBulletDef, int blackBulletIp, int blackBulletHeal,
        int nekomingMaxHp, int nekomingAtk, int nekomingDef, int nekomingIp, int nekomingHeal)
    {
        var rewards = new List<DHEventCharacterRewardEntry>(4);
        AddCharacterReward(rewards, "10002", ruminaMaxHp, ruminaAtk, ruminaDef, ruminaIp, ruminaHeal);
        AddCharacterReward(rewards, "10001", justiceMaxHp, justiceAtk, justiceDef, justiceIp, justiceHeal);
        AddCharacterReward(rewards, "10003", blackBulletMaxHp, blackBulletAtk, blackBulletDef, blackBulletIp, blackBulletHeal);
        AddCharacterReward(rewards, "10004", nekomingMaxHp, nekomingAtk, nekomingDef, nekomingIp, nekomingHeal);
        return rewards;
    }

    private static void AddCharacterReward(
        List<DHEventCharacterRewardEntry> rewards,
        string unitTemplateKey,
        int maxHp,
        int atk,
        int def,
        int influence,
        int heal)
    {
        if (rewards == null)
            return;

        rewards.Add(new DHEventCharacterRewardEntry(unitTemplateKey, maxHp, atk, def, influence, heal));
    }

    private void AddBattleUnitAlias(
                                    Dictionary<string, EnemyUnit1SectorData> lookup,
                                    Dictionary<string, DHEventBattleUnitTemplate> templateLookup,
                                    EnemyUnit1SectorData row,
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

        DHEventBattleUnitTemplate template = ConvertBattleUnitTemplate(row, key);
        if (template != null)
            templateLookup.Add(key, template);
    }

    private static DHEventBattleUnitTemplate ConvertBattleUnitTemplate(EnemyUnit1SectorData row, string unitKey)
    {
        if (row == null)
            return null;

        var skills = new List<DHEventBattleSkillTemplate>();
        TryAddBattleSkillTemplate(
            skills, 1,
            row.EnemySkill1_Name, row.EnemySkill1_Description,
            row.EnemySkill1Effect, row.EnemySkill1Range, row.EnemySkill1RangeLine,
            row.EnemySkill1Target, row.EnemySkill1MultiTarget,
            row.EnemySkill1_MultiTargetType, row.EnemySkill1_MultiTargetCount,
            row.EnemySkill1Value, row.EnemySkill1SubValue);
        TryAddBattleSkillTemplate(
            skills, 2,
            row.EnemySkill2_Name, row.EnemySkill2_Description,
            row.EnemySkill2Effect, row.EnemySkill2Range, row.EnemySkill2RangeLine,
            row.EnemySkill2Target, row.EnemySkill2MultiTarget,
            row.EnemySkill2_MultiTargetType, row.EnemySkill2_MultiTargetCount,
            row.EnemySkill2Value, row.EnemySkill2SubValue);
        TryAddBattleSkillTemplate(
            skills, 3,
            row.EnemySkill3_Name, row.EnemySkill3_Description,
            row.EnemySkill3Effect, row.EnemySkill3Range, row.EnemySkill3RangeLine,
            row.EnemySkill3Target, row.EnemySkill3MultiTarget,
            row.EnemySkill3_MultiTargetType, row.EnemySkill3_MultiTargetCount,
            row.EnemySkill3Value, row.EnemySkill3SubValue);

        return new DHEventBattleUnitTemplate(
            unitKey,
            row.EnemyName,
            row.EnemyConcept,
            row.UnitMaxHP,
            row.UnitATK,
            row.UnitDEF,
            row.CriticalRate,
            row.CounterRate,
            row.ReduceRate,
            row.Speed,
            row.UnitAI,
            row.ExperiencePoint,
            row.LevelGrowthExperiencePoint,
            row.LevelGrowthMaxHP,
            row.LevelGrowthAtk,
            row.LevelGrowthDef,
            skills);
    }

    private static void TryAddBattleSkillTemplate(
        List<DHEventBattleSkillTemplate> skills,
        int slot,
        string skillName,
        string description,
        int effect,
        int range,
        int rangeLine,
        int target,
        IEnumerable<int> boundary,
        int multiTargetType,
        int multiTargetCount,
        float value,
        float subValue)
    {
        if (skills == null || string.IsNullOrWhiteSpace(skillName))
            return;

        skills.Add(new DHEventBattleSkillTemplate(
            slot,
            skillName,
            description,
            effect,
            range,
            rangeLine,
            target,
            boundary,
            multiTargetType,
            multiTargetCount,
            value,
            subValue));
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
        chatTemplateByZone.Clear();
        branchByZone.Clear();
        branchTemplateByZone.Clear();
        battleGroupByZone.Clear();
        battleGroupTemplateByZone.Clear();
        battleUnitByZone.Clear();
        battleUnitTemplateByZone.Clear();
        mainSubRewardLookup.Clear();
        worldRewardLookup.Clear();
        rewardTemplateLookup.Clear();
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
