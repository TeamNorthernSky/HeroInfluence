using System;
using System.Collections.Generic;
using ASB.Work.Battle.Core;

#nullable enable // 코드 최상단에 추가

namespace ASB.Work.Battle.SkillExecution
{
    public class SkillExecutionResult
    {
        public bool Success { get; private set; }
        public List<DamageContext> DamageContexts { get; private set; } = new List<DamageContext>();
        public List<HealContext> HealContexts { get; private set; } = new List<HealContext>();

        /// <summary>스킬 실행에 사용된 핸들러. 광역(AoE) 연출 분기 등에 사용합니다.</summary>
        public ISkillEffectHandler Handler { get; set; }

        // 총 가해진 데미지(전투 계산 후 실제 적용된 값)를 전달합니다.
        // (예: 흡혈, 누적 반응 등 사후 처리)

        public Action<float>? OnPostExecution { get; set; } 

        public static SkillExecutionResult SuccessResult()
        {
            return new SkillExecutionResult { Success = true };
        }

        public static SkillExecutionResult Failed()
        {
            return new SkillExecutionResult { Success = false };
        }

        public SkillExecutionResult AddDamage(DamageContext context)
        {
            if (context.Caster != null && context.Target != null)
            {
                DamageContexts.Add(context);
                Success = true;
            }

            return this;
        }

        public SkillExecutionResult AddHeal(BattleCharactor caster, BattleCharactor target, float amount, int skillIndex = 0)
        {
            if (caster != null && target != null && amount > 0f)
            {
                HealContexts.Add(new HealContext
                {
                    Caster = caster,
                    Target = target,
                    HealAmount = amount,
                    SkillIndex = skillIndex
                });
                Success = true;
            }

            return this;
        }
    }
}

