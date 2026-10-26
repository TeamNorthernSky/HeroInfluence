namespace ASB.Work.Battle.Core
{
    /// <summary>
    /// 단일 히트 결과. ResolveHitAction → BattleVisualDirector로 전달됩니다.
    /// </summary>
    public class BattleHitResult
    {
        public BattleCharactor Target;
        public float Damage;
        /// <summary>HP 제한과 무력화 규칙을 반영해 실제로 감소한 HP입니다.</summary>
        public float AppliedDamage;
        public bool IsCritical;
        /// <summary>피해 계산 시점에 후열·비치명타 타격의 확률적 50% 경감이 발동했는지 표시용으로 전달한다. 상시 보정과 스킬 피해 감소는 제외한다.</summary>
        public bool DamageReduced;
        public bool IsHeal;
        public bool IsMiss;
        public bool TargetDied;
        public bool WasDeadBefore;
        public bool IsDeadAfter;
        public bool CausedDeath;
        public int SkillIndex;
        public string ExecutionId;

        public static BattleHitResult Empty(BattleCharactor target) =>
            new BattleHitResult { Target = target };
    }
}
