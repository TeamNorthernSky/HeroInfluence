using System;
using System.Collections.Generic;
using ASB.Work.Battle.Core;

namespace ASB.Work.Battle.SkillExecution
{
    public class SkillExecutionResult
    {
        public bool Success { get; private set; }
        public List<DamageContext> DamageContexts { get; private set; } = new List<DamageContext>();

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
    }
}

