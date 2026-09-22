using System;
using System.Collections;
using System.Linq;
using JC.VFX;
using UnityEngine;

/// <summary>확정된 전투 대상을 JC VFX에 전달하고 재생 수명을 관리합니다.</summary>
[DisallowMultipleComponent]
public sealed class JcVfxPresentationAdapter : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
{
    public enum TargetSource { PrimaryTarget, ReviveTarget, SpawnAnchor }
    [Tooltip("주 대상, 부활 대상 또는 Cue 생성 위치 중 VFX의 목적지를 선택합니다.")]
    [SerializeField] private TargetSource targetSource = TargetSource.PrimaryTarget;
    [Tooltip("자연 종료가 없는 부품의 최대 유지 시간(전투 배속 기준 초)입니다.")]
    [SerializeField, Min(0.1f)] private float safetyLifetimeSeconds = 8f;
    private VfxEffect _effect;
    private Coroutine _safetyRoutine;
    private bool _played;
    private SkillEffectContext _context;
    private TaoBarrageVfx _barrage;
    private PawForYouVfx _paw;
    private JcVfxPartSequence _parts;

    private void Start()
    {
        // 피격 효과는 BattleVisualDirector가 Play()를 직접 호출하므로 수명만 연결합니다.
        if (_played) return;
        _effect = GetComponent<VfxEffect>();
        if (_effect == null) return;
        _played = true;
        _effect.OnFinished += OnEffectFinished;
        _safetyRoutine = StartCoroutine(SafetyCleanupRoutine(safetyLifetimeSeconds));
    }

    public void Play(SkillEffectContext context)
    {
        StopRunningEffect();
        _context = context;
        _effect = GetComponent<VfxEffect>();
        Transform caster = context?.Caster != null ? context.Caster.transform : null;
        BattleCharactor targetUnit = targetSource == TargetSource.ReviveTarget ? context?.ReviveTarget : context?.PrimaryTarget;
        Transform target = targetSource == TargetSource.SpawnAnchor ? context?.SocketTransform : targetUnit != null ? targetUnit.transform : null;
        if (_effect == null || caster == null || target == null)
        {
            Debug.LogWarning($"[JcVfxPresentationAdapter] '{name}' requires VfxEffect, caster, and target.", this);
            Destroy(gameObject);
            return;
        }
        var targets = context?.Targets?.Where(unit => unit != null && (!(_effect is LetsFightingLoveVfx) || unit != targetUnit))
            .Select(unit => VfxTarget.Of(unit.transform)).ToArray() ?? Array.Empty<VfxTarget>();
        _effect.SetTargets(targets);
        _effect.PlaybackSpeed = Mathf.Max(0.01f, context?.PlaybackSpeed ?? 1f);
        _parts = _effect as JcVfxPartSequence;
        if (_parts != null) { _parts.MuzzleSocket = context.SocketTransform; _parts.Impacted += OnTargetsImpacted; }
        _barrage = _effect as TaoBarrageVfx;
        if (_barrage != null) _barrage.OnTargetsImpacted += OnTargetsImpacted;
        _paw = _effect as PawForYouVfx;
        if (_paw != null) _paw.OnTargetImpacted += OnTargetsImpacted;
        _played = true;
        _effect.OnFinished += OnEffectFinished;
        _effect.Play(caster, target);
        _safetyRoutine = StartCoroutine(SafetyCleanupRoutine(safetyLifetimeSeconds / Mathf.Max(0.01f, context?.PlaybackSpeed ?? 1f)));
    }
    private void OnTargetsImpacted()
    {
        if (_context == null) return;
        var targets = _context.Targets?.Where(u => u != null).Select(u => u.transform).ToArray() ?? Array.Empty<Transform>();
        SkillImpactSignalBus.PublishImpact(new ImpactKey(_context.ActionInstanceId), _context.TargetPosition, targets);
    }
    public bool Signal(SkillEffectContext context) { Stop(); return true; }
    public void Stop() { StopRunningEffect(); Destroy(gameObject); }
    private IEnumerator SafetyCleanupRoutine(float seconds) { yield return new WaitForSeconds(Mathf.Max(0.1f, seconds)); _safetyRoutine = null; Stop(); }
    private void OnEffectFinished(VfxEffect effect) => Stop();
    private void OnDisable() => StopRunningEffect();
    private void StopRunningEffect()
    {
        if (_safetyRoutine != null) StopCoroutine(_safetyRoutine);
        _safetyRoutine = null;
        if (_barrage != null) _barrage.OnTargetsImpacted -= OnTargetsImpacted;
        _barrage = null;
        if (_paw != null) _paw.OnTargetImpacted -= OnTargetsImpacted;
        _paw = null;
        if (_parts != null) _parts.Impacted -= OnTargetsImpacted;
        _parts = null;
        if (_effect != null) { _effect.OnFinished -= OnEffectFinished; if (_played) _effect.Stop(); }
        _played = false;
        _context = null;
    }
}
