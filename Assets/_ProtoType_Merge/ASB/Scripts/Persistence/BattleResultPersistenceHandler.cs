using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 종료 시 보상 계산(Preview)과 실제 저장(Commit)을 분리해서 처리합니다.
/// </summary>
public static class BattleResultPersistenceHandler
{
    /// <summary>
    /// Repository와 JSON을 변경하지 않고 보상/레벨업 결과만 계산해서 반환합니다.
    /// </summary>
    public static BattleRewardPlan BuildBattleRewardPlan(
        IReadOnlyList<BattleCharactor> playerUnits,
        IReadOnlyList<BattleCharactor> enemyUnits,
        BattleResult result)
    {
        var plan = new BattleRewardPlan { Result = result };

        if (result != BattleResult.Victory)
            return plan;

        if (playerUnits == null || enemyUnits == null)
            return plan;

        float totalExp = 0f;
        foreach (var enemy in enemyUnits)
        {
            if (enemy != null && enemy.IsDead)
                totalExp += enemy.ExperienceReward;
        }

        if (totalExp <= 0f)
            return plan;

        var survivors = new List<BattleCharactor>();
        foreach (var player in playerUnits)
        {
            if (player != null && !player.IsDead && player.SourceData != null)
                survivors.Add(player);
        }

        if (survivors.Count == 0)
            return plan;

        int expPerUnit = Mathf.CeilToInt(totalExp / (float)survivors.Count);

        foreach (var player in survivors)
        {
            UnitPersistentData src = player.SourceData;
            int newLevel = PersistentUnitRepository.SimulateFinalLevel(src, expPerUnit);

            plan.UnitPreviews.Add(new UnitRewardPreview
            {
                UnitIndex  = src.UnitIndex,
                UnitName   = player.UnitName,
                OldLevel   = src.Level,
                NewLevel   = newLevel,
                GainedExp  = expPerUnit,
            });
        }

        return plan;
    }

    /// <summary>
    /// plan과 실제 전투체 목록을 받아 Repository에 반영하고 디스크에 저장합니다.
    /// </summary>
    public static void CommitBattleRewardPlan(
        BattleRewardPlan plan,
        IReadOnlyList<BattleCharactor> playerUnits,
        IReadOnlyList<BattleCharactor> enemyUnits,
        BattleResult result)
    {
        float influenceRatio = (result == BattleResult.Victory) ? 1.1f : 0.9f;

        if (playerUnits != null)
        {
            for (int i = 0; i < playerUnits.Count; i++)
            {
                playerUnits[i]?.ApplyInfluenceModifier(influenceRatio);
                TryPersistPlayerUnit(playerUnits[i]);
            }
        }

        if (enemyUnits != null)
        {
            for (int i = 0; i < enemyUnits.Count; i++)
                TryPersistEnemyUnit(enemyUnits[i], result);
        }

        if (result == BattleResult.Victory && plan != null)
        {
            PersistentUnitRepository repo = PersistentUnitRepository.Instance;
            if (repo != null)
            {
                foreach (var preview in plan.UnitPreviews)
                    repo.AddExp(preview.UnitIndex, preview.GainedExp);
            }
        }

        PersistentUnitRepository unitRepo = PersistentUnitRepository.Instance;
        if (unitRepo != null)
            unitRepo.SaveRuntimeStateToDisk();

        PersistentEnemyRepository enemyRepo = PersistentEnemyRepository.Instance;
        if (enemyRepo != null)
            enemyRepo.SaveRuntimeStateToDisk();
    }

    private static void TryPersistPlayerUnit(BattleCharactor battle)
    {
        if (battle == null || battle.TeamType != TeamType.Player)
            return;

        UnitPersistentData src = battle.SourceData;
        if (src == null)
            return;

        PersistentUnitRepository repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.ContainsUnit(src.UnitIndex))
            return;

        float hp = ResolvePersistedHp(battle);
        float influence = Mathf.Clamp(battle.CurrentInfluence, 0f, battle.MaxInfluence);

        StatBlock ingame = src.IngameStats;
        ingame.Influence = influence;

        bool ok = repo.UpdateUnitRuntimeState(
            src.UnitIndex,
            src.UnitTemplateKey,
            src.Level,
            src.Favorability,
            src.BaseStats,
            src.LevelupStats,
            src.CurrentSkillIndex,
            src.CurrentWeaponIndex,
            src.CurrentWeaponStats,
            ingame,
            hp);

        if (!ok)
            Debug.LogWarning($"[BattleResultPersistenceHandler] 플레이어 unitIndex={src.UnitIndex} UpdateUnitRuntimeState 실패.", battle);
    }

    private static void TryPersistEnemyUnit(BattleCharactor battle, BattleResult result)
    {
        if (battle == null || battle.TeamType == TeamType.Player)
            return;

        EnemyUnitPersistentData src = battle.SourceEnemyData;
        if (src == null)
            return;

        PersistentEnemyRepository repo = PersistentEnemyRepository.Instance;
        if (repo == null || !repo.ContainsUnit(src.UnitIndex))
            return;

        if (battle.IsDead && result == BattleResult.Victory)
        {
            repo.RemoveUnit(src.UnitIndex);
            return;
        }

        float hp = ResolvePersistedHp(battle);
        float influence = Mathf.Clamp(battle.CurrentInfluence, 0f, battle.MaxInfluence);

        StatBlock ingame = src.IngameStats;
        ingame.Influence = influence;

        bool ok = repo.UpdateUnitRuntimeState(
            src.UnitIndex,
            src.UnitTemplateKey,
            src.Level,
            src.BaseStats,
            ingame,
            hp);

        if (!ok)
            Debug.LogWarning($"[BattleResultPersistenceHandler] 적 unitIndex={src.UnitIndex} UpdateUnitRuntimeState 실패.", battle);
    }

    private static float ResolvePersistedHp(BattleCharactor battle)
    {
        if (battle == null)
            return 0f;

        if (battle.IsDead)
            return 0f;

        float hp = Mathf.Clamp(battle.CurrentHp, 0f, battle.MaxHp);
        if (hp <= 0f)
            hp = 1f;

        return hp;
    }
}
