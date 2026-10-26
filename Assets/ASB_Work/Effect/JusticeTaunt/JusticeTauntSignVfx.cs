using UnityEngine;

// Visual observer only. Taunt rules and turn duration stay in StatusEffectManager.
public sealed class JusticeTauntSignVfx : MonoBehaviour
{
    [SerializeField] private GameObject signPrefab;
    [SerializeField] private float height = 2.4f;
    private BattleCharactor owner;
    private BattleCharactor source;
    private GameObject sign;

    private void OnEnable() => Bind();
    private void Start() => Bind();

    private void Bind()
    {
        if (owner != null) return;
        owner = GetComponentInParent<BattleCharactor>(true);
        if (owner == null) return;
        owner.OnStatusEffectsChanged += Refresh;
        owner.OnDied += OnUnitDied;
        Refresh();
    }

    private void Refresh()
    {
        StatusEffectInstance taunt = null;
        if (owner != null && !owner.IsDead)
            foreach (var effect in owner.ActiveStatusEffects)
                if (effect != null && effect.effectType == StatusEffectType.taunt && effect.remainingTurns > 0)
                { taunt = effect; break; }
        var next = taunt != null ? taunt.source : null;
        if (next != source)
        {
            if (source != null) source.OnDied -= OnSourceDied;
            source = next;
            if (source != null) source.OnDied += OnSourceDied;
        }
        bool show = source != null && !source.IsDead && source.PresentationIndex == "10001";
        if (!show) { ClearSign(); return; }
        if (sign == null && signPrefab != null)
        {
            sign = Instantiate(signPrefab, owner.transform.position + Vector3.up * height, Quaternion.identity);
            sign.name = "JusticeTauntSign";
            sign.transform.SetParent(owner.transform, true);
            foreach (var ps in sign.GetComponentsInChildren<ParticleSystem>(true)) ps.Play(false);
        }
    }

    private void LateUpdate()
    {
        if (sign != null && owner != null)
            sign.transform.position = owner.transform.position + Vector3.up * height;
    }

    private void OnUnitDied(BattleCharactor unit) => ClearSign();
    private void OnSourceDied(BattleCharactor unit) => Refresh();

    private void ClearSign()
    {
        if (sign == null) return;
        sign.SetActive(false);
        Destroy(sign);
        sign = null;
    }

    private void OnDisable()
    {
        if (owner != null)
        {
            owner.OnStatusEffectsChanged -= Refresh;
            owner.OnDied -= OnUnitDied;
        }
        if (source != null) source.OnDied -= OnSourceDied;
        owner = source = null;
        ClearSign();
    }
}
