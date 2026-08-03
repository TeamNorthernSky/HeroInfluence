using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ASB.Work.Battle.Core;
using ASB.Work.Battle.Sequence;

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

    // 체인 라이트닝은 일반 투사체처럼 매 타격마다 생성하지 않고, 프리팹별 런타임 인스턴스를 재사용한다.
    private readonly Dictionary<GameObject, JC.VFX.ChainLightningVfx> _chainLightningEffects = new();
    private List<Transform> _chainLightningTargets;
    private int _chainLightningActionInstanceId;
    private JC.VFX.ChainLightningVfx _chainLightningImpactEffect;

    public SkillPresentationDirector(BattleManager battle)
    {
        _battle = battle;
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
            GameObject instance = Object.Instantiate(effectPrefab);
            effect = instance.GetComponent<JC.VFX.ChainLightningVfx>();
            if (effect == null)
            {
                Debug.LogWarning($"[SkillPresentationDirector] {effectPrefab.name}에 ChainLightningVfx가 없습니다.", effectPrefab);
                Object.Destroy(instance);
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
}
