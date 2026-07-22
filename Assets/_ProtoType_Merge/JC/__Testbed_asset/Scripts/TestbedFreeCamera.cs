using UnityEngine;

/// <summary>
/// 테스트베드 전용 프리 카메라. ShaderTestbed의 FreeFlyCamera + OrbitCameraRig 통합본.
/// - 우클릭 홀드 + WASD: 카메라 기준 전후/좌우 이동 (Shift 가속 / Ctrl 감속)
///   ※ 우클릭 없이 WASD는 무동작 — 테스트베드 스킬 키(Q,W,E,R,…)와의 충돌 방지 (260722)
/// - 우클릭(누르는 동안): 마우스 시점 회전
/// - 우클릭+좌클릭 동시 홀드: W/S가 상하 이동으로 전환 (A/D는 좌우 유지)
/// - Alt+좌클릭 드래그: 피벗(타겟 또는 시선 전방 지점) 주위 오빗
/// - 휠: 시선 방향 줌(달리)
/// </summary>
public class TestbedFreeCamera : MonoBehaviour
{
    [Header("Move Speed")]
    [Tooltip("기본 이동 속도 (units/sec). 적정값 3~10. 작은 씬일수록 낮게.")]
    public float moveSpeed = 6f;

    [Tooltip("Shift를 누르고 있을 때 속도 배수. 적정값 2~6.")]
    public float fastMultiplier = 3f;

    [Tooltip("Ctrl을 누르고 있을 때 속도 배수(감속). 적정값 0.1~0.5.")]
    public float slowMultiplier = 0.25f;

    [Header("Look")]
    [Tooltip("우클릭 시점 회전 감도. 적정값 1~5. 높을수록 빠르게 돈다.")]
    public float lookSensitivity = 3f;

    [Tooltip("위/아래로 올려다보거나 내려다볼 수 있는 최대 각도(도). 적정값 80~89.")]
    public float pitchClamp = 89f;

    [Header("Orbit (Alt+좌클릭)")]
    [Tooltip("오빗 피벗. 비워두면 시선 전방 orbitDefaultDistance 지점을 피벗으로 사용.")]
    public Transform orbitTarget;

    [Tooltip("orbitTarget이 없을 때 피벗까지의 기본 거리. 라인업 관찰 기준 10~15.")]
    public float orbitDefaultDistance = 12f;

    [Tooltip("오빗 회전 속도 (deg/sec 환산 계수). 적정값 120~240.")]
    public float orbitSpeed = 180f;

    [Header("Zoom (휠)")]
    [Tooltip("휠 1틱당 이동 거리 계수. 적정값 4~12.")]
    public float zoomSpeed = 8f;

    float _yaw;
    float _pitch;
    Vector3 _orbitPivot;
    bool _orbiting;

    void Start()
    {
        Vector3 e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = e.x;
        if (_pitch > 180f) _pitch -= 360f; // 0~360 표기를 -180~180로 보정
    }

    void Update()
    {
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool lmb = Input.GetMouseButton(0);
        bool rmb = Input.GetMouseButton(1);

        // --- 오빗 (Alt+좌클릭) ---
        if (alt && lmb)
        {
            if (!_orbiting)
            {
                _orbiting = true;
                _orbitPivot = orbitTarget != null
                    ? orbitTarget.position
                    : transform.position + transform.forward * orbitDefaultDistance;
            }
            float dist = Vector3.Distance(transform.position, _orbitPivot);
            _yaw += Input.GetAxis("Mouse X") * orbitSpeed * Time.unscaledDeltaTime;
            _pitch -= Input.GetAxis("Mouse Y") * orbitSpeed * Time.unscaledDeltaTime;
            _pitch = Mathf.Clamp(_pitch, -pitchClamp, pitchClamp);
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = _orbitPivot - rot * Vector3.forward * dist;
            transform.rotation = rot;
            return; // 오빗 중에는 이동/시점 입력 무시
        }
        _orbiting = false;

        // --- 시점 회전 (우클릭 중) ---
        if (rmb)
        {
            _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -pitchClamp, pitchClamp);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // --- 이동 (우클릭 홀드 중에만 — WASD가 스킬 테스트 키와 겹치지 않게) ---
        if (rmb)
        {
            float speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) speed *= fastMultiplier;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) speed *= slowMultiplier;

            float h = Input.GetAxisRaw("Horizontal"); // A/D
            float v = Input.GetAxisRaw("Vertical");   // W/S

            Vector3 move;
            if (lmb)
                move = transform.right * h + Vector3.up * v;      // 우+좌클릭 중: W/S = 상하
            else
                move = transform.forward * v + transform.right * h; // 우클릭 중: W/S = 전후

            transform.position += move * speed * Time.unscaledDeltaTime;
        }

        // --- 휠 줌 (시선 방향 달리, 틱당 이산 이동) ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
            transform.position += transform.forward * scroll * zoomSpeed * 3f;
    }
}
