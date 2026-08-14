using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnemyAI
{
    // 포탑/4구역 율리아 진영 신규 AI.
    // 규칙: DetermineSpecificAction은 부작용 없는 순수 판정. 상태변경은 EnemyActionDecision.Commit으로만.
    // 자기행동(충전 시작/응축/불발)은 SelfAction(비-Skip)으로 반환해 도발 폴백 재호출을 유발하지 않는다.

    // 포탑(무인 중포탑): 기모으기(2턴 충전 → 발사). 대상 타일 고정(재타게팅 금지).
    public sealed class EAI_20004 : BaseEnemyAI
    {
        public override int Index => 20004;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
        {
            if (self == null || validTargets == null || validTargets.Count == 0)
            {
                return EnemyActionDecision.SkipTurn();
            }

            // 사이클: 충전 → 발사 → 휴식 → (반복). 모두 순수 판정, 상태변경은 Commit으로만.

            // [발사] 충전 중이면 고정 타일 점유자에게 단일공격. 발사 후 '휴식 예약'.
            if (self.IsCharging)
            {
                SkillData fireSkill = self.ReservedChargeSkill;
                BattleCharactor occupant = self.ResolveChargeOccupant();   // 고정 타일의 현재 점유자
                System.Action commit = () => { self.ClearCharge(); self.SetPendingRest(true); }; // 발사 정리 + 다음은 휴식(1회)

                // 점유자 사망/부재/아군 → 불발(피해 없음). 불발이어도 사이클상 다음은 휴식.
                if (fireSkill == null || occupant == null || occupant.IsDead || occupant.IsPlayer == self.IsPlayer)
                {
                    return EnemyActionDecision.SelfAction(commit);
                }

                return EnemyActionDecision.Create(occupant, EnemyActionType.ClassSkill, fireSkill, commit);
            }

            // [휴식] 발사 직후 1턴 쉼. 이 턴을 소비하고 휴식 예약 해제 → 다음은 충전.
            if (self.HasPendingRest)
            {
                return EnemyActionDecision.SelfAction(() => self.SetPendingRest(false));
            }

            // [충전] 사이클 시작. 대상 타일을 고정하고 발사 스킬 예약.
            if (!canUseSkill)
            {
                return EnemyActionDecision.SkipTurn();
            }

            SkillData chargeSkill = ResolveSkillBySlot(self, 1);          // FV20004_1 단일공격
            BattleCharactor target = GetLowestHpTarget(validTargets);
            if (chargeSkill == null || target == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            ASB.Work.BattleGrid.BattleGridManager gm = ASB.Work.BattleGrid.BattleGridManager.Instance;
            var cell = target.OccupiedCell ?? (gm != null ? gm.FindCellByUnit(target) : null);
            if (cell == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            Vector2Int lockCoords = cell.Coords;
            // 충전 시작(예약+마커)을 커밋에 담는다. 순수판정과 분리, 1회만.
            return EnemyActionDecision.SelfAction(() => self.BeginCharge(chargeSkill, lockCoords));
        }

        private static SkillData ResolveSkillBySlot(BattleCharactor self, int slot)
        {
            return self.availableSkills?.FirstOrDefault(s => s != null && s.skillIndex == (20004 * 10) + slot);
        }
    }

    // 나락의 증폭기: 응축(EnergyStack +1). 능력개방/페이즈는 미룸.
    public sealed class EAI_40002 : BaseEnemyAI
    {
        public override int Index => 40002;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
        {
            if (self == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            // 순수판정: 상태변경 없음. 커밋에서 정확히 +1(도발 이중호출·이중커밋 방지).
            return EnemyActionDecision.SelfAction(() => self.AddEnergyStack(1));
        }
    }

    // 공멸의 증폭기: 응축(EnergyStack +1). 나락과 동일(차이는 능력개방/phase3 — 미룸).
    public sealed class EAI_40003 : BaseEnemyAI
    {
        public override int Index => 40003;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
        {
            if (self == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            return EnemyActionDecision.SelfAction(() => self.AddEnergyStack(1));
        }
    }

    // 절망의 굴렁쇠: 돌진 + 대상 행 광역 + 자폭. 자해는 SelfDestructRowAoEHandler가 처리.
    public sealed class EAI_40005 : BaseEnemyAI
    {
        public override int Index => 40005;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
        {
            if (self == null || validTargets == null || validTargets.Count == 0)
            {
                return EnemyActionDecision.SkipTurn();
            }

            SkillData skill = ResolveSkillBySlot(self, 1);   // FV40005_1
            BattleCharactor target = GetLowestHpTarget(validTargets);
            if (skill == null || target == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            return EnemyActionDecision.Create(target, EnemyActionType.ClassSkill, skill);
        }

        private static SkillData ResolveSkillBySlot(BattleCharactor self, int slot)
        {
            return self.availableSkills?.FirstOrDefault(s => s != null && s.skillIndex == (40005 * 10) + slot);
        }
    }
}
