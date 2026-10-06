// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Command/SkillActionCommand.cs
// 원본 객체: SkillActionCommand -> JcSkillActionCommand
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
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
    public class JcSkillActionCommand : JcIBattleActionCommand
    {
        // 단일 타겟용
        private readonly BattleCharactor _actor;
        private readonly ISkillTarget _target;
        private readonly SkillData _skill;
        private readonly bool _playBasicAttackAnimation;
        private readonly bool _playTargetHitAnimation;
        private readonly Func<BattleHitResult> _onHitCallback;
        private readonly IReadOnlyList<BattleCharactor> _presentationTargets;

        // 다중 타겟(AoE)용
        private readonly HitDeliveryGate _deliveryGate;
        private readonly List<DamageContext> _aoeContexts;
        private readonly List<Func<BattleHitResult>> _aoeHitCallbacks;

        private readonly bool _isAoE;

        public int Depth { get; set; }

        /// <summary>단일 타겟 스킬(공격/힐).</summary>
        public JcSkillActionCommand(
            BattleCharactor actor,
            ISkillTarget target,
            SkillData skill,
            bool playBasicAttackAnimation,
            bool playTargetHitAnimation,
            Func<BattleHitResult> onHitCallback,
            HitDeliveryGate deliveryGate,
            IReadOnlyList<BattleCharactor> presentationTargets = null)
        {
            _actor = actor;
            _target = target;
            _skill = skill;
            _playBasicAttackAnimation = playBasicAttackAnimation;
            _playTargetHitAnimation = playTargetHitAnimation;
            _onHitCallback = onHitCallback;
            _deliveryGate = deliveryGate;
            _presentationTargets = presentationTargets;
            _isAoE = false;
        }

        /// <summary>다중 타겟(AoE) 스킬. 전 타겟이 동시에 피격 연출됩니다.</summary>
        public JcSkillActionCommand(
            List<DamageContext> aoeContexts,
            List<Func<BattleHitResult>> aoeHitCallbacks,
            HitDeliveryGate deliveryGate)
        {
            _deliveryGate = deliveryGate;
            _aoeContexts = aoeContexts;
            _aoeHitCallbacks = aoeHitCallbacks;
            _isAoE = true;
        }

        // 원본 함수 대응: SkillActionCommand.Execute (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Command/SkillActionCommand.cs)

        public IEnumerator Execute(JcBattleManager battleManager)
        {
            if (_isAoE)
            {
                yield return battleManager.StartCoroutine(battleManager.Presentation.RunAoESkillSequence(_aoeContexts, _aoeHitCallbacks, _deliveryGate));
                yield break;
            }

            yield return battleManager.StartCoroutine(battleManager.Presentation.RunSkillSequenceCore(
                _actor,
                _target,
                _skill,
                _playBasicAttackAnimation,
                _playTargetHitAnimation,
                _onHitCallback, _deliveryGate, _presentationTargets));
        }
    }
}
