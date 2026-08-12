using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 스폰 계획 빌더(공용 계층). CombatContext의 "키 + 레벨" 계약으로 EnemySpawnPlan을 복원한다.
/// - 일반 전투: EnemyGroupKey + EnemyLevel  → 그룹 템플릿 조회 + 레벨 스탯 계산
/// - 이벤트 전투: (현행) 사전 빌드된 CombatEventBattleData → Plan 변환
///
/// 씬 의존이 없어 전투씬(EnemySpawner)과 DH 소비처(스킵 전력/프롬프트/보상)가 함께 재사용한다.
/// 레벨 스탯은 영속 경로(PersistentEnemyRepository.CalculateEnemyIngameStats)와 동일하게
/// UnitStatCalculator.CalculateLevelAdjustedBaseStats(base, 적유닛템플릿.LevelupStats, level)로 계산한다.
/// </summary>
public static class EnemySpawnPlanBuilder
{
    /// <summary>일반 전투: EnemyGroupKey + level → Plan. 실패 시 false + error(부분 결과 반환 안 함).</summary>
    public static bool TryBuildFromEnemyGroup(string enemyGroupKey, int enemyLevel, out EnemySpawnPlan plan, out string error)
    {
        plan = null;
        error = string.Empty;

        string groupKey = string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
        if (string.IsNullOrEmpty(groupKey))
        {
            error = "EnemyGroupKey is empty.";
            return false;
        }

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null)
        {
            error = "DHCsvTemplateCatalog is not available.";
            return false;
        }

        if (!catalog.TryGetEnemyGroupTemplate(groupKey, out DHEnemyGroupTemplate group) || group == null)
        {
            error = $"Enemy group template not found. groupKey='{groupKey}'";
            return false;
        }

        if (group.Members == null || group.Members.Count == 0)
        {
            error = $"Enemy group has no members. groupKey='{groupKey}'";
            return false;
        }

        int safeLevel = Mathf.Max(1, enemyLevel);
        var entries = new List<EnemySpawnEntry>(group.Members.Count);
        for (int i = 0; i < group.Members.Count; i++)
        {
            DHEnemyGroupMember member = group.Members[i];
            string templateKey = member.EnemyUnitIndex.ToString();

            if (!catalog.TryGetEnemyTemplate(templateKey, out EnemyData csvTemplate) || csvTemplate == null)
            {
                error = $"Enemy template not found. groupKey='{groupKey}', unit='{templateKey}'";
                return false;
            }

            // 영속 경로(EnemyUnitState.InitializeFromTemplate)와 동일한 소스/계산식으로 맞춘다:
            //   ingame = CalculateLevelAdjustedBaseStats(적유닛템플릿.BaseStats, 적유닛템플릿.LevelupStats, level)
            // 적유닛템플릿이 없을 때만 CSV EnemyData로 폴백(영속 경로도 템플릿을 요구하므로 예외 경로).
            catalog.TryGetEnemyUnitTemplate(templateKey, out DHEnemyUnitTemplate unitTemplate);
            StatBlock baseStatsForLevel = unitTemplate != null ? unitTemplate.BaseStats : csvTemplate.baseStats;
            StatBlock levelupStats = unitTemplate != null ? unitTemplate.LevelupStats : csvTemplate.levelupStats;
            StatBlock ingameStats =
                UnitStatCalculator.CalculateLevelAdjustedBaseStats(baseStatsForLevel, levelupStats, safeLevel);

            // 공유 템플릿 변형 금지 — 복제본에 레벨 스탯을 반영한다.
            EnemyData spawnData = CloneForSpawn(csvTemplate);
            spawnData.baseStats = ingameStats;
            spawnData.levelupStats = default; // 전투 진입 시 레벨 스케일 off이므로 추가 성장 없음.

            entries.Add(new EnemySpawnEntry(spawnData, member.CombatSlot, spawnData.Index, null));
        }

