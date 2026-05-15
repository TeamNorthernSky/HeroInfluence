using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 종료 시점에 <see cref="BattleCharactor"/> 런타임 HP/IP를
/// <see cref="PersistentUnitRepository"/> / <see cref="PersistentEnemyRepository"/>에 반영하고 디스크에 저장합니다.
/// </summary>
public static class BattleResultPersistenceHandler
{
    /// <summary>
    /// 플레이어·적 전투체 목록을 순회해 영속 데이터를 갱신한 뒤 리포지토리 Save를 호출합니다.
    /// </summary>
    public static void PersistAtBattleEnd(
        IReadOnlyList<BattleCharactor> playerUnits,
        IReadOnlyList<BattleCharactor> enemyUnits,
        BattleResult result)
    {
        if (playerUnits != null)
        {
            for (int i = 0; i < playerUnits.Count; i++)
                TryPersistPlayerUnit(playerUnits[i]);
        }

        if (enemyUnits != null)
        {
            for (int i = 0; i < enemyUnits.Count; i++)
                TryPersistEnemyUnit(enemyUnits[i], result);
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

    /// <summary>사망 시 0, 생존 시 0 이하 HP는 최소 1로 올려 저장합니다.</summary>
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
