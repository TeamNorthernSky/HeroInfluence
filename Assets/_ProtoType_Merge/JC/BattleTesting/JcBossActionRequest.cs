using System;
using System.Collections;

namespace JC.BattleTesting
{
    // 원본 대응: EnemyActionDecision.Commit + EnemyScript.RunAITurn.
    // 보완: 선택 요청과 확정을 분리해 미래 직접 조작에서도 재선택/턴 넘김/한 번만 커밋을 보장합니다.
    public sealed class JcBossActionRequest
    {
        public int SkillId { get; }
        public BattleCharactor Target { get; }
        public bool Skip { get; }
        public bool Cancelled { get; private set; }
        public bool Committed { get; private set; }
        public JcBossActionRequest(int skillId, BattleCharactor target = null, bool skip = false)
        { SkillId = skillId; Target = target; Skip = skip; }
        public void Cancel() { if (!Committed) Cancelled = true; }
        public bool TryCommit() { if (Cancelled || Committed) return false; Committed = true; return true; }
    }

    // 선택 UI는 이번 범위 밖입니다. 공급자는 실패 이유를 받은 뒤 다시 선택하거나 Skip 요청을 반환할 수 있습니다.
    public interface IJcBossActionProvider
    {
        IEnumerator Select(BattleCharactor actor, string previousFailure, Action<JcBossActionRequest> complete);
        void CancelPending();
    }
}
