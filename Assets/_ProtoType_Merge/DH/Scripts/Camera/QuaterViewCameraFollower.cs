using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    [SerializeField] private bool keepTargetCenteredOnZoom = true;

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

    [Header("UI Blocking")]
    [SerializeField] private bool blockEdgeScrollOverButtons = true;
    [SerializeField] private bool blockCameraInputDuringCombatPrompt = true;
    [SerializeField] private CombatPromptService combatPromptService;

    [Header("Reset")]
    [SerializeField] private KeyCode resetKey = KeyCode.Y;

    private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();

    private Vector3 followVelocity;
    private Vector3 smoothedFollowAnchor;
    private Vector3 panOffset;
    private Vector3 edgeScrollVelocity;
    private float defaultZoomY;
    private float defaultZoomZ;
    private float zoomZPerY;
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

    public void FocusWorldPosition(Vector3 worldPosition)
    {
        Vector3 anchor = GetCurrentFollowAnchor();
        panOffset.x = worldPosition.x - anchor.x;
        panOffset.z = worldPosition.z - anchor.z;
        panOffset.y = 0f;
        edgeScrollVelocity = Vector3.zero;
        ClampPanOffset();
    }

    private void Awake()
    {
        positionOffset.x = 0f;
        defaultZoomY = positionOffset.y;
        defaultZoomZ = positionOffset.z;
        zoomZPerY = Mathf.Abs(defaultZoomY) > 0.001f ? defaultZoomZ / defaultZoomY : 0f;

        if (combatPromptService == null)
            combatPromptService = FindFirstObjectByType<CombatPromptService>();
    }

    private void Update()
    {
        if (IsCameraInputBlocked())
        {
            edgeScrollVelocity = Vector3.zero;
            return;
        }

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
        ApplyZoomDepthOffset();
    }

    private void HandleEdgeScrolling()
    {
        if (blockEdgeScrollOverButtons && IsPointerOverBlockingUI())
        {
            edgeScrollVelocity = Vector3.zero;
            return;
        }

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
        ClampPanOffset();
    }

    private void HandleResetInput()
    {
        if (!Input.GetKeyDown(resetKey))
            return;

        RecenterOnFollowTarget();
        positionOffset = new Vector3(0f, defaultZoomY, defaultZoomZ);
    }

    private void ApplyZoomDepthOffset()
    {
        if (!keepTargetCenteredOnZoom)
            return;

        positionOffset.z = positionOffset.y * zoomZPerY;
    }

    private Vector3 GetCurrentFollowAnchor()
    {
        if (hasSmoothedFollowAnchor)
            return smoothedFollowAnchor;

        if (followTarget == null)
            return Vector3.zero;

        Vector3 followAnchor = followTarget.position;
        return new Vector3(followAnchor.x, 0f, followAnchor.z);
    }

    private void ClampPanOffset()
    {
        panOffset.x = Mathf.Clamp(panOffset.x, -edgeLimitRange, edgeLimitRange);
        panOffset.z = Mathf.Clamp(panOffset.z, -edgeLimitRange, edgeLimitRange);
        panOffset.y = 0f;
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

    /// <summary>
    /// 포인터 아래에 엣지 스크롤을 막아야 할 UI가 있는지.
    /// 버튼과, [KJ 260730] CameraEdgeScrollBlocker가 붙은 UI(전멸 시 턴종료 강제 패널 등)를 같은 판정으로 본다.
    /// </summary>
    private static bool IsPointerOverBlockingUI()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        PointerEventData pointerEventData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };

        UiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, UiRaycastResults);

        for (int i = 0; i < UiRaycastResults.Count; i++)
        {
            GameObject hitObject = UiRaycastResults[i].gameObject;
            if (hitObject == null)
                continue;

            if (hitObject.GetComponentInParent<Button>() != null)
                return true;

            if (hitObject.GetComponentInParent<CameraEdgeScrollBlocker>() != null)
                return true;
        }

        return false;
    }

    private bool IsCameraInputBlocked()
    {
        if (!blockCameraInputDuringCombatPrompt)
            return false;

        if (combatPromptService == null)
            combatPromptService = FindFirstObjectByType<CombatPromptService>();

        return combatPromptService != null && combatPromptService.IsOpen;
    }
}

