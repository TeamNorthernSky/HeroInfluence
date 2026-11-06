using System.Collections.Generic;
using UnityEngine;

public static class ExplorationDefeatResultHandler
{
    private const float DefeatRecoveryHpRatio = 0.3f;

    public static int RecoverDefeatedUnitsForReturn(IReadOnlyList<int> unitIndices)
    {
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null || unitIndices == null)
            return 0;

        int recoveredCount = 0;
        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0 || !repository.TryGetUnit(unitIndex, out UnitPersistentData data) || data == null)
                continue;

            float recoveredHp = CalculateDefeatRecoveryHp(data);
            repository.UpdateUnitRuntimeState(
                data.UnitIndex,
                data.UnitTemplateKey,
                data.Level,
                data.BaseStats,
                data.LevelupStats,
                data.CurrentSkillIndex,
                data.CurrentWeaponKey,
                data.CurrentWeaponStats,
                data.IngameStats,
                recoveredHp,
                data.Exp,
                data.MaxExp,
                data.SkillLevel,
                data.EquippedWeaponInstanceIndex,
                data.CurrentInfluence,
                false);

            recoveredCount++;
        }

        if (recoveredCount > 0)
        {
            repository.SaveRuntimeStateToDisk();
            PartyRepositorySync.ApplyUnitsToScene(unitIndices);
        }

        return recoveredCount;
    }

    private static float CalculateDefeatRecoveryHp(UnitPersistentData data)
    {
        if (data == null)
            return 0f;

        float maxHp = Mathf.Max(0f, data.IngameStats.HP);
        if (maxHp <= 0f)
            return 0f;

        return Mathf.Clamp(Mathf.Ceil(maxHp * DefeatRecoveryHpRatio), 1f, maxHp);
    }

}
