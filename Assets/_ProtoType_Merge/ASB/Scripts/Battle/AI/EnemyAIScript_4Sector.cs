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

    // 나락의 증폭기(소켓1): phase1·2에서 확률로 능력개방(율리아 예약) 또는 응축. 상세는 AmplifierDecision.
    public sealed class EAI_40002 : BaseEnemyAI
    {
        public override int Index => 40002;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
            => AmplifierDecision.Decide(self, socket: 1);
    }

    // 공멸의 증폭기(소켓2): 나락과 동일 로직, 소켓만 2.
    public sealed class EAI_40003 : BaseEnemyAI
    {
        public override int Index => 40003;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
            => AmplifierDecision.Decide(self, socket: 2);
    }

    /// <summary>
    /// 증폭기 공용 판정: phase1·2에서 EnergyStack 확률로 '능력개방'(율리아 EncounterBlackboard 예약) 시도, 아니면 '응축'(+1).
    /// 확률: 스택>=2 →100%, ==1 →50%, ==0 →불가. 라운드당 해방 1회(블랙보드 게이트). 소켓1 우선.
    /// 순수 판정에서 roll 1회(게이트 닫혀 있으면 roll 안 함), 상태변경은 커밋에서만(도발 이중호출·이중커밋 안전).
    /// 예약은 BattleCharactor(self)로 저장 → 생존검사는 flow.Participants. phase>=3 증폭기 직접시전은 범위 밖(응축 유지).
    /// </summary>
    internal static class AmplifierDecision
    {
        public static EnemyActionDecision Decide(BattleCharactor self, int socket)
        {
            if (self == null) return EnemyActionDecision.SkipTurn();
            if (self.IsIncapacitated) return EnemyActionDecision.SkipTurn();

            EncounterBlackboard bb = UnityEngine.Object.FindFirstObjectByType<EncounterBlackboard>();
            int phase = bb != null ? bb.CurrentPhase : 1;

            bool tryRelease = false;
            if (phase <= 2 && bb != null && self.EnergyStack >= 1 && bb.CanReserveYulia(socket))
            {
                tryRelease = self.EnergyStack >= 2 || UnityEngine.Random.value < 0.5f;
            }

            return EnemyActionDecision.SelfAction(() =>
            {
                if (tryRelease && bb != null && bb.TryReserveYulia(socket, self))
                {
                    self.ResetEnergyStack();
                    UnityEngine.Debug.Log($"[EnemyAI/Amplifier] 능력개방: {self.UnitName} → 율리아 소켓{socket} 예약");
                }
                else
                {
                    self.AddEnergyStack(1);
                    UnityEngine.Debug.Log($"[EnemyAI/Amplifier] 응축: {self.UnitName} EnergyStack={self.EnergyStack}");
                }
            });
        }
    }

    /// <summary>
    /// 율리아(40001): phase1·2엔 예약(증폭기 능력개방) 있으면 소켓1/2, 없으면 소켓3. phase>=3은 항상 소켓3.
    /// 예약은 Peek만 → 스킬·타깃 확정 후 non-Skip 반환의 커밋에서만 소비(도발 이중호출 안전).
    /// 소켓1=열 광역, 소켓2=행 광역(대표 대상 1명 전달), 소켓3=소환(BossController 필요, 대상은 파이프라인 통과용).
    /// 타깃: 스킬 유효대상 ∩ validTargets 중 '무작위'(GetLowestHp 아님). 후보 없으면 예약 미소비 + Skip.
    /// </summary>
    public sealed class EAI_40001 : BaseEnemyAI
    {
        public override int Index => 40001;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
        {
            if (self == null || validTargets == null || validTargets.Count == 0)
                return EnemyActionDecision.SkipTurn();

            // (A) 안전시점: 지연된 페이즈 효과(부활/소환/보스교체) 드레인. 멱등 → 이중 호출 안전.
            BossController boss = self.GetComponent<BossController>();
            if (boss != null) boss.DrainPhaseEffectsAtTurnStart();

            if (!canUseSkill) return EnemyActionDecision.SkipTurn(); // 예약 소비 없이 스킵

            EncounterBlackboard bb = UnityEngine.Object.FindFirstObjectByType<EncounterBlackboard>();
            BattleFlowManager flow = UnityEngine.Object.FindFirstObjectByType<BattleFlowManager>();
            int phase = bb != null ? bb.CurrentPhase : 1;

            // (B) 예약 Peek만(소비 금지). phase>=3은 항상 소켓3.
            int socket = 3;
            int reservedSocket = 0;
            BattleCharactor reservedSource = null;
            if (phase <= 2 && bb != null &&
                bb.TryPeekYuliaReservation(flow, out reservedSocket, out reservedSource))
            {
                socket = reservedSocket;
            }

            SkillData skill = ResolveSkillBySlot(self, socket);
            if (skill == null)
            {
                UnityEngine.Debug.LogWarning($"[EnemyAI/Yulia] 소켓{socket} 스킬({(40001 * 10) + socket})을 찾지 못함 → 스킵");
                return EnemyActionDecision.SkipTurn(); // 예약 미소비
            }

            BattleCharactor target = PickRandomTarget(self, validTargets, skill);
            if (target == null)
                return EnemyActionDecision.SkipTurn(); // 예약 미소비

            // (C) non-Skip 확정 직전에만 예약 소비(커밋에서 1회).
            bool consume = socket != 3 && bb != null;
            int consumeSocket = socket;
            BattleCharactor consumeSource = reservedSource;
            int logPhase = phase;
            return EnemyActionDecision.Create(target, EnemyActionType.ClassSkill, skill, () =>
            {
                if (consume) bb.ConsumeYuliaReservationIfMatch(consumeSocket, consumeSource);
                UnityEngine.Debug.Log($"[EnemyAI/Yulia] phase{logPhase} 소켓{consumeSocket} 시전: {skill.skillName} → {target.UnitName}");
            });
        }

        private static SkillData ResolveSkillBySlot(BattleCharactor self, int slot)
            => self.availableSkills?.FirstOrDefault(s => s != null && s.skillIndex == (40001 * 10) + slot);

        // 무작위 유효 대상(열/행 광역은 대표 1명, 소환은 파이프라인용). 도발 시 validTargets가 도발대상만이라 자동 우선.
        private static BattleCharactor PickRandomTarget(
            BattleCharactor self, List<BattleCharactor> validTargets, SkillData skill)
        {
            var pool = new List<BattleCharactor>();
            List<BattleCharactor> candidates = TargetingHelper.GetValidTargetsForSkillData(self, skill);
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    BattleCharactor c = candidates[i];
                    if (c != null && !c.IsDead && validTargets.Contains(c)) pool.Add(c);
                }
            }
            if (pool.Count == 0)   // 스킬 필터로 비면(예: 소환) validTargets에서 무작위 → 파이프라인 통과 보장
            {
                for (int i = 0; i < validTargets.Count; i++)
                    if (validTargets[i] != null && !validTargets[i].IsDead) pool.Add(validTargets[i]);
            }
            return pool.Count == 0 ? null : pool[UnityEngine.Random.Range(0, pool.Count)];
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
