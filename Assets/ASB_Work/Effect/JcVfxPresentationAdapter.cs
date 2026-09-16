using System;
using System.Collections;
using System.Linq;
using JC.VFX;
using UnityEngine;

/// <summary>ASB Cue context를 JC 원본 VFX에 전달하는 Variant 전용 어댑터입니다.</summary>
[DisallowMultipleComponent]
public sealed class JcVfxPresentationAdapter : MonoBehaviour, ISkillEffectBehaviour
{
    public enum TargetSource { PrimaryTarget, ReviveTarget, SpawnAnchor }

    [SerializeField] private TargetSource targetSource = TargetSource.PrimaryTarget;
    [SerializeField, Min(0.1f)] private float safetyLifetimeSeconds = 8f;

    private VfxEffect _effect;
    private Coroutine _safetyRoutine;
    private bool _played;

    public void Play(SkillEffectContext context)
    {
        StopRunningEffect();
        _effect = GetComponent<VfxEffect>();
        Transform caster = context?.Caster != null ? context.Caster.transform : null;
        BattleCharactor targetUnit = targetSource == TargetSource.ReviveTarget ? context?.ReviveTarget : context?.PrimaryTarget;
        Transform target = targetSource == TargetSource.SpawnAnchor
            ? context?.SocketTransform
            : targetUnit != null ? targetUnit.transform : null;
        if (_effect == null || caster == null || target == null)
        {
            Debug.LogWarning($"[JcVfxPresentationAdapter] '{name}' requires VfxEffect, caster, and target.", this);
            Destroy(gameObject);
            return;
        }

        if (_effect is LetsFightingLoveVfx letsFightingLove)
        {
            letsFightingLove.SetTargets(context?.Targets?
                .Where(unit => unit != null && unit != targetUnit)
                .Select(unit => JC.VFX.VfxTarget.Of(unit.transform))
                .ToArray() ?? System.Array.Empty<JC.VFX.VfxTarget>());
        }

        _played = true;
        _effect.OnFinished += OnEffectFinished;
        _effect.Play(caster, target);
        _safetyRoutine = StartCoroutine(SafetyCleanupRoutine(safetyLifetimeSeconds / Mathf.Max(0.01f, context?.PlaybackSpeed ?? 1f)));
    }

    private IEnumerator SafetyCleanupRoutine(float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));
        StopRunningEffect();
        Destroy(gameObject);
    }

    private void OnEffectFinished(VfxEffect effect)
    {
        StopRunningEffect();
        Destroy(gameObject);
    }

    private void OnDisable() => StopRunningEffect();

    private void StopRunningEffect()
    {
        if (_safetyRoutine != null) StopCoroutine(_safetyRoutine);
        _safetyRoutine = null;
        if (_effect != null)
        {
            _effect.OnFinished -= OnEffectFinished;
            if (_played) _effect.Stop();
        }
        _played = false;
    }
}
