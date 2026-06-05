using UnityEngine;

/// <summary>
/// 카메라를 대상(플레이어 등) 기준으로 쿼터뷰 오프셋만큼 유지하며 계속 따라가는 컴포넌트.
/// 카메라 GameObject에 붙여 사용합니다.
/// </summary>
[DisallowMultipleComponent]
public class QuarterViewCameraFollower : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private bool followEnabled = true;
    [SerializeField] private float followDelay = 0.5f;

    [Header("Offsets")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 12f, -8.6f);

    [Header("Smoothing")]
    [SerializeField] private float rotationLerp = 10f;

    [Header("Fixed Rotation")]
    [SerializeField] private Vector3 fixedEulerAngles = new Vector3(55f, 0f, 0f);

    [Header("Zoom")]
    [SerializeField] private float minZoomY = 5f;
    [SerializeField] private float maxZoomY = 30f;
    [SerializeField] private float zoomSpeed = 200f;

    [Header("Edge Scrolling")]
    [SerializeField] private bool edgeScrollEnabled = true;
    [SerializeField] private float edgeThresholdX = 40f;
    [SerializeField] private float edgeThresholdY = 40f;
    [SerializeField] private float edgeMinSpeed = 5f;
    [SerializeField] private float edgeMaxSpeed = 30f;
    [SerializeField] private float edgeAcceleration = 10f;
    [SerializeField] private float edgeDeceleration = 35f;
    [SerializeField] private bool snapStopWhenLeavingEdge = false;
    [SerializeField] private float edgeLimitRange = 50f;
    [SerializeField] private bool invertVerticalEdgeScroll = false;

    [Header("Reset")]
    [SerializeField] private KeyCode resetKey = KeyCode.Y;

    private Vector3 followVelocity;
    private Vector3 smoothedFollowAnchor;
    private Vector3 panOffset;
    private Vector3 edgeScrollVelocity;
    private float defaultZoomY;
    private bool hasSmoothedFollowAnchor;

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    public void SetFollowEnabled(bool enabled)
    {
        followEnabled = enabled;
    }

    public void RecenterOnFollowTarget()
    {
        panOffset = Vector3.zero;
        edgeScrollVelocity = Vector3.zero;
    }

    private void Awake()
    {
        positionOffset.x = 0f;
        defaultZoomY = positionOffset.y;
    }

    private void Update()
    {
        HandleZoomInput();
        HandleEdgeScrolling();
        HandleResetInput();
    }

    private void LateUpdate()
    {
        if (!followEnabled || followTarget == null)
            return;

        Vector3 followAnchor = followTarget.position;
        Vector3 targetFollowAnchor = new Vector3(followAnchor.x, 0f, followAnchor.z);

        if (!hasSmoothedFollowAnchor)
        {
            smoothedFollowAnchor = targetFollowAnchor;
            hasSmoothedFollowAnchor = true;
        }

        // 스무딩 (프레임 독립 느낌)
        float smoothTime = Mathf.Max(0.01f, followDelay);
        float tRot = 1f - Mathf.Exp(-rotationLerp * Time.deltaTime);

        smoothedFollowAnchor = Vector3.SmoothDamp(
            smoothedFollowAnchor,
            targetFollowAnchor,
            ref followVelocity,
            smoothTime);

        Vector3 desiredPos = smoothedFollowAnchor + panOffset + positionOffset;
        desiredPos.x = smoothedFollowAnchor.x + panOffset.x;
        desiredPos.y = positionOffset.y;
        desiredPos.z = smoothedFollowAnchor.z + panOffset.z + positionOffset.z;

        transform.position = desiredPos;

        Quaternion desiredFixedRot = Quaternion.Euler(fixedEulerAngles);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredFixedRot, tRot);
    }

    private void HandleZoomInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f))
            return;

        positionOffset.y = Mathf.Clamp(positionOffset.y - scroll * zoomSpeed * Time.deltaTime, minZoomY, maxZoomY);
    }

    private void HandleEdgeScrolling()
    {
        Vector2 edgeInput = Vector2.zero;
        if (edgeScrollEnabled && IsMouseInsideScreen())
        {
            Vector3 mousePosition = Input.mousePosition;
            edgeInput.x = EvaluateEdgeInput(mousePosition.x, Screen.width, edgeThresholdX);
            edgeInput.y = EvaluateEdgeInput(mousePosition.y, Screen.height, edgeThresholdY);
        }

        if (invertVerticalEdgeScroll)
            edgeInput.y *= -1f;

        float inputStrength = Mathf.Clamp01(edgeInput.magnitude);

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 desiredVelocity = (right * edgeInput.x) + (forward * edgeInput.y);
        if (desiredVelocity.sqrMagnitude > 1f)
            desiredVelocity.Normalize();

        float minSpeed = Mathf.Max(0f, edgeMinSpeed);
        float maxSpeed = Mathf.Max(minSpeed, edgeMaxSpeed);
        float speed = inputStrength > 0f
            ? Mathf.Lerp(minSpeed, maxSpeed, inputStrength)
            : 0f;
        desiredVelocity *= speed;

        if (snapStopWhenLeavingEdge && inputStrength <= 0f)
        {
            edgeScrollVelocity = Vector3.zero;
        }
        else
        {
            float response = inputStrength > 0f ? edgeAcceleration : edgeDeceleration;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, response) * Time.deltaTime);
            edgeScrollVelocity = Vector3.Lerp(edgeScrollVelocity, desiredVelocity, blend);
        }

        edgeScrollVelocity.y = 0f;

        panOffset += edgeScrollVelocity * Time.deltaTime;
        panOffset.x = Mathf.Clamp(panOffset.x, -edgeLimitRange, edgeLimitRange);
        panOffset.z = Mathf.Clamp(panOffset.z, -edgeLimitRange, edgeLimitRange);
        panOffset.y = 0f;
    }

    private void HandleResetInput()
    {
        if (!Input.GetKeyDown(resetKey))
            return;

        RecenterOnFollowTarget();
        positionOffset = new Vector3(0f, defaultZoomY, positionOffset.z);
    }

    private float EvaluateEdgeInput(float mouseAxis, float screenSize, float threshold)
    {
        float safeThreshold = Mathf.Max(1f, threshold);

        if (mouseAxis <= safeThreshold)
            return -Mathf.Clamp01((safeThreshold - mouseAxis) / safeThreshold);

        if (mouseAxis >= screenSize - safeThreshold)
            return Mathf.Clamp01((mouseAxis - (screenSize - safeThreshold)) / safeThreshold);

        return 0f;
    }

    private static bool IsMouseInsideScreen()
    {
        Vector3 mousePosition = Input.mousePosition;
        return mousePosition.x >= 0f
            && mousePosition.x <= Screen.width
            && mousePosition.y >= 0f
            && mousePosition.y <= Screen.height;
    }
}

