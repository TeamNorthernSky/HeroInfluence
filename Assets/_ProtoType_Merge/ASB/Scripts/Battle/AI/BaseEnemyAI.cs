using System.Collections.Generic;
using System.Linq;
using ASB.Work.Battle.SkillExecution;

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
        // 단일 AI 인스턴스의 도발 공격 우선 탐색 동안만 true다.
        protected bool SelectingTauntAttack { get; private set; }

        protected static bool IsAttackSkill(SkillData skill)
        {
            if (skill == null) return false;

            // 표에 공격(0)으로 기재된 보호·소환 스킬은 실제 효과를 우선한다.
            if (skill.skillIndex == 200032 || skill.skillIndex == 400013 || skill.skillIndex == 400041)
                return false;
            if (SkillExecutionRegistry.TryGetHandler(skill.skillKey, out ISkillEffectHandler handler))
            {
                if (handler is GuardSkillHandler || handler is SummonSkillHandler)
                    return false;
                // 피해와 상태이상을 함께 주는 스킬은 표의 효과 분류가 디버프여도 공격이다.
                if (handler is TauntStrikeSkillHandler || handler is AoETauntStrikeSkillHandler
                    || handler is BleeedSkillHandler || handler is TargetHealBanSkill
                    || handler is HolyBulletHpRecoveryHandler)
                    return true;
            }
            return skill.classSkillEffect == 0;
        }

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

            // 도발자를 먼저 시도한다. 적 AI 대상 필터는 이 후보를 행 우선순위보다 먼저 적용한다.
            // 스킬의 고유 조건 등으로 행동이 성립하지 않으면 원래 목록으로 다시 시도한다.
            BattleCharactor tauntTarget = GetTauntTarget(self, validTargets);
            if (tauntTarget != null)
            {
                EnemyActionDecision tauntDecision;
                SelectingTauntAttack = true;
                try
                {
                    tauntDecision = DetermineSpecificAction(self, new List<BattleCharactor> { tauntTarget }, canUseSkill);
                }
                finally
                {
                    SelectingTauntAttack = false;
                }

                SkillData attackSkill = tauntDecision?.ActionType == EnemyActionType.WeaponSkill
                    ? self.EquippedWeaponData?.ToSkillData() : tauntDecision?.SelectedSkill;
                if (tauntDecision != null && !tauntDecision.Skip
                    && (tauntDecision.IsSelfAction || IsAttackSkill(attackSkill)))
                {
                    return tauntDecision;
                }

                UnityEngine.Debug.LogWarning(
                    $"[EnemyAI] {self.UnitName}: 도발 대상 {tauntTarget.UnitName}으로 행동할 수 없어 다른 대상을 시도한다.");
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
