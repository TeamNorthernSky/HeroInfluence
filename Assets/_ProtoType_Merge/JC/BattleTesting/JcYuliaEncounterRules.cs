using System;
using System.Collections.Generic;

namespace JC.BattleTesting
{
    // 원본 대응: Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs
    //   CurrentPhase, NotifyBossHp, participant 파괴 기록, TryAdvancePhase, 율리아 예약 저장.
    // 보완 근거: 이번 합의 및 기획서 ver0.19 §7.3.1/7.3.2.
    // 원본과의 차이: 사건마다 즉시 전환하지 않고 전체 행동 완료 경계에서 한 번 판정합니다.
    // Unity 객체/테스트 UI에 의존하지 않습니다. 본게임 이식 시 사건 입력과 결과 적용을 연결합니다.
    public sealed class JcYuliaEncounterRules
    {
        private readonly HashSet<int> destroyedAmplifiers = new HashSet<int>();
        private readonly Dictionary<int, int> charges = new Dictionary<int, int>();
        public int Phase { get; private set; }
        public bool FixedPhase { get; }
        public int ReservedSocket { get; private set; }
        public int Energy1 { get; set; }
        public int Energy2 { get; set; }
        public int Overload { get; private set; }
        public int DestroyedAmplifierCount => destroyedAmplifiers.Count;
        public float VeilDamageMultiplier => 1f + Overload * 0.5f;

        public JcYuliaEncounterRules(int startPhase, bool fixedPhase)
        {
            if (startPhase < 1 || startPhase > 3) throw new ArgumentOutOfRangeException(nameof(startPhase));
            Phase = startPhase;
            FixedPhase = fixedPhase;
        }

        // 원본 대응: EncounterBlackboard 파괴 ID 집합. 반복 파괴로 1→2 조건을 중복 증가시키지 않습니다.
        public void AmplifierDestroyed(int templateId)
        {
            if (templateId != 40002 && templateId != 40003) throw new ArgumentOutOfRangeException(nameof(templateId));
            if (Phase == 1) destroyedAmplifiers.Add(templateId);
            if (Phase == 3)
            {
                charges[templateId] = Math.Max(0, GetCharge(templateId) - 3);
                Overload++;
            }
            // 증폭기 파괴 자체로 ReservedSocket을 취소하지 않습니다(현행 코드 재현).
        }

        // 원본 대응: TryAdvancePhase. 변경: HP≤50%의 3페이즈를 우선, 중간 2 진입 효과를 발생시키지 않습니다.
        public bool EvaluateAfterAction(float bossHpRatio)
        {
            if (FixedPhase || Phase == 3) return false;
            int next = bossHpRatio <= 0.5f ? 3 : Phase == 1 && destroyedAmplifiers.Count >= 2 ? 2 : Phase;
            if (next == Phase) return false;
            Phase = next;
            if (Phase == 3)
            {
                ReservedSocket = 0;
                Energy1 = Energy2 = 0;
                charges.Clear();
                Overload = 0;
            }
            return true;
        }

        // 원본 대응: EncounterBlackboard.TryReserveYulia/ConsumeYuliaReservationIfMatch.
        public bool TryReserve(int socket)
        {
            if (Phase >= 3 || ReservedSocket != 0 || socket < 1 || socket > 2) return false;
            ReservedSocket = socket;
            return true;
        }
        public int ConsumeReservation()
        {
            int result = ReservedSocket;
            ReservedSocket = 0;
            return result;
        }

        // 원본 미구현 부분 대응: EnemyAIScript_4Sector.cs AmplifierDecision의 phase>=3 분기.
        // 기획서 §7.3.2: 행동 종료 +1, 공격력 스택당 +20%, 파괴 시 -3, 장막 과부하 +1/+50%.
        public void AmplifierActionCompleted(int templateId)
        {
            if (Phase == 3) charges[templateId] = GetCharge(templateId) + 1;
        }
        public int GetCharge(int templateId) => charges.TryGetValue(templateId, out int value) ? value : 0;
        public float AmplifierAttackMultiplier(int templateId) => 1f + GetCharge(templateId) * 0.2f;

        // 원본 대응: BattleFlowManager.TryEvaluateBattleResult. 변경: 독립 승리목표 및 동시 달성 승리 우선.
        public static BattleResult EvaluateResult(bool objectiveDestroyed, bool hasAliveAlly)
            => objectiveDestroyed ? BattleResult.Victory : !hasAliveAlly ? BattleResult.Defeat : BattleResult.None;
    }
}
