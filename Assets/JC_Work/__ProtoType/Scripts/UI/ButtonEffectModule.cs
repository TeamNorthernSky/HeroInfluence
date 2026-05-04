using UnityEngine;

public abstract class ButtonEffectModule : MonoBehaviour
{
    [SerializeField] protected bool moduleEnabled = true;
    public bool ModuleEnabled => moduleEnabled;

    public virtual void OnHoverEnter() { }
    public virtual void OnHoverExit() { }
    public virtual void OnClick() { }
}
