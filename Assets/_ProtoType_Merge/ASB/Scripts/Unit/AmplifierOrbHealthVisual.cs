using System.Collections;
using UnityEngine;

/// <summary>
/// Keeps the amplifier's 2010 orb visible while its battle unit has HP.
/// The amplifier model remains in the scene after death; only the orb is toggled.
/// <para>
/// 사망(기획 A03): 빛이 사그라지듯 구체가 줄어든 뒤 숨기고, 그 자리에서 연기를 피운다.
/// 복구(기획 A04): 부활하면 보조 발광을 한 번 켜고, 구체 아래 끝을 고정한 채 위로 차오르게 다시 만든다.
/// 다 차오른 뒤에 대기 전기를 켠다. 처음 활성화될 때의 상태 반영은 연출 없이 즉시 한다(전투 시작에는 발광 없음).
/// 형상 마스크 대신 세로 스케일을 쓴다 — 구체 셰이더(FlareBomb)는 루미나 2010 스킬과 공유라 건드리지 않는다.
/// </para>
/// </summary>
public sealed class AmplifierOrbHealthVisual : MonoBehaviour
{
    private const float MinRevealScale = 0.001f;

    [SerializeField] private GameObject orb;

    [Header("Death (기획 A03)")]
    [SerializeField]
    [Tooltip("구체가 사라진 뒤 피어오를 연기(Stylized Smoke). 비우면 숨김만 한다.")]
    private GameObject deathSmokePrefab;

    [SerializeField, Min(0f)]
    [Tooltip("빛이 사그라지며 구체가 줄어드는 시간(전투 배속 기준 초). 0이면 즉시 숨김.")]
    private float fadeSeconds = 0.4f;

    [Header("Revive (기획 A04)")]
    [SerializeField, Min(0f)]
    [Tooltip("부활 때 아래부터 위로 차오르는 시간(전투 배속 기준 초). 0이면 즉시 표시.")]
    private float reviveSeconds = 0.6f;

    [SerializeField]
    [Tooltip("부활 시작에 재생할 보조 발광(구체 자식). 평소에는 꺼 두고 부활 때만 켠다.")]
    private JC.VFX.VfxEffect reviveFlash;

    [SerializeField]
    [Tooltip("다 차오른 뒤 켤 대기 전기(구체 자식). 차오르는 동안은 끈다.")]
    private GameObject idleElectric;

    private BattleCharactor owner;
    private Coroutine _fade;
    private Coroutine _revive;
    private Vector3 _orbScale = Vector3.one;
    private Vector3 _orbPosition;
    private bool _orbScaleCached;
    private bool _shownAlive;

    /// <summary>2010 구체. 흡수 연출 등 외부 이펙트의 기준 위치.</summary>
    public Transform Orb => orb != null ? orb.transform : null;

    private void OnEnable()
    {
        owner = GetComponentInParent<BattleCharactor>();
        if (owner == null)
        {
            return;
        }

        if (orb != null && !_orbScaleCached)
        {
            _orbScale = orb.transform.localScale;
            _orbPosition = orb.transform.localPosition;
            _orbScaleCached = true;
        }

        // 보조 발광은 부활 때만 켠다(구체가 켜질 때마다 자동 재생되면 전투 시작에도 번쩍인다).
        if (reviveFlash != null) reviveFlash.gameObject.SetActive(false);

        owner.OnHpChanged += HandleHpChanged;
        SetShown(owner.CurrentHp > 0f, false);
    }

    private void OnDisable()
    {
        if (owner != null)
        {
            owner.OnHpChanged -= HandleHpChanged;
        }

        if (_fade != null)
        {
            StopCoroutine(_fade);
            _fade = null;
            FinishHide(false);
        }

        if (_revive != null)
        {
            StopCoroutine(_revive);
            _revive = null;
            FinishRevive();
        }

        owner = null;
    }

    private void HandleHpChanged(float currentHp, float maxHp)
    {
        SetShown(currentHp > 0f, true);
    }

