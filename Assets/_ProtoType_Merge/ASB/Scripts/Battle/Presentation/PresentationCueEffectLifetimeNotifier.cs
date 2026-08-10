using UnityEngine;

/// <summary>
/// Observes an existing one-shot Cue effect lifetime without changing it.
/// The effect's own OneShotEffectBehaviour or EffectFallbackRelease remains responsible for cleanup.
/// </summary>
[DisallowMultipleComponent]
public sealed class PresentationCueEffectLifetimeNotifier : MonoBehaviour
{
    private PresentationRuntimeContext owner;
    private bool registered;

    public void Bind(PresentationRuntimeContext nextOwner)
    {
        if (registered && owner == nextOwner)
        {
            return;
        }

        Release();
        owner = nextOwner;
        if (owner == null)
        {
            return;
        }

        registered = true;
        owner.RegisterActiveOneShotCueEffect(gameObject);
    }

    private void OnDisable()
    {
        Release();
    }

    private void OnDestroy()
    {
        Release();
    }

    private void Release()
    {
        if (!registered)
        {
            owner = null;
            return;
        }

        PresentationRuntimeContext previousOwner = owner;
        owner = null;
        registered = false;
        if (previousOwner != null)
        {
            previousOwner.UnregisterActiveOneShotCueEffect(gameObject);
        }
    }
}
