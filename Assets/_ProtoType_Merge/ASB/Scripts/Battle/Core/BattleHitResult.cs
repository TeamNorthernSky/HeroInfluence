namespace ASB.Work.Battle.Core
{
    /// <summary>
    /// 단일 히트 결과. ResolveHitAction → BattleVisualDirector로 전달됩니다.
    /// </summary>
    public class BattleHitResult
    {
        public BattleCharactor Target;
        public float Damage;
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
