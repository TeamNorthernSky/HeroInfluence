using UnityEngine;

[DisallowMultipleComponent]
public class FakeMouseCursor : MonoBehaviour
{
    [SerializeField] private RectTransform _cursorRect;
    private static FakeMouseCursor Instance;

    private void Reset()
    {
        _cursorRect = transform as RectTransform;
    }

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 수명은 부모(CommonUIManager 루트)의 DontDestroyOnLoad를 따른다.
        // 자식 GameObject에 DontDestroyOnLoad를 걸면 Unity가 무시하므로 여기서는 호출하지 않는다.
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        if (_cursorRect == null)
            _cursorRect = transform as RectTransform;

        if (_cursorRect == null)
        {
            Debug.LogError("FakeMouseCursor requires a RectTransform.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // Screen Space - Overlay 캔버스 전제: RectTransform.position이 스크린 픽셀 좌표와 1:1로 대응한다.
        _cursorRect.position = Input.mousePosition;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
