using UnityEngine;

/// <summary>
/// 전투 공유 상태(EncounterBlackboard)에 참여하는 유닛의 엔드포인트. (구현지시서: 증폭기창구_페이즈상태머신 §7)
///
/// 이름은 범용(EncounterParticipant) — "증폭기(Amplifier)"는 이 컴포넌트를 붙인 유닛의 도메인 역할일 뿐이다.
/// 증폭기·미니언·기타 부하 유닛 모두 이 하나를 붙인다. id 값(FV40002 등)만 세부.
///
/// 역할:
/// - 자기 사망(OnDied) → 블랙보드에 파괴 기록(페이즈 1→2 판정 기여).
/// - 스킬 사용 확정 시 → 블랙보드에 이번 라운드 스킬 기록(보스 스킬 게이팅용).
/// - 현재 페이즈 읽기(참여자 AI가 행동 분기).
/// - 무력화 라운드 카운트다운 구동(자기 턴 무관, 라운드 경계 기준).
///
/// 의존은 단방향(이 컴포넌트 → 코어/블랙보드). 코어(BattleCharactor)는 블랙보드를 모른다.
/// </summary>
[RequireComponent(typeof(BattleCharactor))]
public sealed class EncounterParticipant : MonoBehaviour
{
    [Tooltip("이 유닛의 식별자(세부=값). 블랙보드 파괴/스킬 기록의 key. 예: 율리아 증폭기 = FV40002 / FV40003")]
    [SerializeField] private string _participantId;
    [Tooltip("씬의 EncounterBlackboard. 미지정 시 런타임 탐색.")]
    [SerializeField] private EncounterBlackboard _blackboard;

    private BattleCharactor _self;
    private BattleFlowManager _flow;
    private int _lastRound = -1;

    public string Id => _participantId;

    private void Awake()
    {
        _self = GetComponent<BattleCharactor>();
        EncounterBlackboard bb = ResolveBlackboard();
        // 2페이즈 이후 재소환분은 무력화 모드로. 1페이즈 배치분은 phase==1이라 false → 정상 사망.
        if (_self != null && bb != null)
        {
            _self.CanBeIncapacitated = bb.CurrentPhase >= 2;
        }
    }

    private void OnEnable()
    {
        if (_self != null) _self.OnDied += HandleDied;
        BattleFlowManager flow = ResolveFlow();
        if (flow != null) flow.OnTurnStarted += HandleTurnStarted;
    }

    private void OnDisable()
    {
        if (_self != null) _self.OnDied -= HandleDied;
        if (_flow != null) _flow.OnTurnStarted -= HandleTurnStarted;
    }

    // 진짜 사망(1페이즈). 무력화는 OnDied를 발화하지 않으므로 여기 안 들어옴 → 파괴 카운트 무관.
    private void HandleDied(BattleCharactor who)
    {
        ResolveBlackboard()?.MarkParticipantDestroyed(_participantId);
    }

    // 라운드 경계에서 무력화 카운트다운(자기 턴 무관 — 다운으로 턴이 스킵돼도 진행).
    private void HandleTurnStarted(int roundIndex, BattleCharactor current)
    {
        if (roundIndex == _lastRound) return;   // 라운드가 증가한 프레임에만 1회
        _lastRound = roundIndex;
        if (_self != null) _self.TickIncapacitationRound();
    }

    /// <summary>참여자 AI가 스킬로 non-Skip 결정을 확정하기 직전 1회 호출.</summary>
    public void SignalSkillUsed(int skillIndex)
    {
        ResolveBlackboard()?.MarkSkillUsedThisRound(_participantId, skillIndex);
    }

    /// <summary>참여자 AI가 행동 분기에 쓸 현재 페이즈.</summary>
    public int ReadPhase()
    {
        EncounterBlackboard bb = ResolveBlackboard();
        return bb != null ? bb.CurrentPhase : 1;
    }

    private EncounterBlackboard ResolveBlackboard()
    {
        if (_blackboard == null) _blackboard = FindFirstObjectByType<EncounterBlackboard>();
        return _blackboard;
    }

    private BattleFlowManager ResolveFlow()
    {
        if (_flow == null) _flow = FindFirstObjectByType<BattleFlowManager>();
        return _flow;
    }
}
