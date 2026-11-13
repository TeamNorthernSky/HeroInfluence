using UnityEngine;

public partial class BattleCharactor
{
    // JC | 2026-11-12 | 등록된 모의전 임시 유닛만 성장/장비 로더로 갱신합니다. 실게임 저장·버프·부활 사용 이력은 건드리지 않습니다.
    public bool ApplySimulationGrowth(UnitPersistentData next)
    {
        var context = CombatContext.Instance;
        if (!IsPlayer || IsDead || SourceData == null || next == null || context == null || !context.IsSimulation ||
            next.UnitTemplateKey != SourceData.UnitTemplateKey ||
            !context.TryGetSimulationAlly(SourceData.UnitIndex, out var registered) ||
            !ReferenceEquals(registered.UnitData, SourceData)) return false;

        bool changedRank = Level != next.Level;
        float hpRatio = MaxHp > 0 ? CurrentHp / MaxHp : 1;
        var source = SourceData;
        source.ApplyRuntimeState(source.UnitTemplateKey, next.Level, source.BaseStats, source.LevelupStats,
            source.CurrentSkillIndex, source.CurrentWeaponKey, source.CurrentWeaponIndex, source.CurrentWeaponStats,
            next.IngameStats, CurrentHp, nextCurrentInfluence: next.CurrentInfluence);
        if (changedRank)
        {
            SetBaseStats(next.IngameStats);
            ApplyCombatTuning(next.Level, levelWeight, classWeight);
            LoadPersistentEquipment(source.CurrentSkillIndex, source.CurrentWeaponIndex, source.SkillLevel, source.EquippedWeaponInstanceIndex);
            RecalculateStats(false);
            currentHp = Mathf.Clamp(RoundHpValue(MaxHp * hpRatio), 1, MaxHp);
            OnHpChanged?.Invoke(CurrentHp, MaxHp);
        }
        SetInfluence(next.CurrentInfluence);
        // JC | 2026-11-12 | 이후 테스트 재배치도 동일한 HP/IP 스냅샷을 읽도록 변경된 체력 비율을 임시 원본에 동기화합니다.
        source.ApplyRuntimeState(source.UnitTemplateKey, next.Level, source.BaseStats, source.LevelupStats,
            source.CurrentSkillIndex, source.CurrentWeaponKey, source.CurrentWeaponIndex, source.CurrentWeaponStats,
            next.IngameStats, CurrentHp, nextCurrentInfluence: CurrentInfluence);
        return true;
    }
}
