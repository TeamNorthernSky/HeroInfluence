using System.Collections.Generic;
using UnityEngine;

public enum CombatAdvantageState
{
    HeroAdvantage,
    Close,
    VillainAdvantage
}

public enum CombatSkipDecision
{
    Victory,
    Defeat
}

public readonly struct CombatAdvantageEvaluation
{
    public readonly CombatAdvantageState State;
    public readonly float HeroWinIndex;
    public readonly float VillainWinIndex;

    public CombatAdvantageEvaluation(CombatAdvantageState state, float heroWinIndex, float villainWinIndex)
    {
        State = state;
        HeroWinIndex = heroWinIndex;
        VillainWinIndex = villainWinIndex;
    }
}

public sealed class CombatSkipHpResult
{
    public CombatSkipDecision Decision { get; }
    public readonly Dictionary<int, float> HeroHpAfter = new Dictionary<int, float>();
    public readonly Dictionary<int, float> VillainHpAfter = new Dictionary<int, float>();

    public CombatSkipHpResult(CombatSkipDecision decision)
    {
        Decision = decision;
    }
}

public static class CombatSkipCalculator
{
    private const float AdvantageRatio = 1.05f;
    private const float MinBaseDamage = 12f;
    private const float HeroInfluenceDamageDivisor = 400f;

    public static CombatAdvantageEvaluation EvaluateAdvantage(CombatContext context)
    {
        CombatSideSnapshot heroes = BuildHeroSnapshot(context);
        CombatSideSnapshot villains = BuildVillainSnapshot(context);

        if (heroes.CurrentHp <= 0f)
            return new CombatAdvantageEvaluation(CombatAdvantageState.VillainAdvantage, 0f, 1f);

        if (villains.CurrentHp <= 0f)
            return new CombatAdvantageEvaluation(CombatAdvantageState.HeroAdvantage, 1f, 0f);

        float heroWinIndex = (heroes.Attack - villains.Defense) * heroes.CurrentInfluence / villains.CurrentHp;
        float villainWinIndex = (villains.Attack - heroes.Defense) / heroes.CurrentHp;

        CombatAdvantageState state = CombatAdvantageState.Close;
        if (heroWinIndex > villainWinIndex * AdvantageRatio)
            state = CombatAdvantageState.HeroAdvantage;
        else if (villainWinIndex > heroWinIndex * AdvantageRatio)
            state = CombatAdvantageState.VillainAdvantage;

        return new CombatAdvantageEvaluation(state, heroWinIndex, villainWinIndex);
    }

    public static CombatSkipDecision ResolveDecision(CombatAdvantageState state)
    {
        return state switch
        {
            CombatAdvantageState.HeroAdvantage => CombatSkipDecision.Victory,
            CombatAdvantageState.VillainAdvantage => CombatSkipDecision.Defeat,
            _ => Random.value < 0.5f ? CombatSkipDecision.Victory : CombatSkipDecision.Defeat,
        };
    }

    public static string GetDisplayText(CombatAdvantageState state)
    {
        return state switch
        {
            CombatAdvantageState.HeroAdvantage => "Hero Advantage",
            CombatAdvantageState.VillainAdvantage => "Villain Advantage",
            _ => "Close Battle",
        };
    }

    public static CombatSkipHpResult CalculateHpResult(CombatContext context, CombatSkipDecision decision)
    {
        CombatSideSnapshot heroes = BuildHeroSnapshot(context);
        CombatSideSnapshot villains = BuildVillainSnapshot(context);
        CombatSkipHpResult result = new CombatSkipHpResult(decision);

        float heroBaseDamage = CalculateHeroBaseDamage(heroes, villains);
        float villainBaseDamage = CalculateVillainBaseDamage(heroes, villains);

        if (decision == CombatSkipDecision.Victory)
        {
            FillWinnerHp(heroes, heroBaseDamage, villains, villainBaseDamage, result.HeroHpAfter);
            FillLoserHp(villains, result.VillainHpAfter);
        }
        else
        {
            FillLoserHp(heroes, result.HeroHpAfter);
            FillWinnerHp(villains, villainBaseDamage, heroes, heroBaseDamage, result.VillainHpAfter);
        }

        return result;
    }

    private static CombatSideSnapshot BuildHeroSnapshot(CombatContext context)
    {
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        IReadOnlyList<int> unitIndices = context != null ? context.CombatParty?.UnitIndices : null;
        CombatSideSnapshot snapshot = new CombatSideSnapshot();
        if (repository == null || unitIndices == null)
            return snapshot;

        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0 || !repository.TryGetUnit(unitIndex, out UnitPersistentData data) || data == null)
                continue;

