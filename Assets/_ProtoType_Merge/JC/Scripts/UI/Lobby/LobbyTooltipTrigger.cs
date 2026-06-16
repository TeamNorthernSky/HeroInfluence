using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [JC 260617] 항목에 부착해 마우스 롤오버 시 <see cref="LobbyTooltip"/>에 내용을 띄운다.
/// 내용(content)은 인스펙터 고정값 또는 런타임 SetContent로 주입(컨트롤러가 상태별로 갱신).
/// content가 비면 표시하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class LobbyTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] [TextArea] private string content;

    private bool hovering;

    public void SetContent(string text)
    {
        content = text;
        // 표시 중이면 즉시 갱신
        if (hovering && LobbyTooltip.Instance != null)
        {
            if (string.IsNullOrEmpty(content)) LobbyTooltip.Instance.Hide();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (LobbyTooltip.Instance != null) LobbyTooltip.Instance.Show(content, transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        if (LobbyTooltip.Instance != null) LobbyTooltip.Instance.Hide();
    }

    private void OnDisable()
    {
        hovering = false;
        if (LobbyTooltip.Instance != null) LobbyTooltip.Instance.Hide();
    }
}
