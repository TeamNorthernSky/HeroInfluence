using System.Collections;
using UnityEngine;

/// <summary>월드에 독립적으로 재생되고 파티클 종료 후 정리되는 단발성 이펙트 재료.</summary>
public class OneShotEffectBehaviour : MonoBehaviour, ISkillEffectBehaviour
{
    [SerializeField, Min(0f)] private float fallbackLifetime = 2f;

    public void Play(SkillEffectContext ctx)
    {
        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++) particles[i].Play(true);
        StartCoroutine(ReleaseWhenFinished(particles));
    }

    private IEnumerator ReleaseWhenFinished(ParticleSystem[] particles)
    {
        float elapsed = 0f;
        while (elapsed < fallbackLifetime)
        {
            bool alive = false;
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null && particles[i].IsAlive(true)) { alive = true; break; }
            }
            if (!alive) break;
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
}