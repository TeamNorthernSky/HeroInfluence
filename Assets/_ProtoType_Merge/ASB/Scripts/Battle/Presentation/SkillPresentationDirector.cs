using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using ASB.Work.Battle.Core;
using ASB.Work.Battle.Sequence;
using ASB.Work.Battle.SkillExecution;

/// <summary>
/// 스킬 연출의 소유자. BattleManager에서 연출 책임만 떼어낸 클래스다.
///
/// 규칙(피해·상태·부활 확정)은 BattleManager가 이미 끝낸 뒤 여기로 넘어온다.
/// 이 클래스는 확정된 결과를 <b>재생</b>만 하며 게임 상태를 바꾸지 않는다.
/// 예외적으로 '언제 적용되는가'를 정하는 트리거(예: 부활 Cue 시점)는 BattleManager가
/// 넘겨준 멱등 델리게이트를 호출하는 형태로만 관여한다.
///
/// MonoBehaviour가 아니다. 코루틴은 BattleManager를 host로 삼아 실행하므로
/// 씬/프리팹에 컴포넌트를 추가할 필요가 없고 직렬화 값도 건드리지 않는다.
/// </summary>
public sealed class SkillPresentationDirector
{
    private readonly BattleManager _battle;
    private int _activeSequenceCount;

    /// <summary>
    /// True while an attack presentation owns the battle screen. Battle-end UI must
    /// wait for this rather than reacting to the damage/death commit mid-Cue.
    /// </summary>
    public bool IsSequenceRunning => _activeSequenceCount > 0;

    // 체인 라이트닝은 일반 투사체처럼 매 타격마다 생성하지 않고, 프리팹별 런타임 인스턴스를 재사용한다.
    private readonly Dictionary<GameObject, JC.VFX.ChainLightningVfx> _chainLightningEffects = new();
    private List<Transform> _chainLightningTargets;
    private int _chainLightningActionInstanceId;
    private JC.VFX.ChainLightningVfx _chainLightningImpactEffect;

    // 투사체 체인 캐스트-로컬 상태. 규칙 계층이 캐스트 시작 시 준비하고 연출이 읽는다.
    private ProjectileChainState _projectileChainState;
    private DamageRole _projectileChainRole = DamageRole.Primary;

    /// <summary>이번 캐스트의 체인 상태. null이면 일반(단발) 투사체.</summary>
    public ProjectileChainState ChainState
    {
        get => _projectileChainState;
        set => _projectileChainState = value;
    }

    /// <summary>현재 처리 중인 컨텍스트의 체인 역할(Primary/Additional).</summary>
    public DamageRole ChainRole
    {
        get => _projectileChainRole;
        set => _projectileChainRole = value;
    }

    public SkillPresentationDirector(BattleManager battle)
    {
        _battle = battle;
    }

    private IEnumerator TrackSequence(IEnumerator sequence)
    {
        _activeSequenceCount++;
        try
        {
            yield return sequence;
        }
        finally
        {
            _activeSequenceCount = Mathf.Max(0, _activeSequenceCount - 1);
        }
    }

    /// <summary>체인 임팩트 신호를 식별하는 액션 인스턴스 ID. 0이면 체인 연출이 없는 캐스트다.</summary>
    public int ChainActionInstanceId => _chainLightningActionInstanceId;

    /// <summary>이번 캐스트의 체인 추가 대상 Transform 목록.</summary>
    public IReadOnlyList<Transform> ChainTargets =>
        _chainLightningTargets ?? (IReadOnlyList<Transform>)System.Array.Empty<Transform>();

    /// <summary>캐스트마다 초기화. 이전 캐스트의 체인 상태가 새 캐스트로 새지 않게 한다.</summary>
    public void ResetCastState()
    {
        _chainLightningTargets = null;
        _chainLightningActionInstanceId = 0;
    }

    /// <summary>이번 캐스트의 체인 추가 대상을 준비한다.</summary>
    public void PrepareChainTargets(IReadOnlyList<DamageContext> contexts, int actionInstanceId)
    {
        _chainLightningTargets = BuildChainLightningTargets(contexts);
        _chainLightningActionInstanceId = actionInstanceId;
    }

    private static List<Transform> BuildChainLightningTargets(IReadOnlyList<DamageContext> contexts)
    {
        var targets = new List<Transform>();
        var seen = new HashSet<Transform>();
        if (contexts == null)
        {
            return targets;
        }

        for (int i = 0; i < contexts.Count; i++)
        {
            DamageContext context = contexts[i];
            Transform targetTransform = context?.Role == DamageRole.Additional && context.Target != null
                ? context.Target.transform
                : null;
            if (targetTransform != null && seen.Add(targetTransform))
            {
                targets.Add(targetTransform);
            }
        }

        return targets;
    }

    /// <summary>체인 볼트가 해당 대상에 실제로 닿을 때까지 기다린다. 체인 연출이 없으면 즉시 반환.</summary>
    public IEnumerator WaitForPresentationImpactRoutine(
        BattleCharactor target,
        SkillPresentationData presentation,
        HitDeliveryGate deliveryGate = null)
    {
        if (target == null || _chainLightningActionInstanceId <= 0)
        {
            yield break;
        }

        ImpactKey key = new ImpactKey(_chainLightningActionInstanceId, target.transform.GetInstanceID());
        yield return CustomEffectImpactAction.WaitForImpactKeyRoutine(presentation, key, deliveryGate, false);
    }

    public static string ResolveAdditionalChainTargetAnimationTrigger(SkillPresentationData presentation, SkillData skill)
    {
        if (presentation != null && !string.IsNullOrWhiteSpace(presentation.TargetAnimationTriggerOverride))
        {
            return presentation.TargetAnimationTriggerOverride.Trim();
        }

        return skill != null ? skill.ResolvedTargetAnimationTrigger : null;
    }

    public bool PlayChainLightningEffect(
        SkillPresentationData presentation,
        BattleCharactor actor,
        Transform primaryTargetTransform,
        IReadOnlyList<Transform> chainTargets)
    {
        if (presentation?.ChainLightningEffectPrefab == null
            || presentation.ProjectileVisual?.DeliveryMode != ProjectileDeliveryMode.ChainAdditionalTargets
            || actor == null
            || primaryTargetTransform == null)
        {
            return false;
        }

        GameObject effectPrefab = presentation.ChainLightningEffectPrefab;
        if (!_chainLightningEffects.TryGetValue(effectPrefab, out JC.VFX.ChainLightningVfx effect)
            || effect == null)
        {
            GameObject instance = UnityEngine.Object.Instantiate(effectPrefab);
            effect = instance.GetComponent<JC.VFX.ChainLightningVfx>();
            if (effect == null)
            {
                Debug.LogWarning($"[SkillPresentationDirector] {effectPrefab.name}에 ChainLightningVfx가 없습니다.", effectPrefab);
                UnityEngine.Object.Destroy(instance);
                return false;
            }

            _chainLightningEffects[effectPrefab] = effect;
        }

        _chainLightningImpactEffect = effect;
        ChainLightningImpactProbe probe = effect.GetComponent<ChainLightningImpactProbe>();
        if (probe == null)
        {
            probe = effect.gameObject.AddComponent<ChainLightningImpactProbe>();
        }

        probe.Configure(_chainLightningActionInstanceId, primaryTargetTransform, chainTargets);
        effect.Play(actor.transform, primaryTargetTransform, chainTargets);
        return true;
    }

    // ──────────────────────────────────────────────────────────────
    // 프레젠테이션 컨텍스트 / Cue 페이즈
    // ──────────────────────────────────────────────────────────────

    private static int _actionInstanceCounter;

    /// <summary>연출 액션 1회를 식별하는 ID. Cue·임팩트 신호가 같은 캐스트 소속인지 판별하는 데 쓴다.</summary>
    public static int NextActionInstanceId() => ++_actionInstanceCounter;

    /// <summary>연출 데이터의 값으로 스킬의 애니메이션 관련 필드를 덮어쓴다.</summary>
    public void ApplyPresentationOverride(SkillData skill)
    {
        SkillPresentationCatalog catalog = _battle.PresentationCatalog;
        if (skill == null || catalog == null) return;
        SkillPresentationData presentation = catalog.Get(skill.skillIndex);
        if (presentation == null) return;

        AttackBeat beat = GetPrimaryAttackBeat(presentation);
        string stateName = beat != null && !string.IsNullOrWhiteSpace(beat.AnimationStateName)
            ? beat.AnimationStateName.Trim()
            : presentation.ResolvedAnimationStateName;
        if (!string.IsNullOrWhiteSpace(stateName))
        {
            skill.StateName = stateName;
        }

        if (!string.IsNullOrWhiteSpace(presentation.TargetAnimationTriggerOverride))
        {
            skill.TargetAnimationTrigger = presentation.TargetAnimationTriggerOverride.Trim();
        }

        skill.UseAnimEvent = presentation.UseAnimEvent;
        skill.HitDelay = presentation.HitDelay;
    }

    public static AttackBeat GetPrimaryAttackBeat(SkillPresentationData presentation)
    {
        if (presentation?.Attack != null && presentation.Attack.Enabled
            && presentation.Attack.Beats != null && presentation.Attack.Beats.Count > 0)
        {
            return presentation.Attack.Beats[0];
        }

        return null;
    }

    // Schema=1(PhaseCue)일 때만 유닛 로컬 Cue 컨텍스트를 등록한다. 같은 actionId의 Beat 갱신은 Held Handle을 보존한다.
    public void SetupPresentationContext(BattleCharactor actor, BattleCharactor target, SkillData skill,
        SkillPresentationData presentation, int actionInstanceId, List<CueBinding> activeCues = null, string activeStateName = null,
        IReadOnlyList<BattleCharactor> aoeTargets = null, Vector3? targetPositionOverride = null)
    {
        BattleVisualDirector visual = _battle.VisualDirector;
        if (actor == null || presentation == null || visual == null || !presentation.IsPhaseCue || actionInstanceId == 0) return;

        // 선언 순서를 보존한다 — 같은 프레임에 여러 Cue가 걸릴 때 리스트 순서로 발화하기 위해서다.
        var runtimeCues = new List<RuntimeCue>();
        var seenCueNames = new HashSet<string>();
        List<CueBinding> cues = activeCues ?? GetPrimaryAttackBeat(presentation)?.Cues;
        if (cues != null)
        {
            foreach (CueBinding binding in cues)
            {
                if (binding == null) continue;
                string key = binding.NormalizedCueName;
                if (string.IsNullOrEmpty(key) || !seenCueNames.Add(key)) continue;

                var cue = new RuntimeCue
                {
                    NormalizedCueName = key,
                    CueId = binding.CueId,
                    Operation = binding.Operation,
                    InstanceKey = binding.NormalizedInstanceKey,
                    Anchor = binding.Anchor,
                    Socket = binding.Socket,
                    Timing = binding.Timing,
                    Time = binding.Time,
                };
                if (binding.EffectIds != null)
                {
                    foreach (int id in binding.EffectIds)
                    {
                        GameObject prefab = visual.GetRegisteredEffect(id);
                        if (prefab != null) cue.EffectPrefabs.Add(prefab);
                    }
                }
                if (binding.SoundIds != null) cue.SoundIds.AddRange(binding.SoundIds);
                runtimeCues.Add(cue);
            }
        }

        PresentationRuntimeContext context = actor.EnsurePresentationComponents();
        if (context == null) return;

        var effectContext = new SkillEffectContext
        {
            ActionInstanceId = actionInstanceId,
            Caster = actor,
            PrimaryTarget = target,
            // EachTarget 앵커가 대상 수만큼 스폰할 수 있도록 AoE 타깃 리스트를 그대로 보존. 없으면 주 타깃 1개.
            Targets = (aoeTargets != null && aoeTargets.Count > 0)
                ? aoeTargets
                : (target != null ? new List<BattleCharactor> { target } : null),
            // 인질 등 BattleCharactor가 아닌 대상은 위치만 오버라이드로 주입(캐스팅/컴포넌트 추가 없이 Target 앵커 스폰 지원).
            TargetPosition = targetPositionOverride ?? (target != null ? target.transform.position : actor.transform.position),
            SocketTransform = actor.GetComponent<UnitVisualProfile>()?.AttackEffectSocket ?? actor.transform,
            PlaybackSpeed = _battle.CurrentBattleSpeed,
            HitIndex = 0,
            // 부활 스킬(4040): 공격 대상(적)과 다른 부활 아군에 이펙트를 꽂기 위한 참조. 부활 없으면 null.
            ReviveTarget = actor.PendingReviveTarget,
        };
        string expectedStateName = !string.IsNullOrWhiteSpace(activeStateName)
            ? activeStateName
            : skill != null ? skill.StateName : string.Empty;
        int expectedHash = !string.IsNullOrWhiteSpace(expectedStateName)
            ? Animator.StringToHash(expectedStateName.Trim())
            : 0;
        context.SetActive(actionInstanceId, effectContext, runtimeCues, expectedHash,
            $"{presentation.name} / state='{expectedStateName}'");
    }

