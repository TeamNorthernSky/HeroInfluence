using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [JC 260619] HeroInfoModal의 스킬/무기/무기스킬 버튼 롤오버 감지 → 모달이 SkillTooltip(리치)을 띄운다.
/// slot: 0=클래스스킬, 1=무기, 2=무기스킬. HeroInfoModal이 런타임에 버튼에 부착·Bind한다.
/// </summary>
[DisallowMultipleComponent]
public class HeroInfoButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private HeroInfoModal modal;
    private int slot;

    public void Bind(HeroInfoModal m, int s) { modal = m; slot = s; }

    public void OnPointerEnter(PointerEventData eventData) { if (modal != null) modal.OnButtonHover(slot, true); }
    public void OnPointerExit(PointerEventData eventData) { if (modal != null) modal.OnButtonHover(slot, false); }
    private void OnDisable() { if (modal != null) modal.OnButtonHover(slot, false); }
}
