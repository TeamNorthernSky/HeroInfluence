namespace ASB.Work.Battle.Core
{
    public class DamageContext
    {
        public BattleCharactor Caster;
        public BattleCharactor Target;
        /// <summary>스킬 배율. SkillValue가 0보다 크면 CombatCalculator는 SkillValue를 우선 사용합니다.</summary>
        public float SkillMultiplier;
        /// <summary>0보다 크면 SkillMultiplier 대신 이 값을 배율로 사용합니다.</summary>
        public float SkillValue;
        public int SkillIndex;
        public bool IsCritical;
        public float DelayAfter = 0.2f;
    }
}