    public static void ClearPresentationContext(BattleCharactor actor)
    {
        actor?.GetComponent<PresentationRuntimeContext>()?.Clear();
    }

    public IEnumerator ActivatePresentationCueRoutine(BattleCharactor actor, BattleCharactor target, SkillData skill,
        SkillPresentationData presentation, int actionInstanceId, List<CueBinding> cues, string stateName,
        IReadOnlyList<BattleCharactor> aoeTargets = null)
    {
        SetupPresentationContext(actor, target, skill, presentation, actionInstanceId, cues, stateName, aoeTargets);
        yield break;
    }

    public void EnqueuePhaseCuePrologue(ActionSequenceRunner runner, BattleCharactor actor, BattleCharactor target,
        SkillData skill, SkillPresentationData presentation, int actionInstanceId, float? nextBlendSeconds,
        System.Action<float> onElapsed)
    {
        EnqueueCuePhase(runner, actor, target, skill, presentation, actionInstanceId, presentation?.MovePrepare,
            nextBlendSeconds, onElapsed);
    }

    public void EnqueuePhaseCueAttackPrepare(ActionSequenceRunner runner, BattleCharactor actor, BattleCharactor target,
        SkillData skill, SkillPresentationData presentation, int actionInstanceId, float? nextBlendSeconds,
        System.Action<float> onElapsed)
    {
        EnqueueCuePhase(runner, actor, target, skill, presentation, actionInstanceId, presentation?.AttackPrepare,
            nextBlendSeconds, onElapsed);
    }

    public void EnqueuePhaseCueEpilogue(ActionSequenceRunner runner, BattleCharactor actor, BattleCharactor target,
        SkillData skill, SkillPresentationData presentation, int actionInstanceId, System.Action<float> onElapsed)
    {
        EnqueueCuePhase(runner, actor, target, skill, presentation, actionInstanceId, presentation?.Post,
            null, onElapsed);
    }

    private void EnqueueCuePhase(ActionSequenceRunner runner, BattleCharactor actor, BattleCharactor target,
        SkillData skill, SkillPresentationData presentation, int actionInstanceId, CuePhase phase,
        float? nextBlendSeconds, System.Action<float> onElapsed)
    {
        if (runner == null || presentation?.IsPhaseCue != true || phase == null)
            return;

        runner.Enqueue(new RunRoutineAction(host => PlayCuePhaseRoutine(
            actor, target, skill, presentation, actionInstanceId, phase, nextBlendSeconds, onElapsed)));
    }

    private IEnumerator PlayCuePhaseRoutine(BattleCharactor actor, BattleCharactor target, SkillData skill,
        SkillPresentationData presentation, int actionInstanceId, CuePhase phase, float? nextBlendSeconds,
        System.Action<float> onElapsed)
    {
        if (phase == null || !phase.Enabled)
            yield break;

        string stateName = phase.AnimationStateName?.Trim();
        SetupPresentationContext(actor, target, skill, presentation, actionInstanceId, phase.Cues, stateName);

        // 부활 대기가 있으면 준비 페이즈 시작 시 Cue를 보장하고, 같은 시점에 부활 확정을 트리거한다.
        // (클립에 revive Cue 이벤트가 없는 구성이 있어 Cue를 여기서 직접 낸다.)
        // 확정 자체는 규칙 계층이 만든 멱등 델리게이트다 — 여기서 '시점'만 정하고, 트리거가 없어도 스윕이 보장한다.
        if (_battle.HasPendingRevive && phase is AttackPreparePhase)
        {
            UnitEffectPresenter presenter = actor != null ? actor.GetComponent<UnitEffectPresenter>() : null;
            // PlayState 이전이라 아직 기대 state에 진입하지 않았다 → State 게이트를 우회한다.
            presenter?.PresentationCue("revive", bypassStateGate: true);
            _battle.TriggerPendingRevive();
        }

        CharactorAnimationController animation = actor?.Anim;
        if (!string.IsNullOrEmpty(stateName) && animation != null)
        {
            animation.PlayState(stateName, Mathf.Max(0f, phase.BlendInSeconds));
            if (nextBlendSeconds.HasValue)
            {
                yield return animation.WaitForSkillTransitionStart(stateName, nextBlendSeconds.Value);
            }
            else
            {
                yield return animation.WaitForSkillClipEnd(stateName);
            }
            onElapsed?.Invoke(animation.LastClipWaitBattleSeconds);
        }

        else if (phase is MovePreparePhase && phase.Cues != null)
        {
            // MovePrepare cues without an animation state fire immediately before movement.
            UnitEffectPresenter presenter = actor != null ? actor.GetComponent<UnitEffectPresenter>() : null;
            if (presenter != null)
            {
                for (int i = 0; i < phase.Cues.Count; i++)
                {
                    string cueName = phase.Cues[i]?.CueName;
                    if (!string.IsNullOrWhiteSpace(cueName))
                    {
                        // 애니 state가 없는 MovePrepare Cue는 이동 직전에 즉시 발화한다 → 게이트 우회.
                        presenter.PresentationCue(cueName, bypassStateGate: true);
                    }
                }
            }
        }

        if (phase is PostPhase post && post.ExtraDelay > 0f)
        {
            yield return _battle.WaitForBattleSeconds(post.ExtraDelay);
            onElapsed?.Invoke(post.ExtraDelay);
        }
    }

    public static string ResolveAttackBeatState(CharactorAnimationController anim, SkillData skill,
        SkillPresentationData presentation, AttackBeat beat, int beatIndex)
    {
        if (beat != null && !string.IsNullOrWhiteSpace(beat.AnimationStateName)) return beat.AnimationStateName.Trim();
        if (beatIndex > 0) return string.Empty;
        if (!string.IsNullOrWhiteSpace(presentation?.ResolvedAnimationStateName)) return presentation.ResolvedAnimationStateName;
        return anim != null ? anim.GetTargetStateName(skill) : string.Empty;
    }

    public static float? ResolveFirstAttackBeatBlendInSeconds(CharactorAnimationController anim, SkillData skill,
        SkillPresentationData presentation)
    {
        if (presentation?.Attack?.Beats == null)
        {
            return null;
        }

        for (int i = 0; i < presentation.Attack.Beats.Count; i++)
        {
            AttackBeat beat = presentation.Attack.Beats[i];
            if (beat == null)
            {
                continue;
            }

            string stateName = ResolveAttackBeatState(anim, skill, presentation, beat, i);
            if (!string.IsNullOrWhiteSpace(stateName))
            {
                return Mathf.Max(0f, beat.BlendInSeconds);
            }
        }

        return null;
    }

    public static float? ResolveMovePrepareNextBlendInSeconds(CharactorAnimationController anim, SkillData skill,
        SkillPresentationData presentation)
    {
        CuePhase attackPrepare = presentation?.AttackPrepare;
        if (attackPrepare != null && attackPrepare.Enabled
            && !string.IsNullOrWhiteSpace(attackPrepare.AnimationStateName))
        {
            return Mathf.Max(0f, attackPrepare.BlendInSeconds);
        }

        return ResolveFirstAttackBeatBlendInSeconds(anim, skill, presentation);
    }

    public static float? ResolvePostBlendInSeconds(SkillPresentationData presentation)
    {
        PostPhase post = presentation?.Post;
        if (post == null || !post.Enabled || string.IsNullOrWhiteSpace(post.AnimationStateName))
        {
            return null;
        }

        return Mathf.Max(0f, post.BlendInSeconds);
    }

    public IEnumerator PlayChainLightningEffectRoutine(
        SkillPresentationData presentation,
        BattleCharactor actor,
        BattleCharactor primaryTarget,
        List<DamageContext> contexts,
        int pairCount)
    {
        var chainTargets = new List<Transform>();
        var seen = new HashSet<Transform>();
        for (int i = 1; i < pairCount; i++)
        {
            Transform targetTransform = contexts[i]?.Target != null ? contexts[i].Target.transform : null;
            if (targetTransform != null && targetTransform != primaryTarget.transform && seen.Add(targetTransform))
            {
                chainTargets.Add(targetTransform);
            }
        }

        PlayChainLightningEffect(presentation, actor, primaryTarget != null ? primaryTarget.transform : null, chainTargets);
        yield break;
    }

    public static SkillData CreateDefaultAnimationSkillData()
    {
        return new SkillData
        {
            AnimationTrigger = "Attack",
            HitDelay = 0.25f,
            TotalDelay = 0.5f,
            TargetAnimationTrigger = "Hit",
            UseAnimEvent = false
        };
    }

