using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 클릭 시 대상 모달의 활성 상태를 토글한다(열림↔닫힘).
/// 출전 버튼처럼 "같은 버튼 재클릭으로 닫기"가 필요한 곳에 사용.
/// </summary>
[DisallowMultipleComponent]
public class ModalToggleButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject target;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Toggle);
    }

    public void Toggle()
    {
        if (target != null) target.SetActive(!target.activeSelf);
    }
}
