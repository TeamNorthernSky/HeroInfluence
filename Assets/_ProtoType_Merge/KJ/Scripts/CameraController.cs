using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float scrollSpeed = 15f;
    [Tooltip("화면 경계에서 마우스를 인식할 두께(픽셀 단위)")]
    public float edgeScrollSize = 20f;
    
    [Header("Zoom Settings")]
    public float zoomSpeed = 50f;
    public float minZoom = 15f;   // 최소 FOV (최대 확대)
    public float maxZoom = 90f;   // 최대 FOV (최대 축소)

    [Header("Rotation Settings")]
    public float lookSensitivity = 2f;
    public float returnRotationSpeed = 5f; // 우클릭 떼었을 때 원래 각도로 돌아가는 속도 (필요시)
    public bool invertY = false;

    private float pitch;
    private float yaw;
    private Camera _cam;

    private void Start()
    {
        _cam = GetComponent<Camera>();

        // 시작할 때 카메라의 현재 회전값을 가져와서 초기화
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x;
        yaw = angles.y;
    }

    private void Update()
    {
        //HandleRotation();
        HandleMovement();
    }

    private void HandleRotation()
    {
        // 1번 버튼 = 마우스 우클릭 (뷰 회전)
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            yaw += mouseX * lookSensitivity;
            pitch += mouseY * lookSensitivity * (invertY ? 1f : -1f);

            // 카메라가 360도 뒤집히지 않도록 피치(상하 회전각)를 제한
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }
    }

    private void HandleMovement()
    {
        Vector3 moveDirection = Vector3.zero;

        // 1. 화면 엣지 스크롤 (월드 좌표 기준 X, Z 이동)
        Vector3 mousePos = Input.mousePosition;
        
        // 오른쪽 / 왼쪽
        if (mousePos.x >= Screen.width - edgeScrollSize)      moveDirection.x = 1f; // +X 방향
        else if (mousePos.x <= edgeScrollSize)                moveDirection.x = -1f;// -X 방향

        // 위쪽 / 아래쪽
        if (mousePos.y >= Screen.height - edgeScrollSize)     moveDirection.z = 1f; // +Z 방향
        else if (mousePos.y <= edgeScrollSize)                moveDirection.z = -1f;// -Z 방향

        if (moveDirection != Vector3.zero)
        {
            // 카메라의 로컬 방향이 아닌 절대 좌표(월드 방향) 기준으로 상하좌우를 이동시킵니다.
            transform.position += moveDirection.normalized * (scrollSpeed * Time.deltaTime);
        }

        // 2. 마우스 휠 스크롤 확대/축소 (FOV 혹은 OrthographicSize 조절)
        float scrollData = Input.GetAxis("Mouse ScrollWheel");
        if (scrollData != 0f && _cam != null)
        {
            if (_cam.orthographic)
            {
                _cam.orthographicSize -= scrollData * zoomSpeed * 0.1f;
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize, minZoom, maxZoom);
            }
            else
            {
                // FOV 조절 방식으로 줌 인/아웃
                _cam.fieldOfView -= scrollData * zoomSpeed;
                _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView, minZoom, maxZoom);
            }
        }
    }
}
