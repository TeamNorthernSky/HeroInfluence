using UnityEngine;

/// <summary>
/// Keeps the amplifier's 2010 orb visible while its battle unit has HP.
/// The amplifier model remains in the scene after death; only the orb is toggled.
/// </summary>
public sealed class AmplifierOrbHealthVisual : MonoBehaviour
{
    [SerializeField] private GameObject orb;

    private BattleCharactor owner;

    private void OnEnable()
    {
        owner = GetComponentInParent<BattleCharactor>();
        if (owner == null)
        {
            return;
        }

        owner.OnHpChanged += HandleHpChanged;
        HandleHpChanged(owner.CurrentHp, owner.MaxHp);
    }

    private void OnDisable()
    {
        if (owner != null)
        {
            owner.OnHpChanged -= HandleHpChanged;
        }

        owner = null;
    }

    private void HandleHpChanged(float currentHp, float maxHp)
    {
        if (orb != null)
        {
            orb.SetActive(currentHp > 0f);
        }
    }
}
