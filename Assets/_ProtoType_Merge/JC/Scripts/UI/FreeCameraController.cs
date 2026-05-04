using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 프리 카메라 컨트롤러. Unity Scene View 스타일 자유 시점 이동.
/// Main Camera의 GameObject에 부착해 사용.
///
/// 작동 조건 (두 조건 모두 충족해야 함):
///   1. 활성 씬 이름이 "PlayScene"
///   2. PlayGridManagerJC 인스턴스가 씬에 존재
///
/// 활성화 시 MapCameraController와 PlayerControllerJC를 자동 비활성화,
/// 비활성화 시 저장된 카메라 위치/회전을 복원하고 두 컴포넌트 복원.
///
/// 토글: Inspector의 `freeCameraEnabled` 체크박스, 또는 런타임 Backspace 키.
///
/// 조작:
///   - 우클릭 드래그: 마우스 룩 (yaw + pitch)
///   - WASD: 전/후/좌/우 이동 (카메라 local)
///   - Q/E: 수직 하강/상승 (월드 Y축)
///   - Shift 홀드: 빠른 이동
///   - 마우스 휠: 전진/후진 (local forward)
/// </summary>
public class FreeCameraController : MonoBehaviour
{
    private const string AllowedSceneName = "PlayScene";

    [Header("Activation")]
    [Tooltip("프리 카메라 모드 On/Off. 런타임에 Backspace 키로도 토글 가능")]
    [SerializeField] private bool freeCameraEnabled = false;

    [Header("Move Speed")]
    [SerializeField, Min(0f)] private float moveSpeed = 10f;
    [SerializeField, Min(1f)] private float fastMoveMultiplier = 3f;
    [SerializeField, Min(0f)] private float verticalSpeed = 10f;
    [SerializeField, Min(0f)] private float zoomSpeed = 15f;

    [Header("Look Speed")]
    [SerializeField, Min(0f)] private float lookSpeed = 2f;
    [SerializeField, Range(1f, 89f)] private float pitchLimit = 89f;

    // === 런타임 상태 ===
    private MapCameraController mapCameraController;
    private PlayerControllerJC playerController;

    // Enable 시점 저장 상태
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private bool savedMapCamEnabled;
    private bool savedPlayerControllerEnabled;

    // 모드 전환 감지
    private bool prevEnabled = false;

    // 마우스 룩 상태 (euler yaw/pitch 누적)
    private float yaw;
    private float pitch;

    private void Start()
    {
        mapCameraController = GetComponent<MapCameraController>();
        playerController = FindFirstObjectByType<PlayerControllerJC>();

        // 시작 시 프리 모드가 켜진 상태로 serialize 되어 있을 수 있으므로 동기화
        prevEnabled = false;
    }

    private void Update()
    {
        // === 씬 제한 체크 (방법 A + B) ===
        if (SceneManager.GetActiveScene().name != AllowedSceneName) return;
        if (GameManager.Instance == null || FindFirstObjectByType<PlayGridManagerJC>() == null) return;

        // === Backspace 런타임 토글 ===
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            freeCameraEnabled = !freeCameraEnabled;
        }

        // === 모드 전환 감지 ===
        if (freeCameraEnabled != prevEnabled)
        {
            if (freeCameraEnabled) EnterFreeMode();
            else ExitFreeMode();
            prevEnabled = freeCameraEnabled;
        }

        if (!freeCameraEnabled) return;

        // === 프리 카메라 조작 ===
        HandleLook();
        HandleMove();
    }

    private void EnterFreeMode()
    {
        // 런타임에 AddComponent로 추가되는 컴포넌트 대비, EnterFreeMode 시점에 재탐색
        if (mapCameraController == null)
            mapCameraController = GetComponent<MapCameraController>();
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerControllerJC>();

        // 현재 카메라 상태 저장
        savedPosition = transform.position;
        savedRotation = transform.rotation;

        // yaw/pitch를 현재 회전에서 추출
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
        if (pitch > 180f) pitch -= 360f; // 0~360 → -180~180로 변환

        // 다른 컴포넌트 자동 비활성화
        if (mapCameraController != null)
        {
            savedMapCamEnabled = mapCameraController.enabled;
            mapCameraController.enabled = false;
        }
        if (playerController != null)
        {
            savedPlayerControllerEnabled = playerController.enabled;
            playerController.enabled = false;
        }

        Debug.Log("[FreeCameraController] Free mode ON");
    }

    private void ExitFreeMode()
    {
        // 저장된 카메라 상태 복원
        transform.position = savedPosition;
        transform.rotation = savedRotation;

        // 다른 컴포넌트 복원
        if (mapCameraController != null)
        {
            mapCameraController.enabled = savedMapCamEnabled;
        }
        if (playerController != null)
        {
            playerController.enabled = savedPlayerControllerEnabled;
        }

        Debug.Log("[FreeCameraController] Free mode OFF");
    }

    private void HandleLook()
    {
        // 우클릭 홀드 중에만 마우스 룩
        if (!Input.GetMouseButton(1)) return;

        float mx = Input.GetAxis("Mouse X") * lookSpeed;
        float my = Input.GetAxis("Mouse Y") * lookSpeed;

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleMove()
    {
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            speed *= fastMoveMultiplier;
        }

        // WASD — 카메라 local forward/right (Y 성분 포함, 회전 기준 이동)
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;
        if (Input.GetKey(KeyCode.D)) move += transform.right;

        // Q/E — 월드 Y축 수직 이동
        float vertical = 0f;
        if (Input.GetKey(KeyCode.E)) vertical += verticalSpeed;
        if (Input.GetKey(KeyCode.Q)) vertical -= verticalSpeed;

        transform.position += move * speed * Time.deltaTime;
        transform.position += Vector3.up * vertical * Time.deltaTime;

        // 마우스 휠 — local forward 전진/후진
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            transform.position += transform.forward * scroll * zoomSpeed;
        }
    }
}
