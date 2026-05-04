using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class ButtonEffectController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private ButtonEffectModule[] modules;

    private void Awake()
    {
        modules = GetComponents<ButtonEffectModule>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        for (int i = 0; i < modules.Length; i++)
            if (modules[i].ModuleEnabled) modules[i].OnHoverEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        for (int i = 0; i < modules.Length; i++)
            if (modules[i].ModuleEnabled) modules[i].OnHoverExit();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        for (int i = 0; i < modules.Length; i++)
            if (modules[i].ModuleEnabled) modules[i].OnClick();
    }
}
