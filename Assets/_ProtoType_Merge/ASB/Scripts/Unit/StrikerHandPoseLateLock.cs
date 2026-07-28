using UnityEngine;

/// <summary>
/// Restores Striker's hand local rotations after the Animator has evaluated.
/// This keeps the elbows and fingers animated while preventing Humanoid retargeting
/// from twisting the wrist meshes.
/// </summary>
[DisallowMultipleComponent]
public sealed class StrikerHandPoseLateLock : MonoBehaviour
{
    private const string LeftHandPath =
        "Armature/Root/Pelvis/Spine01/Spine02/L_Clavicle/L_Upperarm/L_Forearm/L_Hand";
    private const string RightHandPath =
        "Armature/Root/Pelvis/Spine01/Spine02/R_Clavicle/R_Upperarm/R_Forearm/R_Hand";

    [Header("Hand Pose Lock")]
    [SerializeField] private bool lockLeftHand = true;
    [SerializeField] private bool lockRightHand = true;

    [Tooltip("Striker.001 left-hand rest local rotation.")]
    [SerializeField] private Vector3 leftHandRestEuler = new Vector3(-2.72f, 0.732f, -2.023f);

    [Tooltip("Striker.001 right-hand rest local rotation.")]
    [SerializeField] private Vector3 rightHandRestEuler = new Vector3(-1.761f, 54.09f, 2.636f);

    private Transform _leftHand;
    private Transform _rightHand;

    private void Awake()
    {
        CacheHands();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (_leftHand == null || _rightHand == null)
        {
            CacheHands();
        }

        if (lockLeftHand && _leftHand != null)
        {
            _leftHand.localRotation = Quaternion.Euler(leftHandRestEuler);
        }

        if (lockRightHand && _rightHand != null)
        {
            _rightHand.localRotation = Quaternion.Euler(rightHandRestEuler);
        }
    }

    private void CacheHands()
    {
        _leftHand = transform.Find(LeftHandPath);
        _rightHand = transform.Find(RightHandPath);
    }
}
