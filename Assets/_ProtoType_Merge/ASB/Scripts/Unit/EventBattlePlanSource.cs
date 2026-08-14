using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이벤트 전투 유닛/시나리오를 (zoneId, battleKey, level) 키로 빌드하는 공용 소스.
/// EventScriptCatalog(런타임 카탈로그) / BattleScenarioCatalog(Resources)만 참조 → 씬 의존이 없다.
/// DH 탐험씬(과도기)과 전투씬(EnemySpawnPlanBuilder)이 동일한 빌드 공식을 재사용한다.
///
/// failFast 규약(§4):
/// - false: 유효하지 않은 멤버/인질은 건너뛰고 계속(기존 DHEventBattleRuntimeManager 동작 보존).
/// - true : 멤버/키/슬롯/인질 중 하나라도 무효면 즉시 false + error를 돌려주고 부분 결과를 남기지 않는다.
/// </summary>
public static class EventBattlePlanSource
{
    /// <summary>
    /// (zoneId, battleKey, level)로 이벤트 전투 유닛 목록을 빌드한다.
    /// failFast=false는 기존 DH 빌드와 동일(무효 멤버 skip). failFast=true는 부분 결과 금지.
    /// </summary>
    public static bool TryBuildUnits(
        int zoneId,
        string battleKey,
        int enemyLevel,
        bool failFast,
        out List<CombatEventBattleUnitData> units,
        out string error)
    {
        units = new List<CombatEventBattleUnitData>();
        error = string.Empty;

        string normalizedBattleKey = string.IsNullOrWhiteSpace(battleKey) ? string.Empty : battleKey.Trim();
        if (string.IsNullOrEmpty(normalizedBattleKey))
        {
            error = "BattleKey is empty.";
            return false;
        }

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null)
        {
            error = "EventScriptCatalog is not available.";
            return false;
        }

        if (!catalog.TryGetBattleEnemyGroupTemplate(zoneId, normalizedBattleKey, out DHEventBattleGroupTemplate group) ||
            group == null)
        {
            error = $"Battle enemy group was not found. Zone={zoneId}, BattleKey={normalizedBattleKey}";
            return false;
        }

        if (group.Members == null || group.Members.Count == 0)
        {
            error = $"Battle enemy group has no members. Zone={zoneId}, BattleKey={normalizedBattleKey}";
            return false;
        }

        int safeLevel = Mathf.Max(1, enemyLevel);
        var claimedSlots = new HashSet<int>();

        for (int i = 0; i < group.Members.Count; i++)
        {
            DHEventBattleGroupMember member = group.Members[i];
            string unitKey = member.UnitKey;

            if (string.IsNullOrWhiteSpace(unitKey))
            {
                if (failFast)
                {
                    error = $"Empty UnitKey at member index {i}. Zone={zoneId}, BattleKey={normalizedBattleKey}";
                    units.Clear();
                    return false;
                }
                continue;
            }

            string normalizedUnitKey = unitKey.Trim();
            if (!catalog.TryGetBattleEnemyUnitTemplate(zoneId, normalizedUnitKey, out DHEventBattleUnitTemplate source) ||
                source == null)
            {
                if (failFast)
                {
                    error = $"Battle enemy unit was not found. Zone={zoneId}, UnitKey={normalizedUnitKey}";
                    units.Clear();
                    return false;
                }

                Debug.LogWarning(
                    $"[EventBattlePlanSource] Battle enemy unit was not found. Zone: {zoneId}, UnitKey: {normalizedUnitKey}");
                continue;
            }

            int resolvedSlot = member.CombatSlot > 0 ? member.CombatSlot : units.Count + 1;

            if (failFast && !claimedSlots.Add(resolvedSlot))
            {
                error =
                    $"Duplicate combat slot {resolvedSlot}. Zone={zoneId}, BattleKey={normalizedBattleKey}, UnitKey={normalizedUnitKey}";
                units.Clear();
                return false;
            }

            CombatEventBattleUnitData unit = new CombatEventBattleUnitData(normalizedUnitKey, resolvedSlot, safeLevel, source);
            ApplyLevelGrowth(unit, source, safeLevel);
            PopulateEventBattleSkills(unit, source);
            units.Add(unit);
        }

