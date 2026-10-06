using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnemyAI
{
    /// <summary>BattleCharactor.UnitId가 런타임 접미사를 포함해도 적 CSV 인덱스를 역산할 수 있도록 합니다.</summary>
    internal static class EnemyAiIndexHelper
    {
        /// <summary>EnemyScript 규칙: skillIndex = enemyIndex*10 + slot(1..9).</summary>
        internal static int TryResolveEnemyIndexFromClassSkill(BattleCharactor self)
        {
            if (self == null)
            {
                return 0;
            }

            int csi = self.ClassSkillIndex;
            if (csi < 11)
            {
                return 0;
            }

            for (int slot = 1; slot <= 9; slot++)
            {
                int diff = csi - slot;
                if (diff > 0 && diff % 10 == 0)
                {
                    int enemyId = diff / 10;
                    if (enemyId > 0)
                    {
                        return enemyId;
                    }
                }
            }

            return 0;
        }
    }

    public sealed class EAI_20001 : BaseEnemyAI
    {
        public override int Index => 20001;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self,
            List<BattleCharactor> validTargets,
            bool canUseSkill)
        {
            if (self == null || validTargets == null || validTargets.Count == 0)
            {
                return EnemyActionDecision.SkipTurn();
            }

            // 1) 행동 유형을 먼저 결정합니다.
            EnemyActionType actionType = EnemyActionType.ClassSkill;
            SkillData selectedSkill = null;
            if (canUseSkill && self.SelectedSkillData != null)
            {
                actionType = EnemyActionType.ClassSkill;
                selectedSkill = self.SelectedSkillData;
            }
            else if (self.EquippedWeaponData != null)
            {
                actionType = EnemyActionType.WeaponSkill;
            }

            // 2) 행동별 타겟팅 룰로 실제 후보군을 계산합니다.
            SkillData effectiveSkill = selectedSkill;
            if (actionType == EnemyActionType.WeaponSkill)
            {
                effectiveSkill = self.EquippedWeaponData.ToSkillData();
            }

            List<BattleCharactor> skillValidTargets = TargetingHelper.GetValidTargetsForSkillData(self, effectiveSkill);

            // 3) 부모에서 처리된 강제 타겟/공통 필터(validTargets)와 교집합합니다.
            var finalCandidates = new List<BattleCharactor>();
            for (int i = 0; i < skillValidTargets.Count; i++)
            {
                BattleCharactor candidate = skillValidTargets[i];
                if (candidate != null && validTargets.Contains(candidate))
                {
                    finalCandidates.Add(candidate);
                }
            }

            // 4) 전열 70% / 후열 30%를 먼저 결정한 뒤, 선택된 열에서 균등 랜덤으로 선택합니다.
            // 선택된 열에 유효 대상이 없으면 반대 열로 폴백해 턴이 소실되지 않게 합니다.
            BattleCharactor chosenTarget = GetRandomTargetByRow(finalCandidates);
            if (chosenTarget == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            return EnemyActionDecision.Create(chosenTarget, actionType, selectedSkill);
        }

        private static BattleCharactor GetRandomTargetByRow(List<BattleCharactor> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            var frontRow = new List<BattleCharactor>();
            var backRow = new List<BattleCharactor>();
            var unclassified = new List<BattleCharactor>();

            for (int i = 0; i < candidates.Count; i++)
            {
                BattleCharactor candidate = candidates[i];
                if (candidate == null || candidate.IsDead)
                {
                    continue;
                }

                bool isFront = TargetingHelper.IsUnitInFrontRow(candidate);
                bool isBack = TargetingHelper.IsUnitInBackRow(candidate);

                if (isFront)
                {
                    frontRow.Add(candidate);
                }

                if (isBack)
                {
                    backRow.Add(candidate);
                }

                if (!isFront && !isBack)
                {
                    unclassified.Add(candidate);
                }
            }

            bool chooseFront = UnityEngine.Random.Range(0, 100) < 70;
            List<BattleCharactor> selectedRow = chooseFront ? frontRow : backRow;
            List<BattleCharactor> fallbackRow = chooseFront ? backRow : frontRow;

            if (selectedRow.Count == 0)
            {
                selectedRow = fallbackRow;
            }

            if (selectedRow.Count == 0)
            {
                selectedRow = unclassified;
            }

            return selectedRow.Count == 0
                ? null
                : selectedRow[UnityEngine.Random.Range(0, selectedRow.Count)];
        }
    }

    public sealed class EAI_20002 : BaseEnemyAI
    {
        public override int Index => 20002;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self,
            List<BattleCharactor> validTargets,
            bool canUseSkill)
        {
            if (self == null || validTargets == null || validTargets.Count == 0)
            {
                return EnemyActionDecision.SkipTurn();
            }

            // 기획: 매 턴 원거리 사격. 같은 행에서는 HP가 낮은 대상,
            // 같은 행이 비면 가장 가까운 대상을 선택한다.
            if (!canUseSkill) return EnemyActionDecision.SkipTurn();
            SkillData shot = self.availableSkills?.FirstOrDefault(s => s != null && s.skillIndex == 200021);
            if (shot == null) return EnemyActionDecision.SkipTurn();
            List<BattleCharactor> candidates = TargetingHelper.GetValidTargetsForSkillData(self, shot)
                .Where(t => validTargets.Contains(t)).ToList();
            BattleCharactor target = EnemyAiTargetRules.PickSameRowThenLowestHpOrNearest(self, candidates);
            return target == null ? EnemyActionDecision.SkipTurn()
                : EnemyActionDecision.Create(target, EnemyActionType.ClassSkill, shot);
        }
    }

    // 빌런연합 방패병(20003): 아군 중 HP<30%가 있으면 '대신 맞기'로 그 아군 보호, 아니면 '방패 치기'(단일).
    // 보호 링크 만료는 BattleFlowManager 턴 시작에서 처리하므로 AI는 링크를 정리하지 않는다.
    public sealed class EAI_20003 : BaseEnemyAI
    {
        public override int Index => 20003;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self,
            List<BattleCharactor> validTargets,
            bool canUseSkill)
        {
            if (self == null || validTargets == null || validTargets.Count == 0)
            {
                return EnemyActionDecision.SkipTurn();
            }

            // 아군(같은 편) 중 HP 30% 미만 대상 → 대신 맞기.
            if (canUseSkill)
            {
                BattleCharactor ward = FindLowHpAlly(self, 0.3f);
                SkillData guardSkill = ResolveSkillBySlot(self, 2); // FV20003_2
                if (ward != null && guardSkill != null)
                {
                    return EnemyActionDecision.Create(ward, EnemyActionType.ClassSkill, guardSkill);
                }
            }

            // 아니면 방패 치기: 가장 가까운 적, 동거리면 무작위.
            SkillData strike = ResolveSkillBySlot(self, 1);         // FV20003_1
            List<BattleCharactor> candidates = strike == null ? new List<BattleCharactor>()
                : TargetingHelper.GetValidTargetsForSkillData(self, strike)
                    .Where(t => validTargets.Contains(t)).ToList();
            BattleCharactor target = EnemyAiTargetRules.PickNearest(self, candidates);
            if (!canUseSkill || strike == null || target == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            return EnemyActionDecision.Create(target, EnemyActionType.ClassSkill, strike);
        }

        // 같은 편(self 제외) 생존 중 HP 비율이 threshold 미만인 최저 HP 대상.
        private static BattleCharactor FindLowHpAlly(BattleCharactor self, float ratio)
        {
            BattleFlowManager flow = UnityEngine.Object.FindFirstObjectByType<BattleFlowManager>();
            if (flow == null)
            {
                return null;
            }

            BattleCharactor best = null;
            System.Collections.Generic.IReadOnlyList<BattleCharactor> parts = flow.Participants;
            for (int i = 0; i < parts.Count; i++)
            {
                BattleCharactor u = parts[i];
                if (u == null || u == self || u.IsDead || u.IsPlayer != self.IsPlayer) continue;
                if (u.MaxHp <= 0f || (u.CurrentHp / u.MaxHp) >= ratio) continue;
                if (best == null || u.CurrentHp < best.CurrentHp) best = u;
            }

            return best;
        }

        private static SkillData ResolveSkillBySlot(BattleCharactor self, int slot)
        {
            return self.availableSkills?.FirstOrDefault(s => s != null && s.skillIndex == (20003 * 10) + slot);
        }
    }

    public sealed class EAI_20005 : BaseEnemyAI
    {
        public override int Index => 20005;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
        {
            if (self == null || !canUseSkill || validTargets == null || validTargets.Count == 0)
                return EnemyActionDecision.SkipTurn();

            SkillData wave = self.availableSkills?.FirstOrDefault(s => s != null && s.skillIndex == 200051);
            SkillData combo = self.availableSkills?.FirstOrDefault(s => s != null && s.skillIndex == 200052);
            bool useWave = UnityEngine.Random.Range(0, 100) < 60;
            SkillData skill = useWave ? wave : combo;
            if (skill == null) skill = useWave ? combo : wave;
            if (skill == null) return EnemyActionDecision.SkipTurn();

            List<BattleCharactor> candidates = TargetingHelper.GetValidTargetsForSkillData(self, skill)
                .Where(t => validTargets.Contains(t)).ToList();
            // 연격 첫 타격은 전열 우선. 파동탄은 모든 유효 대상이 같은 확률이다.
            if (skill == combo)
            {
                List<BattleCharactor> front = candidates.Where(TargetingHelper.IsUnitInFrontRow).ToList();
                if (front.Count > 0) candidates = front;
            }
            if (candidates.Count == 0) return EnemyActionDecision.SkipTurn();
            BattleCharactor target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return EnemyActionDecision.Create(target, EnemyActionType.ClassSkill, skill);
        }
    }

    internal static class EnemyAiTargetRules
    {
        internal static BattleCharactor PickSameRowThenLowestHpOrNearest(
            BattleCharactor self, List<BattleCharactor> candidates)
        {
            if (self == null || candidates == null || candidates.Count == 0) return null;
            var selfCell = self.OccupiedCell;
            if (selfCell != null)
            {
                List<BattleCharactor> sameRow = candidates
                    .Where(t => t != null && t.OccupiedCell != null && t.OccupiedCell.Coords.y == selfCell.Coords.y)
                    .ToList();
                if (sameRow.Count > 0)
                {
                    float lowestHp = sameRow.Min(t => t.CurrentHp);
                    List<BattleCharactor> lowest = sameRow.Where(t => Mathf.Approximately(t.CurrentHp, lowestHp)).ToList();
                    return lowest[UnityEngine.Random.Range(0, lowest.Count)];
                }
            }
            return PickNearest(self, candidates);
        }

        internal static BattleCharactor PickNearest(BattleCharactor self, List<BattleCharactor> candidates)
        {
            if (self == null || candidates == null || candidates.Count == 0) return null;
            var selfCell = self.OccupiedCell;
            if (selfCell == null) return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            int nearest = int.MaxValue;
            var ties = new List<BattleCharactor>();
            foreach (BattleCharactor target in candidates)
            {
                if (target == null || target.IsDead || target.OccupiedCell == null) continue;
                Vector2Int delta = target.OccupiedCell.Coords - selfCell.Coords;
                int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
                if (distance < nearest) { nearest = distance; ties.Clear(); }
                if (distance == nearest) ties.Add(target);
            }
            return ties.Count == 0 ? null : ties[UnityEngine.Random.Range(0, ties.Count)];
        }
    }
}