            if (data.IsIncapacitated || data.CurrentHp <= 0f)
                continue;

            snapshot.Add(data.UnitIndex, data.IngameStats, data.CurrentHp, data.CurrentInfluence);
        }

        return snapshot;
    }

    private static CombatSideSnapshot BuildVillainSnapshot(CombatContext context)
    {
        CombatSideSnapshot snapshot = new CombatSideSnapshot();
        if (!CombatEnemyTemplatePreviewBuilder.TryBuildFromContext(
                context,
                out IReadOnlyList<CombatEnemyTemplatePreviewUnit> units))
        {
            return snapshot;
        }

        for (int i = 0; i < units.Count; i++)
        {
            CombatEnemyTemplatePreviewUnit unit = units[i];
            if (!unit.IsValid)
                continue;

            snapshot.Add(unit.CombatSlot, unit.IngameStats, unit.CurrentHp, 0f);
        }

        return snapshot;
    }

    private static float CalculateHeroBaseDamage(CombatSideSnapshot heroes, CombatSideSnapshot villains)
    {
        float rawDamage = (heroes.Attack - villains.Defense) * heroes.CurrentInfluence / HeroInfluenceDamageDivisor;
        return Mathf.Max(MinBaseDamage, Mathf.Round(rawDamage));
    }

    private static float CalculateVillainBaseDamage(CombatSideSnapshot heroes, CombatSideSnapshot villains)
    {
        float rawDamage = villains.Attack - heroes.Defense;
        return Mathf.Max(MinBaseDamage, Mathf.Round(rawDamage));
    }

    private static void FillWinnerHp(
        CombatSideSnapshot winner,
        float winnerBaseDamage,
        CombatSideSnapshot loser,
        float loserBaseDamage,
        Dictionary<int, float> hpAfter)
    {
        if (hpAfter == null || winner.Units.Count == 0)
            return;

        if (winner.CurrentHp <= 0f)
        {
            FillMinimumSurvivorHp(winner, hpAfter);
            return;
        }

        float safeWinnerBaseDamage = Mathf.Max(MinBaseDamage, winnerBaseDamage);
        float remainingHpSum = winner.CurrentHp - (loserBaseDamage * loser.CurrentHp / safeWinnerBaseDamage);
        if (remainingHpSum <= 0f)
        {
            FillMinimumSurvivorHp(winner, hpAfter);
            return;
        }

        for (int i = 0; i < winner.Units.Count; i++)
        {
            CombatSkipUnitSnapshot unit = winner.Units[i];
            float ratio = unit.CurrentHp / winner.CurrentHp;
            float nextHp = Mathf.Round(remainingHpSum * ratio);
            nextHp = Mathf.Clamp(nextHp, 1f, unit.CurrentHp);
            hpAfter[unit.UnitIndex] = nextHp;
        }
    }

    private static void FillMinimumSurvivorHp(CombatSideSnapshot winner, Dictionary<int, float> hpAfter)
    {
        for (int i = 0; i < winner.Units.Count; i++)
            hpAfter[winner.Units[i].UnitIndex] = 1f;
    }

    private static void FillLoserHp(CombatSideSnapshot loser, Dictionary<int, float> hpAfter)
    {
        if (hpAfter == null)
            return;

        for (int i = 0; i < loser.Units.Count; i++)
            hpAfter[loser.Units[i].UnitIndex] = 0f;
    }

    private sealed class CombatSideSnapshot
    {
        public float Attack;
        public float Defense;
        public float CurrentHp;
        public float CurrentInfluence;
        public readonly List<CombatSkipUnitSnapshot> Units = new List<CombatSkipUnitSnapshot>();

        public void Add(int unitIndex, StatBlock stats, float currentHp, float currentInfluence)
        {
            Attack += stats.Atk;
            Defense += stats.DEF;
            CurrentHp += Mathf.Max(0f, currentHp);
            CurrentInfluence += Mathf.Max(0f, currentInfluence);
            Units.Add(new CombatSkipUnitSnapshot(unitIndex, Mathf.Max(0f, currentHp)));
        }
    }

    private readonly struct CombatSkipUnitSnapshot
    {
        public readonly int UnitIndex;
        public readonly float CurrentHp;

        public CombatSkipUnitSnapshot(int unitIndex, float currentHp)
        {
            UnitIndex = unitIndex;
            CurrentHp = currentHp;
        }
    }
}