    // Apply SkillPresentation overrides to the transient SkillData used by this sequence.
    public static SkillData ResolveSkillAnimationData(SkillData source)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.AnimationTrigger))
        {
            SkillData defaults = CreateDefaultAnimationSkillData();
            if (source == null)
            {
                return defaults;
            }

            SkillData copy = CloneSkillDataForAnimation(source);
            copy.AnimationTrigger = defaults.AnimationTrigger;
            copy.HitDelay = defaults.HitDelay;
            copy.TotalDelay = defaults.TotalDelay;
            copy.TargetAnimationTrigger = defaults.TargetAnimationTrigger;
            copy.UseAnimEvent = defaults.UseAnimEvent;
            return copy;
        }

        return CloneSkillDataForAnimation(source);
    }

    public static SkillData CloneSkillDataForAnimation(SkillData source)
    {
        return new SkillData
        {
            skillIndex = source.skillIndex,
            skillKey = source.skillKey,
            category = source.category,
            slot = source.slot,
            skillClass = source.skillClass,
            acquireLevel = source.acquireLevel,
            skillName = source.skillName,
            description = source.description,
            ipCost = source.ipCost,
            classSkillEffect = source.classSkillEffect,
            classSkillRange = source.classSkillRange,
            EnemySkill1Range = source.EnemySkill1Range,
            EnemySkill2Range = source.EnemySkill2Range,
            classSkillRangeLine = source.classSkillRangeLine,
            classSkillTarget = source.classSkillTarget,
            boundary = source.boundary != null ? new System.Collections.Generic.List<int>(source.boundary) : new System.Collections.Generic.List<int>(),
            multiTargetType = source.multiTargetType,
            multiTargetCount = source.multiTargetCount,
            skillValue = source.skillValue,
            skillSubValue = source.skillSubValue,
            AnimationTrigger = source.AnimationTrigger,
            StateName = source.StateName,
            UseAnimEvent = source.UseAnimEvent,
            HitDelay = source.HitDelay,
            TotalDelay = source.TotalDelay,
            TargetAnimationTrigger = source.TargetAnimationTrigger
        };
    }

    public static SkillData TryGetSkillDataForDamageContext(DamageContext damageContext)
    {
        if (damageContext == null)
        {
            return null;
        }

        if (damageContext.Caster != null && damageContext.Caster.availableSkills != null)
        {
            for (int i = 0; i < damageContext.Caster.availableSkills.Count; i++)
            {
                SkillData skill = damageContext.Caster.availableSkills[i];
                if (skill != null && skill.skillIndex == damageContext.SkillIndex)
                {
                    return skill;
                }
            }
        }

        if (DHCsvTemplateCatalog.Instance != null)
        {
            SkillData loaded = DHCsvTemplateCatalog.Instance.GetSkillTemplate(damageContext.SkillIndex);
            if (loaded != null) return loaded;
        }

        return null;
    }

    public static SkillData TryGetSkillDataForHealContext(HealContext healContext)
    {
        if (healContext == null)
        {
            return null;
        }

        if (healContext.Caster != null && healContext.Caster.availableSkills != null)
        {
            for (int i = 0; i < healContext.Caster.availableSkills.Count; i++)
            {
                SkillData skill = healContext.Caster.availableSkills[i];
                if (skill != null && skill.skillIndex == healContext.SkillIndex)
                {
                    return skill;
                }
            }
        }

        if (DHCsvTemplateCatalog.Instance != null)
        {
            SkillData loaded = DHCsvTemplateCatalog.Instance.GetSkillTemplate(healContext.SkillIndex);
            if (loaded != null) return loaded;
        }

        return null;
    }

        public sealed class MovingAttackPlan
    {
        public MovingAttackPresentation Presentation;
        public Vector3 FormationCenter;
        public Vector3 OriginPosition;
        public MovingAttackPath.Points PathPoints;
        public List<BattleCharactor> Targets;
    }

    public static bool TryBuildMovingAttackPlan(BattleCharactor actor, CharactorAnimationController animation,
        SkillPresentationData presentation, IEnumerable<BattleCharactor> candidateTargets, out MovingAttackPlan plan,
        Vector3? fallbackCenter = null)
    {
        plan = null;
        MovingAttackPresentation sweep = presentation?.MovingAttack;
        if (sweep == null || !sweep.Enabled || actor == null || animation == null
            || string.IsNullOrWhiteSpace(sweep.AnimationStateName))
        {
            return false;
        }

        var targets = new List<BattleCharactor>();
        var seen = new HashSet<BattleCharactor>();
        if (candidateTargets != null)
        {
            foreach (BattleCharactor candidate in candidateTargets)
            {
                if (candidate != null && !candidate.IsDead && seen.Add(candidate))
                {
                    targets.Add(candidate);
                }
            }
        }

        // 인질 등 BattleCharactor가 아닌 타깃은 candidateTargets가 비므로, 명시된 중심점(인질 위치)으로 스윕한다.
        if (targets.Count == 0 && !fallbackCenter.HasValue) return false;

        Vector3 center;
        if (targets.Count > 0)
        {
            center = Vector3.zero;
            for (int i = 0; i < targets.Count; i++) center += targets[i].transform.position;
            center /= targets.Count;
        }
        else
        {
            center = fallbackCenter.Value;
        }

        Vector3 origin = actor.transform.position;
        plan = new MovingAttackPlan
        {
            Presentation = sweep,
            FormationCenter = center,
            OriginPosition = origin,
            PathPoints = MovingAttackPath.BuildPoints(sweep, origin, center),
            Targets = targets
        };
        return true;
    }

    public void EnqueueMovingAttackSequence(ActionSequenceRunner runner, MovingAttackPlan plan,
        BattleCharactor actor, BattleCharactor presentationTarget, SkillData skill,
        SkillPresentationData presentation, int actionInstanceId, CharactorAnimationController actorAnim,
        Func<MonoBehaviour, IEnumerator> primaryHitRoutine, List<ASB.Work.Battle.Sequence.MovingAttackHitTarget> sequentialHits,
        float nextBlendInSeconds, Action<float> onElapsed)
    {
        if (runner == null || plan == null) return;

        runner.Enqueue(new ASB.Work.Battle.Sequence.MovingAttackPresentationAction(
            actor, actorAnim, plan.Presentation, plan.PathPoints, plan.OriginPosition,
            () => SetupPresentationContext(actor, presentationTarget, skill, presentation, actionInstanceId,
                plan.Presentation.Cues, plan.Presentation.AnimationStateName),
            primaryHitRoutine, sequentialHits, _battle.CurrentBattleSpeed, nextBlendInSeconds, onElapsed));
    }

    public void EnqueueMovingAttackApproach(ActionSequenceRunner runner, MovingAttackPlan plan,
        CharactorAnimationController actorAnim, UnitMovementProfile movement, MovePhase movePhase)
    {
        if (runner == null || plan == null || actorAnim == null || movement == null) return;

        string stateName = movePhase?.AnimationStateName;
        float blend = movePhase != null ? Mathf.Max(0f, movePhase.BlendInSeconds) : 0.1f;
        runner.Enqueue(new ASB.Work.Battle.Sequence.MoveToWorldPositionAction(
            actorAnim, plan.PathPoints.Entry, movement.MoveDuration / _battle.CurrentBattleSpeed, stateName, blend));
    }

    public static float ResolveMovingAttackIncomingBlendInSeconds(MovingAttackPlan plan)
    {
        return plan?.Presentation != null ? Mathf.Max(0f, plan.Presentation.AnimationBlendInSeconds) : 0f;
    }

    public static float ResolveMovingAttackOutgoingBlendInSeconds(SkillPresentationData presentation,
        bool returnEnabled)
    {
        if (returnEnabled && presentation?.Return != null)
        {
            return Mathf.Max(0f, presentation.Return.BlendInSeconds);
        }

        return ResolvePostBlendInSeconds(presentation) ?? 0f;
    }

    public void EnqueueMovingAttackReturn(ActionSequenceRunner runner, CharactorAnimationController actorAnim,
        UnitMovementProfile movement, Vector3 originPosition, float originRotationY, ReturnPhase returnPhase)
    {
        if (movement == null) return;
        EnqueueSkillReturn(runner, actorAnim, movement, true, false, originPosition, originRotationY, returnPhase);
    }

    public IEnumerator RunSkillSequenceCore(
        BattleCharactor actor,
        ISkillTarget skillTarget,
        SkillData skill,
        bool playBasicAttackAnimation,
        bool playTargetHitAnimation,
        Func<BattleHitResult> onHitCallback,
        HitDeliveryGate deliveryGate,
        IReadOnlyList<BattleCharactor> presentationTargets = null)
    {
        yield return TrackSequence(RunSkillSequenceCoreInternal(
            actor, skillTarget, skill, playBasicAttackAnimation, playTargetHitAnimation,
            onHitCallback, deliveryGate, presentationTargets));
    }

    // ──────────────────────────────────────────────────────────────
    // Path A — Timeline 레일 재생기 (지시서 §4)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Timeline 레일 스킬 재생: 캐릭터 Variant를 PlayableDirector로 틀고, 전투 배속을 그래프에 반영하며,
    /// 재생 중 마커 알림(Cue/Impact/Projectile)을 처리하고, 완료까지 대기한 뒤 Idle로 복귀한다.
    /// Cue/Impact/Projectile의 시점은 Timeline Signal이 소유한다. Impact·Projectile이 없던 스킬은 완료 시
    /// 대미지 콜백을 1회 적용해 전투 상태 일관성과 무-hang을 보장한다(§10). 이동(MoveSignal)은 아직 미배선.
    /// </summary>
    private IEnumerator RunTimelineRailRoutine(
        BattleCharactor actor, BattleCharactor target, SkillData skill, SkillPresentationData presentation,
        Func<BattleHitResult> onHitCallback, HitDeliveryGate deliveryGate,
        Transform targetTransform, Vector3 targetPosition, bool playTargetHitAnimation,
        string sectionId = null, bool returnActorToIdle = true, TimelineRailPlaybackResult playbackResult = null)
    {
        // 피격 연출(대미지 + 피격 애니 + 데미지 팝업). Path B의 ResolveHitAction을 재사용한다 —
        // 대미지 콜백만 부르면 HP는 깎이지만 피격 모션·데미지 팝업이 안 나온다.
        string targetAnimTrigger = playTargetHitAnimation ? (skill?.ResolvedTargetAnimationTrigger ?? "Hit") : null;
        bool hitResolved = false;
        Coroutine hitRoutine = null;
        void ResolveHit()
        {
            if (hitResolved) return;
            hitResolved = true;
            hitRoutine = _battle.StartCoroutine(
                new ResolveHitAction(actor, target, onHitCallback, targetAnimTrigger, _battle.CurrentBattleSpeed, _battle.VisualDirector)
                    .ExecuteRoutine(_battle));
        }

        TimelineAsset timeline = actor != null ? presentation.ResolveTimeline(actor.UnitName) : null;

        // 저장/빌드 검증에서 사전 차단하지만(§3), 런타임 도달 시에도 시퀀스는 반드시 종료한다 —
        // 조용한 Path B 폴백은 하지 않고, 대미지 유실/행 hang을 막는다.
        if (timeline == null)
        {
            Debug.LogError(
                $"[PathA] Timeline 레일 스킬(idx {presentation.SkillIndex})인데 캐릭터 '{actor?.UnitName}'의 Variant가 없습니다. " +
                "피격 연출만 적용하고 종료합니다.", actor);
            ResolveHit();
            if (hitRoutine != null) yield return hitRoutine;
            yield break;
        }

        if (!PresentationTimelineSections.TryResolve(timeline, sectionId,
                out PresentationTimelineRange playbackRange, out string sectionError))
        {
            Debug.LogError(
                $"[PathA] Timeline 구간을 해석하지 못했습니다(idx {presentation.SkillIndex}, section='{sectionId}'): {sectionError}",
                timeline);
            ResolveHit();
            if (hitRoutine != null) yield return hitRoutine;
            yield break;
        }

        PlayableDirector director = EnsureTimelineDirector(actor);
        BindTimelineToActor(director, timeline, actor);

        // 재생 중 Signal이 CueId로 발화할 수 있도록, 이 스킬의 모든 Cue를 컨텍스트에 id로 등록한다.
        int actionInstanceId = NextActionInstanceId();
        RegisterTimelineRailCues(actor, target, skill, presentation, actionInstanceId);

        // 마커 알림 → Cue 발화 / Impact 피격연출 / Projectile 발사. 시점 소유는 Timeline이 갖는다.
        Coroutine projectileRoutine = null;
        bool projectileLaunched = false;
        void LaunchProjectile()
        {
            if (projectileLaunched) return;
            projectileLaunched = true;
            // Timeline과 병행 재생: 투사체를 별도 코루틴으로 날리고, 도착 시 피격연출을 적용한다.
            projectileRoutine = _battle.StartCoroutine(
                LaunchProjectileRoutine(actor, target, presentation, deliveryGate, ResolveHit));
        }

        PresentationSignalReceiver receiver = EnsureSignalReceiver(actor);
        receiver.Configure(actor.GetComponent<UnitAnimationEventRouter>(), ResolveHit, LaunchProjectile,
            playbackRange.Start, playbackRange.End);

        bool stopped = false;
        void OnStopped(PlayableDirector d) => stopped = true;
        director.stopped += OnStopped;

        // 피격/사망 인터럽트: 반응 CrossFade가 Timeline 포즈를 덮기 전에 director를 '동기' 정지시킨다.
        // (플래그만 세우면 PlayableDirector가 같은/다음 프레임에 한 번 더 Evaluate해 Hit/Dead를 덮어쓴다.)
        PresentationInterruptReason interrupt = PresentationInterruptReason.None;
        void OnInterrupt(PresentationInterruptReason reason)
        {
            if (reason == PresentationInterruptReason.None) return;
            interrupt = reason;
            if (director != null) director.Stop();
        }
        if (actor != null) actor.PresentationInterruptRequested += OnInterrupt;
        try
        {
            director.Play();
            // Play()가 정지 상태의 Director 시간을 초기화할 수 있으므로 그래프를 먼저 연 뒤
            // 구간 시작점으로 이동한다. 범위 밖 Retroactive Marker는 receiver가 차단한다.
            director.time = playbackRange.Start;
            director.Evaluate();
            while (!stopped && actor != null && !actor.IsDead && interrupt == PresentationInterruptReason.None)
            {
                float regionSpeed = PresentationTimelineSpeed.SpeedAt(timeline, director.time);
                ApplyBattleSpeedToDirector(director, _battle.CurrentBattleSpeed * regionSpeed);   // 전투배속 × 구간속도
                if (director.time >= playbackRange.End - 0.0001d)
                {
                    break;
                }
                yield return null;
            }
        }
        finally
        {
            if (actor != null) actor.PresentationInterruptRequested -= OnInterrupt;
            director.stopped -= OnStopped;
            director.Stop();               // 그래프 정리
            director.playableAsset = null; // 다음 재생을 위해 바인딩 해제
            receiver.ClearConfig();
            ClearPresentationContext(actor);
        }

        if (playbackResult != null) playbackResult.Interrupt = interrupt;

        // Actor Presentation 완료와 Delivery 완료를 분리한다. 정상 완료 && 생존 && 인터럽트 아님일 때만
        // 즉시 Idle로 복귀시킨다(Hit/Dead 반응을 Idle로 덮지 않기 위함). 아래 Delivery(투사체/폴백)는
        // 인터럽트와 무관하게 계속 진행해 '커밋된 공격은 1회 확정' 규칙을 지킨다.
        if (returnActorToIdle && actor != null && !actor.IsDead && interrupt == PresentationInterruptReason.None)
        {
            actor.Anim?.PlayIdleAnimation();
        }

        // 발사된 투사체가 있으면 도착까지 기다린다(도착 시 피격연출). 이 대기는 전투 Delivery만 막고
        // 시전자의 애니메이션 소유권은 더 이상 막지 않는다.
        if (projectileRoutine != null) yield return projectileRoutine;

        // Impact도 투사체도 없던 스킬은 여기서 피격연출을 1회 보장한다(대미지+애니+팝업 유실 방지, §10).
        if (!hitResolved) ResolveHit();
        if (hitRoutine != null) yield return hitRoutine;
    }

    /// <summary>
    /// 근거리 Timeline Rail의 소유권 핸드오프. 접근/복귀 로코모션은 기존 이동 Action이 담당하고,
    /// 그 사이 공격 구간만 Timeline이 Animator를 소유한다.
    /// </summary>
    private IEnumerator RunMeleeTimelineRailRoutine(
        BattleCharactor actor, BattleCharactor target, SkillData skill, SkillPresentationData presentation,
        Func<BattleHitResult> onHitCallback, HitDeliveryGate deliveryGate,
        Transform targetTransform, Vector3 targetPosition, bool playTargetHitAnimation,
        CharactorAnimationController actorAnim, UnitMovementProfile movement,
        bool shouldMove, bool shouldRotate, Vector3 originPosition, float originRotationY)
    {
        bool moveEnabled = presentation?.Move?.Enabled ?? true;
        bool returnEnabled = presentation?.Return?.Enabled ?? true;

        if (moveEnabled)
        {
            var approach = new ActionSequenceRunner();
            EnqueueSkillApproach(approach, actor, target, actorAnim, movement, shouldMove, shouldRotate,
                presentation?.Move, targetTransform);
            yield return _battle.StartCoroutine(approach.RunAll(_battle));
        }

        // 공격 Timeline 종료 후 복귀 이동이 이어지므로 여기서는 Idle CrossFade를 넣지 않는다.
        var playbackResult = new TimelineRailPlaybackResult();
        yield return RunTimelineRailRoutine(
            actor, target, skill, presentation, onHitCallback, deliveryGate,
            targetTransform, targetPosition, playTargetHitAnimation,
            sectionId: null, returnActorToIdle: false, playbackResult: playbackResult);

        // 피격/사망으로 공격 Timeline이 끊긴 경우, 복귀 이동(MoveReturn)이나 Idle로 Hit/Dead 반응을 덮지 않는다(§1-3).
        // Dead면 시체가 MoveReturn하지 않고, Hit면 반응이 유지된다. (위치 복구는 이번 파일럿 범위에서 생략.)
        if (playbackResult.Interrupted)
        {
            yield break;
        }

        var tail = new ActionSequenceRunner();
        if (returnEnabled)
        {
            EnqueueSkillReturn(tail, actorAnim, movement, shouldMove, shouldRotate,
                originPosition, originRotationY, presentation?.Return);
        }
        else if (shouldRotate)
        {
            EnqueueSkillReturn(tail, actorAnim, movement, false, true,
                originPosition, originRotationY, presentation?.Return);
        }
        tail.Enqueue(new ReturnToIdleAction(actor));
        yield return _battle.StartCoroutine(tail.RunAll(_battle));
    }

    /// <summary>
    /// Path A 투사체 발사(지시서 §4 ProjectileSignal). 스킬의 ProjectileVisual을 타깃으로 발사하고,
    /// 도착 시 <paramref name="onArrive"/>(대미지)를 호출한다. 기존 <see cref="ProjectileImpactAction"/> 재사용.
    /// 투사체가 없으면(비-원거리 등) 즉시 대미지를 적용해 유실을 막는다.
    /// </summary>
    private IEnumerator LaunchProjectileRoutine(BattleCharactor actor, BattleCharactor target,
        SkillPresentationData presentation, HitDeliveryGate deliveryGate, Action onArrive)
    {
        ProjectileVisualData visual = presentation != null ? presentation.GetProjectileVisual() : null;
        if (actor == null || visual == null || visual.Prefab == null)
        {
            onArrive?.Invoke();
            yield break;
        }

        var projectile = new ProjectileImpactAction(
            actor, target, visual, _battle.CurrentBattleSpeed, deliveryGate, presentation.SkillIndex);
        yield return projectile.ExecuteRoutine(_battle);

        onArrive?.Invoke();   // 도착 → 대미지
    }

    /// <summary>
    /// AoE Timeline Rail. Actor Timeline과 다대상 Delivery를 분리하고, Impact/Projectile 마커는 기존
    /// AoEApplyDamageAction을 정확히 한 번 시작하는 역할만 한다.
    /// </summary>
    private IEnumerator RunAoETimelineRailRoutine(
        BattleCharactor actor, BattleCharactor primaryTarget, SkillData skill,
        SkillPresentationData presentation, HitDeliveryGate deliveryGate,
        List<DamageContext> contexts, List<Func<BattleHitResult>> hitCallbacks, int pairCount,
        ProjectileVisualData projectileVisual)
    {
        TimelineAsset timeline = actor != null ? presentation.ResolveTimeline(actor.UnitName) : null;
        if (timeline == null)
        {
            Debug.LogError(
                $"[PathA/AoE] Timeline 레일 스킬(idx {presentation.SkillIndex})인데 캐릭터 '{actor?.UnitName}'의 Variant가 없습니다. " +
                "AoE Delivery를 즉시 확정하고 종료합니다.", actor);
            yield return new AoEApplyDamageAction(
                    contexts, hitCallbacks, pairCount, _battle.CurrentBattleSpeed, _battle.VisualDirector,
                    projectileVisual, deliveryGate)
                .ExecuteRoutine(_battle);
            yield break;
        }

        PlayableDirector director = EnsureTimelineDirector(actor);
        BindTimelineToActor(director, timeline, actor);

        var presentationTargets = new List<BattleCharactor>();
        for (int i = 0; i < pairCount; i++)
        {
            BattleCharactor candidate = contexts[i]?.Target;
            if (candidate != null && !presentationTargets.Contains(candidate)) presentationTargets.Add(candidate);
        }

        int actionInstanceId = NextActionInstanceId();
        RegisterTimelineRailCues(actor, primaryTarget, skill, presentation, actionInstanceId, presentationTargets);

        bool deliveryStarted = false;
        Coroutine deliveryRoutine = null;
        void ResolveDelivery()
        {
            if (deliveryStarted) return;
            deliveryStarted = true;
            deliveryRoutine = _battle.StartCoroutine(
                new AoEApplyDamageAction(
                        contexts, hitCallbacks, pairCount, _battle.CurrentBattleSpeed, _battle.VisualDirector,
                        projectileVisual, deliveryGate)
                    .ExecuteRoutine(_battle));
        }

        PresentationSignalReceiver receiver = EnsureSignalReceiver(actor);
        receiver.Configure(actor.GetComponent<UnitAnimationEventRouter>(), ResolveDelivery, ResolveDelivery);

        bool stopped = false;
        void OnStopped(PlayableDirector d) => stopped = true;
        director.stopped += OnStopped;

        // 피격/사망 인터럽트: AoE 시전자도 반응 CrossFade가 Timeline 포즈를 덮기 전에 director를 동기 정지한다.
        PresentationInterruptReason interrupt = PresentationInterruptReason.None;
        void OnInterrupt(PresentationInterruptReason reason)
        {
            if (reason == PresentationInterruptReason.None) return;
            interrupt = reason;
            if (director != null) director.Stop();
        }
        if (actor != null) actor.PresentationInterruptRequested += OnInterrupt;
        try
        {
            director.time = 0d;
            director.Evaluate();
            director.Play();
            while (!stopped && actor != null && !actor.IsDead && interrupt == PresentationInterruptReason.None)
            {
                float regionSpeed = PresentationTimelineSpeed.SpeedAt(timeline, director.time);
                ApplyBattleSpeedToDirector(director, _battle.CurrentBattleSpeed * regionSpeed);   // 전투배속 × 구간속도
                yield return null;
            }
        }
        finally
        {
            if (actor != null) actor.PresentationInterruptRequested -= OnInterrupt;
            director.stopped -= OnStopped;
            director.Stop();
            director.playableAsset = null;
            receiver.ClearConfig();
            ClearPresentationContext(actor);
        }

        // 캐릭터 포즈는 Delivery와 독립적으로 즉시 복귀하되, 사망/인터럽트 시에는 Idle로 Hit/Dead를 덮지 않는다.
        // AoE Delivery(다대상 대미지)는 아래에서 인터럽트와 무관하게 계속 확정한다('커밋된 공격은 1회 확정').
        if (actor != null && !actor.IsDead && interrupt == PresentationInterruptReason.None)
        {
            actor.Anim?.PlayIdleAnimation();
        }

        if (!deliveryStarted) ResolveDelivery();
        if (deliveryRoutine != null) yield return deliveryRoutine;

        for (int i = 0; i < pairCount; i++)
        {
            ReturnToIdleIfAlive(contexts[i]?.Target);
        }
    }

    private static PlayableDirector EnsureTimelineDirector(BattleCharactor actor)
    {
        PlayableDirector director = actor.GetComponent<PlayableDirector>();
        if (director == null) director = actor.gameObject.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        // 끝에서 멈추고 stopped를 발생시킨다(Hold/Loop면 stopped가 안 와 완료 대기가 hang).
        director.extrapolationMode = DirectorWrapMode.None;
        return director;
    }

    /// <summary>
    /// 마커 알림 수신 컴포넌트를 확보한다. Director와 같은 GameObject에 있어야 마커 트랙 알림을 받는다.
    /// </summary>
    private static PresentationSignalReceiver EnsureSignalReceiver(BattleCharactor actor)
    {
        PresentationSignalReceiver receiver = actor.GetComponent<PresentationSignalReceiver>();
        if (receiver == null) receiver = actor.gameObject.AddComponent<PresentationSignalReceiver>();
        return receiver;
    }

    private static void BindTimelineToActor(PlayableDirector director, TimelineAsset timeline, BattleCharactor actor)
    {
        director.playableAsset = timeline;

        Animator animator = actor.Anim != null ? actor.Anim.Animator : actor.GetComponentInChildren<Animator>();
        if (animator == null) return;

        // Animation Track을 이 캐릭터의 Animator에 바인딩한다(바인딩은 director 인스턴스에 저장 — 공유 에셋을 뮤테이트하지 않음).
        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track is AnimationTrack)
            {
                director.SetGenericBinding(track, animator);
            }
        }
    }

    /// <summary>전투 배속을 Director 그래프에 반영한다(§10). 그래프는 Play() 이후에만 유효하다. 배속 0 = 프리즈.</summary>
    private static void ApplyBattleSpeedToDirector(PlayableDirector director, float battleSpeed)
    {
        if (director == null) return;
        PlayableGraph graph = director.playableGraph;
        if (!graph.IsValid()) return;

        battleSpeed = Mathf.Max(0f, battleSpeed);
        int rootCount = graph.GetRootPlayableCount();
        for (int i = 0; i < rootCount; i++)
        {
            Playable root = graph.GetRootPlayable(i);
            if (root.IsValid()) root.SetSpeed(battleSpeed);
        }
    }

    /// <summary>
    /// Path A 재생 동안 Signal이 CueId로 발화할 수 있도록, 이 연출의 <b>모든</b> 페이즈/Beat Cue를
    /// 컨텍스트에 등록한다(id 인덱스). 기존 <see cref="SetupPresentationContext"/>는 이름 중복을 미리 제거해
    /// 동명 Cue를 누락하므로 Path A 전용으로 별도 등록한다.
    ///
    /// 발화 시점은 Timeline Signal이 소유하므로, 각 RuntimeCue를 <c>ClipEvent</c>로 등록해
    /// normalizedTime 드라이버의 이중 발화를 차단한다(레일 배타, 지시서 §6). State 게이트도 불필요(hash 0).
    /// </summary>
    private void RegisterTimelineRailCues(BattleCharactor actor, BattleCharactor target, SkillData skill,
        SkillPresentationData presentation, int actionInstanceId,
        IReadOnlyList<BattleCharactor> presentationTargets = null)
    {
        BattleVisualDirector visual = _battle.VisualDirector;
        if (actor == null || presentation == null || visual == null || actionInstanceId == 0) return;

        var bindings = new List<CueBinding>();
        presentation.CollectAllCues(bindings);

        var runtimeCues = new List<RuntimeCue>(bindings.Count);
        for (int i = 0; i < bindings.Count; i++)
        {
            CueBinding binding = bindings[i];
            if (binding == null || string.IsNullOrEmpty(binding.CueId)) continue;   // Path A는 id로 발화

            var cue = new RuntimeCue
            {
                NormalizedCueName = binding.NormalizedCueName,
                CueId = binding.CueId,
                Operation = binding.Operation,
                InstanceKey = binding.NormalizedInstanceKey,
                Anchor = binding.Anchor,
                Socket = binding.Socket,
                Timing = CueTimingSource.ClipEvent,   // 드라이버 이중 발화 차단(§6). Signal 경로는 Timing 무관.
                Time = 0f,
            };
            if (binding.EffectIds != null)
            {
                foreach (int id in binding.EffectIds)
                {
                    GameObject prefab = visual.GetRegisteredEffect(id);
                    if (prefab != null) cue.EffectPrefabs.Add(prefab);
                }
            }
            if (binding.SoundIds != null) cue.SoundIds.AddRange(binding.SoundIds);
            runtimeCues.Add(cue);
        }

        PresentationRuntimeContext context = actor.EnsurePresentationComponents();
        if (context == null) return;

        var effectContext = new SkillEffectContext
        {
            ActionInstanceId = actionInstanceId,
            Caster = actor,
            PrimaryTarget = target,
            Targets = presentationTargets != null
                ? new List<BattleCharactor>(presentationTargets)
                : target != null ? new List<BattleCharactor> { target } : null,
            TargetPosition = target != null ? target.transform.position : actor.transform.position,
            SocketTransform = actor.GetComponent<UnitVisualProfile>()?.AttackEffectSocket ?? actor.transform,
            PlaybackSpeed = _battle.CurrentBattleSpeed,
            HitIndex = 0,
            ReviveTarget = actor.PendingReviveTarget,
        };

        // State 게이트 불필요 — Timeline이 시점을 소유하고 by-id 발화가 게이트를 우회한다 → expectedHash 0.
        context.SetActive(actionInstanceId, effectContext, runtimeCues, 0, $"{presentation.name} / PathA-Timeline");
    }

    private IEnumerator RunSkillSequenceCoreInternal(
        BattleCharactor actor,
        ISkillTarget skillTarget,
        SkillData skill,
        bool playBasicAttackAnimation,
        bool playTargetHitAnimation,
        Func<BattleHitResult> onHitCallback,
        HitDeliveryGate deliveryGate,
        IReadOnlyList<BattleCharactor> presentationTargets = null)
    {
        // 전투유닛 타깃(적/아군)만 BattleCharactor. 인질 등은 null → 위치 기반 연출만 사용하고
        // 피격 리액션/투사체 대상 추적 등 BattleCharactor 전용 처리는 건너뛴다.
        BattleCharactor target = skillTarget as BattleCharactor;
        // 연출 위치 앵커(타깃 유형 무관). 인질은 셀 자식 transform, 전투유닛은 자기 transform.
        Transform targetTransform = skillTarget?.TargetTransform;
        Vector3 targetPosition = skillTarget != null ? skillTarget.TargetPosition : actor.transform.position;

        if (deliveryGate == null)
        {
            deliveryGate = new HitDeliveryGate();
        }

        if (actor == null)
        {
            onHitCallback?.Invoke();
            yield break;
        }

        // 특이 스킬 커스텀 연출 탈출구(등록 없으면 no-op → 기본 시퀀서).
        if (skill != null && SkillPresentationSequenceRegistry.TryGet(skill.skillKey, out ISkillPresentationSequence customSeq))
        {
            yield return _battle.StartCoroutine(customSeq.Run(_battle, actor, target, skill, onHitCallback));
            yield break;
        }

        skill = ResolveSkillAnimationData(skill);
        ApplyPresentationOverride(skill);
        actor.EnsureAnimationController();
        CharactorAnimationController actorAnim = actor.Anim;
        actor.Anim?.SetAnimationSpeed(_battle.CurrentBattleSpeed);

        string targetState = actorAnim != null
            ? actorAnim.GetTargetStateName(playBasicAttackAnimation ? null : skill)
            : string.Empty;

        ResolveSkillMovement(actor, target, skill, out UnitMovementProfile movement, out bool shouldMove, out bool shouldRotate, out Vector3 originPosition, out float originRotationY, targetTransform);

        string targetAnimTrigger = playTargetHitAnimation
            ? (skill?.ResolvedTargetAnimationTrigger ?? "Hit")
            : null;

        SkillPresentationData presentation = skill != null ? _battle.PresentationCatalog?.Get(skill.skillIndex) : null;

        // Path A — Timeline 레일: 페이즈/CrossFade 시퀀스 대신 캐릭터 Variant Timeline을 재생한다(지시서 §8 단일 포크).
        // 우선순위: 커스텀 시퀀스 레지스트리(위) > Timeline 레일(여기) > 기본 경로(아래).
        bool forceAnimatorRail = _battle.PresentationCatalog != null
                                 && _battle.PresentationCatalog.ForceAnimatorRail;
        if (presentation != null && presentation.IsTimelineRail && !forceAnimatorRail)
        {
            if (presentation.PresentationArchetype == PresentationArchetype.Melee)
            {
                yield return RunMeleeTimelineRailRoutine(
                    actor, target, skill, presentation, onHitCallback, deliveryGate,
                    targetTransform, targetPosition, playTargetHitAnimation,
                    actorAnim, movement, shouldMove, shouldRotate, originPosition, originRotationY);
            }
            else
            {
                yield return RunTimelineRailRoutine(actor, target, skill, presentation, onHitCallback, deliveryGate,
                    targetTransform, targetPosition, playTargetHitAnimation);
            }
            yield break;
        }
        if (presentation != null && presentation.IsTimelineRail && forceAnimatorRail)
        {
            Debug.LogWarning(
                $"[SkillPresentation] Kill Switch로 Timeline Rail을 Animator Rail로 우회합니다. skill={presentation.SkillIndex}",
                presentation);
        }

        int presentationActionInstanceId = presentation?.IsPhaseCue == true ? NextActionInstanceId() : 0;
        bool moveEnabled       = presentation?.Move?.Enabled ?? true;
        bool attackPrepEnabled = presentation?.AttackPrepare?.Enabled ?? true;
        bool returnEnabled     = presentation?.Return?.Enabled ?? true;
        bool usePhaseCue       = presentation?.IsPhaseCue == true;
                MovingAttackPlan spinSweepPlan = null;
                bool useMovingAttack = !playBasicAttackAnimation
                            && usePhaseCue
                            && moveEnabled
                            && movement != null
                            && TryBuildMovingAttackPlan(actor, actorAnim, presentation, new[] { target }, out spinSweepPlan,
                                fallbackCenter: target == null ? targetPosition : (Vector3?)null);

        float sequenceBattleElapsed = 0f;
        var runner = new ActionSequenceRunner();
        bool hasApproachMovementAnimation = !useMovingAttack && moveEnabled && shouldMove;
                bool hasReturnMovementAnimation = !useMovingAttack && returnEnabled && (shouldMove || shouldRotate);
                bool hasPhaseCueCombo = !playBasicAttackAnimation && presentation?.IsPhaseCue == true
                    && presentation.Attack != null && presentation.Attack.Enabled
                    && presentation.Attack.Beats != null && presentation.Attack.Beats.Count > 0;
                float? movePrepareNextBlendSeconds = useMovingAttack
                    ? Mathf.Max(0f, presentation?.Move?.BlendInSeconds ?? 0.1f)
                    : hasPhaseCueCombo && !hasApproachMovementAnimation
                        ? ResolveMovePrepareNextBlendInSeconds(actorAnim, skill, presentation)
                        : null;
                float? postBlendAfterLastBeatSeconds = hasPhaseCueCombo && !hasReturnMovementAnimation
                    ? ResolvePostBlendInSeconds(presentation)
                    : null;
        
                EnqueuePhaseCuePrologue(runner, actor, target, skill, presentation, presentationActionInstanceId,
            movePrepareNextBlendSeconds, elapsed => sequenceBattleElapsed += elapsed);

        bool useCombo = !playBasicAttackAnimation && presentation?.IsPhaseCue == true
                    && presentation.Attack != null && presentation.Attack.Enabled
                    && presentation.Attack.Beats != null && presentation.Attack.Beats.Count > 0;
        
                if (useMovingAttack)
                        {
                            EnqueueMovingAttackApproach(runner, spinSweepPlan, actorAnim, movement, presentation?.Move);
        
                            EnqueuePhaseCueAttackPrepare(runner, actor, target, skill, presentation, presentationActionInstanceId,
                                attackPrepEnabled ? ResolveMovingAttackIncomingBlendInSeconds(spinSweepPlan) : null,
                                elapsed => sequenceBattleElapsed += elapsed);
        
                            EnqueueMovingAttackSequence(runner, spinSweepPlan, actor, target, skill, presentation,
                                presentationActionInstanceId, actorAnim,
                                host => ResolveSkillHitWithPresentationDeliveryRoutine(host, actor, target, onHitCallback, targetAnimTrigger, playTargetHitAnimation, skill, deliveryGate, presentation, presentationActionInstanceId, targetPosition, targetTransform),
                                null,
                                ResolveMovingAttackOutgoingBlendInSeconds(presentation, returnEnabled && movement != null),
                                elapsed => sequenceBattleElapsed += elapsed);
                        }
                        
                else
                {
                    if (moveEnabled)
                        EnqueueSkillApproach(runner, actor, target, actorAnim, movement, shouldMove, shouldRotate, presentation?.Move, targetTransform);
        
                    EnqueuePhaseCueAttackPrepare(runner, actor, target, skill, presentation, presentationActionInstanceId,
                        useCombo ? ResolveFirstAttackBeatBlendInSeconds(actorAnim, skill, presentation) : null,
                        elapsed => sequenceBattleElapsed += elapsed);
        
                    if (useCombo)
                    {
                        runner.Enqueue(new ASB.Work.Battle.Sequence.ComboSkillAction(
                            actorAnim,
                            presentation.Attack.Beats,
                            (beat, index) => ResolveAttackBeatState(actorAnim, skill, presentation, beat, index),
                            (beat, stateName) => SetupPresentationContext(actor, target, skill, presentation, presentationActionInstanceId, beat?.Cues, stateName, presentationTargets,
                                targetPositionOverride: target == null ? targetPosition : (Vector3?)null),
                            host => ResolveSkillHitWithPresentationDeliveryRoutine(host, actor, target, onHitCallback, targetAnimTrigger, playTargetHitAnimation, skill, deliveryGate, presentation, presentationActionInstanceId, targetPosition, targetTransform),
                            _battle.CurrentBattleSpeed,
                            BattleManager.AnimEventTimeoutSeconds,
                            postBlendAfterLastBeatSeconds,
                            elapsed => sequenceBattleElapsed += elapsed));
                    }
                    else
                    {
                        if (usePhaseCue)
                            runner.Enqueue(new ASB.Work.Battle.Sequence.RunRoutineAction(host => ActivatePresentationCueRoutine(
                                actor, target, skill, presentation, presentationActionInstanceId, GetPrimaryAttackBeat(presentation)?.Cues, targetState, presentationTargets)));
                        runner.Enqueue(new PlaySkillAnimAction(actorAnim, skill, playBasicAttackAnimation, actor));
                        runner.Enqueue(new WaitHitAction(actorAnim, skill, _battle.CurrentBattleSpeed, elapsed => sequenceBattleElapsed += elapsed, BattleManager.AnimEventTimeoutSeconds));
                        runner.Enqueue(new ASB.Work.Battle.Sequence.RunRoutineAction(host => ResolveSkillHitRoutine(
                            host, actor, target, onHitCallback, targetAnimTrigger, playTargetHitAnimation, skill, deliveryGate, targetPosition, targetTransform)));
                        if (!string.IsNullOrEmpty(targetState))
                            runner.Enqueue(new WaitClipEndAction(actorAnim, targetState, elapsed => sequenceBattleElapsed += elapsed));
                    }
                }
        
                if (returnEnabled)
                {
                    if (useMovingAttack)
                        EnqueueMovingAttackReturn(runner, actorAnim, movement, originPosition, originRotationY, presentation?.Return);
                    else
                        EnqueueSkillReturn(runner, actorAnim, movement, shouldMove, shouldRotate, originPosition, originRotationY,
                            presentation?.Return);
                }
        
                if (!returnEnabled && !useMovingAttack && shouldRotate)
                {
                    EnqueueSkillReturn(runner, actorAnim, movement, false, true, originPosition, originRotationY,
                        presentation?.Return);
                }

                EnqueuePhaseCueEpilogue(runner, actor, target, skill, presentation, presentationActionInstanceId,
            elapsed => sequenceBattleElapsed += elapsed);

        runner.Enqueue(new ReturnToIdleAction(actor));

        yield return _battle.StartCoroutine(runner.RunAll(_battle));

        // 연출 종료: 늦게 도착한 이벤트가 다음 실행 컨텍스트를 오용하지 않도록 정리.
        ClearPresentationContext(actor);

        float remainingTotal = Mathf.Max(0f, skill.TotalDelay - sequenceBattleElapsed);
        yield return _battle.WaitForBattleSeconds(Mathf.Max(remainingTotal, 0.2f));

        if (target != null)
            yield return _battle.StartCoroutine(new WaitTargetReactionAction(target, _battle.CurrentBattleSpeed).ExecuteRoutine(_battle));
    }


            public IEnumerator ResolveSkillHitWithPresentationDeliveryRoutine(
                MonoBehaviour host,
                BattleCharactor actor,
                BattleCharactor target,
                Func<BattleHitResult> onHitCallback,
                string targetAnimTrigger,
                bool playTargetHitAnimation,
                SkillData skill,
                HitDeliveryGate deliveryGate,
                SkillPresentationData presentation,
                int actionInstanceId,
                Vector3? targetPositionOverride = null,
                Transform targetTransformOverride = null)
            {
                yield return ASB.Work.Battle.Sequence.CustomEffectImpactAction.WaitForImpactRoutine(
                    presentation, actionInstanceId, deliveryGate);
                if (deliveryGate == null || !deliveryGate.ShouldPlayImpactPresentation)
                {
                    yield break;
                }

                yield return ResolveSkillHitRoutine(
                    host, actor, target, onHitCallback, targetAnimTrigger, playTargetHitAnimation, skill, deliveryGate, targetPositionOverride, targetTransformOverride);
            }
        
            public IEnumerator ResolveAoEHitWithPresentationDeliveryRoutine(
                MonoBehaviour host,
                BattleCharactor actor,
                SkillPresentationData presentation,
                int actionInstanceId,
                HitDeliveryGate deliveryGate,
                List<DamageContext> contexts,
                List<Func<BattleHitResult>> hitCallbacks,
                int pairCount,
                ProjectileVisualData projectileVisual)
            {
                SignalCustomImpactEffectAtHit(actor, presentation);
                yield return ASB.Work.Battle.Sequence.CustomEffectImpactAction.WaitForImpactRoutine(
                    presentation, actionInstanceId, deliveryGate);
                if (deliveryGate == null || !deliveryGate.ShouldPlayImpactPresentation)
                {
                    yield break;
                }
        
                yield return new AoEApplyDamageAction(
                    contexts, hitCallbacks, pairCount, _battle.CurrentBattleSpeed, _battle.VisualDirector, projectileVisual, deliveryGate)
                    .ExecuteRoutine(host);
            }
        
        
            public static void SignalCustomImpactEffectAtHit(BattleCharactor actor, SkillPresentationData presentation)
            {
                if (actor == null
                    || presentation == null
                    || presentation.ImpactDeliveryMode != SkillImpactDeliveryMode.CustomEffectImpact
                    || string.IsNullOrWhiteSpace(presentation.CustomImpactSignalInstanceKey))
                {
                    return;
                }
        
                PresentationRuntimeContext runtime = actor.GetComponent<PresentationRuntimeContext>();
                string instanceKey = presentation.CustomImpactSignalInstanceKey.Trim();
                if (runtime != null
                    && runtime.TryGetHandle(instanceKey, out ISkillEffectHandle handle)
                    && handle != null
                    && handle.Signal(null))
                {
                    runtime.RemoveHandle(instanceKey);
                }
            }
        
    /// <summary>
    /// 캐스트의 투사체 전달 방식이 ChainAdditionalTargets면 체인 상태를 생성한다. 아니면 null(=일반 Single 동작).
    /// </summary>
    public ProjectileChainState ResolveChainStateForCast(SkillExecutionResult result)
    {
        if (result?.DamageContexts == null)
        {
            return null;
        }

        DamageContext first = null;
        for (int i = 0; i < result.DamageContexts.Count; i++)
        {
            if (result.DamageContexts[i] != null)
            {
                first = result.DamageContexts[i];
                break;
            }
        }
        if (first == null)
        {
            return null;
        }

        SkillPresentationData presentation = _battle.PresentationCatalog?.Get(first.SkillIndex);
        ProjectileVisualData projectileVisual = presentation?.ProjectileVisual;
        if (projectileVisual != null
            && projectileVisual.DeliveryMode == ProjectileDeliveryMode.ChainAdditionalTargets
            && (projectileVisual.Prefab != null || presentation.ChainLightningEffectPrefab != null))
        {
            return new ProjectileChainState();
        }
        return null;
    }

    public IEnumerator ResolveSkillHitRoutine(MonoBehaviour host, BattleCharactor actor, BattleCharactor target,
        Func<BattleHitResult> onHitCallback, string targetAnimTrigger, bool playTargetHitAnimation, SkillData skill,
        HitDeliveryGate deliveryGate, Vector3? targetPositionOverride = null, Transform targetTransformOverride = null)
    {
        SkillPresentationData presentation = skill != null ? _battle.PresentationCatalog?.Get(skill.skillIndex) : null;
        // 체인 번개의 1차 지점: 전투유닛은 자기 transform, 인질 등은 override transform.
        // (인질은 target=null이라 이전엔 스킵돼 이펙트가 아예 안 떴다.)
        Transform chainPrimaryTransform = target != null ? target.transform : targetTransformOverride;
        bool waitForChainLightningImpact = false;
        if (chainPrimaryTransform != null && _projectileChainRole == DamageRole.Primary)
        {
            waitForChainLightningImpact = PlayChainLightningEffect(
                presentation, actor, chainPrimaryTransform, ChainTargets);
        }

        if (waitForChainLightningImpact)
        {
            yield return WaitForPresentationImpactRoutine(target, presentation, deliveryGate);
            if (!deliveryGate.ShouldPlayImpactPresentation)
            {
                yield break;
            }
        }

        if (TryGetProjectileVisual(actor, skill, presentation, playTargetHitAnimation, out ProjectileVisualData projectileVisual))
        {
            bool isChain = projectileVisual.DeliveryMode == ProjectileDeliveryMode.ChainAdditionalTargets
                           && _projectileChainState != null;
            Vector3? originOverride = null;
            if (isChain && _projectileChainRole == DamageRole.Additional && _projectileChainState.HasPreviousImpact)
            {
                // 추가 타깃 투사체는 1차 도착 좌표에서 출발(타깃 사망에도 안전).
                originOverride = _projectileChainState.LastImpactPoint;
            }

            // 준비(차징) 단계에서 만든 held 인스턴스를 재사용(낙하형: 손에 소환한 이펙트를 그대로 발사).
            // 지금까지 AoE 경로(AoEApplyDamageAction)에만 있던 처리를 단일 대상에도 동일 적용한다.
            GameObject preparedCharge = null;
            if (projectileVisual.UsePreparedCharge && actor != null && !string.IsNullOrEmpty(projectileVisual.ChargeInstanceKey))
            {
                PresentationRuntimeContext chargeCtx = actor.GetComponent<PresentationRuntimeContext>();
                if (chargeCtx != null && chargeCtx.TryGetHandle(projectileVisual.ChargeInstanceKey, out ISkillEffectHandle chargeHandle))
                {
                    preparedCharge = (chargeHandle as Component)?.gameObject;
                    chargeCtx.RemoveHandle(projectileVisual.ChargeInstanceKey);
                }
            }

            // 인질 등 BattleCharactor가 아닌 타깃은 target이 null이라, 목적지를 명시(destinationOverride)해야
            // 투사체가 취소되지 않고 그 위치까지 날아간다. 전투유닛(target!=null)은 기존대로 타깃 추적.
            Vector3? projectileDestination = target != null ? (Vector3?)null : targetPositionOverride;
            var projectile = new ProjectileImpactAction(actor, target, projectileVisual, _battle.CurrentBattleSpeed, deliveryGate, skill.skillIndex, originOverride, destinationOverride: projectileDestination, existingInstance: preparedCharge);
            yield return projectile.ExecuteRoutine(host);

            // 주 타깃 단계: 실제 도착 좌표를 저장 → 추가 타깃 투사체의 원점으로 사용.
            if (isChain && _projectileChainRole == DamageRole.Primary)
            {
                switch (projectile.DeliveryResult)
                {
                    case ProjectileDeliveryResult.Arrived:
                        _projectileChainState.LastImpactPoint = projectile.ImpactPoint;
                        _projectileChainState.HasPreviousImpact = true;
                        break;
                    case ProjectileDeliveryResult.Fallback:
                        // 폴백: 주 타깃의 그 시점 위치를 도착점으로 사용하고 체인 계속.
                        _projectileChainState.LastImpactPoint = target != null ? target.transform.position : projectile.ImpactPoint;
                        _projectileChainState.HasPreviousImpact = true;
                        break;
                    case ProjectileDeliveryResult.Cancelled:
                        // 1차 취소: 체인 전체 종료.
                        _projectileChainState.IsCancelled = true;
                        break;
                }
            }
            if (!deliveryGate.ShouldPlayImpactPresentation)
            {
                yield break;
            }
            }
        else
        {
            bool isArcher = actor != null && actor.GetComponent<UnitVisualProfile>()?.HoldArrow != null;
            bool shouldSpawnArrowImpact = playTargetHitAnimation && skill != null && skill.classSkillEffect == 0;
            if (isArcher && target != null && shouldSpawnArrowImpact)
            {
                yield return new ArrowImpactAction(actor, target, _battle.CurrentBattleSpeed, targetAnimTrigger).ExecuteRoutine(host);
                targetAnimTrigger = null;
            }
        }
        yield return new ResolveHitAction(actor, target, onHitCallback, targetAnimTrigger, _battle.CurrentBattleSpeed, _battle.VisualDirector).ExecuteRoutine(host);
    }

    public bool TryGetProjectileVisual(BattleCharactor actor, SkillData skill, SkillPresentationData presentation,
        bool playTargetHitAnimation, out ProjectileVisualData projectileVisual)
    {
        projectileVisual = presentation?.GetProjectileVisual();
        // ProjectileVisual이 명시적으로 설정(Prefab 존재)된 스킬은 힐/부활·근거리·피격애니 여부와 무관하게
        // 투사체 연출을 허용한다(예: 힐 구체를 아군에게 투척). Prefab 미설정이면 투사체 연출 없음.
        // (기존엔 원거리 공격 스킬만 허용했으나, 연출은 프레젠테이션이 명시하면 따르도록 완화)
        return skill != null && projectileVisual != null && projectileVisual.Prefab != null;
    }

    public ProjectileVisualData GetProjectileVisual(BattleCharactor actor, SkillData skill, SkillPresentationData presentation)
    {
        return TryGetProjectileVisual(actor, skill, presentation, true, out ProjectileVisualData projectileVisual)
            ? projectileVisual : null;
    }
    public void ResolveSkillMovement(
        BattleCharactor actor,
        BattleCharactor target,
        SkillData skill,
        out UnitMovementProfile movement,
        out bool shouldMove,
        out bool shouldRotate,
        out Vector3 originPosition,
        out float originRotationY,
        Transform targetTransformOverride = null)
    {
        originPosition = actor != null ? actor.transform.position : Vector3.zero;
        originRotationY = actor != null ? actor.transform.eulerAngles.y : 0f;
        movement = actor != null ? actor.GetComponent<UnitMovementProfile>() : null;

        // 인질 등 BattleCharactor가 아닌 타깃은 target이 null이라 위치(override)로만 접근/회전을 판단한다.
        bool hasTarget = target != null || targetTransformOverride != null;

        // Friendly projectiles must face their target before launch.
        SkillPresentationData presentation = skill != null ? _battle.PresentationCatalog?.Get(skill.skillIndex) : null;
        bool isFriendlyProjectile = actor != null
            && target != null
            && actor.IsPlayer == target.IsPlayer
            && (skill?.classSkillEffect == BattleManager.ClassSkillEffect_Heal || skill?.classSkillEffect == BattleManager.ClassSkillEffect_Revive)
            && presentation?.GetProjectileVisual() != null;

        shouldMove = movement != null
            && !movement.RotateOnly
            && hasTarget
            && !isFriendlyProjectile
            && BattleManager.IsMeleeSkillRange(actor, skill);
        shouldRotate = hasTarget && ((movement != null && movement.RotateOnly) || isFriendlyProjectile);
    }

    public void EnqueueSkillApproach(
        ActionSequenceRunner runner,
        BattleCharactor actor,
        BattleCharactor target,
        CharactorAnimationController actorAnim,
        UnitMovementProfile movement,
        bool shouldMove,
        bool shouldRotate,
        MovePhase movePhase,
        Transform targetTransformOverride = null)
    {
        if (actor == null)
        {
            return;
        }

        // 전투유닛은 자기 transform, 인질 등은 override transform으로 접근/회전 목표를 삼는다.
        Transform targetTransform = target != null ? target.transform : targetTransformOverride;
        if (targetTransform == null)
        {
            return;
        }

        string animationStateName = movePhase?.AnimationStateName;
        float blendInSeconds = movePhase != null ? Mathf.Max(0f, movePhase.BlendInSeconds) : 0.1f;

        if (shouldMove && movement != null)
        {
            runner.Enqueue(new MoveToTargetAction(
                actorAnim, targetTransform, movement.ApproachDistance, movement.MoveDuration / _battle.CurrentBattleSpeed,
                animationStateName, blendInSeconds));
        }
        else if (shouldRotate)
        {
            float rotateDuration = movement != null ? movement.RotateDuration : 0.15f;
            runner.Enqueue(new RotateToTargetAction(actor.transform, targetTransform, rotateDuration / _battle.CurrentBattleSpeed));
        }
    }

    public void EnqueueSkillReturn(
        ActionSequenceRunner runner,
        CharactorAnimationController actorAnim,
        UnitMovementProfile movement,
        bool shouldMove,
        bool shouldRotate,
        Vector3 originPosition,
        float originRotationY,
        ReturnPhase returnPhase)
    {
        if (runner == null || actorAnim == null || (!shouldMove && !shouldRotate))
        {
            return;
        }

        Quaternion originRotation = Quaternion.Euler(0f, originRotationY, 0f);
        string animationStateName = returnPhase?.AnimationStateName;
        float blendInSeconds = returnPhase != null ? Mathf.Max(0f, returnPhase.BlendInSeconds) : 0.1f;
        float duration = shouldMove
            ? (movement != null ? movement.ReturnDuration : 0.15f)
            : (movement != null ? movement.RotateReturnDuration : 0.15f);

        runner.Enqueue(new MoveToOriginAction(
            actorAnim, originPosition, originRotation, duration / _battle.CurrentBattleSpeed,
            animationStateName, blendInSeconds));
    }

    /// <summary>
    /// 광역 스킬: 시전 애니 1회, HitDelay 시점에 전 타겟 동시 피격·데미지, 이후 전원 Idle 복귀.
    /// </summary>
    public IEnumerator RunAoESkillSequence(List<DamageContext> contexts, List<Func<BattleHitResult>> hitCallbacks,
        HitDeliveryGate deliveryGate)
    {
        yield return TrackSequence(RunAoESkillSequenceInternal(contexts, hitCallbacks, deliveryGate));
    }

    private IEnumerator RunAoESkillSequenceInternal(List<DamageContext> contexts, List<Func<BattleHitResult>> hitCallbacks,
        HitDeliveryGate deliveryGate)
    {
        if (deliveryGate == null)
        {
            deliveryGate = new HitDeliveryGate();
        }

        if (contexts == null || contexts.Count == 0 || hitCallbacks == null || hitCallbacks.Count == 0)
        {
            yield break;
        }

        int pairCount = Mathf.Min(contexts.Count, hitCallbacks.Count);
        DamageContext leadContext = contexts[0];
        BattleCharactor actor = leadContext?.Caster;
        if (actor == null)
        {
            for (int i = 0; i < pairCount; i++)
            {
                hitCallbacks[i]?.Invoke();
            }

            yield break;
        }

        BattleCharactor primaryTarget = leadContext.Target;
        SkillData skill = ResolveSkillAnimationData(TryGetSkillDataForDamageContext(leadContext));
        ApplyPresentationOverride(skill);
        actor.EnsureAnimationController();
        CharactorAnimationController actorAnim = actor.Anim;
        actor.Anim?.SetAnimationSpeed(_battle.CurrentBattleSpeed);

        string targetState = actorAnim != null
            ? actorAnim.GetTargetStateName(skill)
            : string.Empty;

        ResolveSkillMovement(actor, primaryTarget, skill, out UnitMovementProfile movement, out bool shouldMove, out bool shouldRotate, out Vector3 originPosition, out float originRotationY);

        SkillPresentationData presentation = skill != null ? _battle.PresentationCatalog?.Get(skill.skillIndex) : null;
        ProjectileVisualData projectileVisual = GetProjectileVisual(actor, skill, presentation);

        bool forceAnimatorRail = _battle.PresentationCatalog != null
                                 && _battle.PresentationCatalog.ForceAnimatorRail;
        bool unsupportedMovingTimeline = presentation?.PresentationArchetype == PresentationArchetype.MovingAttack;
        if (presentation != null && presentation.IsTimelineRail && !forceAnimatorRail && !unsupportedMovingTimeline)
        {
            yield return RunAoETimelineRailRoutine(
                actor, primaryTarget, skill, presentation, deliveryGate,
                contexts, hitCallbacks, pairCount, projectileVisual);
            yield break;
        }
        if (presentation != null && presentation.IsTimelineRail && forceAnimatorRail)
        {
            Debug.LogWarning(
                $"[SkillPresentation/AoE] Kill Switch로 Timeline Rail을 Animator Rail로 우회합니다. skill={presentation.SkillIndex}",
                presentation);
        }
        else if (presentation != null && presentation.IsTimelineRail && unsupportedMovingTimeline)
        {
            Debug.LogWarning(
                $"[SkillPresentation/AoE] MovingAttack Timeline 핸드오프는 아직 지원하지 않아 Animator Rail을 사용합니다. skill={presentation.SkillIndex}",
                presentation);
        }

        int presentationActionInstanceId = presentation?.IsPhaseCue == true ? NextActionInstanceId() : 0;
        bool moveEnabled       = presentation?.Move?.Enabled ?? true;
        bool attackPrepEnabled = presentation?.AttackPrepare?.Enabled ?? true;
        bool returnEnabled     = presentation?.Return?.Enabled ?? true;
        bool usePhaseCue       = presentation?.IsPhaseCue == true;
                var spinSweepCandidates = new List<BattleCharactor>();
                for (int i = 0; i < pairCount; i++)
                {
                    if (contexts[i]?.Target != null)
                    {
                        spinSweepCandidates.Add(contexts[i].Target);
                    }
                }
                MovingAttackPlan spinSweepPlan = null;
                bool useMovingAttack = usePhaseCue
                            && moveEnabled
                            && movement != null
                            && TryBuildMovingAttackPlan(actor, actorAnim, presentation, spinSweepCandidates,
                                out spinSweepPlan);

        float sequenceBattleElapsed = 0f;
        var runner = new ActionSequenceRunner();
        bool hasApproachMovementAnimation = !useMovingAttack && moveEnabled && shouldMove;
                bool hasReturnMovementAnimation = !useMovingAttack && returnEnabled && (shouldMove || shouldRotate);
                bool hasPhaseCueCombo = usePhaseCue && presentation.Attack != null && presentation.Attack.Enabled
                    && presentation.Attack.Beats != null && presentation.Attack.Beats.Count > 0;
                float? movePrepareNextBlendSeconds = useMovingAttack
                    ? Mathf.Max(0f, presentation?.Move?.BlendInSeconds ?? 0.1f)
                    : hasPhaseCueCombo && !hasApproachMovementAnimation
                        ? ResolveMovePrepareNextBlendInSeconds(actorAnim, skill, presentation)
                        : null;
                float? postBlendAfterLastBeatSeconds = hasPhaseCueCombo && !hasReturnMovementAnimation
                    ? ResolvePostBlendInSeconds(presentation)
                    : null;
        
                EnqueuePhaseCuePrologue(runner, actor, primaryTarget, skill, presentation, presentationActionInstanceId,
            movePrepareNextBlendSeconds, elapsed => sequenceBattleElapsed += elapsed);

        bool useCombo = usePhaseCue && presentation.Attack != null && presentation.Attack.Enabled
                    && presentation.Attack.Beats != null && presentation.Attack.Beats.Count > 0;
        
                if (useMovingAttack)
                {
                    Func<MonoBehaviour, IEnumerator> primaryHitRoutine = null;
                    List<ASB.Work.Battle.Sequence.MovingAttackHitTarget> sequentialHits = null;
        
                    if (spinSweepPlan.Presentation.HitMode == MovingAttackHitMode.SequentialAoE)
                    {
                        sequentialHits = new List<ASB.Work.Battle.Sequence.MovingAttackHitTarget>();
                        for (int i = 0; i < pairCount; i++)
                        {
                            DamageContext context = contexts[i];
                            Func<BattleHitResult> callback = hitCallbacks[i];
                            if (context?.Target == null) continue;
        
                            sequentialHits.Add(new ASB.Work.Battle.Sequence.MovingAttackHitTarget
                            {
                                Target = context.Target,
                                HitRoutine = host => new AoEApplyDamageAction(
                                    new List<DamageContext> { context },
                                    new List<Func<BattleHitResult>> { callback },
                                    1, _battle.CurrentBattleSpeed, _battle.VisualDirector, projectileVisual, deliveryGate).ExecuteRoutine(host)
                            });
                        }
                    }
                    else
                            {
                                if (spinSweepPlan.Presentation.HitMode == MovingAttackHitMode.SingleTarget)
                                {
                                    primaryHitRoutine = host => new AoEApplyDamageAction(
                                        new List<DamageContext> { contexts[0] },
                                        new List<Func<BattleHitResult>> { hitCallbacks[0] },
                                        1, _battle.CurrentBattleSpeed, _battle.VisualDirector, projectileVisual, deliveryGate).ExecuteRoutine(host);
                                }
                                else
                                {
                                    primaryHitRoutine = host => new AoEApplyDamageAction(
                                        contexts, hitCallbacks, pairCount, _battle.CurrentBattleSpeed, _battle.VisualDirector, projectileVisual, deliveryGate).ExecuteRoutine(host);
                                }
                            }
        
                            EnqueueMovingAttackApproach(runner, spinSweepPlan, actorAnim, movement, presentation?.Move);
        
                                    EnqueuePhaseCueAttackPrepare(runner, actor, primaryTarget, skill, presentation, presentationActionInstanceId,
                                        attackPrepEnabled ? ResolveMovingAttackIncomingBlendInSeconds(spinSweepPlan) : null,
                                        elapsed => sequenceBattleElapsed += elapsed);
        
                                    EnqueueMovingAttackSequence(runner, spinSweepPlan, actor, primaryTarget, skill, presentation,
                                        presentationActionInstanceId, actorAnim, primaryHitRoutine, sequentialHits,
                                        ResolveMovingAttackOutgoingBlendInSeconds(presentation, returnEnabled && movement != null),
                                        elapsed => sequenceBattleElapsed += elapsed);
                }
                else
                {
                    if (moveEnabled)
                        EnqueueSkillApproach(runner, actor, primaryTarget, actorAnim, movement, shouldMove, shouldRotate, presentation?.Move);
        
                    EnqueuePhaseCueAttackPrepare(runner, actor, primaryTarget, skill, presentation, presentationActionInstanceId,
                        useCombo ? ResolveFirstAttackBeatBlendInSeconds(actorAnim, skill, presentation) : null,
                        elapsed => sequenceBattleElapsed += elapsed);
        
                    // AoE 타깃 리스트(EachTarget 앵커가 대상 수만큼 스폰하는 데 사용).
                    var aoePresentationTargets = new List<BattleCharactor>();
                    if (contexts != null)
                    {
                        foreach (DamageContext c in contexts)
                        {
                            if (c?.Target != null && !aoePresentationTargets.Contains(c.Target))
                            {
                                aoePresentationTargets.Add(c.Target);
                            }
                        }
                    }

                    if (useCombo)
                    {
                        runner.Enqueue(new ASB.Work.Battle.Sequence.ComboSkillAction(
                            actorAnim,
                            presentation.Attack.Beats,
                            (beat, index) => ResolveAttackBeatState(actorAnim, skill, presentation, beat, index),
                            (beat, stateName) => SetupPresentationContext(actor, primaryTarget, skill, presentation, presentationActionInstanceId, beat?.Cues, stateName, aoePresentationTargets),
                            host => ResolveAoEHitWithPresentationDeliveryRoutine(host, actor, presentation, presentationActionInstanceId, deliveryGate, contexts, hitCallbacks, pairCount, projectileVisual),
                            _battle.CurrentBattleSpeed,
                            BattleManager.AnimEventTimeoutSeconds,
                            postBlendAfterLastBeatSeconds,
                            elapsed => sequenceBattleElapsed += elapsed));
                    }
                    else
                    {
                        if (usePhaseCue)
                            runner.Enqueue(new ASB.Work.Battle.Sequence.RunRoutineAction(host => ActivatePresentationCueRoutine(
                                actor, primaryTarget, skill, presentation, presentationActionInstanceId, GetPrimaryAttackBeat(presentation)?.Cues, targetState, aoePresentationTargets)));
                        runner.Enqueue(new PlaySkillAnimAction(actorAnim, skill, false));
                        runner.Enqueue(new WaitHitAction(actorAnim, skill, _battle.CurrentBattleSpeed, elapsed => sequenceBattleElapsed += elapsed, BattleManager.AnimEventTimeoutSeconds));
                        runner.Enqueue(new ASB.Work.Battle.Sequence.RunRoutineAction(host => PlayChainLightningEffectRoutine(
                            presentation, actor, primaryTarget, contexts, pairCount)));
                        runner.Enqueue(new ASB.Work.Battle.Sequence.CustomEffectImpactAction(presentation, presentationActionInstanceId, deliveryGate, new AoEApplyDamageAction(contexts, hitCallbacks, pairCount, _battle.CurrentBattleSpeed, _battle.VisualDirector, projectileVisual, deliveryGate)));
        
                        if (!string.IsNullOrEmpty(targetState))
                            runner.Enqueue(new WaitClipEndAction(actorAnim, targetState, elapsed => sequenceBattleElapsed += elapsed));
                    }
                }
        
                if (returnEnabled)
                {
                    if (useMovingAttack)
                        EnqueueMovingAttackReturn(runner, actorAnim, movement, originPosition, originRotationY, presentation?.Return);
                    else
                        EnqueueSkillReturn(runner, actorAnim, movement, shouldMove, shouldRotate, originPosition, originRotationY,
                            presentation?.Return);
                }
        
                if (!returnEnabled && !useMovingAttack && shouldRotate)
                {
                    EnqueueSkillReturn(runner, actorAnim, movement, false, true, originPosition, originRotationY,
                        presentation?.Return);
                }

                EnqueuePhaseCueEpilogue(runner, actor, primaryTarget, skill, presentation, presentationActionInstanceId,
            elapsed => sequenceBattleElapsed += elapsed);

        runner.Enqueue(new ReturnToIdleAction(actor));

        yield return _battle.StartCoroutine(runner.RunAll(_battle));

        ClearPresentationContext(actor);

        float remainingTotal = Mathf.Max(0f, skill.TotalDelay - sequenceBattleElapsed);
        yield return _battle.WaitForBattleSeconds(Mathf.Max(remainingTotal, 0.2f));

        for (int i = 0; i < pairCount; i++)
        {
            ReturnToIdleIfAlive(contexts[i]?.Target);
        }
    }

    public static void ReturnToIdleIfAlive(BattleCharactor unit)
    {
        if (unit == null || unit.IsDead)
        {
            return;
        }

        unit.EnsureAnimationController();
        unit.Anim?.PlayIdleAnimation();
    }

}
