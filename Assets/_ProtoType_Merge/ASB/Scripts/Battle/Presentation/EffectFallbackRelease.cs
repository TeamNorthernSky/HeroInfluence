using System.Collections;
using UnityEngine;

/// <summary>
/// 핸들로 회수되지 않는 이펙트 인스턴스에 수명 상한을 보장한다.
/// <para>
/// 패턴 H 규칙: 유지형 재료는 반드시 fallback 수명(파티클 종료 또는 타임아웃)을 가져야 한다.
/// <see cref="ISkillEffectHandle"/>로 등록되지 못한 인스턴스는 <c>StopAllHandles</c>/<c>StopAndRemoveHandle</c>
/// 대상이 아니어서 아무도 회수하지 않으므로, 스폰한 쪽이 이 컴포넌트로 수명을 걸어준다.
/// </para>
/// <para>
/// 파티클이 하나도 없는 재료(메시 오브 등)는 상한 시간을 그대로 기다린다.
/// 파티클 유무로 즉시 파괴하면 메시만 있는 이펙트가 스폰 직후 사라진다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class EffectFallbackRelease : MonoBehaviour
{
    /// <summary>기본 수명 상한(초). <see cref="OneShotEffectBehaviour"/>의 기본값과 맞춘다.</summary>
    public const float DefaultFallbackSeconds = 2f;

    [SerializeField, Min(0f)] private float fallbackSeconds = DefaultFallbackSeconds;

    private bool _running;

    /// <summary>
    /// 인스턴스에 수명 보장을 붙인다. 이미 자체 수명 관리가 있으면 아무것도 하지 않는다.
    /// </summary>
    public static void Ensure(GameObject instance, float fallbackSeconds = DefaultFallbackSeconds)
    {
        if (instance == null)
        {
            return;
        }

        // OneShotEffectBehaviour는 스스로 파티클 종료를 보고 정리한다. 중복으로 걸지 않는다.
        if (instance.GetComponent<OneShotEffectBehaviour>() != null)
        {
            return;
        }

        if (instance.GetComponent<EffectFallbackRelease>() != null)
        {
            return;
        }

        EffectFallbackRelease release = instance.AddComponent<EffectFallbackRelease>();
        release.fallbackSeconds = Mathf.Max(0f, fallbackSeconds);
        release.Begin();
    }

    /// <summary>수명 카운트를 시작한다. 이미 시작했으면 무시한다.</summary>
    public void Begin()
    {
        if (_running || !isActiveAndEnabled)
        {
            return;
        }

        _running = true;
        StartCoroutine(ReleaseRoutine());
    }

    private void OnEnable()
    {
        // 프리팹에 직접 붙여둔 경우(인스펙터 배선)에도 동작하도록 한다.
        Begin();
    }

    private IEnumerator ReleaseRoutine()
    {
        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        bool hasParticles = particles != null && particles.Length > 0;

        float elapsed = 0f;
        while (elapsed < fallbackSeconds)
        {
            // 파티클이 있는 재료는 전부 끝나면 상한을 기다리지 않고 즉시 정리한다.
            if (hasParticles && !AnyParticleAlive(particles))
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    private static bool AnyParticleAlive(ParticleSystem[] particles)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] != null && particles[i].IsAlive(true))
            {
                return true;
            }
        }

        return false;
    }
}
