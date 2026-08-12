using UnityEngine;

/// <summary>
/// 보스가 소환한 미니언에 붙는 조합 컴포넌트. (구현지시서: 보스유닛_소환_창구스킬_페이즈AI §4)
///
/// 미니언 AI가 "트리거 스킬로 non-Skip 결정을 반환하기 직전"에만 <see cref="SignalTriggerSelected"/>를
/// 호출한다(§0-7 — 결정 함수는 여러 번 호출될 수 있으므로 확정 시점 1회만).
/// Owner(보스)는 크로스 GameObject 참조라 소환 시 <see cref="Bind"/>로 주입된다.
///
/// 의존은 단방향(이 컴포넌트 → 코어). 일반 유닛에는 붙지 않으므로 없으면 자동 no-op이 된다.
/// </summary>
public sealed class MinionController : MonoBehaviour
{
    private BossController _owner;
    private int _triggerSkillIndex;
    private int _reactionSkillIndex;

    /// <summary>이 미니언이 사용하면 보스 창구에 신호가 발생하는 스킬 index.</summary>
    public int TriggerSkillIndex => _triggerSkillIndex;

    /// <summary>소환 시점에 보스가 주입. Owner + 트리거/반응 스킬 index를 바인딩한다.</summary>
    public void Bind(BossController owner, int triggerSkillIndex, int reactionSkillIndex)
    {
        _owner = owner;
        _triggerSkillIndex = triggerSkillIndex;
        _reactionSkillIndex = reactionSkillIndex;
    }

    /// <summary>
    /// 미니언이 트리거 스킬로 행동을 확정했을 때 1회 호출. 보스 창구에 반응 요청을 적재한다.
    /// Owner가 없으면(일반 전투 등) no-op.
    /// </summary>
    public void SignalTriggerSelected()
    {
        if (_owner == null)
        {
            return;
        }

        _owner.Enqueue(new BossSkillRequest
        {
            BossSkillIndex = _reactionSkillIndex,
            Source = GetComponent<BattleCharactor>(),
        });
    }
}
