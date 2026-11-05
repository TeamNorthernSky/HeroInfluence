using UnityEngine;
using UnityEngine.Rendering;

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
    // JC | 2026-11-04 | 굉음 대화의 진동과 개별 발걸음 반동을 별도로 조절해 반복 이동 중에도 타이밍을 유지합니다.
    private Object impactOwner;
    private float impactStart, impactStrength, impactDuration;
    private float footRebound;
    private Vector3 renderOffset;
    private Object noiseOwner;
    private float noiseStart, noiseStrength, noiseDuration;
    private int noiseFrame = -1;
    private Vector2 noiseOffset;
    public const float ImpactDuration = .14f;

    public static CameraShakeEffect ForCamera(Camera camera)
    {
        if (camera == null) return null;
        return camera.TryGetComponent<CameraShakeEffect>(out var effect)
            ? effect : camera.gameObject.AddComponent<CameraShakeEffect>();
    }

    public void PlayImpact(Object owner, float strength, float seconds = ImpactDuration)
    {
        impactOwner = owner;
        impactStrength = strength;
        impactDuration = Mathf.Max(.01f, seconds);
        impactStart = Time.unscaledTime;
        footRebound = -1f;
    }

    public void PlayFootstep(Object owner, float strength, float seconds, float reboundPercent)
    {
        PlayImpact(owner, strength, seconds);
        footRebound = Mathf.Clamp01(reboundPercent * .01f);
    }

    /// <summary>EffectLab 방식의 감쇠하는 무작위 진동을 기존 반동에 더합니다.</summary>
    public void PlayNoise(Object owner, float strength, float seconds)
    {
        noiseOwner = owner;
        noiseStrength = Mathf.Max(0f, strength);
        noiseDuration = Mathf.Max(0f, seconds);
        noiseStart = Time.unscaledTime;
        noiseFrame = -1;
    }

    private static Vector2 NoiseOffset(float age, float seconds, float strength)
    {
        if (seconds <= 0f || age >= seconds || strength <= 0f) return Vector2.zero;
        return Random.insideUnitCircle * (strength * (1f - Mathf.Clamp01(age / seconds)));
    }

    private static Vector2 FootstepOffset(float t, float rebound)
    {
        // Drop quickly, rise past rest, then settle more slowly.
        float offset = t < .20f ? Mathf.SmoothStep(0f, -1f, t / .20f)
            : t < .62f ? Mathf.SmoothStep(-1f, rebound, (t - .20f) / .42f)
            : Mathf.SmoothStep(rebound, 0f, (t - .62f) / .38f);
        return new Vector2(-.12f, 1f) * offset;
    }

    public void CancelImpact(Object owner)
    {
        if (impactOwner == owner) impactOwner = null;
        if (noiseOwner == owner) noiseOwner = null;
    }

    // A quick downward hit followed by a smaller rebound, in screen coordinates.
    public static Vector2 ImpactOffset(float seconds, float duration = ImpactDuration)
    {
        float t = Mathf.Clamp01(seconds / duration);
        float wave = Mathf.Sin(2f * Mathf.PI * t) * (1f - t) * (1f - t);
        return new Vector2(.12f, -1f) * (1.65f * wave);
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginRender;
        RenderPipelineManager.endCameraRendering += EndRender;
    }

    private void BeginRender(ScriptableRenderContext context, Camera camera)
    {
        if (camera.transform != transform) return;
        RestoreRenderOffset();
        Vector2 offset = Vector2.zero;
        if (impactOwner)
        {
            float age = Time.unscaledTime - impactStart;
            if (age >= impactDuration) impactOwner = null;
            else offset = (footRebound >= 0f ? FootstepOffset(age / impactDuration, footRebound)
                : ImpactOffset(age, impactDuration)) * impactStrength;
        }
        if (noiseOwner)
        {
            float age = Time.unscaledTime - noiseStart;
            if (age >= noiseDuration) noiseOwner = null;
            else
            {
                if (noiseFrame != Time.frameCount)
                {
                    noiseOffset = NoiseOffset(age, noiseDuration, noiseStrength);
                    noiseFrame = Time.frameCount;
                }
                offset += noiseOffset;
            }
        }
        // Move opposite to the desired image displacement. Restore before follow logic runs.
        renderOffset = -transform.TransformDirection((Vector3)offset);
        transform.position += renderOffset;
    }

    private void EndRender(ScriptableRenderContext context, Camera camera)
    {
        if (camera.transform == transform) RestoreRenderOffset();
    }

    private void RestoreRenderOffset()
    {
        if (renderOffset == Vector3.zero) return;
        transform.position -= renderOffset;
        renderOffset = Vector3.zero;
    }

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
        Vector2 noise = NoiseOffset(elapsed, duration, intensity);
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

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginRender;
        RenderPipelineManager.endCameraRendering -= EndRender;
        RestoreRenderOffset();
        impactOwner = null;
        noiseOwner = null;
        StopShake();
    }
}
