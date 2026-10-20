using UnityEngine;

/// <summary>
/// 증폭기 능력 개방 → 율리아 흡수 연출(기획 A02 + Y03).
/// <para>
/// 흡수 Timeline(<see cref="MadonnaSkillReceiveTimeline"/>)의 시간에 맞춰
/// 0초: 증폭기 구체 증광 → <see cref="arcStartSeconds"/>: 구체에서 율리아 손으로 전기 아크 →
/// <see cref="completeSeconds"/>: 손빛 1회, 아크·증광 정지(연결선을 먼저 소거).
/// 흡수가 중간에 끊기면(보스 턴 시작 등) 손빛 없이 정리한다.
/// </para>
/// <para>
/// 3페이즈에는 증폭기가 율리아에게 능력을 넘기지 않으므로(직접 시전) 이 연출도 나오지 않는다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MadonnaSkillReceiveTimeline))]
public sealed class YuliaAbsorbVfx : MonoBehaviour
{
    [SerializeField] private GameObject amplifierGlowPrefab;
    [SerializeField] private GameObject arcPrefab;
    [SerializeField] private GameObject handFlashPrefab;
    [SerializeField] private UnitSocket handSocket = UnitSocket.RightWeapon;

    [SerializeField, Min(0f)]
    [Tooltip("아크를 잇기 시작하는 시각(흡수 Timeline 시간, 초). 증광이 먼저 보이도록 약간 늦춘다.")]
    private float arcStartSeconds = 0.1f;

    [SerializeField, Min(0f)]
    [Tooltip("흡수 완료 시각(흡수 Timeline 시간, 초). Skill400014_SkillReceive의 Cue 마커(0.667초)와 맞춘다.")]
    private float completeSeconds = 0.667f;

    [SerializeField, Min(0.01f)]
    [Tooltip("증폭기 구체 증광의 월드 크기. 구체 스케일을 상쇄해 적용한다.")]
    private float amplifierGlowWorldScale = 0.45f;

    private MadonnaSkillReceiveTimeline _receive;
    private Transform _orb;
    private Transform _hand;
    private JC.VFX.VfxEffect _glow;
    private JC.VFX.VfxEffect _arc;
    private bool _active;
    private bool _arcSpawned;
    private bool _completed;

    private void Awake()
    {
        _receive = GetComponent<MadonnaSkillReceiveTimeline>();
    }

    private void OnEnable()
    {
        if (_receive == null) _receive = GetComponent<MadonnaSkillReceiveTimeline>();
        _receive.ReceiveStarted += HandleReceiveStarted;
        _receive.ReceiveEnded += StopLink;
    }

    private void OnDisable()
    {
        if (_receive != null)
        {
            _receive.ReceiveStarted -= HandleReceiveStarted;
            _receive.ReceiveEnded -= StopLink;
        }

        StopLink();
    }

    private void HandleReceiveStarted(BattleCharactor source)
    {
        StopLink();
        _orb = ResolveOrb(source);
        _hand = ResolveHand();
        if (_orb == null || _hand == null) return;

        _active = true;
        _arcSpawned = false;
        _completed = false;
        _glow = Spawn(amplifierGlowPrefab, _orb.position, _orb);
        if (_glow != null)
        {
            _glow.transform.localScale = Vector3.one * (amplifierGlowWorldScale / Mathf.Max(0.0001f, _orb.lossyScale.x));
            _glow.Play();
        }
    }

    private void Update()
    {
        if (!_active) return;
        if (_orb == null || _hand == null)
        {
            // 연결 도중 증폭기나 율리아가 사라짐.
            StopLink();
            return;
        }

        double time = _receive.ReceiveTime;
        if (ShouldSpawnArc(time, arcStartSeconds, _arcSpawned))
        {
            _arcSpawned = true;
            _arc = Spawn(arcPrefab, _orb.position, null);
            if (_arc != null) _arc.Play(_orb, _hand);
        }

        if (ShouldComplete(time, completeSeconds, _completed))
        {
            _completed = true;
            JC.VFX.VfxEffect flash = _hand != null ? Spawn(handFlashPrefab, _hand.position, null) : null;
            if (flash != null) flash.Play();
            StopLink();
        }
    }

    /// <summary>아크와 증광을 우아하게 정지한다. 손빛은 스스로 끝난다.</summary>
    private void StopLink()
    {
        _active = false;
        if (_arc != null) _arc.Stop();
        if (_glow != null) _glow.Stop();
        _arc = null;
        _glow = null;
    }

    private Transform ResolveHand()
    {
        UnitSocketHolder sockets = GetComponentInChildren<UnitSocketHolder>(true);
        Transform hand = sockets != null ? sockets.GetNamedSocket(handSocket) : null;
        return hand != null ? hand : transform;
    }

    private static Transform ResolveOrb(BattleCharactor source)
    {
        if (source == null) return null;
        AmplifierOrbHealthVisual orbVisual = source.GetComponentInChildren<AmplifierOrbHealthVisual>(true);
        Transform orb = orbVisual != null ? orbVisual.Orb : null;
        return orb != null ? orb : source.transform;
    }

    private static JC.VFX.VfxEffect Spawn(GameObject prefab, Vector3 position, Transform parent)
    {
        if (prefab == null) return null;

        GameObject instance = parent != null
            ? Instantiate(prefab, position, Quaternion.identity, parent)
            : Instantiate(prefab, position, Quaternion.identity);
        var effect = instance.GetComponent<JC.VFX.VfxEffect>();
        // 수명 관리가 없는 재료가 잘못 연결돼도 남지 않게 한다.
        if (effect == null) EffectFallbackRelease.Ensure(instance);
        return effect;
    }

    // Timeline 시간(double)과 인스펙터 값(float)을 float으로 비교한다. double로 올리면 0.1f가 0.1보다 커져 경계에서 한 프레임 늦는다.
    public static bool ShouldSpawnArc(double time, float arcStartSeconds, bool spawned)
    {
        return !spawned && (float)time >= arcStartSeconds;
    }

    public static bool ShouldComplete(double time, float completeSeconds, bool completed)
    {
        return !completed && (float)time >= completeSeconds;
    }
}
