using System.Collections.Generic;

// 대신 맞기(피해 가로채기): 가디언(A)이 보호 대상(B)을 소유하는 단일 진실원본.
// - B에 상태를 걸지 않는다(만료 타이밍이 B 턴에 얽히는 것을 피함). 만료는 A의 턴 시작에서 처리(BattleFlowManager).
// - 전투 시작 리셋은 BattleCharactor.PersistenceEquipment.cs의 MarkInitializedFromDataPipeline에서.
public partial class BattleCharactor
{
    private BattleCharactor guardedAlly;                 // A가 보호 중인 B (없으면 null)
    public BattleCharactor GuardedAlly => guardedAlly;
    public bool IsGuarding => guardedAlly != null && !guardedAlly.IsDead;

    public void BeginGuard(BattleCharactor ally) { guardedAlly = ally; }
    public void ClearGuard() { guardedAlly = null; }
}

// B의 가디언을 후보 목록에서 찾는다(단일 소스 스캔, B에는 역참조를 두지 않음 → desync 회피).
public static class GuardLink
{
    public static BattleCharactor FindGuardianFor(BattleCharactor ward, IReadOnlyList<BattleCharactor> candidates)
    {
        if (ward == null || candidates == null)
        {
            return null;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            BattleCharactor u = candidates[i];
            if (u != null && u != ward && !u.IsDead && u.GuardedAlly == ward)
            {
                return u;
            }
        }

        return null;
    }
}
