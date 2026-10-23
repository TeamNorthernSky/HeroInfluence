using JC.VFX;
using UnityEngine;

/// <summary>협회용 솔라 프리즘의 비행·착탄 연출이 모두 끝난 뒤 인스턴스를 정리한다.</summary>
[RequireComponent(typeof(SolarPrismVfx))]
public sealed class AssociationSolarPrismCleanup : MonoBehaviour
{
    private SolarPrismVfx effect;

    private void Awake()
    {
        effect = GetComponent<SolarPrismVfx>();
        effect.OnFinished += OnFinished;
    }

    private void OnDestroy()
    {
        if (effect != null) effect.OnFinished -= OnFinished;
    }

    private void OnFinished(VfxEffect finished)
    {
        Destroy(gameObject, 0.3f);
    }
}
