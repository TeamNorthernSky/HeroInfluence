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

    // [JC 260902] 유니티는 플레이어가 포커스를 잃으면 OS 커서를 되돌려 놓는다(에디터의 씬 뷰 전환, 빌드의 Alt-Tab).
    // Awake에서 한 번 끈 것만으로는 복귀 후에도 켜진 채로 남아 가짜 커서와 겹쳐 2개로 보이므로, 포커스를 되찾을 때 재적용한다.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
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
