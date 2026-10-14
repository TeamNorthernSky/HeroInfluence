using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>짧은 클릭은 Button이 처리하고, 0.5초 이상 누르면 반복한다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class PublicityButtonHoldRepeat : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private const float HoldDelay = 0.5f;
    private const float RepeatInterval = 0.05f;
    private Button button;
    private PointerEventData heldPointer;
    private float nextRepeatTime;

    public static void Attach(Button target)
    {
        if (target != null && target.GetComponent<PublicityButtonHoldRepeat>() == null)
            target.gameObject.AddComponent<PublicityButtonHoldRepeat>();
    }

    private void Awake() => button = GetComponent<Button>();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (heldPointer != null || eventData.button != PointerEventData.InputButton.Left ||
            !button.IsActive() || !button.IsInteractable()) return;
        heldPointer = eventData;
        nextRepeatTime = Time.unscaledTime + HoldDelay;
    }

    private void Update()
    {
        if (heldPointer == null) return;
        if (!button.IsActive() || !button.IsInteractable()) { heldPointer = null; return; }
        if (Time.unscaledTime < nextRepeatTime) return;
        // 손을 놓을 때 일반 클릭이 추가로 발생하지 않게 한다.
        heldPointer.eligibleForClick = false;
        nextRepeatTime = Time.unscaledTime + RepeatInterval;
        button.onClick.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (heldPointer != null && eventData.pointerId == heldPointer.pointerId)
            heldPointer = null;
    }

    public void OnPointerExit(PointerEventData eventData) => OnPointerUp(eventData);
    private void OnDisable() => heldPointer = null;
    private void OnApplicationFocus(bool focused) { if (!focused) heldPointer = null; }
}
