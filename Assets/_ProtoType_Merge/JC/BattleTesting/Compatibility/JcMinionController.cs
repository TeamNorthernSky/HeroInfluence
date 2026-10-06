// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Unit/MinionController.cs
// 원본 객체: MinionController -> JcMinionController
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
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
public sealed class JcMinionController : MonoBehaviour
{
    private JcBossController _owner;
    private int _triggerSkillIndex;
    private int _reactionSkillIndex;

    /// <summary>이 미니언이 사용하면 보스 창구에 신호가 발생하는 스킬 index.</summary>
    public int TriggerSkillIndex => _triggerSkillIndex;

    /// <summary>소환 시점에 보스가 주입. Owner + 트리거/반응 스킬 index를 바인딩한다.</summary>
    // 원본 함수 대응: MinionController.Bind (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MinionController.cs)
    public void Bind(JcBossController owner, int triggerSkillIndex, int reactionSkillIndex)
    {
        _owner = owner;
        _triggerSkillIndex = triggerSkillIndex;
        _reactionSkillIndex = reactionSkillIndex;
    }

    /// <summary>
    /// 미니언이 트리거 스킬로 행동을 확정했을 때 1회 호출. 보스 창구에 반응 요청을 적재한다.
    /// Owner가 없으면(일반 전투 등) no-op.
    /// </summary>
    // 원본 함수 대응: MinionController.SignalTriggerSelected (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MinionController.cs)
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
