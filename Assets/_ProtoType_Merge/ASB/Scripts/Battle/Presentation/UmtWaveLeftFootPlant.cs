using UnityEngine;

/// <summary>
/// UMT 파동탄 동안 왼발을 시작 지점에 고정한다. Timeline이 뼈 포즈를 쓴 뒤에만 적용한다.
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class UmtWaveLeftFootPlant : MonoBehaviour
{
    // Idle의 현재 위상과 파동탄 고정 시작 포즈가 달라도 발을 놓지 않도록 여유를 둔다.
    private const float MaxShinExtension = 0.15f;

    private Transform _upper;
    private Transform _lower;
    private Transform _foot;
    private Vector3 _anchorLocalPosition;
    private Quaternion _anchorLocalRotation;
    private Vector3 _kneeBendLocalDirection;
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
            ? 1f - Mathf.Clamp01(_releaseElapsed / _releaseDuration)
            : 1f;
        if (weight <= 0f)
        {
            End();
            return;
        }
        ApplyPose(weight);
    }

    public void ApplyPose() => ApplyPose(1f);

    private void ApplyPose(float weight)
    {
        if (!_active || _upper == null || _lower == null || _foot == null) return;

        Vector3 target = Vector3.Lerp(_foot.position,
            transform.TransformPoint(_anchorLocalPosition), weight);
        Quaternion targetRotation = Quaternion.Slerp(_foot.rotation,
            transform.rotation * _anchorLocalRotation, weight);
        if (Vector3.SqrMagnitude(_foot.position - target) < 0.00000025f &&
            Quaternion.Angle(_foot.rotation, targetRotation) < 0.1f)
            return;
        Vector3 root = _upper.position;
        Vector3 mid = _lower.position;
        Vector3 tip = _foot.position;
        float upperLength = Vector3.Distance(root, mid);
        float lowerLength = Vector3.Distance(mid, tip);
        if (upperLength < 0.0001f || lowerLength < 0.0001f) return;

        Vector3 toTarget = target - root;
        float distance = Mathf.Clamp(toTarget.magnitude, 0.0001f,
            upperLength + lowerLength - 0.0001f);
        Vector3 axis = toTarget.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(
            transform.TransformDirection(_kneeBendLocalDirection), axis).normalized;
        if (bend.sqrMagnitude < 0.0001f)
            bend = Vector3.ProjectOnPlane(mid - root, axis).normalized;
        if (bend.sqrMagnitude < 0.0001f) return;

        float cosAngle = Mathf.Clamp(
            (distance * distance + upperLength * upperLength - lowerLength * lowerLength) /
            (2f * distance * upperLength), -1f, 1f);
        Vector3 desiredMid = root + axis * (cosAngle * upperLength) +
                             bend * (Mathf.Sqrt(1f - cosAngle * cosAngle) * upperLength);
        _upper.rotation = Quaternion.FromToRotation(mid - root, desiredMid - root) * _upper.rotation;

        Vector3 newMid = _lower.position;
        _lower.rotation = Quaternion.FromToRotation(_foot.position - newMid, target - newMid) *
                          _lower.rotation;

        // 원본 루트 동작 때문에 다리가 짧아지는 구간만 보정한다.
        _foot.position = Vector3.MoveTowards(_foot.position, target, MaxShinExtension);
        _foot.rotation = targetRotation;
    }
}
