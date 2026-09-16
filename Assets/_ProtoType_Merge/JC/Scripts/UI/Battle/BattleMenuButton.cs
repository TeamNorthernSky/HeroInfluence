using UnityEngine;
using UnityEngine.UI;

/// <summary>전투씬 메뉴 버튼을 기존 공용 시스템 메뉴에 연결합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class BattleMenuButton : MonoBehaviour
{
    [Tooltip("메뉴 클릭을 받을 버튼입니다. 비우면 같은 오브젝트의 Button을 사용합니다.")]
    [SerializeField] private Button button;

    private SystemMenuController menu;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null) button = GetComponent<Button>();
        button.onClick.AddListener(OpenMenu);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OpenMenu);
    }

    private void OpenMenu()
    {
        // 공용 메뉴는 다른 씬에서 생성되어 DDOL로 유지될 수 있습니다.
        if (menu == null) menu = FindFirstObjectByType<SystemMenuController>(FindObjectsInactive.Include);
        if (menu != null)
            menu.OpenMenu();
        else
            Debug.LogWarning("[BattleMenuButton] 공용 시스템 메뉴를 찾을 수 없습니다.", this);
    }
}
