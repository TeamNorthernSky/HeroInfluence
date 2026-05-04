using System.Collections.Generic;
using System.Linq;

namespace EnemyAI
{
    /// <summary>
    /// 적 AI 공통 예외 처리 템플릿.
    /// - 행동 불가 상태(기절) 처리
    /// - 도발 강제 타겟 처리
    /// - 스킬 사용 가능 여부 계산
    /// </summary>
    public abstract class BaseEnemyAI : IEnemyAI
    {
        public abstract int Index { get; }

        public EnemyActionDecision DecideAction(BattleCharactor self, List<BattleCharactor> targets)
        {
            if (self == null || targets == null || targets.Count == 0)
            {
                return EnemyActionDecision.SkipTurn();
            }

            // BattleFlowManager에서 선처리하지만 AI 단에서도 안전하게 방어합니다.
            if (self.IsDead || self.IsStunned)
            {
                return EnemyActionDecision.SkipTurn();
            }

            List<BattleCharactor> validTargets = targets
                .Where(t => t != null && !t.IsDead && t.IsPlayer != self.IsPlayer)
                .ToList();

            if (validTargets.Count == 0)
            {
                return EnemyActionDecision.SkipTurn();
            }

            BattleCharactor tauntTarget = GetTauntTarget(self, validTargets);
            if (tauntTarget != null)
            {
                validTargets = new List<BattleCharactor> { tauntTarget };
            }

            bool canUseSkill = true; // 추후 침묵/봉인 상태이상 추가 시 여기서 제어
            return DetermineSpecificAction(self, validTargets, canUseSkill);
        }

        protected abstract EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self,
            List<BattleCharactor> validTargets,
            bool canUseSkill);

        protected BattleCharactor GetLowestHpTarget(List<BattleCharactor> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return null;
            }

            BattleCharactor best = null;
            for (int i = 0; i < targets.Count; i++)
            {
                BattleCharactor candidate = targets[i];
                if (candidate == null || candidate.IsDead)
                {
                    continue;
                }

                if (best == null || candidate.CurrentHp < best.CurrentHp)
                {
                    best = candidate;
                }
            }

            return best;
        }

        protected BattleCharactor GetTauntTarget(BattleCharactor self, List<BattleCharactor> targets)
        {
            if (self == null || targets == null || targets.Count == 0)
            {
                return null;
            }

            BattleCharactor tauntSource = self.GetTauntSource();
            if (tauntSource == null || tauntSource.IsDead)
            {
                return null;
            }

            return targets.Contains(tauntSource) ? tauntSource : null;
        }
    }
}
