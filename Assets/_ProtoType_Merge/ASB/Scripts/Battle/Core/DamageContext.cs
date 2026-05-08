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
        public bool IsRangedAttack = false;
        public float BonusCritRate = 0f;
        public float TargetAvoidRateReduction = 0f;
        public bool CanTriggerCounter = false;
        public bool IsCounterAttack = false;
        // 다단 히트 연출용 대기 시간(초).
        // TODO: 장기적으로는 DamageContext(순수 전투 데이터)와 연출 스텝을
        // SkillExecutionStep 같은 별도 구조로 분리하는 것이 바람직합니다.
        public float DelayAfter = 0f;
    }
}
