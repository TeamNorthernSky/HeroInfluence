using System.Collections;
using UnityEngine;

/// <summary>
/// 율리아 3페이즈 정지장 연출(기획 Y11 감금·유지, Y12 정지장 상태 피격).
/// <para>
/// 진입: 블랙보드가 3페이즈로 전진하면 <see cref="BattleFlowManager.EnqueueTurnInterlude"/>로 예약한다.
/// 페이즈는 피해 처리 도중에 바뀌므로, 진행 중인 행동이 끝난 뒤 다음 턴 배너 직전에 재생된다.
/// 같은 진입에서 쓰러진 증폭기의 즉시 부활(EncounterParticipant, order -10)이 먼저 끝난 뒤 재생된다(기본 order 0).
/// 피격 포즈를 잠깐 재생한 뒤 그 자세로 멈추고, 몸 전체를 감싸는 보라 육각 막(Sci-Fi Shield)을 띄운다.
/// 손 구체와 피격 점멸은 감금 동안 끄고, 손 충전(FX_400010) 같은 시전 이펙트는 스폰하지 않는다.
/// </para>
/// <para>
/// 유지: 감금 해제 조건이 없어 막은 율리아가 죽을 때까지 남는다(죽으면 줄어들며 사라진다).
/// 정지장 상태에서 맞으면 율리아와 막을 짧게 흔든다(피격 모션 대신).
/// 감금 중 율리아는 굴렁쇠 소환만 쓰고(EAI_40001), 그 시전 중에도 정지 포즈를 유지한다 —
/// 다른 코드가 Animator를 다시 켜면(스킬 연출·피격 반응) 같은 프레임에 정지 포즈로 되돌린다.
/// </para>
/// 피해 규칙은 바꾸지 않는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BattleCharactor))]
public sealed class YuliaStasisVfx : MonoBehaviour, IPresentationEffectSpawnFilter
{
    [SerializeField, Min(1)] private int stasisPhase = 3;

    [Header("Shield")]
    [SerializeField] private GameObject shieldPrefab;
    [Tooltip("막 중심의 높이(유닛 루트 기준, m). 몸 전체가 막 안에 보이도록.")]
    [SerializeField] private float shieldHeight = 0.9f;
    [SerializeField, Min(0.01f)] private float shieldWorldScale = 1.2f;
    [Tooltip("율리아 사망 시 막이 줄어들며 사라지는 시간(전투 배속 기준 초).")]
    [SerializeField, Min(0f)] private float shieldDismissSeconds = 0.3f;

    [Header("Freeze")]
    [SerializeField] private string hitStateName = "Hit";
    [Tooltip("피격 포즈를 재생한 뒤 멈추기까지의 시간(전투 배속 기준 초).")]
    [SerializeField, Min(0f)] private float freezeDelaySeconds = 0.2f;
    [Tooltip("진입 연출 전체 길이(전투 배속 기준 초). 이 시간 동안 다음 턴 배너가 기다린다.")]
    [SerializeField, Min(0f)] private float enterSeconds = 1.6f;

    [Header("Hit Shake (Y12)")]
    [SerializeField, Min(0f)] private float shakeSeconds = 0.2f;
    [SerializeField, Min(0f)] private float shakeAmplitude = 0.06f;

    [Header("감금 동안 끄기")]
    [Tooltip("손 구체 등 감금 중 숨길 오브젝트.")]
    [SerializeField] private GameObject[] hideWhileStasis;
    [Tooltip("피격 점멸 등 감금 중 멈출 컴포넌트.")]
    [SerializeField] private Behaviour[] disableWhileStasis;
    [Tooltip("감금 중 스폰하지 않을 Cue 이펙트 프리팹(손 충전 등).")]
    [SerializeField] private GameObject[] suppressEffectsWhileStasis;

    private BattleCharactor _boss;
    private EncounterBlackboard _blackboard;
    private BattleFlowManager _flow;
    private Animator _animator;
    private Transform _visualRoot;
    private GameObject _shield;
    private bool _queued;
    private bool _stasis;
    private float _frozenNormalizedTime;
    private float _lastHp = -1f;
    private Coroutine _shake;
    private Vector3 _visualRootHome;
    private Vector3 _shieldHome;

    public bool IsInStasis => _stasis;