        plan = new EnemySpawnPlan(entries, null);
        return true;
    }

    /// <summary>
    /// 이벤트 전투: 현행 사전 빌드된 CombatEventBattleData → Plan 변환(과도기 폴백 경로).
    /// [TEMP:EVENTBUILD] 런타임 동등성 검증 후 §6에서 TryBuildFromEventBattleKey로 완전 대체.
    /// </summary>
    public static bool TryBuildFromEventBattle(CombatEventBattleData eventBattle, out EnemySpawnPlan plan, out string error)
    {
        plan = null;
        error = string.Empty;

        if (eventBattle == null || eventBattle.EnemyUnits == null || eventBattle.EnemyUnits.Count == 0)
        {
            error = "Event battle has no enemy units.";
            return false;
        }

        if (!TryBuildEntriesFromUnits(eventBattle.EnemyUnits, out List<EnemySpawnEntry> entries, out error))
            return false;

        plan = new EnemySpawnPlan(entries, eventBattle.Scenario);
        return true;
    }

    /// <summary>
    /// 이벤트 전투: (zoneId, battleKey, level) 키로 플랜을 직접 빌드한다(§4). 부분 결과 반환 금지.
    /// 유닛/시나리오 빌드는 공용 <see cref="EventBattlePlanSource"/>(failFast:true)에 위임하고,
    /// 시나리오는 반드시 이 플랜(plan.Scenario) 안에 담아 반환한다 — 스포너/BattleSceneManager가
    /// 시나리오를 별도 경로로 넘기지 않도록 강제(§5·§7).
    /// </summary>
    public static bool TryBuildFromEventBattleKey(int zoneId, string battleKey, int enemyLevel, out EnemySpawnPlan plan, out string error)
    {
        plan = null;
        error = string.Empty;

        if (!EventBattlePlanSource.TryBuildUnits(
                zoneId, battleKey, enemyLevel, failFast: true,
                out List<CombatEventBattleUnitData> units, out error))
        {
            return false;
        }

        if (!TryBuildEntriesFromUnits(units, out List<EnemySpawnEntry> entries, out error))
            return false;

        if (!EventBattlePlanSource.TryBuildScenario(
                zoneId, battleKey, units, failFast: true,
                out BattleScenarioConfig scenario, out error))
        {
            return false;
        }

        plan = new EnemySpawnPlan(entries, scenario);
        return true;
    }

    // CombatEventBattleUnitData 목록 → EnemySpawnEntry 목록. 이벤트 키/사전빌드 경로가 공유한다.
    // 키 경로는 EventBattlePlanSource가 상류에서 이미 검증하므로 여기서 skip이 발생하지 않는다.
    private static bool TryBuildEntriesFromUnits(
        IReadOnlyList<CombatEventBattleUnitData> units,
        out List<EnemySpawnEntry> entries,
        out string error)
    {
        entries = new List<EnemySpawnEntry>(units != null ? units.Count : 0);
        error = string.Empty;

        if (units == null || units.Count == 0)
        {
            error = "Event battle has no enemy units.";
            return false;
        }

        for (int i = 0; i < units.Count; i++)
        {
            CombatEventBattleUnitData unit = units[i];
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitKey))
                continue;

            EnemyData spawnData = BuildEventEnemyData(unit);
            string prefabKey = !string.IsNullOrWhiteSpace(unit.PrefabResourcePath)
                ? unit.PrefabResourcePath.Trim()
                : ResolveEventFallbackPrefabPath(unit.UnitKey);
            entries.Add(new EnemySpawnEntry(spawnData, unit.Slot, prefabKey, unit.Skills, unit.UnitKey));
        }

        if (entries.Count == 0)
        {
            error = "Event battle produced no valid entries.";
            return false;
        }

        return true;
    }

    // EnemySpawner.BuildEventEnemyData와 동일한 전투 진입용 EnemyData 규약.
    // [TEMP:EVENTBUILD] Phase 7에서 공용화하며 중복 제거 예정.
    private static EnemyData BuildEventEnemyData(CombatEventBattleUnitData unit)
    {
        return new EnemyData
        {
            Index = BattleScenarioUnitKey.Normalize(unit.UnitKey),
            UnitType = unit.EnemyConcept,
            Name = unit.EnemyName,
            baseStats = new StatBlock(
                hp: Mathf.Max(1, unit.MaxHp),
                atk: Mathf.Max(0, unit.Atk),
                def: Mathf.Max(0, unit.Def),
                luck: 0f,
                speed: Mathf.Max(0, unit.Speed),
                criticalRate: Mathf.Max(0f, unit.CriticalRate),
                critMultiplier: 1.5f,
                counterRate: Mathf.Max(0f, unit.CounterRate),
                avoidRate: Mathf.Max(0f, unit.ReduceRate)),
            levelupStats = new StatBlock(0f, 0f, 0f, 0f, 0f),
            IsEnemyRow = true,
            UnitAI = unit.UnitAI,
            ExperiencePoint = Mathf.Max(0, unit.ExperiencePoint)
        };
    }

    // EnemySpawner.FindEventEnemyPrefab의 하드코딩 폴백과 동일 규칙(PrefabResourcePath 미지정 시).
    // [TEMP:EVENTBUILD] Phase 7 완결 시 프리팹 규약 통일 예정.
    private static string ResolveEventFallbackPrefabPath(string unitKey)
    {
        string normalized = BattleScenarioUnitKey.Normalize(unitKey);
        if (normalized == "20007")
            return "prefab/BattlePrefab/EnemyUnit/Unit_AdvancedMonster_20003";
        if (normalized == "20006")
            return "prefab/BattlePrefab/EnemyUnit/Unit_MiddleMonster_20002";
        return "prefab/BattlePrefab/EnemyUnit/Unit_LowerMonster_20001";
    }

    private static EnemyData CloneForSpawn(EnemyData source)
    {
        return new EnemyData
        {
            Index = source.Index,
            UnitType = source.UnitType,
            Name = source.Name,
            baseStats = source.baseStats,
            levelupStats = source.levelupStats,
            IsEnemyRow = source.IsEnemyRow,
            UnitAI = source.UnitAI,
            ExperiencePoint = source.ExperiencePoint,
            // 보상 드랍 목록은 스폰 경로에서 읽기 전용으로만 쓰이므로 참조 공유.
            DropItem1Index = source.DropItem1Index,
            DropItem1Count = source.DropItem1Count,
            DropItem1Rate = source.DropItem1Rate
        };
    }
}
