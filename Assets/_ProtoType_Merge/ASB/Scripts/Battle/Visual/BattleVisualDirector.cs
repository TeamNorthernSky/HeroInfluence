using System;
using System.Collections;
using System.Collections.Generic;
using ASB.Work.Battle.Core;
using UnityEngine;

[Serializable]
public struct HitStopSettings
{
    public bool Enabled;
    [Range(0f, 0.5f)] public float DurationSeconds;
    [Range(0f, 1f)] public float CharacterSpeed;
    [Range(0f, 1f)] public float EffectSpeed;

    public static HitStopSettings Default => new HitStopSettings
    {
        Enabled = true, DurationSeconds = 0.08f, CharacterSpeed = 0.05f, EffectSpeed = 0.25f
    };
}

/// <summary>
/// 전투 연출(이펙트·팝업) 생성 책임.
/// BattleManager의 [SerializeField] 필드로 주입합니다.
/// </summary>
public class BattleVisualDirector : MonoBehaviour
{
    [SerializeField] private SkillPresentationCatalog _catalog;
    [SerializeField] private DamagePopupPresenter _popupPresenter;
    [SerializeField] private EffectRegistry _effectRegistry;
    [Header("Hit Stop")]
    [SerializeField] private HitStopSettings _defaultHitStop = new HitStopSettings
    {
        Enabled = true, DurationSeconds = 0.08f, CharacterSpeed = 0.05f, EffectSpeed = 0.25f
    };

    private readonly HashSet<CharactorAnimationController> _stoppedAnimations = new HashSet<CharactorAnimationController>();
    private readonly List<HitEffectSpeed> _hitEffects = new List<HitEffectSpeed>();
    private Coroutine _hitStopRoutine;
    private float _hitStopUntil;
    private float _characterSpeedMultiplier = 1f;
    private float _effectSpeedMultiplier = 1f;
    private float _lastAutomaticHitStopAt = float.NegativeInfinity;
    private BattleCharactor _lastMarkerActor;
    private BattleCharactor _lastMarkerTarget;
    private float _lastMarkerTime = float.NegativeInfinity;

    private sealed class HitEffectSpeed
    {
        public JC.VFX.VfxEffect Effect;
        public float PlaybackSpeed;
        public ParticleSystem[] Particles;
        public float[] ParticleSpeeds;
        public float RegisteredAt;
    }

    /// <summary>코드에서 직접 호출하는 히트스탑 API. 마커도 이 경로를 사용한다.</summary>
    public void RequestHitStop(BattleCharactor actor, BattleCharactor target, HitStopSettings settings)
    {
        if (!isActiveAndEnabled || !settings.Enabled || settings.DurationSeconds <= 0f) return;

        _hitStopUntil = Mathf.Max(_hitStopUntil, Time.unscaledTime + Mathf.Clamp(settings.DurationSeconds, 0f, 0.5f));
        float characterSpeed = Mathf.Clamp01(settings.CharacterSpeed);
        _characterSpeedMultiplier = Mathf.Min(_characterSpeedMultiplier, characterSpeed);
        AddStoppedAnimation(actor, _characterSpeedMultiplier);
        AddStoppedAnimation(target, _characterSpeedMultiplier);
        _effectSpeedMultiplier = Mathf.Min(_effectSpeedMultiplier, Mathf.Clamp01(settings.EffectSpeed));
        ApplyHitEffectSpeeds();
        if (_hitStopRoutine == null) _hitStopRoutine = StartCoroutine(HitStopRoutine());
    }

    /// <summary>같은 명중에 코드 요청이 뒤따르면 마커 설정을 우선한다.</summary>
    public void RequestHitStopFromMarker(BattleCharactor actor, BattleCharactor target, HitStopSettings settings)
    {
        bool replaceSameFrameAutomatic = _hitStopRoutine != null &&
            Time.unscaledTime - _lastAutomaticHitStopAt < 0.02f;
        _lastMarkerActor = actor;
        _lastMarkerTarget = target;
        _lastMarkerTime = Time.unscaledTime;
        RequestHitStop(actor, target, settings);
        if (!replaceSameFrameAutomatic || !settings.Enabled || settings.DurationSeconds <= 0f) return;

        // 동일 시각에 코드 명중 콜백이 먼저 실행되었어도 저작한 마커 값이 우선한다.
        _hitStopUntil = Time.unscaledTime + Mathf.Clamp(settings.DurationSeconds, 0f, 0.5f);
        _characterSpeedMultiplier = Mathf.Clamp01(settings.CharacterSpeed);
        foreach (CharactorAnimationController animation in _stoppedAnimations)
            if (animation != null) animation.SetHitStopSpeedMultiplier(_characterSpeedMultiplier);
        _effectSpeedMultiplier = Mathf.Clamp01(settings.EffectSpeed);
        ApplyHitEffectSpeeds();
    }