    public bool ShouldSuppress(GameObject effectPrefab)
    {
        return ShouldSuppressEffect(_stasis, effectPrefab, suppressEffectsWhileStasis);
    }

    private void Awake()
    {
        _boss = GetComponent<BattleCharactor>();
        Transform model = transform.Find("Model");
        _visualRoot = model != null ? model : transform;
    }

    private void OnEnable()
    {
        _boss.OnHpChanged += HandleHpChanged;
        _boss.OnDied += HandleDied;
        _lastHp = _boss.CurrentHp;
        Subscribe();
    }

    private void Start()
    {
        // 씬의 Blackboard/Flow가 프리팹보다 늦게 활성화된 경우 한 번 더 연결한다.
        Subscribe();
    }

    private void OnDisable()
    {
        _boss.OnHpChanged -= HandleHpChanged;
        _boss.OnDied -= HandleDied;
        if (_blackboard != null) _blackboard.PhaseAdvanced -= HandlePhaseAdvanced;
        StopShake();
    }

    private void Subscribe()
    {
        if (_blackboard == null) _blackboard = FindFirstObjectByType<EncounterBlackboard>();
        if (_blackboard != null)
        {
            _blackboard.PhaseAdvanced -= HandlePhaseAdvanced;
            _blackboard.PhaseAdvanced += HandlePhaseAdvanced;
        }

        if (_flow == null) _flow = FindFirstObjectByType<BattleFlowManager>();
    }

    private void HandlePhaseAdvanced(int phase)
    {
        if (!ShouldQueueStasis(phase, stasisPhase, _queued || _stasis, _boss != null && !_boss.IsDead)) return;

        _queued = true;
        if (_flow == null) _flow = FindFirstObjectByType<BattleFlowManager>();
        if (_flow != null) _flow.EnqueueTurnInterlude(EnterStasis);
        else StartCoroutine(EnterStasis());
    }

    private IEnumerator EnterStasis()
    {
        if (_boss == null || _boss.IsDead) yield break;

        _animator = _boss.Anim != null ? _boss.Anim.Animator : GetComponentInChildren<Animator>();
        if (_animator != null)
        {
            _animator.enabled = true;
            _animator.CrossFade(hitStateName, 0.05f, 0, 0f);
        }

        yield return Wait(freezeDelaySeconds);
        if (_boss == null || _boss.IsDead) yield break;   // 피격 포즈 재생 중 처치됨
        Freeze();
        SetStasisObjects(true);
        SpawnShield();
        _stasis = true;

        yield return Wait(Mathf.Max(0f, enterSeconds - freezeDelaySeconds));
    }

    private void Freeze()
    {
        if (_animator == null) return;
        _frozenNormalizedTime = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        // 비활성 Animator는 마지막 포즈를 유지하고, 이후 피격 반응(CrossFade "Hit")도 반영하지 않는다.
        _animator.enabled = false;
    }

    private void Refreeze()
    {
        if (_animator == null) return;
        _animator.enabled = true;
        _animator.Play(hitStateName, 0, _frozenNormalizedTime);
        _animator.Update(0f);
        _animator.enabled = false;
    }

    private void SpawnShield()
    {
        if (shieldPrefab == null || _shield != null) return;

        // 막 프리팹(Sci-Fi Shield 메시)은 자체 등장 애니메이션이 루트 스케일을 0→1로 키운다.
        // 크기는 피벗에 걸고 막은 그 아래에서 애니메이션하게 둔다. 흔들림도 피벗을 움직인다.
        _shield = new GameObject("StasisShieldPivot");
        _shield.transform.SetParent(transform, false);
        _shield.transform.position = transform.position + Vector3.up * shieldHeight;
        _shield.transform.localScale = Vector3.one * (shieldWorldScale / Mathf.Max(0.0001f, transform.lossyScale.x));
        GameObject shield = Instantiate(shieldPrefab, _shield.transform, false);
        JC.VFX.VfxEffect effect = shield.GetComponent<JC.VFX.VfxEffect>();
        if (effect != null) effect.Play();
    }

    private void SetStasisObjects(bool stasis)
    {
        if (hideWhileStasis != null)
            for (int i = 0; i < hideWhileStasis.Length; i++)
                if (hideWhileStasis[i] != null) hideWhileStasis[i].SetActive(!stasis);

        if (disableWhileStasis != null)
            for (int i = 0; i < disableWhileStasis.Length; i++)
                if (disableWhileStasis[i] != null) disableWhileStasis[i].enabled = !stasis;
    }

