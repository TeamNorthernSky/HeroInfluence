using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 생존 유닛을 Idle 상태로 복귀시킵니다.
    /// </summary>
    public class ReturnToIdleAction : BattleSequenceAction
    {
        private readonly BattleCharactor _unit;

        public ReturnToIdleAction(BattleCharactor unit)
        {
            _unit = unit;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_unit == null || _unit.IsDead)
            {
                yield break;
            }

            // HoldArrow 복원 (TargetArrowPrefab은 Instantiate 후 자동 Destroy됨). 미할당(궁수 아님)이면 스킵.
            UnitVisualProfile profile = _unit.GetComponent<UnitVisualProfile>();
            // 주의: Unity 오브젝트는 미할당 시 '가짜 null'이라 '?.'로는 걸러지지 않는다 → 반드시 '!= null' 체크.
            if (profile != null && profile.HoldArrow != null)
            {
                profile.HoldArrow.SetActive(true);
            }

            _unit.EnsureAnimationController();
            // 이미 Idle이거나 Idle로 전환 중이면(예: 클립의 AniEvent_ReturnIdle로 이미 블렌드 시작) 재전환하지 않는다.
            // 중복 CrossFade가 Idle을 프레임0으로 재시작시켜 포즈가 순간 튀는(내려갔다 올라오는) 현상을 방지.
            if (_unit.Anim != null && !_unit.Anim.IsInState("Idle"))
            {
                Debug.Log($"[IdleDiag] ReturnToIdleAction → PlayIdleAnimation (unit={_unit.name}) — 시퀀스 복귀가 Idle 전환 수행");
                _unit.Anim.PlayIdleAnimation();
            }
            else
            {
                Debug.Log($"[IdleDiag] ReturnToIdleAction → SKIP(이미 Idle/전환중) (unit={_unit.name})");
            }
            yield break;
        }
    }
}
