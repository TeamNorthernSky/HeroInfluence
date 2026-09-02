using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>전환 조건 종류(세부 규칙의 어휘). 필요 시 여기만 확장.</summary>
public enum PhaseConditionType
{
    ParticipantDestroyCount,   // 파괴된 참여자 수 >= Threshold
    BossHpRatioAtMost,         // 보스 HP 비율 <= Threshold
    // 확장 예: MinionAllDead / TurnCountAtLeast / ...
}

/// <summary>페이즈 전환 규칙(세부=데이터). "이 페이즈에서 조건 충족 시 → 저 페이즈로".</summary>
[Serializable]
public struct PhaseTransition
{
    [Tooltip("이 페이즈일 때만 검사")]      public int FromPhase;
    [Tooltip("전환 조건 종류")]              public PhaseConditionType Condition;
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
public sealed class EncounterBlackboard : MonoBehaviour
{
    [Header("페이즈 전환 규칙 (세부 = 보스마다 인스펙터로 구성)")]
    [Tooltip("율리아 예: [From=1, ParticipantDestroyCount, 2, To=2], [From=2, BossHpRatioAtMost, 0.5, To=3]")]
    [SerializeField] private List<PhaseTransition> _transitions = new List<PhaseTransition>();

    // ── 페이즈 (전진-only) ──
    public int CurrentPhase { get; private set; } = 1;

    /// <summary>페이즈가 실제로 전진했을 때만 발화(중복 없음). 무거운 효과는 구독자가 "지연 실행"할 것.</summary>
    public event Action<int> PhaseAdvanced;

    // ── 참여자 파괴 이력 ──
    private readonly HashSet<string> _destroyedParticipants = new HashSet<string>();
    public int DestroyedParticipantCount => _destroyedParticipants.Count;

    // ── 라운드 스킬 사용 (라운드 리셋) : key = participantId ──
    private readonly Dictionary<string, HashSet<int>> _skillsUsedThisRound = new Dictionary<string, HashSet<int>>();
    private int _lastRoundIndex = -1;

    private BattleFlowManager _flow;

    private void OnEnable()
    {
        ResetAll();   // 전투 판 단위 리셋
        _flow = FindFirstObjectByType<BattleFlowManager>();
        if (_flow != null) _flow.OnTurnStarted += HandleTurnStarted;   // 라운드 경계 → 스킬 플래그 리셋
    }

    private void OnDisable()
    {
        if (_flow != null) _flow.OnTurnStarted -= HandleTurnStarted;
    }

    // 라운드 증가 시 이번 라운드 스킬 기록을 비운다(NotifyRound 멱등).
    private void HandleTurnStarted(int roundIndex, BattleCharactor current) => NotifyRound(roundIndex);

    public void ResetAll()
    {
        CurrentPhase = 1;
        _destroyedParticipants.Clear();
        _skillsUsedThisRound.Clear();
        _lastRoundIndex = -1;
    }

    // ── 쓰기 API ──

    /// <summary>참여자가 자기 OnDied에서 1회 호출. 파괴 기록 후 페이즈 전환을 평가.</summary>
    public void MarkParticipantDestroyed(string participantId)
    {
        if (string.IsNullOrEmpty(participantId)) return;
        if (_destroyedParticipants.Add(participantId))   // 새로 추가된 경우에만
        {
            TryAdvancePhase(1f);                          // 파괴발 전환은 HP 무관. HP는 무의미값 전달.
        }
    }

    /// <summary>참여자 AI가 스킬로 non-Skip 결정을 확정하기 직전 1회 호출.</summary>
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
    public void NotifyRound(int roundIndex)
    {
        if (roundIndex == _lastRoundIndex) return;
        _lastRoundIndex = roundIndex;
        _skillsUsedThisRound.Clear();
    }

    /// <summary>재소환 시 파괴 이력에서 제거(선택). 상태 정합용.</summary>
    public void ClearParticipantDestroyed(string participantId)
    {
        if (!string.IsNullOrEmpty(participantId)) _destroyedParticipants.Remove(participantId);
    }

    // ── 읽기 API ──

    public bool DidParticipantUseSkillThisRound(string participantId, int skillIndex)
    {
        return _skillsUsedThisRound.TryGetValue(participantId, out HashSet<int> set) && set.Contains(skillIndex);
    }

    /// <summary>임의 참여자가 이번 라운드에 해당 스킬을 썼는지(게이팅 편의).</summary>
    public bool DidAnyParticipantUseSkillThisRound(int skillIndex)
    {
        foreach (KeyValuePair<string, HashSet<int>> kv in _skillsUsedThisRound)
        {
            if (kv.Value.Contains(skillIndex)) return true;
        }
        return false;
    }

    // ── 상태머신 (뼈대: 규칙을 "실행"만. 규칙 내용은 _transitions 데이터) ──
    private void TryAdvancePhase(float bossHpRatio)
    {
        // 한 이벤트로 여러 단계 전진 가능 → 더 이상 전진 없을 때까지 반복.
        bool advanced = true;
        while (advanced)
        {
            advanced = false;
            for (int i = 0; i < _transitions.Count; i++)
            {
                PhaseTransition t = _transitions[i];
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

    private bool EvaluateCondition(PhaseTransition t, float bossHpRatio)
    {
        switch (t.Condition)
        {
            case PhaseConditionType.ParticipantDestroyCount:
                return DestroyedParticipantCount >= t.Threshold;
            case PhaseConditionType.BossHpRatioAtMost:
                return bossHpRatio <= t.Threshold;
            default:
                return false;
        }
    }
}