    // 감금 중 소환 시전(Timeline 바인딩)이나 피격 반응이 Animator를 다시 켜도 정지 포즈를 유지한다.
    // 애니메이션 평가(Update) 뒤인 LateUpdate에서 되돌리므로 그 프레임 렌더에도 정지 포즈가 나온다.
    private void LateUpdate()
    {
        if (_stasis && _animator != null && _animator.enabled && _boss != null && !_boss.IsDead) Refreeze();
    }

    private void HandleHpChanged(float currentHp, float maxHp)
    {
        bool hit = UnitHitFlashVfx.ShouldFlash(_lastHp, currentHp);
        _lastHp = currentHp;
        if (!hit || !_stasis || currentHp <= 0f) return;

        StopShake();
        _shake = StartCoroutine(Shake());
    }

    private void HandleDied(BattleCharactor _)
    {
        // 사망 모션이 재생되도록 정지를 풀고 Animator를 되살린다(율리아 처치는 즉시 승리).
        StopShake();
        _stasis = false;
        if (_animator != null) _animator.enabled = true;
        if (_shield != null) StartCoroutine(DismissShield(_shield));
        _shield = null;
    }

    private IEnumerator DismissShield(GameObject pivot)
    {
        Vector3 scale = pivot.transform.localScale;
        for (float t = 0f; t < shieldDismissSeconds && pivot != null; t += Time.deltaTime * BattleSpeed())
        {
            pivot.transform.localScale = scale * DismissScale(t / shieldDismissSeconds);
            yield return null;
        }

        if (pivot != null) Destroy(pivot);
    }

    private IEnumerator Shake()
    {
        _visualRootHome = _visualRoot.localPosition;
        _shieldHome = _shield != null ? _shield.transform.localPosition : Vector3.zero;
        for (float t = 0f; t < shakeSeconds; t += Time.deltaTime * BattleSpeed())
        {
            Vector3 offset = ShakeOffset(t / Mathf.Max(0.0001f, shakeSeconds), shakeAmplitude, Random.insideUnitCircle);
            _visualRoot.localPosition = _visualRootHome + offset;
            if (_shield != null) _shield.transform.localPosition = _shieldHome + offset;
            yield return null;
        }

        RestoreShake();
        _shake = null;
    }

    private void StopShake()
    {
        if (_shake == null) return;
        StopCoroutine(_shake);
        _shake = null;
        RestoreShake();
    }

    private void RestoreShake()
    {
        _visualRoot.localPosition = _visualRootHome;
        if (_shield != null) _shield.transform.localPosition = _shieldHome;
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime * BattleSpeed())
        {
            yield return null;
        }
    }

    private static float BattleSpeed()
    {
        return BattleManager.Instance != null ? Mathf.Max(0.01f, BattleManager.Instance.CurrentBattleSpeed) : 1f;
    }

    /// <summary>정지장 진입을 예약할지. 지정 페이즈로 처음 들어왔고 보스가 살아 있을 때만.</summary>
    public static bool ShouldQueueStasis(int enteredPhase, int stasisPhase, bool alreadyQueuedOrActive, bool bossAlive)
    {
        return enteredPhase == stasisPhase && !alreadyQueuedOrActive && bossAlive;
    }

    /// <summary>감금 중이고 목록에 있는 프리팹이면 스폰하지 않는다.</summary>
    public static bool ShouldSuppressEffect(bool stasis, GameObject effectPrefab, GameObject[] suppressed)
    {
        if (!stasis || effectPrefab == null || suppressed == null) return false;
        return System.Array.IndexOf(suppressed, effectPrefab) >= 0;
    }

    /// <summary>사망 시 막 크기 배율(0~1 진행도). 1에서 0으로 부드럽게 줄어든다.</summary>
    public static float DismissScale(float normalized)
    {
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized));
    }

    /// <summary>흔들림 오프셋. 끝으로 갈수록 줄어 원위치로 돌아온다(수평 흔들림).</summary>
    public static Vector3 ShakeOffset(float normalized, float amplitude, Vector2 random)
    {
        float falloff = 1f - Mathf.Clamp01(normalized);
        return new Vector3(random.x, 0f, random.y) * (amplitude * falloff);
    }
}
