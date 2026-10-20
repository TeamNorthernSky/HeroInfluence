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
        /// <summary>최소 피해·반올림까지 적용한 실제 피해가 감소 계수로 줄었는지 표시용으로 전달한다.</summary>
        public bool DamageReduced;
        public bool IsHeal;
        public bool IsMiss;
        public bool TargetDied;
        public bool WasDeadBefore;
        public bool IsDeadAfter;
        public bool CausedDeath;
        public int SkillIndex;

        public static BattleHitResult Empty(BattleCharactor target) =>
            new BattleHitResult { Target = target };
    }
}
