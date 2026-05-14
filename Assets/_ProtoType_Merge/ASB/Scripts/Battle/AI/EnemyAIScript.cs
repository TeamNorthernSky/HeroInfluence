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
            EnemyActionType actionType = EnemyActionType.BasicAttack;
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

            List<BattleCharactor> skillValidTargets = actionType == EnemyActionType.BasicAttack
                ? TargetingHelper.GetAllValidTargets(validTargets)
                : TargetingHelper.GetValidTargetsForSkillData(self, effectiveSkill);

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

            // 2) 스킬 경로 모두 실패 -> 평타 폴백
            if (finalCandidates.Count == 0)
            {
                finalSkill = null;
                finalAction = EnemyActionType.BasicAttack;

                HashSet<BattleCharactor> basicTargets = TargetingHelper.GetValidTargets(self, PendingActionType.BasicAttack);
                finalCandidates = basicTargets.Intersect(validTargets).ToList();
            }

            // 3) 평타도 불가 -> 스킵
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

            int enemyIndex =  ResolveEnemyIndex(self);
            int skill1Index = (enemyIndex * 10) + 1;
            int skill2Index = (enemyIndex * 10) + 2;

            SkillData skill1 = self.availableSkills != null
                ? self.availableSkills.FirstOrDefault(s => s != null && s.skillIndex == skill1Index)
                : null;
            SkillData skill2 = self.availableSkills != null
                ? self.availableSkills.FirstOrDefault(s => s != null && s.skillIndex == skill2Index)
                : null;

            float hpPercent = self.MaxHp > 0f ? (self.CurrentHp / self.MaxHp) * 100f : 0f;
            SkillData primarySkill = hpPercent > 60f ? skill1 : skill2;
            SkillData secondarySkill = hpPercent > 60f ? skill2 : skill1;

            List<BattleCharactor> GetValidCandidates(SkillData skill)
            {
                if (skill == null)
                {
                    return new List<BattleCharactor>();
                }

                List<BattleCharactor> candidates = TargetingHelper.GetValidTargetsForSkillData(self, skill);
                return candidates
                    .Where(t => t != null && validTargets.Contains(t))
                    .ToList();
            }

            BattleCharactor PickRandomTarget(List<BattleCharactor> candidates)
            {
                if (candidates == null || candidates.Count == 0)
                {
                    return null;
                }

                int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
                return candidates[randomIndex];
            }

            if (canUseSkill)
            {
                List<BattleCharactor> primaryCandidates = GetValidCandidates(primarySkill);
                if (primaryCandidates.Count > 0)
                {
                    BattleCharactor primaryTarget = PickRandomTarget(primaryCandidates);
                    if (primaryTarget != null)
                    {
                        return EnemyActionDecision.Create(primaryTarget, EnemyActionType.ClassSkill, primarySkill);
                    }
                }

                List<BattleCharactor> secondaryCandidates = GetValidCandidates(secondarySkill);
                if (secondaryCandidates.Count > 0)
                {
                    BattleCharactor secondaryTarget = PickRandomTarget(secondaryCandidates);
                    if (secondaryTarget != null)
                    {
                        return EnemyActionDecision.Create(secondaryTarget, EnemyActionType.ClassSkill, secondarySkill);
                    }
                }
            }

            // 스킬 실패 시 평타 폴백. 평타 후보도 validTargets 교집합만 허용합니다.
            HashSet<BattleCharactor> basicTargets = TargetingHelper.GetValidTargets(self, PendingActionType.BasicAttack);
            List<BattleCharactor> basicCandidates = basicTargets
                .Where(t => t != null && validTargets.Contains(t))
                .ToList();

            if (basicCandidates.Count == 0)
            {
                return EnemyActionDecision.SkipTurn();
            }

            BattleCharactor basicTarget = PickRandomTarget(basicCandidates);
            if (basicTarget == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            return EnemyActionDecision.Create(basicTarget, EnemyActionType.BasicAttack, null);
        }
        private static int ResolveEnemyIndex(BattleCharactor self)
        {
            if (self == null)
            {
                return 20003;
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

            return 20003;
        }
    }
}
