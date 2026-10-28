using UnityEngine;

/// <summary>One mouse press may dismiss only the modal that was on top when it began.</summary>
[DefaultExecutionOrder(-32000)]
public sealed class ModalPointerCloseGate : MonoBehaviour
{
    private static GameObject pressedModal;
    private static bool captured;
    private static bool consumed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        pressedModal = null;
        captured = false;
        consumed = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var host = new GameObject("ModalPointerCloseGate");
        DontDestroyOnLoad(host);
        host.AddComponent<ModalPointerCloseGate>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) Capture(ModalManager.Top);
        else if (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0)) captured = false;
    }

    private static void Capture(GameObject top)
    {
        pressedModal = top;
        captured = true;
        consumed = false;
    }

    public static bool TryClose(GameObject modal)
    {
        if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
            return true;
        return Consume(modal);
    }

    private static bool Consume(GameObject modal)
    {
        if (!captured) return true;
        if (consumed || modal != pressedModal) return false;
        consumed = true;
        return true;
    }
}
