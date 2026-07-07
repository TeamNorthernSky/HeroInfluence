using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ButtonEffectController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private ButtonEffectModule[] modules;
    private Selectable selectable;

    private void Awake()
    {
        modules = GetComponents<ButtonEffectModule>();
        selectable = GetComponent<Selectable>();
    }

    // [KJ 260706] 비활성(interactable=false) 버튼은 호버/클릭 연출 생략 — 작동되는 것처럼 보이는 착시 방지.
    // Selectable 없는 오브젝트는 기존대로 항상 연출. Exit는 게이트하지 않음(호버 중 비활성화 시 연출 잔류 방지).
    private bool EffectsAllowed => selectable == null || selectable.IsInteractable();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!EffectsAllowed) return;
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
        if (!EffectsAllowed) return;
        for (int i = 0; i < modules.Length; i++)
            if (modules[i].ModuleEnabled) modules[i].OnClick();
    }
}
