// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs
// 원본 객체: EncounterBlackboard -> JcEncounterBlackboard
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>전환 조건 종류(세부 규칙의 어휘). 필요 시 여기만 확장.</summary>
public enum JcPhaseConditionType
{
    ParticipantDestroyCount,   // 파괴된 참여자 수 >= Threshold
    BossHpRatioAtMost,         // 보스 HP 비율 <= Threshold
    // 확장 예: MinionAllDead / TurnCountAtLeast / ...
}

/// <summary>페이즈 전환 규칙(세부=데이터). "이 페이즈에서 조건 충족 시 → 저 페이즈로".</summary>
[Serializable]
public struct JcPhaseTransition
{
    [Tooltip("이 페이즈일 때만 검사")]      public int FromPhase;
    [Tooltip("전환 조건 종류")]              public JcPhaseConditionType Condition;
    [Tooltip("조건 임계값(수/비율)")]        public float Threshold;
    [Tooltip("조건 충족 시 이동할 페이즈")]  public int ToPhase;
}

/// <summary>
/// 전투 공유 상태 + 페이즈 상태머신. (구현지시서: 4구역율리아_증폭기창구_페이즈상태머신)
///
/// ★ 뼈대(범용, 재사용): 상태 저장 · 전환 "실행" · 라운드 리셋 · 페이즈 이벤트.
/// ★ 세부(보스마다 데이터): 전환 규칙(_transitions) · 참여자 id 값. 코드에 하드코딩 금지.
///
/// - 페이즈의 단일 진실원본. 보스/참여자/AI는 여기서 "읽기만" 한다.
/// - 전환은 게임 이벤트로만: 참여자 파괴 · 보스 HP. AI가 전환하지 않는다.
/// - 수명 = 전투 1판. 씬 배치라 보스 교체(FV40001→FV40004)에도 상태 유지.
/// </summary>
public sealed class JcEncounterBlackboard : MonoBehaviour
{
    [Header("페이즈 전환 규칙 (세부 = 보스마다 인스펙터로 구성)")]
    [Tooltip("율리아 예: [From=1, ParticipantDestroyCount, 2, To=2], [From=2, BossHpRatioAtMost, 0.5, To=3]")]
    [SerializeField] private List<JcPhaseTransition> _transitions = new List<JcPhaseTransition>();

    // ── 페이즈 (전진-only) ──
    public int CurrentPhase { get; private set; } = 1;
    // 원본 대응: TryAdvancePhase. JC 규칙이 행동 완료 시점을 소유하므로 즉시 판정은 중지합니다.
    public bool ExternalPhasePolicy { get; set; }
    public void SetTestPhase(int phase, bool notify)
    {
        CurrentPhase = phase;
        if (phase == 3) { _reservedSocket = 0; _reservedSource = null; _releaseUsedThisRound = false; }
        if (notify) PhaseAdvanced?.Invoke(phase);
    }

    /// <summary>페이즈가 실제로 전진했을 때만 발화(중복 없음). 무거운 효과는 구독자가 "지연 실행"할 것.</summary>
    public event Action<int> PhaseAdvanced;

    // ── 참여자 파괴 이력 ──
    private readonly HashSet<string> _destroyedParticipants = new HashSet<string>();
    public int DestroyedParticipantCount => _destroyedParticipants.Count;

    // ── 라운드 스킬 사용 (라운드 리셋) : key = participantId ──
    private readonly Dictionary<string, HashSet<int>> _skillsUsedThisRound = new Dictionary<string, HashSet<int>>();
    private int _lastRoundIndex = -1;

    // ── 율리아 스킬 예약 (증폭기 능력개방 → 율리아 소비) ──
    // ※ '증폭기→율리아 소켓 스킬' 전용. BossController의 BossSkillRequest 창구/페이즈효과 큐와는 별개 시스템.
    private int _reservedSocket;               // 0=없음, 1=나락(40002), 2=공멸(40003)
    private BattleCharactor _reservedSource;   // 예약한 증폭기(생존검사에 사용, 문자열 id 미사용)
    private bool _releaseUsedThisRound;        // 라운드당 능력개방 1회 게이트

    /// <summary>증폭기의 능력개방이 실제로 예약됐을 때 한 번 발생하는 연출 알림.</summary>
    public event Action<int, BattleCharactor> YuliaSkillReserved;

    private JcBattleFlowManager _flow;

    // 원본 함수 대응: EncounterBlackboard.OnEnable (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)

    private void OnEnable()
    {
        ResetAll();   // 전투 판 단위 리셋
        _flow = FindFirstObjectByType<JcBattleFlowManager>();
        if (_flow != null) _flow.OnTurnStarted += HandleTurnStarted;   // 라운드 경계 → 스킬 플래그 리셋
    }

    // 원본 함수 대응: EncounterBlackboard.OnDisable (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)

    private void OnDisable()
    {
        if (_flow != null) _flow.OnTurnStarted -= HandleTurnStarted;
    }

