using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 전용 조합 컴포넌트. (구현지시서: 보스유닛_소환_창구스킬_페이즈AI)
///
/// - 창구: 미니언 신호 큐(보스 소유). EAI_보스가 자기 턴 결정을 확정하기 직전에 소비(§0-7, §5).
/// - 페이즈: <see cref="BattleCharactor.OnHpChanged"/> 구독으로 임계값 진입을 "즉시 감지"하되,
///   무거운 효과(소환)는 콜백(시퀀스 도중)에서 실행하지 않고 보스 턴 시작 안전시점으로 지연한다(§0-7 정밀화).
/// - 소환: <see cref="EnemySpawner.TrySpawnUnitIfEmpty"/>(점유 시 실패)로 미니언 생성 + Owner 주입(§4).
///
/// EnemyScript/BattleCharactor 등 코어는 변경하지 않으며, 이 컴포넌트가 GetComponent로 코어를 참조한다(단방향).
/// </summary>
[RequireComponent(typeof(BattleCharactor))]
public sealed class BossController : MonoBehaviour
{
    [Serializable]
    public struct SummonEntry
    {
        [Tooltip("소환할 미니언의 EnemyData.Index(=enemyId). 미니언 템플릿의 UnitAI를 미니언 AI index로 지정해 두어야 한다.")]
        public string MinionEnemyId;

        [Tooltip("소환 그리드 번호. 비어 있어야 소환 성공(점유 시 기존 유닛 보존, 실패).")]
        public int GridNumber;

        [Tooltip("이 소환이 발동하는 페이즈(진입 시 1회).")]
        public int Phase;
    }

    [Header("Phase thresholds (HP 비율)")]
    [Range(0f, 1f)] [SerializeField] private float _phase1HpRatio = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float _phase2HpRatio = 0.3f;
    [Tooltip("시작 페이즈 진입 효과도 1회 발동할지. false면 시작 상태는 '이미 진입'으로 간주(진입 효과 억제).")]
    [SerializeField] private bool _fireInitialPhaseEffect = false;

    [Header("창구 스킬 (skillIndex)")]
    [Tooltip("미니언이 이 스킬로 행동을 확정하면 신호가 적재된다.")]
    [SerializeField] private int _triggerSkillIndex;
    [Tooltip("창구 신호를 받아 보스가 사용할 스킬 index.")]
    [SerializeField] private int _reactionSkillIndex;
    [Tooltip("창구 큐 최대 보관 수. 초과분은 무시(오래된 요청 우선 보존).")]
    [SerializeField] private int _maxQueued = 3;

    [Header("소환 구성")]
    [SerializeField] private List<SummonEntry> _summonEntries = new List<SummonEntry>();
    [Tooltip("씬의 EnemySpawner. 미지정 시 런타임에 부모/씬에서 탐색한다.")]
    [SerializeField] private EnemySpawner _spawner;
    [Tooltip("씬의 BattleFlowManager. 미지정 시 런타임에 탐색한다. 소환 미니언의 턴 참가자 등록에 사용.")]
    [SerializeField] private BattleFlowManager _flow;

    /// <summary>페이즈 진입이 감지된 즉시(가벼운 효과용) 호출되는 훅. 인자는 진입 페이즈.
    /// ⚠️ 이 훅은 피해 처리 도중(시퀀스 중) 호출될 수 있으므로 전투를 변경하는 무거운 작업은 넣지 말 것.</summary>
    public event Action<int> ImmediatePhaseEntered;

    private BattleCharactor _self;
    private readonly Queue<BossSkillRequest> _pending = new Queue<BossSkillRequest>();
    private readonly Queue<int> _pendingEnteredPhases = new Queue<int>();
    private int _lastPhase;

    public int TriggerSkillIndex => _triggerSkillIndex;
    public int ReactionSkillIndex => _reactionSkillIndex;

    private void Awake()
    {
        _self = GetComponent<BattleCharactor>();
        // _fireInitialPhaseEffect=false: 시작 페이즈를 '이미 진입'으로 잡아 초기 1회 효과를 막는다.
        _lastPhase = _fireInitialPhaseEffect ? 0 : ComputePhase();
    }

    private void OnEnable()
    {
        if (_self != null) _self.OnHpChanged += HandleHpChanged;
    }

    private void OnDisable()
    {
        if (_self != null) _self.OnHpChanged -= HandleHpChanged;
    }

    private void OnDestroy()
    {
        _pending.Clear();
        _pendingEnteredPhases.Clear();
    }

    // ──────────────────────────────────────────────────────────────
    // 창구 (Enqueue=미니언 확정 시, Consume=보스 결정 확정 직전 — §0-7)
    // ──────────────────────────────────────────────────────────────

    /// <summary>미니언이 트리거 스킬로 행동을 확정한 시점에 호출. 큐 정책: 미니언별 중복 방지 + 최대 N + 오래된 것 우선.</summary>
    public void Enqueue(BossSkillRequest req)
    {
        if (req == null) return;

        // 미니언별 중복 방지: 같은 Source의 요청이 이미 대기 중이면 무시.
        if (req.Source != null)
        {
            foreach (BossSkillRequest existing in _pending)
            {
                if (existing != null && existing.Source == req.Source) return;
            }
        }

        // 최대 N 초과 시, 오래된 요청을 보존하고 새 요청을 버린다.
        if (_maxQueued > 0 && _pending.Count >= _maxQueued) return;

        _pending.Enqueue(req);
    }

