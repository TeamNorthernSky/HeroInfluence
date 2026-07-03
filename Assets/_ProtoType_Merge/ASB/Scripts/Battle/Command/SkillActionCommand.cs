using System;
using System.Collections;
using System.Collections.Generic;
using ASB.Work.Battle.Core;

namespace ASB.Work.Battle.Command
{
    /// <summary>
    /// 스킬(공격/힐) 실행 1건. 타겟이 1명이면 단일 연출(RunSkillSequenceCore, ResolveHitAction),
    /// 여러 명이면 동시 연출(RunAoESkillSequence, AoEApplyDamageAction)을 사용합니다.
    /// </summary>
    public class SkillActionCommand : IBattleActionCommand
    {
        // 단일 타겟용
        private readonly BattleCharactor _actor;
        private readonly BattleCharactor _target;
        private readonly SkillData _skill;
        private readonly bool _playBasicAttackAnimation;
        private readonly bool _playTargetHitAnimation;
        private readonly Func<BattleHitResult> _onHitCallback;

        // 다중 타겟(AoE)용
        private readonly List<DamageContext> _aoeContexts;
        private readonly List<Func<BattleHitResult>> _aoeHitCallbacks;

        private readonly bool _isAoE;

        /// <summary>단일 타겟 스킬(공격/힐).</summary>
        public SkillActionCommand(
            BattleCharactor actor,
            BattleCharactor target,
            SkillData skill,
            bool playBasicAttackAnimation,
            bool playTargetHitAnimation,
            Func<BattleHitResult> onHitCallback)
        {
            _actor = actor;
            _target = target;
            _skill = skill;
            _playBasicAttackAnimation = playBasicAttackAnimation;
            _playTargetHitAnimation = playTargetHitAnimation;
            _onHitCallback = onHitCallback;
            _isAoE = false;
        }

        /// <summary>다중 타겟(AoE) 스킬. 전 타겟이 동시에 피격 연출됩니다.</summary>
        public SkillActionCommand(List<DamageContext> aoeContexts, List<Func<BattleHitResult>> aoeHitCallbacks)
        {
            _aoeContexts = aoeContexts;
            _aoeHitCallbacks = aoeHitCallbacks;
            _isAoE = true;
        }

        public IEnumerator Execute(BattleManager battleManager)
        {
            if (_isAoE)
            {
                yield return battleManager.StartCoroutine(battleManager.RunAoESkillSequence(_aoeContexts, _aoeHitCallbacks));
                yield break;
            }

            yield return battleManager.StartCoroutine(battleManager.RunSkillSequenceCore(
                _actor,
                _target,
                _skill,
                _playBasicAttackAnimation,
                _playTargetHitAnimation,
                _onHitCallback));
        }
    }
}