        if (units.Count == 0)
        {
            error = $"Battle group produced no valid enemy units. Zone={zoneId}, BattleKey={normalizedBattleKey}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// (zoneId, battleKey)로 인질 시나리오를 빌드한다. 시나리오가 정의되지 않은 전투는 scenario=null + true(정상).
    /// failFast=true일 때 시나리오가 정의돼 있는데 인질 구성이 무효면 false(부분 인질 허용 안 함, §4).
    /// </summary>
    public static bool TryBuildScenario(
        int zoneId,
        string battleKey,
        IReadOnlyList<CombatEventBattleUnitData> units,
        bool failFast,
        out BattleScenarioConfig scenario,
        out string error)
    {
        scenario = null;
        error = string.Empty;

        BattleScenarioCatalog scenarioCatalog = BattleScenarioCatalog.LoadDefault();
        if (scenarioCatalog == null ||
            !scenarioCatalog.TryGetHostageScenario(zoneId, battleKey, out HostageScenarioDefinition definition) ||
            definition == null)
        {
            // 시나리오 미정의 = 일반 이벤트 전투(정상). scenario=null.
            return true;
        }

        var hostageConfig = new HostageScenarioConfig
        {
            FullHealthAggroGain = Mathf.Max(0f, definition.FullHealthAggroGain),
            DamagedAggroGain = Mathf.Max(0f, definition.DamagedAggroGain),
            ThreatDamage = Mathf.Max(0f, definition.ThreatDamage),
            ThreatAggroReduction = Mathf.Max(0f, definition.ThreatAggroReduction),
            ThreatChancePerTotalAggro = Mathf.Max(0f, definition.ThreatChancePerTotalAggro),
            MaximumThreatChance = Mathf.Clamp01(definition.MaximumThreatChance),
            ThreatWindupSeconds = Mathf.Max(0f, definition.ThreatWindupSeconds),
            ThreatRecoverySeconds = Mathf.Max(0f, definition.ThreatRecoverySeconds)
        };

        if (definition.ThreatEnemyUnitKeys != null)
            hostageConfig.ThreatEnemyUnitKeys.AddRange(definition.ThreatEnemyUnitKeys);

        float initialHpRatio = Mathf.Clamp01(definition.InitialHpRatio);
        if (definition.Hostages != null)
        {
            for (int i = 0; i < definition.Hostages.Count; i++)
            {
                HostageDefinitionEntry entry = definition.Hostages[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.SourceUnitKey))
                {
                    if (failFast)
                    {
                        error = $"Invalid hostage entry at index {i}. Battle={battleKey}";
                        scenario = null;
                        return false;
                    }
                    continue;
                }

                CombatEventBattleUnitData sourceUnit = FindEventBattleUnit(units, entry.SourceUnitKey);
                if (sourceUnit == null)
                {
                    if (failFast)
                    {
                        error =
                            $"Hostage source unit was not found in battle group. Battle={battleKey}, Unit={entry.SourceUnitKey}";
                        scenario = null;
                        return false;
                    }

                    Debug.LogWarning(
                        $"[EventBattlePlanSource] Hostage unit was not found in battle group. Battle={battleKey}, Unit={entry.SourceUnitKey}");
                    continue;
                }

                float maxHp = Mathf.Max(1f, sourceUnit.MaxHp);
                hostageConfig.Hostages.Add(new HostageSpawnConfig
                {
                    HostageId = string.IsNullOrWhiteSpace(entry.HostageId) ? entry.SourceUnitKey.Trim() : entry.HostageId.Trim(),
                    SourceUnitKey = sourceUnit.UnitKey,
                    Slot = sourceUnit.Slot,
                    MaxHp = maxHp,
                    InitialHp = Mathf.Max(1f, maxHp * initialHpRatio),
                    InitialAggro = 0f,
                    PrefabResourcePath = entry.PrefabResourcePath
                });
            }
        }

        if (hostageConfig.Hostages.Count == 0)
        {
            if (failFast)
            {
                error = $"Hostage scenario has no valid hostages. Battle={battleKey}";
                scenario = null;
                return false;
            }

            Debug.LogWarning($"[EventBattlePlanSource] Hostage scenario has no valid hostages. Battle={battleKey}");
            return true; // 기존 DH 동작: scenario=null로 두고 실패로 취급하지 않음.
        }

        scenario = new BattleScenarioConfig
        {
            ScenarioType = BattleScenarioType.HostageRescue,
            HostageRescue = hostageConfig
        };
        return true;
    }

