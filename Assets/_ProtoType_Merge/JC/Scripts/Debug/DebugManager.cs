using UnityEngine;

public class DebugManager : MonoBehaviour
{
    public static DebugManager Instance { get; private set; }

    [SerializeField] private KeyCode toggleKey = KeyCode.BackQuote;

    public KeyCode ToggleKey => toggleKey;

    private DebugPanelController panelController;

    public void Initialize()
    {
        Instance = this;
        panelController = GetComponentInChildren<DebugPanelController>(true);
        if (panelController != null)
        {
            panelController.Initialize();
        }
    }

    public void TogglePanel()
    {
        if (panelController != null)
        {
            panelController.Toggle();
        }
    }
}
