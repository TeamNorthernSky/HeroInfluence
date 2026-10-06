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
            if (self == null || validTargets == null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            // 사이클: 충전 → 발사 → 휴식 → (반복). 모두 순수 판정, 상태변경은 Commit으로만.

            // [발사] 충전 중이면 고정 타일을 중심으로 포격. 발사 후 '휴식 예약'.
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

            SkillData chargeSkill = ResolveSkillBySlot(self, 1);
            List<BattleCharactor> candidates = chargeSkill == null ? new List<BattleCharactor>()
                : TargetingHelper.GetValidTargetsForSkillData(self, chargeSkill)
                    .Where(t => validTargets.Contains(t)).ToList();
            BattleCharactor target = SelectMostClustered(candidates);
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

        private static BattleCharactor SelectMostClustered(List<BattleCharactor> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;
            BattleFlowManager flow = UnityEngine.Object.FindFirstObjectByType<BattleFlowManager>();
            IEnumerable<BattleCharactor> all = flow != null
                ? (IEnumerable<BattleCharactor>)flow.Participants
                : UnityEngine.Object.FindObjectsByType<BattleCharactor>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            int mostNearby = -1;
            var ties = new List<BattleCharactor>();
            foreach (BattleCharactor candidate in candidates)
            {
                if (candidate == null || candidate.IsDead || candidate.OccupiedCell == null) continue;
                Vector2Int center = candidate.OccupiedCell.Coords;
                int nearby = 0;
                foreach (BattleCharactor other in all)
                {
                    if (other == null || other == candidate || other.IsDead ||
                        other.IsPlayer != candidate.IsPlayer || other.OccupiedCell == null) continue;
                    Vector2Int delta = other.OccupiedCell.Coords - center;
                    if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1) nearby++;
                }
                if (nearby > mostNearby) { mostNearby = nearby; ties.Clear(); }
                if (nearby == mostNearby) ties.Add(candidate);
            }
            return ties.Count == 0 ? null : ties[UnityEngine.Random.Range(0, ties.Count)];
        }
    }

    // 나락의 증폭기(소켓1): phase1·2에서 확률로 능력개방(율리아 예약) 또는 응축, phase3은 나락의 폭풍 직접 시전. 상세는 AmplifierDecision.
    public sealed class EAI_40002 : BaseEnemyAI
    {
        public override int Index => 40002;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
            => AmplifierDecision.Decide(self, socket: 1, Index, validTargets, canUseSkill);
    }

    // 공멸의 증폭기(소켓2): 나락과 동일 로직, 소켓만 2. phase3은 공멸의 궤적 직접 시전.
    public sealed class EAI_40003 : BaseEnemyAI
    {
        public override int Index => 40003;

        protected override EnemyActionDecision DetermineSpecificAction(
            BattleCharactor self, List<BattleCharactor> validTargets, bool canUseSkill)
            => AmplifierDecision.Decide(self, socket: 2, Index, validTargets, canUseSkill);
    }

    /// <summary>
    /// 증폭기 공용 판정: phase1·2에서 EnergyStack 확률로 '능력개방'(율리아 EncounterBlackboard 예약) 시도, 아니면 '응축'(+1).
    /// 확률: 스택>=2 →100%, ==1 →50%, ==0 →불가. 라운드당 해방 1회(블랙보드 게이트).
    /// 순수 판정에서 roll 1회(게이트 닫혀 있으면 roll 안 함), 상태변경은 커밋에서만(도발 이중호출·이중커밋 안전).
    /// [2026-10-02 변경] 율리아에게 대기 중인 예약이 있으면 능력개방을 막는다(CanReserveYulia=false → 응축).
    /// 예전의 '소켓1 우선 덮어쓰기'와 '예약한 증폭기 사망 시 예약 무효'는 제거했다(EncounterBlackboard 참고).
    /// [2026-10-02 변경] phase>=3은 응축·능력개방 대신 매 턴 자기 3번 스킬(나락의 폭풍 / 공멸의 궤적)을 직접 시전한다.
    /// 예전에는 범위 밖이라 응축만 했다. 기획의 '충전 스택(공격력 +20%)'과 '파괴 시 장막 과부하'는 아직 미구현.
    /// </summary>
    internal static class AmplifierDecision
    {
        private const int DirectCastSlot = 3;

        public static EnemyActionDecision Decide(BattleCharactor self, int socket, int enemyIndex,
            List<BattleCharactor> validTargets, bool canUseSkill)
        {
            if (self == null) return EnemyActionDecision.SkipTurn();
            if (self.IsIncapacitated) return EnemyActionDecision.SkipTurn();

            EncounterBlackboard bb = UnityEngine.Object.FindFirstObjectByType<EncounterBlackboard>();
            int phase = bb != null ? bb.CurrentPhase : 1;

            if (phase >= 3) return DecideDirectCast(self, enemyIndex, validTargets, canUseSkill);

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

        // phase3: 자기 3번 스킬을 스킬 유효대상 ∩ validTargets 중 무작위 1명에게 시전한다(율리아 소켓1·2와 같은 타깃 규칙).
        // 응축은 기획상 비활성이므로 시전할 수 없으면 턴을 넘긴다.
        private static EnemyActionDecision DecideDirectCast(BattleCharactor self, int enemyIndex,
            List<BattleCharactor> validTargets, bool canUseSkill)
        {
            if (!canUseSkill || validTargets == null || validTargets.Count == 0) return EnemyActionDecision.SkipTurn();

            int skillIndex = enemyIndex * 10 + DirectCastSlot;
            SkillData skill = self.availableSkills?.FirstOrDefault(s => s != null && s.skillIndex == skillIndex);
            if (skill == null)
            {
                UnityEngine.Debug.LogWarning($"[EnemyAI/Amplifier] {self.UnitName}: phase3 직접 시전 스킬({skillIndex})을 찾지 못함 → 스킵");
                return EnemyActionDecision.SkipTurn();
            }

            var pool = new List<BattleCharactor>();
            List<BattleCharactor> candidates = TargetingHelper.GetValidTargetsForSkillData(self, skill);
            if (candidates != null)
                for (int i = 0; i < candidates.Count; i++)
                {
                    BattleCharactor c = candidates[i];
                    if (c != null && !c.IsDead && validTargets.Contains(c)) pool.Add(c);
                }
            if (pool.Count == 0) return EnemyActionDecision.SkipTurn();

            BattleCharactor target = pool[UnityEngine.Random.Range(0, pool.Count)];
            EncounterParticipant participant = self.GetComponent<EncounterParticipant>();
            return EnemyActionDecision.Create(target, EnemyActionType.ClassSkill, skill, () =>
            {
                participant?.SignalSkillUsed(skill.skillIndex);
                UnityEngine.Debug.Log($"[EnemyAI/Amplifier] phase3 직접 시전: {self.UnitName} {skill.skillName} → {target.UnitName}");
            });
        }
    }

    /// <summary>
    /// 율리아(40001): 예약(증폭기 능력개방)이 있으면 소켓1/2, 없으면 소켓3.
    /// [2026-10-02 변경] 예약이 있으면 페이즈와 무관하게 예약 스킬을 쓴다(phase>=3 포함, 예약한 증폭기가 죽었어도).
    /// 예전에는 phase>=3이면 예약을 무시하고 항상 소켓3이었다. 휴식 턴은 여전히 예약보다 우선 — 예약은 다음 스킬 턴으로 이월된다.
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

            // [한턴 쉼] 소켓3(소환) 시전 직후 1턴 휴식. 이 턴을 소비하고 예약 해제 → 다음 턴부터 정상 판정.
            // SelfAction(비-Skip)이라 도발 폴백 재호출을 유발하지 않는다. 예약 소비 없음.
            if (self.HasPendingRest)
                return EnemyActionDecision.SelfAction(() => self.SetPendingRest(false));

            if (!canUseSkill) return EnemyActionDecision.SkipTurn(); // 예약 소비 없이 스킵

            EncounterBlackboard bb = UnityEngine.Object.FindFirstObjectByType<EncounterBlackboard>();
            BattleFlowManager flow = UnityEngine.Object.FindFirstObjectByType<BattleFlowManager>();
            int phase = bb != null ? bb.CurrentPhase : 1;

            // (B) 예약 Peek만(소비 금지). [2026-10-02 변경] 예약이 있으면 페이즈와 무관하게 예약 소켓(예전: phase>=3은 항상 소켓3).
            int socket = 3;
            int reservedSocket = 0;
            BattleCharactor reservedSource = null;
            if (bb != null &&
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

            // 소켓3(소환)만 타깃=파이프라인 통과용이라 validTargets 폴백 허용. 소켓1·2(공격)는 스킬 유효대상 없으면 예약 유지 + Skip.
            BattleCharactor target = PickRandomTarget(self, validTargets, skill, allowValidTargetsFallback: socket == 3);
            if (target == null)
                return EnemyActionDecision.SkipTurn(); // 예약 미소비(공격 소켓은 사거리 규칙 준수)

            // (C) non-Skip 확정 직전에만 예약 소비(커밋에서 1회).
            bool consume = socket != 3 && bb != null;
            int consumeSocket = socket;
            BattleCharactor consumeSource = reservedSource;
            int logPhase = phase;
            bool restAfter = socket == 3;   // 소켓3(소환) 시전 후 다음 턴 한턴 쉼 예약
            return EnemyActionDecision.Create(target, EnemyActionType.ClassSkill, skill, () =>
            {
                if (consume) bb.ConsumeYuliaReservationIfMatch(consumeSocket, consumeSource);
                if (restAfter) self.SetPendingRest(true);
                UnityEngine.Debug.Log($"[EnemyAI/Yulia] phase{logPhase} 소켓{consumeSocket} 시전: {skill.skillName} → {target.UnitName}");
            });
        }

        private static SkillData ResolveSkillBySlot(BattleCharactor self, int slot)
            => self.availableSkills?.FirstOrDefault(s => s != null && s.skillIndex == (40001 * 10) + slot);

        // 무작위 유효 대상(열/행 광역은 대표 1명, 소환은 파이프라인용). 도발 시 validTargets가 도발대상만이라 자동 우선.
        private static BattleCharactor PickRandomTarget(
            BattleCharactor self, List<BattleCharactor> validTargets, SkillData skill, bool allowValidTargetsFallback)
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
            // 소환(소켓3)만: 스킬 유효대상이 비어도 살아있는 validTargets로 파이프라인 통과 보장. 공격 소켓은 폴백 금지.
            if (pool.Count == 0 && allowValidTargetsFallback)
            {
                for (int i = 0; i < validTargets.Count; i++)
                    if (validTargets[i] != null && !validTargets[i].IsDead) pool.Add(validTargets[i]);
            }
            return pool.Count == 0 ? null : pool[UnityEngine.Random.Range(0, pool.Count)];
        }
    }

    // 절망의 굴렁쇠: 자기 행(같은 Coords.y, 패턴 3 방향)의 히어로 진영으로 돌진 + 행 광역 + 자폭. 자해는 SelfDestructRowAoEHandler가 처리.
    // - 전열/후열 무관하게 같은 행만 노린다. 행 전체 피해는 스킬 범위 [3,7]이 담당하므로 대상은 돌진이 먼저 닿는 쪽(가까운 열)을 고른다.
    // - 전장 전체에서 같은 행에 히어로가 없으면 돌진할 곳이 없으므로 피해 없이 사라진다(최대체력 자해 → 사망 → 시체 제거).
    // - 도발 대상이 다른 행이면 Skip을 돌려주고, BaseEnemyAI가 도발을 포기해 원래 목록으로 다시 고른다.
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
            if (skill == null || !TryGetCoords(self, out Vector2Int selfCoords))
            {
                return EnemyActionDecision.SkipTurn();
            }

            BattleCharactor target = FindSameRowTarget(self, selfCoords, validTargets);
            if (target != null)
            {
                return EnemyActionDecision.Create(target, EnemyActionType.ClassSkill, skill);
            }

            // 도발 호출이면 목록이 도발자 1명뿐이므로 전장 전체로 다시 확인한다.
            // 같은 행에 히어로가 남아 있으면 Skip → BaseEnemyAI가 도발을 포기하고 원래 목록으로 재호출한다.
            if (FindSameRowTarget(self, selfCoords, GetAllOpponents(validTargets)) != null)
            {
                return EnemyActionDecision.SkipTurn();
            }

            return EnemyActionDecision.SelfAction(() =>
            {
                if (!self.IsDead)
                {
                    self.TakeDamage(self.MaxHp);
                }
            });
        }

        // 전장의 살아있는 히어로 전체. BattleFlowManager가 없으면(테스트 등) 넘겨받은 목록을 쓴다.
        private static List<BattleCharactor> GetAllOpponents(List<BattleCharactor> fallback)
        {
            BattleFlowManager flow = UnityEngine.Object.FindFirstObjectByType<BattleFlowManager>();
            return flow != null ? flow.GetAlivePlayerUnits() : fallback;
        }

        // 같은 행의 상대 진영 유닛 중 시전자와 x가 가장 가까운(먼저 닿는) 대상.
        private static BattleCharactor FindSameRowTarget(BattleCharactor self, Vector2Int selfCoords, List<BattleCharactor> candidates)
        {
            BattleCharactor best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                BattleCharactor c = candidates[i];
                if (c == null || c.IsDead || c.IsPlayer == self.IsPlayer
                    || !TryGetCoords(c, out Vector2Int coords) || coords.y != selfCoords.y)
                {
                    continue;
                }

                int distance = Mathf.Abs(coords.x - selfCoords.x);
                if (distance < bestDistance)
                {
                    best = c;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static bool TryGetCoords(BattleCharactor unit, out Vector2Int coords)
        {
            ASB.Work.BattleGrid.GridCell cell = unit.OccupiedCell;
            if (cell == null)
            {
                ASB.Work.BattleGrid.BattleGridManager gm = ASB.Work.BattleGrid.BattleGridManager.Instance;
                cell = gm != null ? gm.FindCellByUnit(unit) : null;
            }

            coords = cell != null ? cell.Coords : default;
            return cell != null;
        }

        private static SkillData ResolveSkillBySlot(BattleCharactor self, int slot)
        {
            return self.availableSkills?.FirstOrDefault(s => s != null && s.skillIndex == (40005 * 10) + slot);
        }
    }
}
