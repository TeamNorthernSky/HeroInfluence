using System.Collections.Generic;



namespace EnemyAI
{
    public sealed class EAI_20001 : IEnemyAI
    {
        public int Index => 20001;

        public EnemyActionDecision DecideAction(BattleCharactor self, List<BattleCharactor> targets)
        {
            if (self == null || targets == null || targets.Count == 0)
            {
                return null;
            }

            BattleCharactor tauntSource = self.GetTauntSource();
            if (tauntSource != null
                && !tauntSource.IsDead
                && targets.Contains(tauntSource))
            {
                return BuildDecision(self, tauntSource);
            }

            // 타겟: 생존 + 반대 진영 중 현재 HP가 가장 낮은 유닛
            BattleCharactor chosenTarget = null;
            for (int i = 0; i < targets.Count; i++)
            {
                BattleCharactor candidate = targets[i];
                if (candidate == null || candidate.IsDead)
                {
                    continue;
                }

                if (candidate.IsPlayer == self.IsPlayer)
                {
                    continue;
                }

                if (chosenTarget == null || candidate.CurrentHp < chosenTarget.CurrentHp)
                {
                    chosenTarget = candidate;
                }
            }

            if (chosenTarget == null)
            {
                return null;
            }

            return BuildDecision(self, chosenTarget);
        }

        private static EnemyActionDecision BuildDecision(BattleCharactor self, BattleCharactor chosenTarget)
        {
            EnemyActionType actionType = EnemyActionType.BasicAttack;
            SkillData selectedSkill = null;
            if (self.SelectedSkillData != null)
            {
                actionType = EnemyActionType.ClassSkill;
                selectedSkill = self.SelectedSkillData;
            }
            else if (self.EquippedWeaponData != null)
            {
                actionType = EnemyActionType.WeaponSkill;
            }

            return new EnemyActionDecision
            {
                Target = chosenTarget,
                ActionType = actionType,
                SelectedSkill = selectedSkill
            };
        }
    }
}