    // 라운드 증가 시 이번 라운드 스킬 기록을 비운다(NotifyRound 멱등).
    private void HandleTurnStarted(int roundIndex, BattleCharactor current) => NotifyRound(roundIndex);

    // 원본 함수 대응: EncounterBlackboard.ResetAll (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)

    public void ResetAll()
    {
        CurrentPhase = 1;
        _destroyedParticipants.Clear();
        _skillsUsedThisRound.Clear();
        _lastRoundIndex = -1;
        _reservedSocket = 0;          // ★ 예약 초기화(전투 재시작 누수 방지)
        _reservedSource = null;
        _releaseUsedThisRound = false;
    }

    // ── 쓰기 API ──

    /// <summary>참여자가 자기 OnDied에서 1회 호출. 파괴 기록 후 페이즈 전환을 평가.</summary>
    // 원본 함수 대응: EncounterBlackboard.MarkParticipantDestroyed (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)
    public void MarkParticipantDestroyed(string participantId)
    {
        if (string.IsNullOrEmpty(participantId)) return;
        if (_destroyedParticipants.Add(participantId))   // 새로 추가된 경우에만
        {
            TryAdvancePhase(1f);                          // 파괴발 전환은 HP 무관. HP는 무의미값 전달.
        }
    }

    /// <summary>참여자 AI가 스킬로 non-Skip 결정을 확정하기 직전 1회 호출.</summary>
    // 원본 함수 대응: EncounterBlackboard.MarkSkillUsedThisRound (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)
    public void MarkSkillUsedThisRound(string participantId, int skillIndex)
    {
        if (string.IsNullOrEmpty(participantId)) return;
        if (!_skillsUsedThisRound.TryGetValue(participantId, out HashSet<int> set))
        {
            set = new HashSet<int>();
            _skillsUsedThisRound[participantId] = set;
        }
        set.Add(skillIndex);
    }

    /// <summary>보스가 자기 OnHpChanged에서 호출. HP발 전환을 평가.</summary>
    public void NotifyBossHp(float bossHpRatio) => TryAdvancePhase(bossHpRatio);

    /// <summary>라운드 경계에서 호출. roundIndex가 증가했을 때만 라운드 스킬 기록을 비운다.</summary>
    // 원본 함수 대응: EncounterBlackboard.NotifyRound (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)
    public void NotifyRound(int roundIndex)
    {
        if (roundIndex == _lastRoundIndex) return;
        _lastRoundIndex = roundIndex;
        _skillsUsedThisRound.Clear();
        _releaseUsedThisRound = false;   // 라운드 경계마다 능력개방 게이트 리셋
    }

    /// <summary>재소환 시 파괴 이력에서 제거(선택). 상태 정합용.</summary>
    // 원본 함수 대응: EncounterBlackboard.ClearParticipantDestroyed (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)
    public void ClearParticipantDestroyed(string participantId)
    {
        if (!string.IsNullOrEmpty(participantId)) _destroyedParticipants.Remove(participantId);
    }

    /// <summary>
    /// 사망한 참여자가 만든 율리아 예약을 즉시 무효화한다.
    /// 이번 라운드에 능력개방을 사용했다는 게이트는 유지해, 사망으로 추가 예약 기회를 만들지 않는다.
    /// [2026-10-02 변경] 증폭기 사망 시 더 이상 호출하지 않는다 — 예약한 증폭기가 죽어도 율리아는 예약 스킬을
    /// 반드시 사용한다(JcEncounterParticipant.HandleDied 참고). 수동 정리용으로만 남긴다.
    /// </summary>
    // 원본 함수 대응: EncounterBlackboard.ClearYuliaReservationFrom (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)
    public void ClearYuliaReservationFrom(BattleCharactor source)
    {
        if (source == null || _reservedSource != source) return;

        _reservedSocket = 0;
        _reservedSource = null;
    }

    // ── 읽기 API ──

    // 원본 함수 대응: EncounterBlackboard.DidParticipantUseSkillThisRound (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)

    public bool DidParticipantUseSkillThisRound(string participantId, int skillIndex)
    {
        return _skillsUsedThisRound.TryGetValue(participantId, out HashSet<int> set) && set.Contains(skillIndex);
    }

    /// <summary>임의 참여자가 이번 라운드에 해당 스킬을 썼는지(게이팅 편의).</summary>
    // 원본 함수 대응: EncounterBlackboard.DidAnyParticipantUseSkillThisRound (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)
    public bool DidAnyParticipantUseSkillThisRound(int skillIndex)
    {
        foreach (KeyValuePair<string, HashSet<int>> kv in _skillsUsedThisRound)
        {
            if (kv.Value.Contains(skillIndex)) return true;
        }
        return false;
    }

    // ── 율리아 스킬 예약 API (증폭기 능력개방 ↔ 율리아 소비) ──

