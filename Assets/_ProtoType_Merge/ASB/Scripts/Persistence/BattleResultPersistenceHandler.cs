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

        if (playerUnits == null)
            return plan;

        float influenceRatio = result == BattleResult.Victory ? 1.1f : 0.9f;

        // Victory: EXP 계산
        int expPerUnit = 0;
        var survivors = new List<BattleCharactor>();

        if (result == BattleResult.Victory && enemyUnits != null)
        {
            float totalExp = 0f;
            foreach (var enemy in enemyUnits)
            {
                if (enemy != null && enemy.IsDead)
                    totalExp += enemy.ExperienceReward;
            }

            foreach (var player in playerUnits)
            {
                if (player != null && !player.IsDead && player.SourceData != null)
                    survivors.Add(player);
            }

            if (totalExp > 0f && survivors.Count > 0)
                expPerUnit = Mathf.CeilToInt(totalExp / (float)survivors.Count);
        }

        // 전체 플레이어 유닛 순회 — EXP는 생존자에게만, IP는 전원에게 적용
        foreach (var player in playerUnits)
        {
            if (player == null || player.SourceData == null)
                continue;

            UnitPersistentData src = player.SourceData;
            bool isSurvivor = survivors.Contains(player);
            int gainedExp = isSurvivor ? expPerUnit : 0;
            int newLevel = gainedExp > 0
                ? PersistentUnitRepository.SimulateFinalLevel(src, gainedExp)
                : src.Level;

            // 임시: UnitGrowthExpData 기반 스터디 스킬 경로 사용
            // TODO: LevelUpData.skill + SkillData.acquireLevel 데이터 정비 후 아래로 교체
            // candidates = SkillUnlockResolver.GetUnlockCandidates(player.UnitName, src.Level, newLevel, src.CurrentSkillIndex);
            int classIndex = int.TryParse(src.UnitTemplateKey, out int parsed) ? parsed : -1;
            List<int> candidates = (gainedExp > 0 && classIndex > 0)
                ? DHCsvTemplateCatalog.Instance?.GetNewlyUnlockedStudySkills(classIndex, src.Level, newLevel) ?? new List<int>()
                : new List<int>();
            Debug.Log($"[BattleRewardPlan] {player.UnitName} | templateKey={src.UnitTemplateKey} classIndex={classIndex} oldLv={src.Level} newLv={newLevel} gainedExp={gainedExp} candidates={candidates.Count}");

            float oldInfluence = player.CurrentInfluence;
            // Both victory and defeat use 10% of maximum IP, rather than current IP.
            float influenceDelta = player.MaxInfluence * (result == BattleResult.Victory ? 0.1f : -0.1f);
            float newInfluence = Mathf.Clamp(oldInfluence + influenceDelta, 0f, player.MaxInfluence);

            plan.UnitPreviews.Add(new UnitRewardPreview
            {
                UnitIndex               = src.UnitIndex,
                UnitName                = player.DisplayName, // [JC 260621] 결과 표시명=히어로명

                OldLevel                = src.Level,
                NewLevel                = newLevel,
                GainedExp               = gainedExp,
                CurrentClassSkillId     = player.ClassSkillIndex > 0 ? player.ClassSkillIndex : src.CurrentSkillIndex,
                OldInfluence            = oldInfluence,
                NewInfluence            = newInfluence,
                UnlockCandidateSkillIds = candidates,
            });
        }

        return plan;
    }

    /// <summary>
    /// plan과 실제 전투체 목록을 받아 Repository에 반영하고 디스크에 저장합니다.
    /// skillResults가 있으면 선택한 스킬을 CurrentSkillIndex에 반영합니다.
    /// </summary>
    public static void CommitBattleRewardPlan(
        BattleRewardPlan plan,
        IReadOnlyList<BattleCharactor> playerUnits,
        IReadOnlyList<BattleCharactor> enemyUnits,
        BattleResult result,
        IReadOnlyList<SkillSelectionResult> skillResults = null)
    {
        float influenceDeltaRatio = (result == BattleResult.Victory) ? 0.1f : -0.1f;

        if (playerUnits != null)
        {
            for (int i = 0; i < playerUnits.Count; i++)
            {
                playerUnits[i]?.ApplyInfluenceDeltaFromMax(influenceDeltaRatio);
                TryPersistPlayerUnit(playerUnits[i]);
            }
        }

        if (enemyUnits != null)
        {
            for (int i = 0; i < enemyUnits.Count; i++)
                TryPersistEnemyUnit(enemyUnits[i], result, isBattleDefeat: result == BattleResult.Defeat);
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

        // 스킬 선택 결과 반영
        if (skillResults != null && skillResults.Count > 0)
        {
            PersistentUnitRepository repo = PersistentUnitRepository.Instance;
            if (repo != null)
            {
                foreach (var selection in skillResults)
                {
                    if (!repo.TryGetUnit(selection.UnitIndex, out UnitPersistentData data))
                        continue;

                    repo.UpdateUnitRuntimeState(
                        data.UnitIndex,
                        data.UnitTemplateKey,
                        data.Level,
                        data.BaseStats,
                        data.LevelupStats,
                        selection.SelectedSkillId,   // CurrentSkillIndex 갱신
                        data.CurrentWeaponIndex,
                        data.CurrentWeaponStats,
                        data.IngameStats,
                        data.CurrentHp,
                        data.Exp,
                        data.MaxExp);
                }
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

        // 적 경로(TryPersistEnemyUnit)와 동일한 규칙: 행동불능은 저장되는 HP에서 파생한다.
        // 여기서는 ResolvePersistedHp가 IsDead일 때만 0을 돌려주므로 결과는 기존과 같다.
        bool isIncapacitated = hp <= 0f;

        StatBlock ingame = src.IngameStats;

        bool ok = repo.UpdateUnitRuntimeState(
            src.UnitIndex,
            src.UnitTemplateKey,
            src.Level,
            src.BaseStats,
            src.LevelupStats,
            src.CurrentSkillIndex,
            src.CurrentWeaponIndex,
            src.CurrentWeaponStats,
            ingame,
            hp,
            currentInfluence: influence,
            isIncapacitated: isIncapacitated);

        if (!ok)
            Debug.LogWarning($"[BattleResultPersistenceHandler] 플레이어 unitIndex={src.UnitIndex} UpdateUnitRuntimeState 실패.", battle);
    }

    private static void TryPersistEnemyUnit(BattleCharactor battle, BattleResult result, bool isBattleDefeat = false)
    {
        if (battle == null || battle.TeamType == TeamType.Player)
            return;

        EnemyUnitPersistentData src = battle.SourceEnemyData;
        if (src == null)
            return;

        PersistentEnemyRepository repo = PersistentEnemyRepository.Instance;
        if (repo == null || !repo.ContainsUnit(src.UnitIndex))
            return;

        // 패배 시 적 HP를 최대 체력으로 복원
        float hp = isBattleDefeat ? battle.MaxHp : ResolvePersistedHp(battle);
        float influence = Mathf.Clamp(battle.CurrentInfluence, 0f, battle.MaxInfluence);

        // 행동불능은 '저장되는 HP'와 일치해야 한다. battle.IsDead를 그대로 쓰면 패배 시
        // HP는 만피로 복원되는데 isIncapacitated=true가 남아 "만피인데 행동불능" 상태가 디스크까지 갔고,
        // 재도전 때 FilterCombatReadyEnemyUnits가 그 적을 제외해 스폰되지 않았다
        // (= 패배 시 HP 복원 의도가 무력화되고 재도전마다 적이 줄어듦).
        bool isIncapacitated = hp <= 0f;

        StatBlock ingame = src.IngameStats;

        bool ok = repo.UpdateUnitRuntimeState(
            src.UnitIndex,
            src.UnitTemplateKey,
            src.Level,
            src.BaseStats,
            ingame,
            hp,
            currentInfluence: influence,
            isIncapacitated: isIncapacitated);

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
