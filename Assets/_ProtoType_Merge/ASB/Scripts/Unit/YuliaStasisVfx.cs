using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 율리아 정지장 연출(기획 Y11 감금·유지).
/// <para>
/// 진입: 율리아전 씬(EncounterBlackboard가 있는 씬)에 등장하면 바로 감금된다(턴 흐름을 막지 않음).
/// [2026-10-23 변경] 율리아전 페이즈를 없애 예전 3페이즈 진입(블랙보드 PhaseAdvanced → 턴 사이 연출) 대신 등장 시점에 들어간다.
/// 블랙보드가 없는 씬(스킬 프리뷰 등)에서는 감금하지 않는다.
/// 몸 전체를 감싸는 보라 육각 막(Sci-Fi Shield)을 띄운다.
/// [2026-10-23 변경] 정지 포즈·손 구체 숨김·피격 점멸 끄기·손 충전(FX_400010) 억제·피격 흔들림(Y12)을 없앴다.
/// 애니메이션과 이펙트는 예전 1·2페이즈처럼 그대로 나오고, 이 컴포넌트는 막만 다룬다.
/// </para>
/// <para>
/// 유지: 감금 해제 조건이 없어 감금 상태는 율리아가 죽을 때까지 남는다.
/// [2026-10-23 변경] 막은 증폭기(EncounterParticipant)가 하나라도 살아 있을 때만 보인다. 전부 쓰러지면 줄어들며 사라지고,
/// 하나라도 부활하면 다시 나타난다. 율리아가 죽어도 줄어들며 사라진다.
/// [2026-10-23 변경] 같은 조건(증폭기 하나라도 생존)의 피해 무효가 생겼다. 판정은 EncounterParticipantImmunity가 하고, 막은 그 상태를 보여 주는 연출이다.
/// </para>
/// 이 컴포넌트는 피해 규칙을 바꾸지 않는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BattleCharactor))]
public sealed class YuliaStasisVfx : MonoBehaviour
{
    [Header("Shield")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private UliaVfx.YuliaVfxRig productionVfxRigPrefab;
    [Tooltip("막 중심의 높이(유닛 루트 기준, m). 몸 전체가 막 안에 보이도록.")]
    [SerializeField] private float shieldHeight = 0.9f;
    [SerializeField, Min(0.01f)] private float shieldWorldScale = 1.2f;
    [Tooltip("증폭기 전멸·율리아 사망 시 막이 줄어들며 사라지는 시간(전투 배속 기준 초).")]
    [SerializeField, Min(0f)] private float shieldDismissSeconds = 0.3f;

    private BattleCharactor _boss;
    private readonly List<BattleCharactor> _amplifiers = new List<BattleCharactor>();
    private GameObject _shield;
    private UliaVfx.YuliaVfxRig _productionShield;
    private bool _initialShieldGenerated;
    private bool _stasis;

    public bool IsInStasis => _stasis;

    public void PlayShieldHit(Vector3 attackerPosition)
    {
        if (_productionShield != null && _productionShield.IsShieldVisible)
            _productionShield.HitShield(attackerPosition);
    }

    private void Awake()
    {
        _boss = GetComponent<BattleCharactor>();
    }

    private void OnEnable()
    {
        _boss.OnDied += HandleDied;
    }

    private void Start()
    {
        // 스폰·초기화(EnemyScript.Initialize)가 끝난 뒤 감금한다.
        bool encounterScene = FindFirstObjectByType<EncounterBlackboard>() != null;
        if (!ShouldEnterStasisOnSpawn(encounterScene, _stasis, _boss != null && !_boss.IsDead)) return;

        BindAmplifiers();   // 증폭기는 율리아와 같은 프레임에 스폰된다.
        _stasis = true;
        RefreshShield();
    }

    private void OnDisable()
    {
        _boss.OnDied -= HandleDied;
        UnbindAmplifiers();
    }

    // 증폭기 사망(HP 0)·부활(HP 복귀)은 모두 OnHpChanged로 들어온다. 시체는 남아 있어 같은 오브젝트를 계속 본다.
    private void BindAmplifiers()
    {
        UnbindAmplifiers();
        foreach (EncounterParticipant participant in FindObjectsByType<EncounterParticipant>(FindObjectsSortMode.None))
        {
            BattleCharactor unit = participant.GetComponent<BattleCharactor>();
            if (unit == null || unit == _boss) continue;
            unit.OnHpChanged += HandleAmplifierHpChanged;
            unit.OnDied += HandleAmplifierDied;
            _amplifiers.Add(unit);
        }
    }

    private void UnbindAmplifiers()
    {
        for (int i = 0; i < _amplifiers.Count; i++)
        {
            if (_amplifiers[i] == null) continue;
            _amplifiers[i].OnHpChanged -= HandleAmplifierHpChanged;
            _amplifiers[i].OnDied -= HandleAmplifierDied;
        }
        _amplifiers.Clear();
    }

    private void HandleAmplifierHpChanged(float currentHp, float maxHp) => RefreshShield();

    private void HandleAmplifierDied(BattleCharactor _) => RefreshShield();

    private int CountAliveAmplifiers()
    {
        int alive = 0;
        for (int i = 0; i < _amplifiers.Count; i++)
            if (_amplifiers[i] != null && !_amplifiers[i].IsDead) alive++;
        return alive;
    }

    // 감금 중에만 막을 증폭기 생존에 맞춘다. 율리아가 죽은 뒤에는 HandleDied가 정리한다.
    private void RefreshShield()
    {
        if (!_stasis || _boss == null || _boss.IsDead) return;

        if (ShouldShowShield(CountAliveAmplifiers())) SpawnShield();
        else DismissCurrentShield();
    }

    private void SpawnShield()
    {
        if (shieldPrefab == null || _shield != null) return;

        if (productionVfxRigPrefab != null)
        {
            _shield = new GameObject("StasisShieldPivot");
            _shield.transform.SetParent(transform, false);
            _shield.transform.localPosition = Vector3.up * shieldHeight;
            _productionShield = Instantiate(productionVfxRigPrefab, transform);
            Animator animator = GetComponentInChildren<Animator>();
            YuliaHandOrbState handOrb = GetComponentInChildren<YuliaHandOrbState>(true);
            Transform handSocket = handOrb != null && handOrb.transform.parent != null
                ? handOrb.transform.parent : transform;
            if (animator != null)
            {
                _productionShield.Bind(transform, animator, handSocket, _shield.transform,
                    2.061252f * shieldWorldScale / 1.15f, Camera.main);
                if (!_initialShieldGenerated)
                {
                    var cores = new List<Vector3>();
                    foreach (BattleCharactor amplifier in _amplifiers)
                    {
                        if (amplifier == null) continue;
                        AmplifierOrbHealthVisual visual = amplifier.GetComponentInChildren<AmplifierOrbHealthVisual>(true);
                        cores.Add(visual != null && visual.Orb != null
                            ? visual.Orb.position : amplifier.transform.position + Vector3.up);
                    }
                    if (cores.Count > 0) _productionShield.GenerateShield(cores.ToArray());
                    else _productionShield.ShowShieldPop();
                    _initialShieldGenerated = true;
                }
                else _productionShield.ShowShieldPop();
                return;
            }

            Destroy(_productionShield.gameObject);
            _productionShield = null;
            Destroy(_shield);
            _shield = null;
        }

        // 막 프리팹(Sci-Fi Shield 메시)은 자체 등장 애니메이션이 루트 스케일을 0→1로 키운다.
        // 크기는 피벗에 걸고 막은 그 아래에서 애니메이션하게 둔다.
        _shield = new GameObject("StasisShieldPivot");
        _shield.transform.SetParent(transform, false);
        _shield.transform.position = transform.position + Vector3.up * shieldHeight;
        _shield.transform.localScale = Vector3.one * (shieldWorldScale / Mathf.Max(0.0001f, transform.lossyScale.x));
        GameObject shield = Instantiate(shieldPrefab, _shield.transform, false);
        JC.VFX.VfxEffect effect = shield.GetComponent<JC.VFX.VfxEffect>();
        if (effect != null) effect.Play();
    }

    private void HandleDied(BattleCharactor _)
    {
        // 율리아 처치는 즉시 승리. 막을 정리한다.
        _stasis = false;
        DismissCurrentShield();
        UnbindAmplifiers();
    }

    private void DismissCurrentShield()
    {
        if (_shield == null) return;
        if (_productionShield != null)
        {
            UliaVfx.YuliaVfxRig rig = _productionShield;
            _productionShield = null;
            rig.BreakShield(rig.transform.position + Vector3.back);
            StartCoroutine(DestroyProductionShield(rig));
        }
        StartCoroutine(DismissShield(_shield));
        _shield = null;
    }

    private IEnumerator DestroyProductionShield(UliaVfx.YuliaVfxRig rig)
    {
        yield return new WaitForSeconds(1.7f / BattleSpeed());
        if (rig != null) Destroy(rig.gameObject);
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

    private static float BattleSpeed()
    {
        return BattleManager.Instance != null ? Mathf.Max(0.01f, BattleManager.Instance.CurrentBattleSpeed) : 1f;
    }

    /// <summary>등장 시 정지장에 들어갈지. 율리아전 씬(블랙보드 있음)이고, 아직 감금 전이며, 보스가 살아 있을 때만.</summary>
    public static bool ShouldEnterStasisOnSpawn(bool encounterScene, bool alreadyEnteringOrActive, bool bossAlive)
    {
        return encounterScene && !alreadyEnteringOrActive && bossAlive;
    }

    /// <summary>막을 보일지. 증폭기가 하나라도 살아 있을 때만(전부 쓰러져 부활 대기 중이면 숨김).</summary>
    public static bool ShouldShowShield(int aliveAmplifiers)
    {
        return aliveAmplifiers > 0;
    }

    /// <summary>사망 시 막 크기 배율(0~1 진행도). 1에서 0으로 부드럽게 줄어든다.</summary>
    public static float DismissScale(float normalized)
    {
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized));
    }
}