    private void SetShown(bool alive, bool animate)
    {
        if (orb == null)
        {
            return;
        }

        if (alive)
        {
            // 차오르는 중에 들어온 체력 변화(회복 등)는 연출을 끊지 않는다.
            if (_revive != null) return;

            bool revived = !_shownAlive;
            if (_fade != null)
            {
                StopCoroutine(_fade);
                _fade = null;
            }

            _shownAlive = true;
            if (ShouldAnimateRevive(revived, animate, reviveSeconds) && isActiveAndEnabled)
            {
                _revive = StartCoroutine(Revive());
                return;
            }

            orb.transform.localScale = _orbScale;
            orb.transform.localPosition = _orbPosition;
            orb.SetActive(true);
            if (idleElectric != null) idleElectric.SetActive(true);
            return;
        }

        if (_revive != null)
        {
            StopCoroutine(_revive);
            _revive = null;
            FinishRevive();
        }

        // 이미 숨겼거나 사라지는 중이면 같은 사망 이벤트를 다시 처리하지 않는다.
        if (!_shownAlive && (!orb.activeSelf || _fade != null))
        {
            return;
        }

        _shownAlive = false;
        if (animate && fadeSeconds > 0f && isActiveAndEnabled && orb.activeInHierarchy)
        {
            _fade = StartCoroutine(FadeOut());
        }
        else
        {
            FinishHide(animate);
        }
    }

    private IEnumerator FadeOut()
    {
        for (float t = 0f; t < fadeSeconds; t += Time.deltaTime * BattleSpeed())
        {
            orb.transform.localScale = _orbScale * FadeScale(t / fadeSeconds);
            yield return null;
        }

        _fade = null;
        FinishHide(true);
    }

    private IEnumerator Revive()
    {
        if (idleElectric != null) idleElectric.SetActive(false);
        ApplyReveal(0f);
        orb.SetActive(true);
        if (reviveFlash != null)
        {
            reviveFlash.gameObject.SetActive(true);
            reviveFlash.Play();
        }

        for (float t = 0f; t < reviveSeconds; t += Time.deltaTime * BattleSpeed())
        {
            ApplyReveal(RevealProgress(t / reviveSeconds));
            yield return null;
        }

        _revive = null;
        FinishRevive();
    }

    private void FinishRevive()
    {
        ApplyReveal(1f);
        if (idleElectric != null) idleElectric.SetActive(true);
    }

    // 아래 끝을 고정한 채 세로로 차오른다. 구체 메시는 지름 1(로컬) 기준이라 반지름 = 세로 스케일의 절반.
    private void ApplyReveal(float progress)
    {
        float p = Mathf.Max(MinRevealScale, progress);
        orb.transform.localScale = new Vector3(_orbScale.x, _orbScale.y * p, _orbScale.z);
        orb.transform.localPosition = _orbPosition + Vector3.down * RevealDrop(_orbScale.y * 0.5f, p);
    }

    private void FinishHide(bool spawnSmoke)
    {
        Vector3 position = orb.transform.position;
        orb.SetActive(false);
        // 부활 때 줄어든 크기로 다시 켜지지 않게 원래 크기로 돌려 둔다.
        orb.transform.localScale = _orbScale;
        orb.transform.localPosition = _orbPosition;
        if (!spawnSmoke || deathSmokePrefab == null)
        {
            return;
        }

        GameObject smoke = Instantiate(deathSmokePrefab, position, Quaternion.identity);
        JC.VFX.VfxEffect effect = smoke.GetComponent<JC.VFX.VfxEffect>();
        if (effect != null) effect.Play();
        else EffectFallbackRelease.Ensure(smoke);
    }

    private static float BattleSpeed()
    {
        return BattleManager.Instance != null ? Mathf.Max(0.01f, BattleManager.Instance.CurrentBattleSpeed) : 1f;
    }

    /// <summary>사라지는 진행도(0~1)에 따른 구체 크기 배율. 처음과 끝을 부드럽게.</summary>
    public static float FadeScale(float normalized)
    {
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized));
    }

    /// <summary>차오르는 진행도(0~1). 처음과 끝을 부드럽게.</summary>
    public static float RevealProgress(float normalized)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized));
    }

    /// <summary>세로 배율 progress일 때 아래 끝을 고정하려면 중심을 얼마나 내려야 하는지(로컬).</summary>
    public static float RevealDrop(float radius, float progress)
    {
        return radius * (1f - Mathf.Clamp01(progress));
    }

    /// <summary>죽어 있다가 연출 허용 상태로 살아났을 때만 차오름 연출.</summary>
    public static bool ShouldAnimateRevive(bool revived, bool animate, float reviveSeconds)
    {
        return revived && animate && reviveSeconds > 0f;
    }
}