    /// <summary>조회만(소비 아님). 보스 AI가 해석/타깃 확인용으로 사용.</summary>
    public bool TryPeek(out BossSkillRequest req)
    {
        if (_pending.Count > 0) { req = _pending.Peek(); return true; }
        req = null;
        return false;
    }

    /// <summary>실제 소비. 보스 AI가 non-Skip 결정을 반환하기 직전에만 호출(정확히 1회).</summary>
    public bool Consume(out BossSkillRequest req)
    {
        if (_pending.Count > 0) { req = _pending.Dequeue(); return true; }
        req = null;
        return false;
    }

    // ──────────────────────────────────────────────────────────────
    // 페이즈 (Tier A 계산 + Tier B 감지/지연 실행)
    // ──────────────────────────────────────────────────────────────

    /// <summary>Tier A: HP 비율로 페이즈 계산(무상태·읽기 전용). 결정 함수에서 부작용 없이 호출 가능.</summary>
    public int ComputePhase()
    {
        float ratio = _self != null && _self.MaxHp > 0f ? _self.CurrentHp / _self.MaxHp : 1f;
        if (ratio > _phase1HpRatio) return 1;
        if (ratio > _phase2HpRatio) return 2;
        return 3;
    }

    // OnHpChanged: 감지만 즉시, 무거운 효과는 지연(§0-7 정밀화). 피해 처리 도중 호출될 수 있음.
    private void HandleHpChanged(float currentHp, float maxHp)
    {
        if (_self == null || _self.IsDead || currentHp <= 0f) return; // 사망중 발동 가드
        int phase = ComputePhase();
        if (phase > _lastPhase) // 전진-only(힐로 HP가 올라도 역행 재발동 방지)
        {
            _lastPhase = phase;
            ImmediatePhaseEntered?.Invoke(phase);   // 가벼운 효과(플래그/연출요청)
            _pendingEnteredPhases.Enqueue(phase);   // 무거운 효과(소환)는 안전시점으로 지연
        }
    }

    /// <summary>보스 턴 시작(EAI가 호출)의 안전 시점에 지연 페이즈 효과를 실행. 비었으면 no-op(멱등 → 이중 호출 안전).</summary>
    public void DrainPhaseEffectsAtTurnStart()
    {
        while (_pendingEnteredPhases.Count > 0)
        {
            int phase = _pendingEnteredPhases.Dequeue();
            RunDeferredPhaseEffects(phase);
        }
    }

    private void RunDeferredPhaseEffects(int phase)
    {
        if (_summonEntries == null) return;
        for (int i = 0; i < _summonEntries.Count; i++)
        {
            SummonEntry entry = _summonEntries[i];
            if (entry.Phase != phase || string.IsNullOrWhiteSpace(entry.MinionEnemyId)) continue;
            TrySummonMinion(entry.MinionEnemyId, entry.GridNumber);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 안전 소환 (점유 시 실패 API 재사용)
    // ──────────────────────────────────────────────────────────────

    /// <summary>지정 그리드가 비어 있을 때만 미니언을 소환하고 Owner를 주입한다. 점유중이면 기존 유닛 보존 후 실패.</summary>
    public bool TrySummonMinion(string minionEnemyId, int gridNumber)
    {
        EnemySpawner spawner = ResolveSpawner();
        if (spawner == null)
        {
            Debug.LogWarning($"[BossController] EnemySpawner를 찾지 못해 소환 실패: {minionEnemyId}@{gridNumber}", this);
            return false;
        }

        if (!spawner.TrySpawnUnitIfEmpty(minionEnemyId, gridNumber, out GameObject go) || go == null)
        {
            return false; // 점유중이면 기존 유닛 보존, 실패
        }

        MinionController mc = go.GetComponent<MinionController>();
        if (mc == null) mc = go.AddComponent<MinionController>();
        mc.Bind(this, _triggerSkillIndex, _reactionSkillIndex);

        // 런타임 참가자 등록: 이게 없으면 미니언이 턴 큐에 못 들어가 EAI를 실행하지 못한다(다음 라운드부터 반영).
        BattleCharactor minionBattle = go.GetComponent<BattleCharactor>();
        if (minionBattle != null)
        {
            BattleFlowManager flow = ResolveFlowManager();
            if (flow != null)
            {
                flow.RegisterRuntimeParticipant(minionBattle);
            }
            else
            {
                Debug.LogWarning($"[BossController] BattleFlowManager를 찾지 못해 미니언 턴 등록 실패: {minionEnemyId}@{gridNumber}", this);
            }
        }
        return true;
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

    private BattleFlowManager ResolveFlowManager()
    {
        if (_flow == null) _flow = FindFirstObjectByType<BattleFlowManager>();
        return _flow;
    }
}
