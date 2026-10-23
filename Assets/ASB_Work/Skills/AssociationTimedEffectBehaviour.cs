using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>Visual Effect Graph 기반 협회 단발 이펙트를 재생하고 수명을 제한한다.</summary>
public sealed class AssociationTimedEffectBehaviour : MonoBehaviour, ISkillEffectBehaviour, ISelfManagedEffectLifetime
{
    [SerializeField, Min(0.1f)] private float lifetime = 1.5f;

    public void Play(SkillEffectContext context)
    {
        foreach (VisualEffect effect in GetComponentsInChildren<VisualEffect>(true))
        {
            effect.gameObject.SetActive(true);
            effect.Play();
        }
        foreach (ParticleSystem particle in GetComponentsInChildren<ParticleSystem>(true))
            particle.Play(true);
        foreach (Animation animation in GetComponentsInChildren<Animation>(true))
            animation.Play();
        StartCoroutine(Release());
    }

    private IEnumerator Release()
    {
        yield return new WaitForSeconds(lifetime);
        Destroy(gameObject);
    }
}
