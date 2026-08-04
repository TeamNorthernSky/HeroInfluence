using UnityEngine;

public enum AttachedEffectScaleMode
{
    InheritSocket,
    UseVisualRoot,
    World,
}

/// <summary>소켓에 붙어 유지되는 이펙트 재료. Inspector의 로컬 오프셋은 소켓 기준으로 해석한다.</summary>
public class AttachedEffectBehaviour : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
{
    private const float ScaleEpsilon = 0.0001f;

    [Header("Socket Local Offset")]
    [SerializeField] private Vector3 localPositionOffset;
    [SerializeField] private Vector3 localEulerOffset;

    [Header("Scale")]
    [SerializeField]
    [Tooltip("InheritSocket: 소켓/본 스케일 상속, UseVisualRoot: 모델 전체 스케일만 유지, World: 부모 스케일 무시.")]
    private AttachedEffectScaleMode scaleMode = AttachedEffectScaleMode.InheritSocket;
    [SerializeField] private Vector3 localScale = Vector3.one;

    [Header("Signal")]
    [SerializeField] private bool detachOnSignal = true;
    [SerializeField] private bool destroyOnSignal;

    [SerializeField, Min(0f)]
    [Tooltip("detachOnSignal로 분리된 뒤 스스로 정리될 때까지의 상한(초). 파티클이 먼저 끝나면 그 시점에 정리된다.")]
    private float detachedLifetimeSeconds = EffectFallbackRelease.DefaultFallbackSeconds;

    public void Play(SkillEffectContext ctx)
    {
        if (ctx?.SocketTransform != null)
        {
            transform.SetParent(ctx.SocketTransform, false);
            transform.localPosition = localPositionOffset;
            transform.localRotation = Quaternion.Euler(localEulerOffset);
            transform.localScale = ResolveAttachedScale(ctx);
        }

        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++) particles[i].Play(true);
    }

    private Vector3 ResolveAttachedScale(SkillEffectContext ctx)
    {
        if (scaleMode == AttachedEffectScaleMode.InheritSocket || ctx?.SocketTransform == null)
        {
            return localScale;
        }

        Vector3 desiredWorldScale = localScale;
        if (scaleMode == AttachedEffectScaleMode.UseVisualRoot)
        {
            UnitSocketHolder socketHolder = ctx.Caster != null
                ? ctx.Caster.GetComponentInChildren<UnitSocketHolder>(true)
                : null;
            Transform visualRoot = socketHolder?.ResolveVisualRoot(ctx.Caster.transform);
            if (visualRoot == null)
            {
                Debug.LogWarning($"[AttachedEffectBehaviour] UseVisualRoot could not resolve a visual root on '{ctx?.Caster?.name}'. World scale fallback is used.", this);
            }
            else
            {
                desiredWorldScale = Multiply(localScale, visualRoot.lossyScale);
            }
        }

        return DivideSafely(desiredWorldScale, ctx.SocketTransform.lossyScale);
    }

    private static Vector3 Multiply(Vector3 lhs, Vector3 rhs)
    {
        return new Vector3(lhs.x * rhs.x, lhs.y * rhs.y, lhs.z * rhs.z);
    }

    private static Vector3 DivideSafely(Vector3 numerator, Vector3 denominator)
    {
        return new Vector3(
            DivideSafely(numerator.x, denominator.x),
            DivideSafely(numerator.y, denominator.y),
            DivideSafely(numerator.z, denominator.z));
    }

    private static float DivideSafely(float numerator, float denominator)
    {
        if (Mathf.Abs(denominator) >= ScaleEpsilon)
        {
            return numerator / denominator;
        }

        float signedEpsilon = denominator < 0f ? -ScaleEpsilon : ScaleEpsilon;
        return numerator / signedEpsilon;
    }
    /// <summary>
    /// <c>true</c>를 반환하면 프리젠터가 핸들을 해제한다 — 계약상 "이후 수명은 재료가 책임진다"는 뜻이다.
    /// 예전에는 <c>detachOnSignal</c>로 월드 루트에 분리만 하고 <c>true</c>를 돌려줘,
    /// 아무도 책임지지 않는 고아 오브젝트가 씬 루트에 남았다(루프 파티클이면 영구 잔류).
    /// 분리 시에는 스스로 수명 상한을 걸고 나서 <c>true</c>를 반환한다.
    /// </summary>
    public bool Signal(SkillEffectContext ctx)
    {
        if (destroyOnSignal)
        {
            Destroy(gameObject);
            return true;
        }

        if (detachOnSignal)
        {
            transform.SetParent(null, true);
            EffectFallbackRelease.Ensure(gameObject, detachedLifetimeSeconds);
            return true;
        }

        // 인수자도 없고 분리도 하지 않았다면 수명이 끝난 게 아니다.
        // false를 반환해 핸들을 유지하고, 이후 Stop()을 받을 수 있게 남겨둔다.
        return false;
    }

    public void Stop()
    {
        Destroy(gameObject);
    }
}