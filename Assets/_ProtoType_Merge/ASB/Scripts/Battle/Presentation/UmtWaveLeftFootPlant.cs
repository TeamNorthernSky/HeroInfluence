using UnityEngine;

/// <summary>
/// UMT 파동탄 동안 왼발을 고정하고, 복귀 시 하체 포즈를 Idle까지 잇는다.
/// Timeline이 뼈 포즈를 쓴 뒤에만 적용한다.
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class UmtWaveLeftFootPlant : MonoBehaviour
{
    // Idle의 현재 위상과 파동탄 고정 시작 포즈가 달라도 발을 놓지 않도록 여유를 둔다.
    private const float MaxShinExtension = 0.15f;

    private Transform _upper;
    private Transform _lower;
    private Transform _foot;
    private Transform _hips;
    private Transform _rightUpper;
    private Transform _rightLower;
    private Transform _rightFoot;
    private Vector3 _anchorLocalPosition;
    private Quaternion _anchorLocalRotation;
    private Vector3 _kneeBendLocalDirection;
    private Vector3 _rightAnchorLocalPosition;
    private Quaternion _rightAnchorLocalRotation;
    private Vector3 _rightKneeBendLocalDirection;
    private Transform[] _returnBones;
    private Quaternion[] _returnRotations;
    private Vector3 _returnHipLocalPosition;
    private bool _hasReturnPose;
    private Animator _animator;
    private bool _active;
    private bool _releasing;
    private float _releaseElapsed;
    private float _releaseDuration;
    private int _releaseStartFrame;

    public bool Begin(Animator animator)
    {
        End();
        if (animator == null || !animator.isHuman) return false;

        Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        return foot != null && Begin(animator, foot.position, foot.rotation);
    }

    public bool Begin(Animator animator, Vector3 anchorWorldPosition, Quaternion anchorWorldRotation)
    {
        End();
        if (animator == null || !animator.isHuman) return false;

        _animator = animator;
        _upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        _lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        _foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        _rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        _rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        _rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (_upper == null || _lower == null || _foot == null) return false;

        _anchorLocalPosition = transform.InverseTransformPoint(anchorWorldPosition);
        _anchorLocalRotation = Quaternion.Inverse(transform.rotation) * anchorWorldRotation;
        Vector3 legAxis = (_foot.position - _upper.position).normalized;
        Vector3 bend = Vector3.ProjectOnPlane(_lower.position - _upper.position, legAxis);
        if (bend.sqrMagnitude < 0.000001f)
            bend = Vector3.ProjectOnPlane(transform.forward, legAxis);
        _kneeBendLocalDirection = transform.InverseTransformDirection(bend.normalized);
        _active = true;
        return true;
    }

    /// <summary>Timeline의 마지막 하체 포즈를 저장해 Idle 첫 프레임까지 부드럽게 잇는다.</summary>
    public void CaptureReturnPose()
    {
        _hasReturnPose = false;
        if (!_active || _hips == null || _rightUpper == null ||
            _rightLower == null || _rightFoot == null) return;

        _returnBones = new[] { _hips, _upper, _lower, _foot,
            _rightUpper, _rightLower, _rightFoot };
        _returnRotations = new Quaternion[_returnBones.Length];
        for (int i = 0; i < _returnBones.Length; i++)
            _returnRotations[i] = _returnBones[i].localRotation;
        _returnHipLocalPosition = _hips.localPosition;

        _rightAnchorLocalPosition = transform.InverseTransformPoint(_rightFoot.position);
        _rightAnchorLocalRotation = Quaternion.Inverse(transform.rotation) * _rightFoot.rotation;
        Vector3 legAxis = (_rightFoot.position - _rightUpper.position).normalized;
        Vector3 bend = Vector3.ProjectOnPlane(_rightLower.position - _rightUpper.position, legAxis);
        if (bend.sqrMagnitude < 0.000001f)
            bend = Vector3.ProjectOnPlane(transform.forward, legAxis);
        _rightKneeBendLocalDirection = transform.InverseTransformDirection(bend.normalized);
        _hasReturnPose = true;
    }

    public void ReleaseToIdle(float durationSeconds)
    {
        if (!_active) return;
        _releasing = true;
        _releaseElapsed = 0f;
        _releaseDuration = Mathf.Max(0.01f, durationSeconds);
        _releaseStartFrame = Time.frameCount;
    }

    public void End()
    {
        _active = false;
        _releasing = false;
        _hasReturnPose = false;
        _returnBones = null;
        _returnRotations = null;
        _animator = null;
    }

    private void OnDisable() => End();

    private void LateUpdate()
    {
        if (!_active) return;
        if (_releasing && _animator != null && Time.frameCount > _releaseStartFrame + 1 &&
            !_animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") &&
            !_animator.IsInTransition(0))
        {
            End();
            return;
        }

        if (_releasing) _releaseElapsed += Time.deltaTime;
        float weight = _releasing
            ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_releaseElapsed / _releaseDuration))
            : 1f;
        if (weight <= 0f)
        {
            End();
            return;
        }
        if (_releasing && _hasReturnPose)
        {
            ApplyReturnPose(weight);
            // Fade the foot constraints with the pose instead of releasing both feet in one frame.
            ApplyPose(weight);
            ApplyFootPose(_rightUpper, _rightLower, _rightFoot,
                _rightAnchorLocalPosition, _rightAnchorLocalRotation,
                _rightKneeBendLocalDirection, weight);
        }
        else
            ApplyPose(weight);
    }

    public void ApplyPose() => ApplyPose(1f);

    private void ApplyPose(float weight)
    {
        if (!_active) return;
        ApplyFootPose(_upper, _lower, _foot, _anchorLocalPosition,
            _anchorLocalRotation, _kneeBendLocalDirection, weight);
    }

    private void ApplyReturnPose(float weight)
    {
        _hips.localPosition = Vector3.Lerp(_hips.localPosition, _returnHipLocalPosition, weight);
        for (int i = 0; i < _returnBones.Length; i++)
            _returnBones[i].localRotation = Quaternion.Slerp(
                _returnBones[i].localRotation, _returnRotations[i], weight);
    }

    private void ApplyFootPose(Transform upper, Transform lower, Transform foot,
        Vector3 anchorLocalPosition, Quaternion anchorLocalRotation,
        Vector3 kneeBendLocalDirection, float weight)
    {
        if (upper == null || lower == null || foot == null) return;

        Vector3 target = Vector3.Lerp(foot.position,
            transform.TransformPoint(anchorLocalPosition), weight);
        Quaternion targetRotation = Quaternion.Slerp(foot.rotation,
            transform.rotation * anchorLocalRotation, weight);
        if (Vector3.SqrMagnitude(foot.position - target) < 0.00000025f &&
            Quaternion.Angle(foot.rotation, targetRotation) < 0.1f)
            return;
        Vector3 root = upper.position;
        Vector3 mid = lower.position;
        Vector3 tip = foot.position;
        float upperLength = Vector3.Distance(root, mid);
        float lowerLength = Vector3.Distance(mid, tip);
        if (upperLength < 0.0001f || lowerLength < 0.0001f) return;

        Vector3 toTarget = target - root;
        float distance = Mathf.Clamp(toTarget.magnitude, 0.0001f,
            upperLength + lowerLength - 0.0001f);
        Vector3 axis = toTarget.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(
            transform.TransformDirection(kneeBendLocalDirection), axis).normalized;
        if (bend.sqrMagnitude < 0.0001f)
            bend = Vector3.ProjectOnPlane(mid - root, axis).normalized;
        if (bend.sqrMagnitude < 0.0001f) return;

        float cosAngle = Mathf.Clamp(
            (distance * distance + upperLength * upperLength - lowerLength * lowerLength) /
            (2f * distance * upperLength), -1f, 1f);
        Vector3 desiredMid = root + axis * (cosAngle * upperLength) +
                             bend * (Mathf.Sqrt(1f - cosAngle * cosAngle) * upperLength);
        upper.rotation = Quaternion.FromToRotation(mid - root, desiredMid - root) * upper.rotation;

        Vector3 newMid = lower.position;
        lower.rotation = Quaternion.FromToRotation(foot.position - newMid, target - newMid) *
                         lower.rotation;

        // 원본 루트 동작 때문에 다리가 짧아지는 구간만 보정한다.
        foot.position = Vector3.MoveTowards(foot.position, target, MaxShinExtension);
        foot.rotation = targetRotation;
    }
}
