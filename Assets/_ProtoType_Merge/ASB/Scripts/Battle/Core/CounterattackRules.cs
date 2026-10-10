namespace ASB.Work.Battle.Core
{
    /// <summary>ASB와 JC가 함께 사용하는 반격 후보 규칙.</summary>
    public static class CounterattackRules
    {
        /// <summary>아군 x=1, 적군 x=2의 고정 전열에 근거리 공격이 적중한 경우.</summary>
        public static bool IsEligibleHit(DamageContext context)
        {
            return context != null
                && !context.IsCounterAttack
                && !context.IsRangedAttack
                && context.Caster != null
                && context.Target != null
                && !context.Target.IsDead
                && context.Target.IsFrontRow();
        }

        /// <summary>
        /// 기본 피해 경로로 반격할 수 있는 스킬만 허용한다.
        /// 응축·능력개방 등 자기행동은 테이블에서 Damage(0)로 기록돼도 피해 계수가 0이다.
        /// </summary>
        public static bool IsAttackSkill(SkillData skill)
        {
            if (skill == null || skill.classSkillEffect != 0 || skill.skillValue <= 0f)
                return false;

            // 두 증폭기의 슬롯 1·2는 실제 시전이 아니라 AI의 SelfAction이다.
            // 이벤트 전투의 숫자 키(40002_1 등)도 같은 스킬로 취급한다.
            string key = skill.skillKey;
            return !EnemySkillKeyRules.IsSameSkill(key, "FV40002_1")
                && !EnemySkillKeyRules.IsSameSkill(key, "FV40002_2")
                && !EnemySkillKeyRules.IsSameSkill(key, "FV40003_1")
                && !EnemySkillKeyRules.IsSameSkill(key, "FV40003_2");
        }

        public static bool CanDefenderCounter(BattleCharactor defender, SkillData skill)
        {
            return defender != null
                && !defender.IsDead
                && defender.IsFrontRow()
                && !defender.IsCharging
                && !defender.HasPendingRest
                && IsAttackSkill(skill);
        }
    }
}