    private static CombatEventBattleUnitData FindEventBattleUnit(
        IReadOnlyList<CombatEventBattleUnitData> units,
        string unitKey)
    {
        if (units == null || string.IsNullOrWhiteSpace(unitKey))
            return null;

        string normalized = BattleScenarioUnitKey.Normalize(unitKey);
        for (int i = 0; i < units.Count; i++)
        {
            CombatEventBattleUnitData candidate = units[i];
            if (candidate != null &&
                string.Equals(BattleScenarioUnitKey.Normalize(candidate.UnitKey), normalized, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void PopulateEventBattleSkills(CombatEventBattleUnitData unit, DHEventBattleUnitTemplate source)
    {
        if (unit == null || source == null)
            return;

        unit.Skills.Clear();
        int enemyIndex = ExtractNumericId(unit.UnitKey);
        if (enemyIndex <= 0)
            return;

        for (int i = 0; i < source.Skills.Count; i++)
        {
            DHEventBattleSkillTemplate skill = source.Skills[i];
            TryAddEventBattleSkill(
                unit.Skills, enemyIndex, skill.Slot, source.EnemyName,
                skill.SkillName, skill.Description,
                skill.Effect, skill.Range, skill.RangeLine,
                skill.Target, skill.Boundary,
                skill.MultiTargetType, skill.MultiTargetCount,
                skill.Value, skill.SubValue);
        }
    }

    private static void TryAddEventBattleSkill(
        List<SkillData> destination,
        int enemyIndex,
        int slot,
        string enemyName,
        string skillName,
        string description,
        int effect,
        int range,
        int rangeLine,
        int target,
        IReadOnlyList<int> boundary,
        int multiTargetType,
        int multiTargetCount,
        float value,
        float subValue)
    {
        if (destination == null || string.IsNullOrWhiteSpace(skillName))
            return;

        var skill = new SkillData
        {
            skillIndex = (enemyIndex * 10) + slot,   // [TEMP:STRKEY] 레거시 int 브리지
            // TODO(§5-11): 정규 enemyKey(FV…)가 이 경로엔 없어 숫자 문자열로 합성. 카탈로그 경로와 키 포맷 통일 필요.
            skillKey = EnemySkillKeyRules.Compose(enemyIndex.ToString(), slot),
            category = SkillCategory.Enemy,
            slot = slot,
            skillClass = enemyName,
            acquireLevel = 1,
            skillName = skillName,
            description = description,
            ipCost = 0,
            classSkillEffect = effect,
            classSkillRange = range,
            EnemySkill1Range = slot == 1 ? range : -1,
            EnemySkill2Range = slot == 2 ? range : -1,
            classSkillRangeLine = rangeLine,
            classSkillTarget = target,
            multiTargetType = multiTargetType,
            multiTargetCount = multiTargetCount,
            skillValue = value,
            skillSubValue = subValue,
            AnimationTrigger = "Attack",
            TargetAnimationTrigger = "Hit",
            HitDelay = 0.25f,
            TotalDelay = 0.5f
        };

        if (boundary != null)
        {
            for (int i = 0; i < boundary.Count; i++)
                skill.boundary.Add(boundary[i]);
        }

        destination.Add(skill);
    }

    private static int ExtractNumericId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        string normalized = BattleScenarioUnitKey.Normalize(value);
        return int.TryParse(normalized, out int parsed) ? parsed : 0;
    }

    private static void ApplyLevelGrowth(CombatEventBattleUnitData unit, DHEventBattleUnitTemplate source, int enemyLevel)
    {
        if (unit == null || source == null)
            return;

        int growthCount = Mathf.Max(0, enemyLevel - 1);
        unit.MaxHp = Mathf.Max(1, source.BaseMaxHp + source.LevelGrowthMaxHp * growthCount);
        unit.Atk = Mathf.Max(0, source.BaseAtk + source.LevelGrowthAtk * growthCount);
        unit.Def = Mathf.Max(0, source.BaseDef + source.LevelGrowthDef * growthCount);
        unit.ExperiencePoint = Mathf.Max(0, source.ExperiencePoint + source.LevelGrowthExperiencePoint * growthCount);
    }
}