    /// <summary>
    /// 읽기전용: 이번 라운드 소켓 예약 가능 여부(확률 소비 전 검사).
    /// [2026-10-02 변경] 율리아에게 대기 중인 예약이 있으면 어떤 소켓도 예약할 수 없다(덮어쓰기 금지).
    /// 예전의 '소켓1이 대기 중인 소켓2 예약을 덮어쓰는 우선 규칙'은 제거했다. 라운드당 1회 게이트는 유지.
    /// </summary>
    // 원본 함수 대응: EncounterBlackboard.CanReserveYulia (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)
    public bool CanReserveYulia(int socket)
    {
        if (socket != 1 && socket != 2) return false;
        return _reservedSocket == 0 && !_releaseUsedThisRound;
    }

    /// <summary>
    /// 증폭기 능력개방 시 호출. 대기 예약이 없고 이번 라운드에 아직 개방이 없을 때만 성공.
    /// [2026-10-02 변경] 대기 예약 덮어쓰기(소켓1 우선) 제거 — 예약은 율리아가 쓸 때까지 고정된다.
    /// </summary>
    // 원본 함수 대응: EncounterBlackboard.TryReserveYulia (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)
    public bool TryReserveYulia(int socket, BattleCharactor source)
    {
        if (source == null || (socket != 1 && socket != 2))
        {
            Debug.LogWarning($"[Encounter] TryReserveYulia 잘못된 인자: socket={socket}, source={(source != null ? source.UnitName : "null")}");
            return false;
        }
        if (_reservedSocket != 0 || _releaseUsedThisRound) return false;

        _reservedSocket = socket; _reservedSource = source; _releaseUsedThisRound = true;
        YuliaSkillReserved?.Invoke(socket, source);
        return true;
    }

    /// <summary>
    /// 율리아가 조회만(소비 금지). 대기 예약이 있으면 true.
    /// [2026-10-02 변경] 예약한 증폭기의 생존·참여 여부를 더 이상 검사하지 않는다 — 증폭기가 죽어도 예약 스킬을 쓴다.
    /// flow 인자는 호출부 호환을 위해 남겼고 사용하지 않는다. source는 사망·파괴됐을 수 있다(로그 용도로만 쓸 것).
    /// </summary>
    // 원본 함수 대응: EncounterBlackboard.TryPeekYuliaReservation (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)
    public bool TryPeekYuliaReservation(JcBattleFlowManager flow, out int socket, out BattleCharactor source)
    {
        socket = _reservedSocket;
        source = _reservedSource;
        return _reservedSocket != 0;
    }

    /// <summary>
    /// 율리아가 non-Skip 결정을 확정하는 커밋에서만 호출(정확히 1회).
    /// [2026-10-02 변경] 덮어쓰기가 금지돼 대기 예약은 항상 1개이고, 예약한 증폭기가 파괴됐을 수 있으므로 소켓만 비교한다.
    /// </summary>
    // 원본 함수 대응: EncounterBlackboard.ConsumeYuliaReservationIfMatch (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)
    public void ConsumeYuliaReservationIfMatch(int socket, BattleCharactor source)
    {
        if (_reservedSocket == socket)
        {
            _reservedSocket = 0; _reservedSource = null;
        }
    }

    // ── 상태머신 (뼈대: 규칙을 "실행"만. 규칙 내용은 _transitions 데이터) ──
    // 원본 함수 대응: EncounterBlackboard.TryAdvancePhase (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)
    private void TryAdvancePhase(float bossHpRatio)
    {
        if (ExternalPhasePolicy) return;
        // 한 이벤트로 여러 단계 전진 가능 → 더 이상 전진 없을 때까지 반복.
        bool advanced = true;
        while (advanced)
        {
            advanced = false;
            for (int i = 0; i < _transitions.Count; i++)
            {
                JcPhaseTransition t = _transitions[i];
                if (t.FromPhase != CurrentPhase) continue;   // 현재 페이즈용 규칙만
                if (t.ToPhase <= CurrentPhase) continue;     // 전진-only
                if (!EvaluateCondition(t, bossHpRatio)) continue;
                CurrentPhase = t.ToPhase;
                PhaseAdvanced?.Invoke(CurrentPhase);         // 각 진입을 1회씩 발화
                advanced = true;
                break;                                       // CurrentPhase 바뀜 → 처음부터 재검사
            }
        }
    }

    // 원본 함수 대응: EncounterBlackboard.EvaluateCondition (Assets/_ProtoType_Merge/ASB/Scripts/Unit/EncounterBlackboard.cs)

    private bool EvaluateCondition(JcPhaseTransition t, float bossHpRatio)
    {
        switch (t.Condition)
        {
            case JcPhaseConditionType.ParticipantDestroyCount:
                return DestroyedParticipantCount >= t.Threshold;
            case JcPhaseConditionType.BossHpRatioAtMost:
                return bossHpRatio <= t.Threshold;
            default:
                return false;
        }
    }
}
