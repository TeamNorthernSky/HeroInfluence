using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DHWorldEventCatalog : MonoBehaviour
{
    public static DHWorldEventCatalog Instance { get; private set; }

    // 이벤트 데이터 테이블 V1.6. 소비형 결과는 시트 구조가 V1.3과 같아 ConsumeResultDataTable을 그대로 씁니다.
    [Header("Consume Event Tables (V1.6)")]
    [SerializeField] private ConsumeNPC1DataTable consumeNpcTableV16;
    [SerializeField] private ConsumeCondition1DataTable consumeConditionTableV16;
    [SerializeField] private ConsumeResultDataTable consumeResultTable;

    [Header("Reward Event Tables (V1.6)")]
    [SerializeField] private RewardNPC1DataTable rewardNpcTableV16;
    [SerializeField] private RewardCondition1DataTable rewardConditionTableV16;

    [Header("Choice Event Tables (V1.6)")]
    [SerializeField] private ChoiceNPC1DataTable choiceNpcTableV16;
    [SerializeField] private ChoiceCondition1DataTable choiceConditionTableV16;
    [SerializeField] private ChoiceOption1DataTable choiceOptionTableV16;
    [SerializeField] private ChoiceResult1DataTable choiceResultTableV16;

    [Header("Settings")]
    [SerializeField] private bool loadOnAwake = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    private readonly Dictionary<string, DHWorldEventTemplate> eventLookup = new Dictionary<string, DHWorldEventTemplate>();
    private readonly Dictionary<int, List<DHWorldEventTemplate>> eventsByZone = new Dictionary<int, List<DHWorldEventTemplate>>();
    private readonly Dictionary<string, List<DHWorldEventChoiceTemplate>> choicesByEventId = new Dictionary<string, List<DHWorldEventChoiceTemplate>>();
    private readonly Dictionary<string, List<DHWorldEventResultTemplate>> choiceResultsByGroupId = new Dictionary<string, List<DHWorldEventResultTemplate>>();
    private readonly Dictionary<string, List<DHWorldEventResultTemplate>> consumeResultsByEventAndResultId = new Dictionary<string, List<DHWorldEventResultTemplate>>();

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

        if (Application.isPlaying)
            DHWorldEventConditionRuntimeManager.EnsureInstance();
    }

    [ContextMenu("Reload")]
    public void Reload()
    {
        ClearCache();

        LoadChoiceOptions(choiceOptionTableV16);
        LoadChoiceResults(choiceResultTableV16);
        LoadConsumeResults(consumeResultTable);

        LoadConsumeNpcEvents(consumeNpcTableV16);
        LoadConsumeConditionEvents(consumeConditionTableV16);
        LoadRewardNpcEvents(rewardNpcTableV16);
        LoadRewardConditionEvents(rewardConditionTableV16);
        LoadChoiceNpcEvents(choiceNpcTableV16);
        LoadChoiceConditionEvents(choiceConditionTableV16);

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
            string resultKey = BuildConsumeResultKey(row.world_event_id, row.result_id);
            if (string.IsNullOrWhiteSpace(resultId) || string.IsNullOrWhiteSpace(resultKey))
                continue;

            if (!consumeResultsByEventAndResultId.TryGetValue(resultKey, out List<DHWorldEventResultTemplate> list))
                consumeResultsByEventAndResultId[resultKey] = list = new List<DHWorldEventResultTemplate>();

            AddConsumeEffect(list, DHWorldEventResultKind.ResourceCost, resultId, 1, row.cost_resource_type_1, row.cost_resource_amount_1, row.note);
            AddConsumeEffect(list, DHWorldEventResultKind.ResourceCost, resultId, 2, row.cost_resource_type_2, row.cost_resource_amount_2, row.note);
            AddConsumeEffect(list, DHWorldEventResultKind.StatusEffect, resultId, 3, row.status_type_1, row.status_amount_per_event_1, row.note);
            AddConsumeEffect(list, DHWorldEventResultKind.StatusEffect, resultId, 4, row.status_type_2, row.status_amount_per_event_2, row.note);
        }
    }

    // ─── V1.6 로더 ───────────────────────────────────────────────────────
    // V1.6 시트 변경점:
    //  - 조건형 3종: zone_no 삭제 → 0 (조건 이벤트는 GetAllEvents로 조회되어 구역 무관)
    //  - 보상형/선택형 NPC형: world_event_name 삭제 → 빈 문자열 (UI 기본 제목 사용)
    //  - 보상형: world_event_accept 추가, 선택형: world_event_cancel/decline 추가
    //  - 선택지: success/failure_result_group_id → success/failure_choice_result_id 이름 변경
    //  - 선택결과: effect_order 삭제 → 그룹 내 행 순서, world_event_accept 추가(미사용)

    private void LoadConsumeNpcEvents(ConsumeNPC1DataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ConsumeNPC1Data row = table.DataList[i];
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
                GetConsumeResults(row.world_event_id, row.result_id)));
        }
    }

    private void LoadConsumeConditionEvents(ConsumeCondition1DataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ConsumeCondition1Data row = table.DataList[i];
            if (row == null)
                continue;

            string eventId = NormalizeKey(row.world_event_id);
            AddEvent(new DHWorldEventTemplate(
                eventId,
                0,
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
                GetConsumeResults(row.world_event_id, row.result_id)));
        }
    }

    private void LoadRewardNpcEvents(RewardNPC1DataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            RewardNPC1Data row = table.DataList[i];
            if (row == null)
                continue;

            AddEvent(new DHWorldEventTemplate(
                row.world_event_id,
                row.zone_no,
                DHWorldEventType.Reward,
                DHWorldEventSourceType.Npc,
                string.Empty,
                row.npc_type,
                row.world_event_description,
                row.world_event_accept,
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

    private void LoadRewardConditionEvents(RewardCondition1DataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            RewardCondition1Data row = table.DataList[i];
            if (row == null)
                continue;

            AddEvent(new DHWorldEventTemplate(
                row.world_event_id,
                0,
                DHWorldEventType.Reward,
                DHWorldEventSourceType.Condition,
                row.world_event_name,
                row.npc_type,
                row.world_event_description,
                row.world_event_accept,
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

    private void LoadChoiceNpcEvents(ChoiceNPC1DataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ChoiceNPC1Data row = table.DataList[i];
            if (row == null)
                continue;

            AddEvent(new DHWorldEventTemplate(
                row.world_event_id,
                row.zone_no,
                DHWorldEventType.Choice,
                DHWorldEventSourceType.Npc,
                string.Empty,
                row.npc_type,
                row.world_event_description,
                string.Empty,
                row.world_event_cancel,
                string.Empty,
                row.world_event_decline,
                string.Empty,
                row.note,
                null,
                GetChoiceList(row.world_event_id, row.choice_1_id, row.choice_2_id)));
        }
    }

    private void LoadChoiceConditionEvents(ChoiceCondition1DataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ChoiceCondition1Data row = table.DataList[i];
            if (row == null)
                continue;

            AddEvent(new DHWorldEventTemplate(
                row.world_event_id,
                0,
                DHWorldEventType.Choice,
                DHWorldEventSourceType.Condition,
                row.world_event_name,
                row.npc_type,
                row.world_event_description,
                string.Empty,
                row.world_event_cancel,
                string.Empty,
                row.world_event_decline,
                string.Empty,
                row.note,
                new[] { BuildCondition(row.trigger_condition_type, row.trigger_condition_target, row.trigger_condition_stat_type, row.trigger_condition_calculation_type, row.trigger_condition_operator.ToString(), row.trigger_condition_value) },
                GetChoiceList(row.world_event_id, row.choice_1_id, row.choice_2_id)));
        }
    }

    private void LoadChoiceOptions(ChoiceOption1DataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ChoiceOption1Data row = table.DataList[i];
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
                row.success_choice_result_id,
                row.failure_choice_result_id,
                row.note));
        }
    }

    private void LoadChoiceResults(ChoiceResult1DataTable table)
    {
        if (table == null)
            return;

        for (int i = 0; i < table.DataList.Count; i++)
        {
            ChoiceResult1Data row = table.DataList[i];
            if (row == null)
                continue;

            string resultGroupId = NormalizeKey(row.result_group_id);
            if (string.IsNullOrWhiteSpace(resultGroupId))
                continue;

            if (!choiceResultsByGroupId.TryGetValue(resultGroupId, out List<DHWorldEventResultTemplate> list))
                choiceResultsByGroupId[resultGroupId] = list = new List<DHWorldEventResultTemplate>();

            // effect_order 컬럼이 없어졌으므로 그룹 내 행 순서(1부터)를 order로 사용합니다.
            list.Add(new DHWorldEventResultTemplate(
                DHWorldEventResultKind.ChoiceEffect,
                resultGroupId,
                list.Count + 1,
                row.target_scope,
                row.effect_type,
                row.effect_amount,
                row.note));
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

    private IReadOnlyList<DHWorldEventResultTemplate> GetConsumeResults(string worldEventId, string resultId)
    {
        string key = BuildConsumeResultKey(worldEventId, resultId);
        return consumeResultsByEventAndResultId.TryGetValue(key, out List<DHWorldEventResultTemplate> list)
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
        if (rewards == null || amount == 0)
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
        if (results == null || amount == 0)
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
        consumeResultsByEventAndResultId.Clear();
        isLoaded = false;
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string BuildConsumeResultKey(string worldEventId, string resultId)
    {
        string eventKey = NormalizeKey(worldEventId);
        string resultKey = NormalizeKey(resultId);
        return string.IsNullOrWhiteSpace(eventKey) || string.IsNullOrWhiteSpace(resultKey)
            ? string.Empty
            : $"{eventKey}::{resultKey}";
    }
}
