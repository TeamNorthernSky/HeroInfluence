using UnityEngine;

[DisallowMultipleComponent]
public class FakeMouseCursor : MonoBehaviour
{
    [SerializeField] private RectTransform _cursorRect;

    private void Reset()
    {
        _cursorRect = transform as RectTransform;
    }

    private void OnEnable()
    {
        if (_cursorRect == null)
            _cursorRect = transform as RectTransform;
    }

    private void Update()
    {
        if (_cursorRect == null) return;
        _cursorRect.position = Input.mousePosition;
    }

    private void LateUpdate()
    {
        if (_cursorRect == null) return;
        _cursorRect.SetAsLastSibling();
    }
}
