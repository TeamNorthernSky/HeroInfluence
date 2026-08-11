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
    /// 이벤트 전투: 현행 사전 빌드된 CombatEventBattleData → Plan 변환.
    /// [TEMP:EVENTBUILD] 제거조건: Phase 7에서 BattleKey로 전투씬이 직접 빌드하도록 이관.
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

        var entries = new List<EnemySpawnEntry>(eventBattle.EnemyUnits.Count);
        for (int i = 0; i < eventBattle.EnemyUnits.Count; i++)
        {
            CombatEventBattleUnitData unit = eventBattle.EnemyUnits[i];
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitKey))
                continue;

            EnemyData spawnData = BuildEventEnemyData(unit);
            string prefabKey = !string.IsNullOrWhiteSpace(unit.PrefabResourcePath)
                ? unit.PrefabResourcePath.Trim()
                : spawnData.Index;
            entries.Add(new EnemySpawnEntry(spawnData, unit.Slot, prefabKey, unit.Skills));
        }

        if (entries.Count == 0)
        {
            error = "Event battle produced no valid entries.";
            return false;
        }

        plan = new EnemySpawnPlan(entries, eventBattle.Scenario);
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
