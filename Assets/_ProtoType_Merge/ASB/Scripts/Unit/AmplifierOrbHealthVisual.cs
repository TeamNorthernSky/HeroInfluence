using System.Collections;
using UnityEngine;

/// <summary>
/// Keeps the amplifier's 2010 orb visible while its battle unit has HP.
/// The amplifier model remains in the scene after death; only the orb is toggled.
/// <para>
/// 사망(기획 A03): 빛이 사그라지듯 구체가 줄어든 뒤 숨기고, 그 자리에서 연기를 피운다.
/// 부활하면 원래 크기로 즉시 다시 켠다. 처음 활성화될 때의 상태 반영은 연출 없이 즉시 한다.
/// </para>
/// </summary>
public sealed class AmplifierOrbHealthVisual : MonoBehaviour
{
    [SerializeField] private GameObject orb;

    [Header("Death (기획 A03)")]
    [SerializeField]
    [Tooltip("구체가 사라진 뒤 피어오를 연기(Stylized Smoke). 비우면 숨김만 한다.")]
    private GameObject deathSmokePrefab;

    [SerializeField, Min(0f)]
    [Tooltip("빛이 사그라지며 구체가 줄어드는 시간(전투 배속 기준 초). 0이면 즉시 숨김.")]
    private float fadeSeconds = 0.4f;

    private BattleCharactor owner;
    private Coroutine _fade;
    private Vector3 _orbScale = Vector3.one;
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
            _orbScaleCached = true;
        }

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
            if (_fade != null)
            {
                StopCoroutine(_fade);
                _fade = null;
            }

            orb.transform.localScale = _orbScale;
            orb.SetActive(true);
            _shownAlive = true;
            return;
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

    private void FinishHide(bool spawnSmoke)
    {
        Vector3 position = orb.transform.position;
        orb.SetActive(false);
        // 부활 때 줄어든 크기로 다시 켜지지 않게 원래 크기로 돌려 둔다.
        orb.transform.localScale = _orbScale;
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
}