    /// <summary>실제 HP 감소가 확인된 타격에만 기본값 또는 스킬별 설정을 적용한다.</summary>
    public void RequestHitStopOnDamage(BattleCharactor actor, BattleHitResult result)
    {
        if (result == null || result.Target == null || result.IsHeal || result.IsMiss || result.AppliedDamage <= 0f) return;
        if (_lastMarkerActor == actor && _lastMarkerTarget == result.Target &&
            Time.unscaledTime - _lastMarkerTime < 0.15f) return;
        SkillPresentationData presentation = GetPresentation(result.SkillIndex);
        HitStopSettings settings = presentation != null && presentation.OverrideHitStop
            ? presentation.HitStop : _defaultHitStop;
        if (!settings.Enabled || settings.DurationSeconds <= 0f) return;
        if (Time.unscaledTime - _lastAutomaticHitStopAt < 0.1f)
        {
            // 동시 광역 대상은 현재 정지에 합류시키되 정지 시간을 다시 늘리지 않는다.
            if (_hitStopRoutine != null)
            {
                AddStoppedAnimation(actor, _characterSpeedMultiplier);
                AddStoppedAnimation(result.Target, _characterSpeedMultiplier);
            }
            return;
        }
        _lastAutomaticHitStopAt = Time.unscaledTime;
        RequestHitStop(actor, result.Target, settings);
    }

    private void AddStoppedAnimation(BattleCharactor unit, float speed)
    {
        if (unit == null) return;
        unit.EnsureAnimationController();
        CharactorAnimationController animation = unit.Anim;
        if (animation == null) return;
        _stoppedAnimations.Add(animation);
        animation.SetHitStopSpeedMultiplier(Mathf.Min(animation.HitStopSpeedMultiplier, speed));
    }

    private IEnumerator HitStopRoutine()
    {
        while (Time.unscaledTime < _hitStopUntil) yield return null;
        RestoreHitStop();
        _hitStopRoutine = null;
    }

    private void RestoreHitStop()
    {
        foreach (CharactorAnimationController animation in _stoppedAnimations)
            if (animation != null) animation.SetHitStopSpeedMultiplier(1f);
        _stoppedAnimations.Clear();
        _characterSpeedMultiplier = 1f;
        _effectSpeedMultiplier = 1f;
        ApplyHitEffectSpeeds();
        _hitEffects.Clear();
        _hitStopUntil = 0f;
        _lastMarkerActor = null;
        _lastMarkerTarget = null;
        _lastMarkerTime = float.NegativeInfinity;
    }

    private void OnDisable()
    {
        if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
        _hitStopRoutine = null;
        RestoreHitStop();
    }

    /// <summary>투사체 자체의 착탄 파티클도 명중 직후 히트스탑에 포함한다.</summary>
    public void RegisterProjectileImpact(GameObject instance) => TrackHitEffect(instance, true);

    private void Update()
    {
        if (_hitStopRoutine == null)
            _hitEffects.RemoveAll(state => Time.unscaledTime - state.RegisteredAt > 0.5f);
    }

    private void TrackHitEffect(GameObject instance, bool retainBeforeStop = false)
    {
        if (instance == null || (_hitStopRoutine == null && !retainBeforeStop)) return;
        var state = new HitEffectSpeed
        {
            Effect = instance.GetComponent<JC.VFX.VfxEffect>(),
            Particles = instance.GetComponentsInChildren<ParticleSystem>(true),
            RegisteredAt = Time.unscaledTime
        };
        state.PlaybackSpeed = state.Effect != null ? state.Effect.PlaybackSpeed : 1f;
        state.ParticleSpeeds = new float[state.Particles.Length];
        for (int i = 0; i < state.Particles.Length; i++)
            state.ParticleSpeeds[i] = state.Particles[i].main.simulationSpeed;
        _hitEffects.Add(state);
        if (_hitStopRoutine != null) ApplyHitEffectSpeed(state);
    }

    private void ApplyHitEffectSpeeds()
    {
        foreach (HitEffectSpeed state in _hitEffects) ApplyHitEffectSpeed(state);
    }

