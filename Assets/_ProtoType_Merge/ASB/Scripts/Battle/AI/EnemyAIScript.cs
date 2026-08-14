using System.Collections.Generic;
using System.Globalization;
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

            // 4) 최종 후보군에서 성향(최저 HP)으로 선택합니다.
            BattleCharactor chosenTarget = GetLowestHpTarget(finalCandidates);
            if (chosenTarget == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            //Debug.Log(
            //    $"[EnemyAI/Debug] Lowest HP target selected: self={self.UnitName} -> target={chosenTarget.UnitName}, hp={chosenTarget.CurrentHp:0.#}");
            return EnemyActionDecision.Create(chosenTarget, actionType, selectedSkill);
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

            int enemyIndex = ResolveEnemyIndex(self);
            int skill1Index = (enemyIndex * 10) + 1;
            int skill2Index = (enemyIndex * 10) + 2;

            SkillData skill1 = self.availableSkills != null
                ? self.availableSkills.FirstOrDefault(s => s != null && s.skillIndex == skill1Index)
                : null;
            SkillData skill2 = self.availableSkills != null
                ? self.availableSkills.FirstOrDefault(s => s != null && s.skillIndex == skill2Index)
                : null;

            List<BattleCharactor> GetValidCandidates(SkillData skill)
            {
                if (skill == null)
                {
                    return new List<BattleCharactor>();
                }

                List<BattleCharactor> skillTargets = TargetingHelper.GetValidTargetsForSkillData(self, skill);
                return skillTargets.Intersect(validTargets).ToList();
            }

            EnemyActionType finalAction = EnemyActionType.ClassSkill;
            SkillData finalSkill;
            List<BattleCharactor> finalCandidates;

            if (canUseSkill)
            {
                int roll = UnityEngine.Random.Range(0, 100);
                SkillData primarySkill = (roll < 70) ? skill1 : skill2;
                SkillData secondarySkill = (roll < 70) ? skill2 : skill1;

                finalSkill = primarySkill;
                finalCandidates = GetValidCandidates(primarySkill);

                // 1) primary 스킬 타겟 불가 -> secondary 스킬 시도
                if (finalCandidates.Count == 0)
                {
                    finalSkill = secondarySkill;
                    finalCandidates = GetValidCandidates(secondarySkill);
                }
            }
            else
            {
                finalSkill = null;
                finalCandidates = new List<BattleCharactor>();
            }

            // 2) 스킬 경로 모두 실패 -> 스킵
            if (finalCandidates.Count == 0)
            {
                return EnemyActionDecision.SkipTurn();
            }

            BattleCharactor target = GetLowestHpTarget(finalCandidates);
            if (target == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            return EnemyActionDecision.Create(target, finalAction, finalSkill);
        }

        private static int ResolveEnemyIndex(BattleCharactor self)
        {
            if (self == null)
            {
                return 20002;
            }

            EnemyScript enemyScript = self.GetComponent<EnemyScript>();
            if (enemyScript != null
                && enemyScript.Data != null
                && !string.IsNullOrWhiteSpace(enemyScript.Data.Index)
                && int.TryParse(enemyScript.Data.Index.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            int fromSkill = EnemyAiIndexHelper.TryResolveEnemyIndexFromClassSkill(self);
            if (fromSkill > 0)
            {
                return fromSkill;
            }

            return 20002;
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

            // 아니면 방패 치기(단일)로 최저 HP 적 공격.
            SkillData strike = ResolveSkillBySlot(self, 1);         // FV20003_1
            BattleCharactor target = GetLowestHpTarget(validTargets);
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
}
