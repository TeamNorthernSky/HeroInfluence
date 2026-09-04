using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DHWorldEventCatalog : MonoBehaviour
{
    public static DHWorldEventCatalog Instance { get; private set; }

    [Header("Consume Event Tables")]
    [SerializeField] private ConsumeNPCDataTable consumeNpcTable;
    [SerializeField] private ConsumeConditionDataTable consumeConditionTable;
    [SerializeField] private ConsumeResultDataTable consumeResultTable;

    [Header("Reward Event Tables")]
    [SerializeField] private RewardNPCDataTable rewardNpcTable;
    [SerializeField] private RewardConditionDataTable rewardConditionTable;

    [Header("Choice Event Tables")]
    [SerializeField] private ChoiceNPCDataTable choiceNpcTable;
    [SerializeField] private ChoiceConditionDataTable choiceConditionTable;
    [SerializeField] private ChoiceOptionDataTable choiceOptionTable;
    [SerializeField] private ChoiceResultDataTable choiceResultTable;

    [Header("Settings")]
    [SerializeField] private bool loadOnAwake = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    private readonly Dictionary<string, DHWorldEventTemplate> eventLookup = new Dictionary<string, DHWorldEventTemplate>();
    private readonly Dictionary<int, List<DHWorldEventTemplate>> eventsByZone = new Dictionary<int, List<DHWorldEventTemplate>>();
    private readonly Dictionary<string, List<DHWorldEventChoiceTemplate>> choicesByEventId = new Dictionary<string, List<DHWorldEventChoiceTemplate>>();
    private readonly Dictionary<string, List<DHWorldEventResultTemplate>> choiceResultsByGroupId = new Dictionary<string, List<DHWorldEventResultTemplate>>();
    private readonly Dictionary<string, List<DHWorldEventResultTemplate>> consumeResultsByResultId = new Dictionary<string, List<DHWorldEventResultTemplate>>();

    private bool isLoaded;

    public bool IsLoaded => isLoaded;

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

    [ContextMenu("Reload")]
    public void Reload()
    {
        ClearCache();

        LoadChoiceOptions(choiceOptionTable);
        LoadChoiceResults(choiceResultTable);
        LoadConsumeResults(consumeResultTable);

        LoadConsumeNpcEvents(consumeNpcTable);
        LoadConsumeConditionEvents(consumeConditionTable);
        LoadRewardNpcEvents(rewardNpcTable);
        LoadRewardConditionEvents(rewardConditionTable);
        LoadChoiceNpcEvents(choiceNpcTable);
        LoadChoiceConditionEvents(choiceConditionTable);

        isLoaded = true;
        Debug.Log($"[DHWorldEventCatalog] Loaded events={eventLookup.Count}, zones={eventsByZone.Count}, choiceResults={choiceResultsByGroupId.Count}.", this);
    }

    public bool TryGetEvent(string worldEventId, out DHWorldEventTemplate template)
    {
        EnsureLoaded();
        template = null;

        string key = NormalizeKey(worldEventId);
        return !string.IsNullOrWhiteSpace(key) && eventLookup.TryGetValue(key, out template);
    }

    public IReadOnlyList<DHWorldEventTemplate> GetEventsByZone(int zoneNo)
    {
        EnsureLoaded();
        return eventsByZone.TryGetValue(zoneNo, out List<DHWorldEventTemplate> list)
            ? list
            : new List<DHWorldEventTemplate>();
    }

    public IReadOnlyList<DHWorldEventTemplate> GetAllEvents()
    {
        EnsureLoaded();
        return new List<DHWorldEventTemplate>(eventLookup.Values);
    }

    public bool TryGetChoiceResults(string resultGroupId, out IReadOnlyList<DHWorldEventResultTemplate> results)
    {
        EnsureLoaded();
        results = null;

        string key = NormalizeKey(resultGroupId);
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (!choiceResultsByGroupId.TryGetValue(key, out List<DHWorldEventResultTemplate> list))
            return false;

        results = list;
        return true;
    }

    private void LoadConsumeNpcEvents(ConsumeNPCDataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ConsumeNPCData row = table.DataList[i];
            if (row == null)
                continue;

            string eventId = NormalizeKey(row.world_event_id);
            AddEvent(new DHWorldEventTemplate(
                eventId,
                row.zone_no,
                DHWorldEventType.Consume,
                DHWorldEventSourceType.Npc,
                string.Empty,
                row.npc_type,
                row.world_event_description,
                row.world_event_accept,
                row.world_event_cancel,
                row.world_event_proceed,
                row.world_event_decline,
                row.result_id,
                row.note,
                null,
                null,
                GetConsumeResults(row.result_id)));
        }
    }

    private void LoadConsumeConditionEvents(ConsumeConditionDataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ConsumeConditionData row = table.DataList[i];
            if (row == null)
                continue;

            string eventId = NormalizeKey(row.world_event_id);
            AddEvent(new DHWorldEventTemplate(
                eventId,
                row.zone_no,
                DHWorldEventType.Consume,
                DHWorldEventSourceType.Condition,
                row.world_event_name,
                row.npc_type,
                row.world_event_description,
                row.world_event_accept,
                row.world_event_cancel,
                row.world_event_proceed,
                row.world_event_decline,
                row.result_id,
                row.note,
                new[] { BuildCondition(row.trigger_condition_type, row.trigger_condition_target, row.trigger_condition_stat_type, row.trigger_condition_calculation_type, row.trigger_condition_operator.ToString(), row.trigger_condition_value) },
                null,
                GetConsumeResults(row.result_id)));
        }
    }

    private void LoadRewardNpcEvents(RewardNPCDataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            RewardNPCData row = table.DataList[i];
            if (row == null)
                continue;

            AddEvent(new DHWorldEventTemplate(
                row.world_event_id,
                row.zone_no,
                DHWorldEventType.Reward,
                DHWorldEventSourceType.Npc,
                row.world_event_name,
                row.npc_type,
                row.world_event_description,
                string.Empty,
                string.Empty,
                row.world_event_proceed,
                string.Empty,
                string.Empty,
                row.note,
                null,
                null,
                null,
                BuildRewardEntries(row.reward_type_1, row.reward_amount_1, row.reward_type_2, row.reward_amount_2)));
        }
    }

    private void LoadRewardConditionEvents(RewardConditionDataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            RewardConditionData row = table.DataList[i];
            if (row == null)
                continue;

            AddEvent(new DHWorldEventTemplate(
                row.world_event_id,
                row.zone_no,
                DHWorldEventType.Reward,
                DHWorldEventSourceType.Condition,
                row.world_event_name,
                row.npc_type,
                row.world_event_description,
                string.Empty,
                string.Empty,
                row.world_event_proceed,
                string.Empty,
                string.Empty,
                row.note,
                new[] { BuildCondition(row.trigger_condition_type, row.trigger_condition_target, row.trigger_condition_stat_type, row.trigger_condition_calculation_type, row.trigger_condition_operator.ToString(), row.trigger_condition_value) },
                null,
                null,
                BuildRewardEntries(row.reward_type_1, row.reward_amount_1, row.reward_type_2, row.reward_amount_2)));
        }
    }

    private void LoadChoiceNpcEvents(ChoiceNPCDataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ChoiceNPCData row = table.DataList[i];
            if (row == null)
                continue;

            AddEvent(new DHWorldEventTemplate(
                row.world_event_id,
                row.zone_no,
                DHWorldEventType.Choice,
                DHWorldEventSourceType.Npc,
                row.world_event_name,
                row.npc_type,
                row.world_event_description,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                row.note,
                null,
                GetChoiceList(row.world_event_id, row.choice_1_id, row.choice_2_id)));
        }
    }

    private void LoadChoiceConditionEvents(ChoiceConditionDataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ChoiceConditionData row = table.DataList[i];
            if (row == null)
                continue;

            AddEvent(new DHWorldEventTemplate(
                row.world_event_id,
                row.zone_no,
                DHWorldEventType.Choice,
                DHWorldEventSourceType.Condition,
                row.world_event_name,
                row.npc_type,
                row.world_event_description,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                row.note,
                new[] { BuildCondition(row.trigger_condition_type, row.trigger_condition_target, row.trigger_condition_stat_type, row.trigger_condition_calculation_type, row.trigger_condition_operator.ToString(), row.trigger_condition_value) },
                GetChoiceList(row.world_event_id, row.choice_1_id, row.choice_2_id)));
        }
    }

    private void LoadChoiceOptions(ChoiceOptionDataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ChoiceOptionData row = table.DataList[i];
            if (row == null)
                continue;

            string eventId = NormalizeKey(row.world_event_id);
            if (string.IsNullOrWhiteSpace(eventId))
                continue;

            if (!choicesByEventId.TryGetValue(eventId, out List<DHWorldEventChoiceTemplate> list))
                choicesByEventId[eventId] = list = new List<DHWorldEventChoiceTemplate>();

            list.Add(new DHWorldEventChoiceTemplate(
                row.choice_id,
                row.choice_text,
                BuildCondition(row.enable_condition_type, row.enable_condition_target, row.enable_condition_unit, row.enable_condition_calculation_type, row.enable_condition_operator.ToString(), row.enable_condition_value),
                row.use_ip_success_rate_bonus,
                row.base_success_rate,
                row.success_result_group_id,
                row.failure_result_group_id,
                row.note));
        }
    }

    private void LoadChoiceResults(ChoiceResultDataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ChoiceResultData row = table.DataList[i];
            if (row == null)
                continue;

            string resultGroupId = NormalizeKey(row.result_group_id);
            if (string.IsNullOrWhiteSpace(resultGroupId))
                continue;

            if (!choiceResultsByGroupId.TryGetValue(resultGroupId, out List<DHWorldEventResultTemplate> list))
                choiceResultsByGroupId[resultGroupId] = list = new List<DHWorldEventResultTemplate>();

            list.Add(new DHWorldEventResultTemplate(
                DHWorldEventResultKind.ChoiceEffect,
                resultGroupId,
                row.effect_order,
                row.target_scope,
                row.effect_type,
                row.effect_amount,
                row.note));
        }

        foreach (List<DHWorldEventResultTemplate> list in choiceResultsByGroupId.Values)
            list.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    private void LoadConsumeResults(ConsumeResultDataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ConsumeResultData row = table.DataList[i];
            if (row == null)
                continue;

            string resultId = NormalizeKey(row.result_id);
            if (string.IsNullOrWhiteSpace(resultId))
                continue;

            if (!consumeResultsByResultId.TryGetValue(resultId, out List<DHWorldEventResultTemplate> list))
                consumeResultsByResultId[resultId] = list = new List<DHWorldEventResultTemplate>();

            AddConsumeEffect(list, DHWorldEventResultKind.ResourceCost, resultId, 1, row.cost_resource_type_1, row.cost_resource_amount_1, row.note);
            AddConsumeEffect(list, DHWorldEventResultKind.ResourceCost, resultId, 2, row.cost_resource_type_2, row.cost_resource_amount_2, row.note);
            AddConsumeEffect(list, DHWorldEventResultKind.StatusEffect, resultId, 3, row.status_type_1, row.status_amount_per_event_1, row.note);
            AddConsumeEffect(list, DHWorldEventResultKind.StatusEffect, resultId, 4, row.status_type_2, row.status_amount_per_event_2, row.note);
        }
    }

    private void AddEvent(DHWorldEventTemplate template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.WorldEventId))
            return;

        if (eventLookup.ContainsKey(template.WorldEventId))
        {
            Debug.LogWarning($"[DHWorldEventCatalog] Duplicate world event id '{template.WorldEventId}' skipped.", this);
            return;
        }

        DHWorldEventTemplate linkedTemplate = LinkChoiceResults(template);
        eventLookup.Add(linkedTemplate.WorldEventId, linkedTemplate);

        if (!eventsByZone.TryGetValue(linkedTemplate.ZoneNo, out List<DHWorldEventTemplate> list))
            eventsByZone[linkedTemplate.ZoneNo] = list = new List<DHWorldEventTemplate>();

        list.Add(linkedTemplate);
    }

    private DHWorldEventTemplate LinkChoiceResults(DHWorldEventTemplate template)
    {
        if (template == null || template.EventType != DHWorldEventType.Choice || template.Choices.Count == 0)
            return template;

        var results = new List<DHWorldEventResultTemplate>();
        for (int i = 0; i < template.Choices.Count; i++)
        {
            DHWorldEventChoiceTemplate choice = template.Choices[i];
            AppendChoiceResults(results, choice.SuccessResultGroupId);
            AppendChoiceResults(results, choice.FailureResultGroupId);
        }

        return template.WithResults(results);
    }

    private void AppendChoiceResults(List<DHWorldEventResultTemplate> results, string resultGroupId)
    {
        if (results == null || string.IsNullOrWhiteSpace(resultGroupId))
            return;

        if (!choiceResultsByGroupId.TryGetValue(NormalizeKey(resultGroupId), out List<DHWorldEventResultTemplate> list))
            return;

        results.AddRange(list);
    }

    private IReadOnlyList<DHWorldEventChoiceTemplate> GetChoiceList(string eventId, string firstChoiceId, string secondChoiceId)
    {
        string normalizedEventId = NormalizeKey(eventId);
        if (!choicesByEventId.TryGetValue(normalizedEventId, out List<DHWorldEventChoiceTemplate> source))
            return new List<DHWorldEventChoiceTemplate>();

        var result = new List<DHWorldEventChoiceTemplate>();
        TryAddChoiceById(result, source, firstChoiceId);
        TryAddChoiceById(result, source, secondChoiceId);

        if (result.Count == 0)
            result.AddRange(source);

        return result;
    }

    private IReadOnlyList<DHWorldEventResultTemplate> GetConsumeResults(string resultId)
    {
        string key = NormalizeKey(resultId);
        return consumeResultsByResultId.TryGetValue(key, out List<DHWorldEventResultTemplate> list)
            ? list
            : new List<DHWorldEventResultTemplate>();
    }

    private static void TryAddChoiceById(
        List<DHWorldEventChoiceTemplate> target,
        List<DHWorldEventChoiceTemplate> source,
        string choiceId)
    {
        string key = NormalizeKey(choiceId);
        if (string.IsNullOrWhiteSpace(key))
            return;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null && source[i].ChoiceId == key)
            {
                target.Add(source[i]);
                return;
            }
        }
    }

    private static List<DHWorldEventRewardEntry> BuildRewardEntries(
        int rewardType1,
        int rewardAmount1,
        int rewardType2,
        int rewardAmount2)
    {
        var rewards = new List<DHWorldEventRewardEntry>();
        AddRewardEntry(rewards, rewardType1, rewardAmount1);
        AddRewardEntry(rewards, rewardType2, rewardAmount2);
        return rewards;
    }

    private static void AddRewardEntry(List<DHWorldEventRewardEntry> rewards, int rewardType, int amount)
    {
        if (rewards == null || rewardType == 0 || amount == 0)
            return;

        rewards.Add(new DHWorldEventRewardEntry(rewardType, amount));
    }

    private static void AddConsumeEffect(
        List<DHWorldEventResultTemplate> results,
        DHWorldEventResultKind resultKind,
        string resultId,
        int order,
        int effectType,
        int amount,
        string note)
    {
        if (results == null || effectType == 0 || amount == 0)
            return;

        results.Add(new DHWorldEventResultTemplate(resultKind, resultId, order, 0, effectType, amount, note));
    }

    private static DHWorldEventConditionTemplate BuildCondition(
        int conditionType,
        int target,
        int statType,
        int calculationType,
        string operatorText,
        int value)
    {
        return new DHWorldEventConditionTemplate(
            conditionType,
            target,
            statType,
            calculationType,
            operatorText,
            value);
    }

    private void EnsureLoaded()
    {
        if (!isLoaded)
            Reload();
    }

    private void ClearCache()
    {
        eventLookup.Clear();
        eventsByZone.Clear();
        choicesByEventId.Clear();
        choiceResultsByGroupId.Clear();
        consumeResultsByResultId.Clear();
        isLoaded = false;
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