    private void ApplyHitEffectSpeed(HitEffectSpeed state)
    {
        if (state.Effect != null) state.Effect.PlaybackSpeed = state.PlaybackSpeed * _effectSpeedMultiplier;
        for (int i = 0; i < state.Particles.Length; i++)
        {
            if (state.Particles[i] == null) continue;
            var main = state.Particles[i].main;
            main.simulationSpeed = state.ParticleSpeeds[i] * _effectSpeedMultiplier;
        }
    }

    public SkillPresentationData GetPresentation(int skillIndex)
    {
        return _catalog != null ? _catalog.Get(skillIndex) : null;
    }

    public void PlayHitEffectAt(Transform targetTransform, int skillIndex)
    {
        if (targetTransform == null)
            return;

        SkillPresentationData presentation = _catalog?.Get(skillIndex);
        if (presentation == null)
            return;

        if (presentation.EnableHitEffect)
        {
            GameObject prefab = ResolveEffectPrefab(presentation.HitEffectId);
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, targetTransform.position, targetTransform.rotation);
                instance.GetComponent<JC.VFX.VfxEffect>()?.Play();
            }
        }

        PlaySound(presentation.HitSoundId, targetTransform, presentation.SfxVolume);
    }

    public void PlayHitEffect(BattleCharactor target, int skillIndex, float playbackSpeed = 1f)
    {
        SkillPresentationData presentation = _catalog?.Get(skillIndex);
        if (presentation == null)
        {
            return;
        }

        // 소켓 해석: HitEffectSocket이 지정돼 있으면 사용, 아니면 대상 루트로 폴백한다.
        // Unity Object는 파괴 시 "fake null"이라 C#의 ?? 연산자가 폴백하지 못하므로,
        // 반드시 Unity의 != null 오버로드로 판정한다(미지정/파괴 소켓 → 대상 루트).
        var profile = target != null ? target.GetComponent<UnitVisualProfile>() : null;
        Transform socket = profile != null && profile.HitEffectSocket != null
            ? profile.HitEffectSocket
            : (target != null ? target.transform : null);
        if (presentation.HitEffectAtTargetRoot && target != null) socket = target.transform;

        if (presentation.EnableHitEffect)
        {
            GameObject prefab = ResolveEffectPrefab(presentation.HitEffectId);
            // 재료 프리팹은 프리젠터/이벤트가 담당 → director는 스폰하지 않음.
            if (prefab != null && socket != null)
            {
                GameObject instance = Instantiate(prefab, socket.position + presentation.HitEffectOffset, socket.rotation);

                // HealOrbitVfx는 parameterless Play()로는 현재 루트 좌표를 쓰므로,
                // 힐 대상의 월드 좌표를 명시해 발밑에서 시작시킨다. 이 호출은 프리팹의
                // OnEnable 자동 재생 여부와 무관하게 런타임 인스턴스를 확실히 구동한다.
                JC.VFX.HealOrbitVfx healAura = instance.GetComponent<JC.VFX.HealOrbitVfx>();
                if (healAura != null)
                {
                    healAura.Play(target.transform.position);
                }
                else
                {
                    var effect = instance.GetComponent<JC.VFX.VfxEffect>();
                    if (effect != null)
                    {
                        if (presentation.ScaleHitEffectWithBattleSpeed) effect.PlaybackSpeed = playbackSpeed;
                        effect.Play();
                        TrackHitEffect(instance);
                    }
                    else TrackHitEffect(instance);
                }
            }
        }

        PlaySound(presentation.HitSoundId, socket, presentation.SfxVolume);
    }

    /// <summary>EffectRegistry에서 id로 프리팹을 조회합니다(시퀀서의 재료 판별용). 0이면 null.</summary>
    public GameObject GetRegisteredEffect(int id) => id != 0 ? _effectRegistry?.Get(id) : null;

    // EffectRegistry id로 프리팹 조회(0이면 null).
    private GameObject ResolveEffectPrefab(int effectId)
    {
        return effectId != 0 ? _effectRegistry?.Get(effectId) : null;
    }

    // SoundRegistry id로 재생(0이면 무시).
    private void PlaySound(int soundId, Transform at, float volume)
    {
        // Legacy ASB SoundManager was removed. Battle audio should route through the shared audio layer later.
    }

    public void ShowDamagePopup(BattleHitResult result)
    {
        if (result == null || result.Target == null)
        {
            return;
        }

        _popupPresenter?.Show(result);
    }

    public void ShowCounterPopup(BattleCharactor actor) => _popupPresenter?.ShowCounter(actor);
}
