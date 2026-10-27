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
    [Tooltip("테스트용 HP 하한으로 피해가 막혀도 명중 연출을 재생합니다.")]
    [SerializeField] private bool _reactToPreventedDamage;
    [Header("공통 피격 이펙트")]
    [InspectorName("공통 피격 이펙트 사용"), SerializeField] private bool _enableCommonHitEffect = true;
    [Tooltip("비어 있으면 BattleEffects/FX_Common_Hit_Basic를 공통으로 사용합니다.")]
    [SerializeField] private GameObject _commonHitEffect;
    [SerializeField] private Vector3 _commonHitEffectOffset = new Vector3(0f, 1.1f, 0f);
    [Tooltip("저스티스 강화판 스킬의 명중에만 추가 피격 이펙트를 겹칩니다.")]
    [InspectorName("저스티스 강화 피격 이펙트 사용")]
    [SerializeField] private bool _enableEnhancedHitEffect = true;
    [Tooltip("비어 있으면 BattleEffects/FX_Justice_Hit_Enhanced를 사용합니다.")]
    [SerializeField] private GameObject _enhancedHitEffect;
    [InspectorName("저스티스 일반 피격 이펙트 사용"), SerializeField] private bool _enableJusticeNormalHitEffect = true;
    [Tooltip("비어 있으면 색상을 수정한 BattleEffects/FX_Justice_Hit_Basic 사본을 사용합니다.")]
    [SerializeField] private GameObject _justiceNormalHitEffect;

    private void PlayJusticeHitEffect(BattleHitResult result)
    {
        if (result.SkillIndex / 1000 != 1 || !HeroSkillRules.IsCurrentHeroSkill(result.SkillIndex)) return;
        GameObject prefab;
        if (HeroSkillRules.FamilyId(result.SkillIndex) != result.SkillIndex)
        {
            if (!_enableEnhancedHitEffect) return;
            if (_enhancedHitEffect == null)
                _enhancedHitEffect = Resources.Load<GameObject>("BattleEffects/FX_Justice_Hit_Enhanced");
            prefab = _enhancedHitEffect;
        }
        else
        {
            if (!_enableJusticeNormalHitEffect) return;
            if (_justiceNormalHitEffect == null)
                _justiceNormalHitEffect = Resources.Load<GameObject>("BattleEffects/FX_Justice_Hit_Basic");
            prefab = _justiceNormalHitEffect;
        }
        if (prefab == null) return;
        var instance = Instantiate(prefab,
            result.Target.transform.position + _commonHitEffectOffset, Quaternion.identity);
        PlaceSharedHitBehindSkills(instance);
        EffectFallbackRelease.Ensure(instance);
        TrackHitEffect(instance, true);
    }

    // Shared damage feedback renders behind skill-specific visuals, including prefab overrides.
    private static void PlaceSharedHitBehindSkills(GameObject instance)
    {
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = -1000;
        }
    }

    private sealed class RecentHitKeys
    {
        private const int Capacity = 128;
        private readonly HashSet<string> _keys = new HashSet<string>();
        private readonly Queue<string> _order = new Queue<string>();

        public bool TryAdd(string cast, BattleCharactor target)
        {
            string key = cast + ":" + target.GetInstanceID();
            if (!_keys.Add(key)) return false;
            _order.Enqueue(key);
            while (_order.Count > Capacity) _keys.Remove(_order.Dequeue());
            return true;
        }

        public void Clear()
        {
            _keys.Clear();
            _order.Clear();
        }
    }

    private readonly RecentHitKeys _sharedHitKeys = new RecentHitKeys();
    private readonly RecentHitKeys _hitReactionKeys = new RecentHitKeys();

    private static bool ShouldPlayFirstHit(BattleCharactor actor, BattleCharactor target,
        BattleHitResult result, RecentHitKeys keys, PresentationRuntimeContext context = null)
    {
        string cast = result?.ExecutionId;
        if (string.IsNullOrEmpty(cast))
        {
            if (context == null && actor != null)
                context = actor.GetComponent<PresentationRuntimeContext>();
            if (context == null || context.CurrentActionInstanceId == 0) return true;
            cast = "presentation:" + context.CurrentActionInstanceId;
        }
        return keys.TryAdd(cast, target);
    }

    public bool ShouldPlayTargetHitAnimation(BattleCharactor actor, BattleCharactor target, BattleHitResult result)
    {
        var context = actor != null ? actor.GetComponent<PresentationRuntimeContext>() : null;
        int skillIndex = result != null && result.SkillIndex > 0 ? result.SkillIndex
            : context?.Current?.Presentation?.SkillIndex ?? 0;
        if ((skillIndex != 3030 && skillIndex != 3031) || target == null) return true;
        return ShouldPlayFirstHit(actor, target, result, _hitReactionKeys, context);
    }

    private bool ShouldPlaySharedHit(BattleCharactor actor, BattleHitResult result)
    {
        return ShouldPlayFirstHit(actor, result.Target, result, _sharedHitKeys);
    }

    private void PlayCommonHitEffect(BattleCharactor target)
    {
        if (!_enableCommonHitEffect || target == null) return;
        if (_commonHitEffect == null)
            _commonHitEffect = Resources.Load<GameObject>("BattleEffects/FX_Common_Hit_Basic");
        if (_commonHitEffect == null) return;
        var instance = Instantiate(_commonHitEffect, target.transform.position + _commonHitEffectOffset, Quaternion.identity);
        PlaceSharedHitBehindSkills(instance);
        EffectFallbackRelease.Ensure(instance);
        TrackHitEffect(instance, true);
    }
    [Header("피격 시 몸체 점멸")]
    [InspectorName("몸체 점멸 사용"), SerializeField] private bool _enableBodyFlash;
    [InspectorName("점멸 시간 (초)"), Min(0.01f)] [SerializeField] private float _bodyFlashSeconds = .12f;
    [InspectorName("흰색 강도"), Range(0f, 1f)] [SerializeField] private float _bodyFlashStrength = 1f;
    private static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");
    private readonly Dictionary<Renderer, float> _bodyFlashes = new Dictionary<Renderer, float>();
    private readonly List<Renderer> _finishedFlashes = new List<Renderer>();
    private MaterialPropertyBlock _flashBlock;

    private void FlashBody(BattleCharactor target)
    {
        if (!_enableBodyFlash || target == null || _bodyFlashSeconds <= 0f) return;
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
            bool supported = false;
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.HasProperty(HitFlashId)) { supported = true; break; }
            if (!supported) continue;
            _bodyFlashes[renderer] = Time.unscaledTime + _bodyFlashSeconds;
            SetBodyFlash(renderer, _bodyFlashStrength);
        }
    }

    private void SetBodyFlash(Renderer renderer, float amount)
    {
        if (renderer == null) return;
        if (_flashBlock == null) _flashBlock = new MaterialPropertyBlock();
        // 다른 연출의 PropertyBlock 값과 원본 공유 재질은 유지합니다.
        renderer.GetPropertyBlock(_flashBlock);
        _flashBlock.SetFloat(HitFlashId, amount);
        renderer.SetPropertyBlock(_flashBlock);
    }

    private void UpdateBodyFlashes()
    {
        _finishedFlashes.Clear();
        foreach (var entry in _bodyFlashes)
        {
            float remaining = entry.Value - Time.unscaledTime;
            float fade = Mathf.Clamp01(remaining / Mathf.Max(.01f, _bodyFlashSeconds * .65f));
            SetBodyFlash(entry.Key, _bodyFlashStrength * fade);
            if (entry.Key == null || remaining <= 0f) _finishedFlashes.Add(entry.Key);
        }
        foreach (Renderer renderer in _finishedFlashes) _bodyFlashes.Remove(renderer);
    }

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

    /// <summary>명중 피해에 기본값 또는 스킬별 설정을 적용한다. HP 하한 테스트는 별도 옵션으로 허용한다.</summary>
    public void RequestHitStopOnDamage(BattleCharactor actor, BattleHitResult result)
    {
        if (!isActiveAndEnabled || result == null || result.Target == null || result.IsHeal || result.IsMiss ||
            (result.AppliedDamage <= 0f && (!_reactToPreventedDamage || result.Damage <= 0f))) return;
        FlashBody(result.Target);
        if (ShouldPlaySharedHit(actor, result))
        {
            PlayCommonHitEffect(result.Target);
            PlayJusticeHitEffect(result);
        }
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
        _sharedHitKeys.Clear();
        _hitReactionKeys.Clear();
        foreach (Renderer renderer in _bodyFlashes.Keys) SetBodyFlash(renderer, 0f);
        _bodyFlashes.Clear();
        if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
        _hitStopRoutine = null;
        RestoreHitStop();
    }

    /// <summary>투사체 자체의 착탄 파티클도 명중 직후 히트스탑에 포함한다.</summary>
    public void RegisterProjectileImpact(GameObject instance) => TrackHitEffect(instance, true);

    private void Update()
    {
        UpdateBodyFlashes();
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

    public static event Action<BattleVisualDirector, BattleHitResult> HealPresented;

    public void ShowDamagePopup(BattleHitResult result)
    {
        if (result == null || result.Target == null)
        {
            return;
        }

        if (result.IsHeal && !result.IsMiss) HealPresented?.Invoke(this, result);
        _popupPresenter?.Show(result);
    }

    public void ShowCounterPopup(BattleCharactor actor) => _popupPresenter?.ShowCounter(actor);
}
