using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 타격마다 화면을 짧게 조였다가 푸는 카메라 줌. BattleVisualDirector가 메인 카메라에 자동으로 붙인다.
/// 렌더 직전에만 시야각을 줄이고 렌더 직후 되돌리므로, 공격 카메라(BattlePresentationController)·보스 카메라가
/// 매 프레임 계산하는 위치·시야각과 다투지 않고 그 결과 위에 얹힌다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCameraHitPunch : MonoBehaviour
{
    [Tooltip("연속 타격이 겹쳐도 넘지 않는 최대 확대 비율.")]
    [SerializeField, Range(0f, 0.3f)] private float _maxZoomAmount = 0.1f;

    private Camera _camera;
    private float _fromAmount;
    private float _peakAmount;
    private float _inSeconds;
    private float _holdSeconds;
    private float _outSeconds;
    private float _elapsed = float.PositiveInfinity;
    private bool _applied;
    private float _savedFov;
    private float _savedOrthoSize;

    /// <summary>지금 화면에 적용될 확대 비율(0 = 원래 화면).</summary>
    public float CurrentAmount => Evaluate();

    public static BattleCameraHitPunch Ensure(Camera camera)
    {
        if (camera == null) return null;
        BattleCameraHitPunch punch = camera.GetComponent<BattleCameraHitPunch>();
        return punch != null ? punch : camera.gameObject.AddComponent<BattleCameraHitPunch>();
    }

    /// <summary>줌을 시작한다. 진행 중이면 현재 값에서 다시 조이고, 최대치를 넘지 않는다.</summary>
    public void Punch(HitCameraPunchSettings settings, float battleSpeed)
    {
        if (!settings.Enabled || settings.ZoomAmount <= 0f) return;
        float speed = Mathf.Max(0.01f, battleSpeed);
        _fromAmount = Evaluate();
        _peakAmount = Mathf.Min(_maxZoomAmount, Mathf.Max(_fromAmount, settings.ZoomAmount));
        _inSeconds = settings.ZoomInSeconds / speed;
        _holdSeconds = settings.HoldSeconds / speed;
        _outSeconds = settings.ZoomOutSeconds / speed;
        _elapsed = 0f;
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        Restore();
        _elapsed = float.PositiveInfinity;
    }

    private void Update()
    {
        if (float.IsPositiveInfinity(_elapsed)) return;
        // 히트스탑은 캐릭터·이펙트만 늦추므로 줌은 실제 시간으로 진행한다.
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed >= _inSeconds + _holdSeconds + _outSeconds) _elapsed = float.PositiveInfinity;
    }

    private float Evaluate()
    {
        if (float.IsPositiveInfinity(_elapsed)) return 0f;
        float t = _elapsed;
        if (t < _inSeconds) return Mathf.Lerp(_fromAmount, _peakAmount, 1f - (1f - t / _inSeconds) * (1f - t / _inSeconds));
        t -= _inSeconds;
        if (t < _holdSeconds) return _peakAmount;
        t -= _holdSeconds;
        if (t < _outSeconds) return Mathf.Lerp(_peakAmount, 0f, Mathf.SmoothStep(0f, 1f, t / _outSeconds));
        return 0f;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera || _applied) return;
        float amount = Evaluate();
        if (amount <= 0.0001f) return;
        _savedFov = camera.fieldOfView;
        _savedOrthoSize = camera.orthographicSize;
        _applied = true;
        // 화면에 보이는 크기가 amount만큼 커지도록 시야 폭(tan)을 줄인다.
        float viewScale = 1f / (1f + amount);
        if (camera.orthographic)
            camera.orthographicSize = _savedOrthoSize * viewScale;
        else
            camera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(_savedFov * 0.5f * Mathf.Deg2Rad) * viewScale) * Mathf.Rad2Deg;
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _camera) Restore();
    }

    private void Restore()
    {
        if (!_applied || _camera == null) return;
        _camera.fieldOfView = _savedFov;
        _camera.orthographicSize = _savedOrthoSize;
        _applied = false;
    }
}
