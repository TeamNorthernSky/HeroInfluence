using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 스킬 애니메이션 직전 공격자 소켓에 공격 이펙트를 생성합니다.
    /// BattleVisualDirector가 null이거나 프리팹이 없으면 무시하고 진행합니다.
    /// </summary>
    public class SpawnAttackEffectAction : BattleSequenceAction
    {
        private readonly BattleCharactor _actor;
        private readonly int _skillIndex;
        private readonly BattleVisualDirector _visual;

        public SpawnAttackEffectAction(BattleCharactor actor, int skillIndex, BattleVisualDirector visual)
        {
            _actor = actor;
            _skillIndex = skillIndex;
            _visual = visual;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            _visual?.PlayAttackEffect(_actor, _skillIndex);
            yield break;
        }
    }
}
