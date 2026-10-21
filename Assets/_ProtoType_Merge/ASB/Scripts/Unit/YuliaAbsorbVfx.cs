using UnityEngine;

/// <summary>흡수 연출 단계. 흡수 Timeline의 Cue 마커 하나가 한 단계에 대응한다.</summary>
public enum YuliaAbsorbStep
{
    None,
    /// <summary>증폭기 구체 증광.</summary>
    Glow,
    /// <summary>구체에서 율리아 손으로 전기 아크.</summary>
    Arc,
    /// <summary>손빛 1회, 아크·증광 정지(연결선을 먼저 소거).</summary>
    Complete,
}

/// <summary>
/// 증폭기 능력 개방 → 율리아 흡수 연출(기획 A02 + Y03).
/// <para>
/// 시점은 흡수 Timeline(<see cref="MadonnaSkillReceiveTimeline"/>, Skill400014_SkillReceive)의 Cue 마커가 정한다:
/// <see cref="glowCue"/> 증광 → <see cref="arcCue"/> 아크 → <see cref="completeCue"/> 손빛·소거.
/// 이 컴포넌트는 "어떻게 그릴지"만 맡는다 — 증폭기 구체와 손을 잇는 아크는 일반 Cue 이펙트(한 지점 배치)로 그릴 수 없다.
/// 흡수가 중간에 끊기면(보스 턴 시작 등) 손빛 없이 정리한다.
/// </para>
/// <para>
/// 마커 위치는 손 동작(팔 Override = Victory_NoWeapon)에 맞춘다: 오른손 0.42~0.67초 올림,
/// 0.67~1.33초 든 채 유지(0.67~0.98초는 0.3배속), 1.33~1.67초 내림. 손을 든 동안 이펙트가 뜨고 사라진 뒤 손을 내린다.
/// </para>
/// <para>
/// 3페이즈에는 증폭기가 율리아에게 능력을 넘기지 않으므로(직접 시전) 이 연출도 나오지 않는다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MadonnaSkillReceiveTimeline))]
public sealed class YuliaAbsorbVfx : MonoBehaviour
{
    public const string DefaultGlowCue = "absorb_glow";
    public const string DefaultArcCue = "absorb_arc";
    public const string DefaultCompleteCue = "absorb_complete";

    [SerializeField] private GameObject amplifierGlowPrefab;
    [SerializeField] private GameObject arcPrefab;
    [SerializeField] private GameObject handFlashPrefab;
    [SerializeField] private UnitSocket handSocket = UnitSocket.RightWeapon;

    [Header("흡수 Timeline Cue 마커 이름")]
    [Tooltip("증폭기 구체 증광. 손을 들기 시작할 때.")]
    [SerializeField] private string glowCue = DefaultGlowCue;
    [Tooltip("구체 → 손 아크. 손이 다 올라간 뒤, 증광보다 늦게.")]
    [SerializeField] private string arcCue = DefaultArcCue;
    [Tooltip("손빛 1회 + 아크·증광 소거. 손을 내리기 전.")]
    [SerializeField] private string completeCue = DefaultCompleteCue;

    [SerializeField, Min(0.01f)]
    [Tooltip("증폭기 구체 증광의 월드 크기. 구체 스케일을 상쇄해 적용한다.")]
    private float amplifierGlowWorldScale = 0.45f;

    private MadonnaSkillReceiveTimeline _receive;
    private Transform _orb;
    private Transform _hand;
    private JC.VFX.VfxEffect _glow;
    private JC.VFX.VfxEffect _arc;
    private bool _active;
    private bool _glowSpawned;
    private bool _arcSpawned;

    private void Awake()
    {
        _receive = GetComponent<MadonnaSkillReceiveTimeline>();
    }

    private void OnEnable()
    {
        if (_receive == null) _receive = GetComponent<MadonnaSkillReceiveTimeline>();
        _receive.ReceiveStarted += HandleReceiveStarted;
        _receive.ReceiveCue += HandleReceiveCue;
        _receive.ReceiveEnded += StopLink;
    }

    private void OnDisable()
    {
        if (_receive != null)
        {
            _receive.ReceiveStarted -= HandleReceiveStarted;
            _receive.ReceiveCue -= HandleReceiveCue;
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
        _glowSpawned = false;
        _arcSpawned = false;
    }

    private void HandleReceiveCue(string cueName)
    {
        if (!_active) return;
        if (_orb == null || _hand == null)
        {
            // 연결 도중 증폭기나 율리아가 사라짐.
            StopLink();
            return;
        }

        switch (ResolveStep(cueName, glowCue, arcCue, completeCue))
        {
            case YuliaAbsorbStep.Glow:
                if (_glowSpawned) return;
                _glowSpawned = true;
                SpawnGlow();
                break;
            case YuliaAbsorbStep.Arc:
                if (_arcSpawned) return;
                _arcSpawned = true;
                _arc = Spawn(arcPrefab, _orb.position, null);
                if (_arc != null) _arc.Play(_orb, _hand);
                break;
            case YuliaAbsorbStep.Complete:
                JC.VFX.VfxEffect flash = Spawn(handFlashPrefab, _hand.position, null);
                if (flash != null) flash.Play();
                StopLink();
                break;
        }
    }

    private void Update()
    {
        // 연결 도중 증폭기나 율리아가 사라지면 다음 마커를 기다리지 않고 바로 정리한다.
        if (_active && (_orb == null || _hand == null)) StopLink();
    }

    private void SpawnGlow()
    {
        _glow = Spawn(amplifierGlowPrefab, _orb.position, _orb);
        if (_glow != null)
        {
            _glow.transform.localScale = Vector3.one * (amplifierGlowWorldScale / Mathf.Max(0.0001f, _orb.lossyScale.x));
            _glow.Play();
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

    /// <summary>Cue 이름이 어느 단계인지. 비었거나 모르는 이름이면 None(다른 마커는 무시).</summary>
    public static YuliaAbsorbStep ResolveStep(string cueName, string glowCue, string arcCue, string completeCue)
    {
        if (string.IsNullOrEmpty(cueName)) return YuliaAbsorbStep.None;
        if (cueName == glowCue) return YuliaAbsorbStep.Glow;
        if (cueName == arcCue) return YuliaAbsorbStep.Arc;
        if (cueName == completeCue) return YuliaAbsorbStep.Complete;
        return YuliaAbsorbStep.None;
    }
}
