using System.Collections;
using System.Collections.Generic;
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
/// - (선택) 지정 페이즈 진입 시 쓰러져 있으면 턴 사이 연출에서 즉시 부활(율리아 증폭기: 3페이즈).
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
    [Tooltip("2페이즈 이후 HP 0에서 사망 대신 무력화할지 여부입니다.")]
    [SerializeField] private bool _useIncapacitation = true;
    [Tooltip("사망 후 논리적 점유만 해제하고 시체 GameObject는 전장에 남길지 여부입니다.")]
    [SerializeField] private bool _keepCorpseAfterDeath;

    [Header("Corpse revival")]
    [Tooltip("동일 시체 부활을 시작할 최소 페이즈입니다.")]
    [Min(1)] [SerializeField] private int _reviveStartPhase = 2;
    [Tooltip("부활까지 지나야 하는 고유 라운드 경계 수입니다.")]
    [Min(1)] [SerializeField] private int _corpseReviveDelayRounds = 2;
    [Tooltip("부활 시 최대 HP 대비 체력 비율입니다.")]
    [Range(0.01f, 1f)] [SerializeField] private float _reviveHpRatio = 0.5f;

    [Header("Immediate revival on phase entry")]
    [Tooltip("이 페이즈로 들어올 때 쓰러져 있으면 라운드 카운트다운 없이 다음 턴 사이 연출에서 바로 부활합니다(0 = 사용 안 함). " +
             "율리아 증폭기 = 3: 증폭기가 먼저 일어난 뒤 율리아 정지장이 나온다. 체력은 _reviveHpRatio, 다시 죽으면 기존 카운트다운.")]
    [Min(0)] [SerializeField] private int _immediateRevivePhase;
    [Tooltip("즉시 부활 뒤 다음 연출(율리아 정지장 등)까지 기다리는 시간(전투 배속 기준 초). 증폭기 복구 연출(A04 0.6초)이 끝나도록.")]
    [Min(0f)] [SerializeField] private float _immediateReviveHoldSeconds = 0.7f;

    /// <summary>즉시 부활 턴 사이 연출의 재생 순서. 같은 페이즈 진입의 다른 연출(정지장, 기본 0)보다 먼저.</summary>
    public const int PhaseReviveInterludeOrder = -10;
    /// <summary>지연 시체 제거(EnemySpawner, 0.8초)가 끝나지 않아 부활이 거절될 때 다시 시도하는 시간(초).</summary>
    private const float PhaseReviveRetrySeconds = 1.5f;

    // 같은 페이즈 진입에서 쓰러진 참여자들은 한 턴 사이 연출에서 동시에 일으킨다(기획 A04: 두 개 동시).
    private static readonly List<EncounterParticipant> s_phaseRevivePending = new List<EncounterParticipant>();
    private static BattleFlowManager s_phaseReviveFlow;

    private BattleCharactor _self;
    private BattleFlowManager _flow;
    private EnemySpawner _spawner;
    private int _lastRound = -1;
    private int _corpseReviveRoundsLeft = -1;
    private int _deathGridNumber = -1;

    public string Id => _participantId;
    public bool UseIncapacitation => _useIncapacitation;
    public bool KeepCorpseAfterDeath => _keepCorpseAfterDeath;
    public int CorpseReviveRoundsLeft => _corpseReviveRoundsLeft;
    public int DeathGridNumber => _deathGridNumber;

    private void Awake()
    {
        _self = GetComponent<BattleCharactor>();
        ApplyDeathPolicy();
    }

    private void Start()
    {
        // EnemyScript.Initialize의 ResetIncapacitation보다 뒤에서 프리팹 정책을 최종 반영합니다.
        ApplyDeathPolicy();
    }

    public void ApplyDeathPolicy()
    {
        if (_self == null) _self = GetComponent<BattleCharactor>();
        EncounterBlackboard bb = ResolveBlackboard();
        if (_self != null)
        {
            // 증폭기는 false로 고정해 실제 Die() + 시체 부활 경로를 사용한다.
            // 다른 참여자는 기존 무력화 옵션을 계속 사용할 수 있다.
            _self.CanBeIncapacitated = _useIncapacitation && bb != null && bb.CurrentPhase >= 2;
        }
    }

    private void OnEnable()
    {
        if (_self != null) _self.OnDied += HandleDied;
        BattleFlowManager flow = ResolveFlow();
        if (flow != null) flow.OnTurnStarted += HandleTurnStarted;
        EncounterBlackboard bb = ResolveBlackboard();
        if (bb != null) bb.PhaseAdvanced += HandlePhaseAdvanced;
    }

    private void OnDisable()
    {
        if (_self != null) _self.OnDied -= HandleDied;
        if (_flow != null) _flow.OnTurnStarted -= HandleTurnStarted;
        if (_blackboard != null) _blackboard.PhaseAdvanced -= HandlePhaseAdvanced;
    }

    // 실제 사망만 들어온다. EnemySpawner의 지연 RemoveUnit 전에 원래 셀 번호를 보존한다.
    private void HandleDied(BattleCharactor who)
    {
        if (who == null || who != _self) return;

        CaptureDeathGridNumber();
        _self.ResetEnergyStack();

        EncounterBlackboard bb = ResolveBlackboard();
        if (bb == null) return;

        // [2026-10-02 변경] 증폭기가 죽어도 율리아 예약은 유지한다 — 율리아는 다음 스킬 턴에 예약 스킬을 반드시 쓴다.
        // 예전에는 여기서 bb.ClearYuliaReservationFrom(_self)로 이 증폭기가 만든 예약을 무효화했다.
        bb.MarkParticipantDestroyed(_participantId);

        // 이미 부활 페이즈에 진입한 뒤 재사망한 경우 PhaseAdvanced가 다시 오지 않는다.
        if (bb.CurrentPhase >= _reviveStartPhase)
        {
            ArmCorpseRevivalIfNeeded();
        }
    }

    private void HandlePhaseAdvanced(int phase)
    {
        if (phase >= _reviveStartPhase)
        {
            // 즉시 부활이 실패해도(셀 점유 등) 기존 카운트다운으로 되살아나도록 먼저 무장한다.
            ArmCorpseRevivalIfNeeded();
        }

        if (ShouldReviveOnPhaseEntry(phase, _immediateRevivePhase, UsesCorpseRevival,
                _self != null && _self.IsDead, _deathGridNumber))
        {
            RequestPhaseRevive();
        }
    }

    // 페이즈 전환은 피해 처리 도중에 일어나므로, 부활은 진행 중인 행동이 끝난 뒤 턴 사이 연출에서 한다.
    private void RequestPhaseRevive()
    {
        BattleFlowManager flow = ResolveFlow();
        if (flow == null) return;   // 흐름이 없으면 기존 카운트다운으로 부활한다.

        s_phaseRevivePending.RemoveAll(p => p == null);
        if (s_phaseReviveFlow != flow)
        {
            s_phaseRevivePending.Clear();
            s_phaseReviveFlow = flow;
            flow.EnqueueTurnInterlude(RunPhaseRevives, PhaseReviveInterludeOrder);
        }

        if (!s_phaseRevivePending.Contains(this)) s_phaseRevivePending.Add(this);
    }

    private static IEnumerator RunPhaseRevives()
    {
        var waiting = new List<EncounterParticipant>(s_phaseRevivePending);
        s_phaseRevivePending.Clear();
        s_phaseReviveFlow = null;

        float hold = 0f;
        for (float t = 0f; waiting.Count > 0; t += Time.deltaTime)
        {
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                EncounterParticipant participant = waiting[i];
                if (participant == null || participant._self == null || !participant._self.IsDead)
                {
                    waiting.RemoveAt(i);
                    continue;
                }

                if (!participant.TryReviveCorpse()) continue;
                hold = Mathf.Max(hold, participant._immediateReviveHoldSeconds);
                waiting.RemoveAt(i);
            }

            // 남은 참여자는 이미 무장된 기존 카운트다운으로 부활한다.
            if (waiting.Count == 0 || t >= PhaseReviveRetrySeconds) break;
            yield return null;
        }

        // 복구 연출이 끝난 뒤 다음 연출(율리아 정지장)로 넘어간다.
        for (float t = 0f; t < hold; t += Time.deltaTime * BattleSpeed())
        {
            yield return null;
        }
    }

    private static float BattleSpeed()
    {
        return BattleManager.Instance != null ? Mathf.Max(0.01f, BattleManager.Instance.CurrentBattleSpeed) : 1f;
    }

    /// <summary>
    /// 페이즈 진입 즉시 부활 대상인지. 지정 페이즈로 들어왔고, 시체 부활을 쓰는 참여자가 쓰러져 있으며,
    /// 사망 셀이 기록돼 있을 때만(셀을 모르면 부활할 곳이 없다).
    /// </summary>
    public static bool ShouldReviveOnPhaseEntry(int enteredPhase, int immediateRevivePhase, bool usesCorpseRevival,
        bool isDead, int deathGridNumber)
    {
        return immediateRevivePhase > 0 && enteredPhase == immediateRevivePhase &&
               usesCorpseRevival && isDead && deathGridNumber >= 0;
    }

    // 라운드 경계에서 무력화/시체 부활 카운트다운(자기 턴 무관).
    private void HandleTurnStarted(int roundIndex, BattleCharactor current)
    {
        if (roundIndex == _lastRound) return;   // 라운드가 증가한 프레임에만 1회
        _lastRound = roundIndex;
        if (_useIncapacitation && _self != null) _self.TickIncapacitationRound();

        if (!UsesCorpseRevival || _self == null || !_self.IsDead || _corpseReviveRoundsLeft < 0)
        {
            return;
        }

        if (_corpseReviveRoundsLeft > 0)
        {
            _corpseReviveRoundsLeft--;
        }

        if (_corpseReviveRoundsLeft == 0)
        {
            TryReviveCorpse();
        }
    }

    private bool UsesCorpseRevival => !_useIncapacitation && _keepCorpseAfterDeath;

    private void CaptureDeathGridNumber()
    {
        _deathGridNumber = -1;
        if (!UsesCorpseRevival || _self == null || _self.OccupiedCell == null) return;

        EnemySpawner spawner = ResolveSpawner();
        if (spawner != null && spawner.TryGetGridNumber(_self.OccupiedCell, out int gridNumber))
        {
            _deathGridNumber = gridNumber;
        }
        else
        {
            Debug.LogWarning($"[EncounterParticipant] 사망 셀 번호를 보존하지 못했습니다: {_participantId}", this);
        }
    }

    private void ArmCorpseRevivalIfNeeded()
    {
        if (!UsesCorpseRevival || _self == null || !_self.IsDead) return;
        if (_deathGridNumber < 0 || _corpseReviveRoundsLeft >= 0) return;

        _corpseReviveRoundsLeft = Mathf.Max(1, _corpseReviveDelayRounds);
    }

    private bool TryReviveCorpse()
    {
        EnemySpawner spawner = ResolveSpawner();
        if (spawner == null || !spawner.TryReviveCorpseAt(_self, _deathGridNumber, _reviveHpRatio))
        {
            // 원래 셀이 점유 중이면 0을 유지하고 다음 고유 라운드에 다시 시도한다.
            return false;
        }

        ResolveBlackboard()?.ClearParticipantDestroyed(_participantId);
        _self.ResetEnergyStack();
        _corpseReviveRoundsLeft = -1;
        _deathGridNumber = -1;
        return true;
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

    private EnemySpawner ResolveSpawner()
    {
        if (_spawner == null)
        {
            _spawner = GetComponentInParent<EnemySpawner>();
            if (_spawner == null) _spawner = FindFirstObjectByType<EnemySpawner>();
        }
        return _spawner;
    }
}
