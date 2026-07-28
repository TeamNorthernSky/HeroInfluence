namespace ASB.Work.Battle.Core
{
    /// <summary>피해 컨텍스트의 역할. 체인 투사체에서 원점 결정에 사용. 순서/CanTriggerCounter 추론 대신 명시적으로 설정.</summary>
    public enum DamageRole
    {
        Primary,
        Additional
    }

    public class DamageContext
    {
        public BattleCharactor Caster;
        /// <summary>주 타깃(Primary) / 추가 타깃(Additional). 기본 Primary. TargetAroundRandom이 명시 설정.</summary>
        public DamageRole Role = DamageRole.Primary;
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
