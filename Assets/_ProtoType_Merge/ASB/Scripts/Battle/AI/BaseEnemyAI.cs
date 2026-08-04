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

            bool canUseSkill = true; // 추후 침묵/봉인 상태이상 추가 시 여기서 제어

            // 도발은 '강제'가 아니라 '우선'이다.
            // 하위 AI는 넘겨받은 목록을 GetValidTargetsForSkillData 결과와 교집합만 취하므로,
            // 도발 시전자가 스킬 사거리 필터(예: FrontFirst = 전열만)에서 탈락하면 교집합이 공집합이 되어
            // 턴이 그대로 날아갔다. 도발이 1턴 스턴처럼 동작하던 문제.
            // 도발 대상으로 행동이 성립하지 않으면 도발을 포기하고 원래 목록으로 다시 시도한다.
            BattleCharactor tauntTarget = GetTauntTarget(self, validTargets);
            if (tauntTarget != null)
            {
                EnemyActionDecision tauntDecision =
                    DetermineSpecificAction(self, new List<BattleCharactor> { tauntTarget }, canUseSkill);

                if (tauntDecision != null && !tauntDecision.Skip)
                {
                    return tauntDecision;
                }

                UnityEngine.Debug.LogWarning(
                    $"[EnemyAI] {self.UnitName}: 도발 대상 {tauntTarget.UnitName}이 스킬 타겟 필터에서 탈락했다. " +
                    "도발을 무시하고 다른 대상으로 폴백한다.");
            }

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
