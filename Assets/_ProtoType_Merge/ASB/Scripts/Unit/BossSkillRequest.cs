/// <summary>
/// 미니언 → 보스 창구에 적재되는 단일 요청.
/// 미니언이 트리거 스킬로 행동을 확정한 시점(구현지시서 §0-7)에 생성되어 BossController 큐에 들어가고,
/// 보스 AI가 자기 턴 결정을 확정하기 직전에 소비한다.
/// </summary>
public sealed class BossSkillRequest
{
    /// <summary>보스가 이 요청을 받아 사용할 스킬 index (enemyIndex*10+slot 규칙).</summary>
    public int BossSkillIndex;

    /// <summary>신호를 낸 미니언(미니언별 중복 억제·타깃 힌트용, 선택).</summary>
    public BattleCharactor Source;
}
