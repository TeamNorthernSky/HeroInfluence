using System.Collections.Generic;
using System.Linq;

namespace EnemyAI
{
    /// <summary>
    /// 보스 AI. (구현지시서: 보스유닛_소환_창구스킬_페이즈AI §5)
    ///
    /// 순서: (A) 지연 페이즈 효과 드레인(안전시점) → (B) 창구 Peek → 확정 시 Consume → (C) 페이즈별 스킬 선택.
    /// 부작용 지점은 (A) 멱등 드레인과 (B) non-Skip 반환 직전 단일 Consume 뿐이라, BaseEnemyAI의
    /// 도발 경로 이중 호출(§0-7)에도 안전하다.
    ///
    /// EnemyData.UnitAI = 20101 로 지정된 보스가 이 AI를 사용한다. (index는 데이터 스키마에 맞게 조정)
    /// </summary>
    public sealed class EAI_20101 : BaseEnemyAI
    {
        public override int Index => 20101;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
        {
            if (self == null || validTargets == null || validTargets.Count == 0)
            {
                return EnemyActionDecision.SkipTurn();
            }

            BossController boss = self.GetComponent<BossController>();

            // (A) 안전시점: 지연된 페이즈 효과(웨이브 소환 등) 실행. 비었으면 no-op → 이중 호출 안전.
            if (boss != null)
            {
                boss.DrainPhaseEffectsAtTurnStart();
            }

            // (B) 창구: 조회만(Peek). 이 요청으로 non-Skip 반환이 확정될 때만 Consume.
            if (canUseSkill && boss != null && boss.TryPeek(out BossSkillRequest req) && req != null)
            {
                SkillData reqSkill = ResolveSkill(self, req.BossSkillIndex);
                BattleCharactor reqTarget = reqSkill != null ? PickTarget(self, validTargets, reqSkill) : null;
                if (reqSkill != null && reqTarget != null)
                {
                    boss.Consume(out _); // non-Skip 반환 직전, 정확히 1회
                    return EnemyActionDecision.Create(reqTarget, EnemyActionType.ClassSkill, reqSkill);
                }
                // 해석/타깃 실패: 소비하지 않고 기본 로직으로(요청 보존).
            }

            // (C) Tier A: 페이즈 = HP 읽기(부작용 없음) → 페이즈별 스킬 풀
            int phase = boss != null ? boss.ComputePhase() : 1;
            SkillData skill = PickSkillForPhase(self, phase, canUseSkill);
            if (skill == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            BattleCharactor target = PickTarget(self, validTargets, skill);
            return target != null
                ? EnemyActionDecision.Create(target, EnemyActionType.ClassSkill, skill)
                : EnemyActionDecision.SkipTurn();
        }

        // 페이즈별 스킬 풀: enemyIndex*10 + slot 규칙. 기본 phase1→slot1, phase>=2→slot2.
        // TODO: 보스별 페이즈-스킬 매핑을 데이터로 옮기려면 여기를 확장.
        private static SkillData PickSkillForPhase(BattleCharactor self, int phase, bool canUseSkill)
        {
            if (!canUseSkill) return null;

            int enemyIndex = ResolveEnemyIndex(self);
            if (enemyIndex > 0)
            {
                int slot = phase >= 2 ? 2 : 1;
                int idx = (enemyIndex * 10) + slot;
                SkillData mapped = ResolveSkill(self, idx);
                if (mapped != null) return mapped;
            }

            // 폴백: 선택 스킬.
            return self.SelectedSkillData;
        }

        private static BattleCharactor PickTarget(BattleCharactor self, List<BattleCharactor> validTargets, SkillData skill)
        {
            List<BattleCharactor> candidates = TargetingHelper.GetValidTargetsForSkillData(self, skill);
            BattleCharactor best = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                BattleCharactor c = candidates[i];
                if (c == null || c.IsDead || !validTargets.Contains(c)) continue;
                if (best == null || c.CurrentHp < best.CurrentHp) best = c; // 최저 HP 성향(데이터로 조정 가능)
            }
            return best;
        }

        private static SkillData ResolveSkill(BattleCharactor self, int skillIndex)
        {
            SkillData s = self.availableSkills != null
                ? self.availableSkills.FirstOrDefault(x => x != null && x.skillIndex == skillIndex)
                : null;
            if (s != null) return s;
            return DHCsvTemplateCatalog.Instance != null ? DHCsvTemplateCatalog.Instance.GetSkillTemplate(skillIndex) : null;
        }

        private static int ResolveEnemyIndex(BattleCharactor self)
        {
            EnemyScript enemyScript = self.GetComponent<EnemyScript>();
            if (enemyScript != null && enemyScript.Data != null
                && !string.IsNullOrWhiteSpace(enemyScript.Data.Index)
                && int.TryParse(enemyScript.Data.Index.Trim(), out int parsed))
            {
                return parsed;
            }

            int fromSkill = EnemyAiIndexHelper.TryResolveEnemyIndexFromClassSkill(self);
            return fromSkill > 0 ? fromSkill : 0;
        }
    }

    /// <summary>
    /// 미니언 AI. (구현지시서 §4/§5)
    ///
    /// 평범한 결정(트리거/선택 스킬로 최저 HP 타깃)을 하되, 트리거 스킬로 non-Skip 결정을 확정하는 직전에만
    /// 보스 창구에 신호한다(§0-7 — 후보 선정 중간·Skip 경로에서는 호출 금지).
    ///
    /// EnemyData.UnitAI = 20102 로 지정된 미니언이 이 AI를 사용한다.
    /// </summary>
    public sealed class EAI_20102 : BaseEnemyAI
    {
        public override int Index => 20102;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
        {
            if (self == null || validTargets == null || validTargets.Count == 0)
            {
                return EnemyActionDecision.SkipTurn();
            }

            SkillData skill = canUseSkill ? self.SelectedSkillData : null;
            if (skill == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            BattleCharactor target = PickTarget(self, validTargets, skill);
            if (target == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            // non-Skip 확정 직전: 트리거 스킬이면 보스 창구에 신호(정확히 1회, §0-7).
            MinionController minion = self.GetComponent<MinionController>();
            if (minion != null && skill.skillIndex == minion.TriggerSkillIndex)
            {
                minion.SignalTriggerSelected();
            }

            return EnemyActionDecision.Create(target, EnemyActionType.ClassSkill, skill);
        }

        private static BattleCharactor PickTarget(BattleCharactor self, List<BattleCharactor> validTargets, SkillData skill)
        {
            List<BattleCharactor> candidates = TargetingHelper.GetValidTargetsForSkillData(self, skill);
            BattleCharactor best = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                BattleCharactor c = candidates[i];
                if (c == null || c.IsDead || !validTargets.Contains(c)) continue;
                if (best == null || c.CurrentHp < best.CurrentHp) best = c;
            }
            return best;
        }
    }
}
