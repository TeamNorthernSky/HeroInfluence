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