using UnityEngine;

/// <summary>Additive camera translation, restored when finished or disabled.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraShakeEffect : MonoBehaviour
{
    [Min(0f), Tooltip("흔들리는 시간(초). 게임 시간 배율과 관계없이 진행합니다.")]
    public float duration = 0.45f;
    [Min(0f), Tooltip("흔들림 강도. 값이 클수록 카메라가 크게 움직입니다.")]
    public float intensity = 0.15f;
    private Vector3 appliedOffset;
    private float elapsed;
    private bool shaking;

    public float Duration { get => duration; set => duration = Mathf.Max(0f, value); }
    public float Intensity { get => intensity; set => intensity = Mathf.Max(0f, value); }
    public bool IsShaking => shaking;

    [ContextMenu("Play Shake")]
    public void PlayShake()
    {
        StopShake();
        elapsed = 0f;
        shaking = duration > 0f && intensity > 0f;
    }

    // Remove our offset before other scripts update the camera this frame.
    private void Update() => RemoveOffset();

    private void LateUpdate()
    {
        RemoveOffset();
        if (!shaking) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= duration || duration <= 0f || intensity <= 0f)
        {
            StopShake();
            return;
        }
        float strength = intensity * (1f - elapsed / duration);
        Vector2 noise = Random.insideUnitCircle * strength;
        appliedOffset = new Vector3(noise.x, noise.y, 0f);
        transform.localPosition += appliedOffset;
    }

    [ContextMenu("Stop Shake")]
    public void StopShake()
    {
        RemoveOffset();
        shaking = false;
    }

    private void RemoveOffset()
    {
        transform.localPosition -= appliedOffset;
        appliedOffset = Vector3.zero;
    }

    private void OnDisable() => StopShake();
}
