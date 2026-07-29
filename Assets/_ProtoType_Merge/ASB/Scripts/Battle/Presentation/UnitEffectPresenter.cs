using System.Collections.Generic;
using UnityEngine;

/// <summary>Cue의 Anchor/Socket으로 이펙트 생성 위치를 해석한다.</summary>
public static class PresentationAnchorUtil
{
    public static Transform Resolve(SkillEffectContext ctx, SpawnAnchor anchor, UnitSocket socket,
        out Vector3 pos, out Quaternion rot)
    {
        Transform transform = null;
        if (ctx != null)
        {
            switch (anchor)
            {
                case SpawnAnchor.Caster:
                    transform = ctx.Caster != null ? ctx.Caster.transform : null;
                    break;
                case SpawnAnchor.CasterSocket:
                    UnitVisualProfile profile = ctx.Caster != null ? ctx.Caster.GetComponent<UnitVisualProfile>() : null;
                    if (profile != null)
                    {
                        transform = profile.GetSocket(socket);
                    }
                    else if (ctx.Caster != null)
                    {
                        // 전투 프리팹은 공통 모델의 UnitSocketHolder만 중첩할 수 있다.
                        UnitSocketHolder socketHolder = ctx.Caster.GetComponentInChildren<UnitSocketHolder>();
                        transform = socketHolder != null ? socketHolder.GetNamedSocket(socket) : null;
                        if (transform == null)
                        {
                            Debug.LogWarning($"[PresentationAnchorUtil] Caster '{ctx.Caster.name}'에서 Socket '{socket}'을 찾지 못해 루트로 폴백합니다.", ctx.Caster);
                            transform = ctx.Caster.transform;
                        }
                    }
                    break;
                case SpawnAnchor.Target:
                    transform = ctx.PrimaryTarget != null && !ctx.PrimaryTarget.IsDead ? ctx.PrimaryTarget.transform : null;
                    break;
                case SpawnAnchor.ReviveTarget:
                    // 부활 대상(아군). 부활 직후라 살아있으므로 IsDead 체크 없이 transform 사용.
                    transform = ctx.ReviveTarget != null ? ctx.ReviveTarget.transform : null;
                    break;
            }
        }

        if (transform != null)
        {
            pos = transform.position;
            rot = transform.rotation;
            return transform;
        }

        pos = ctx != null ? ctx.TargetPosition : Vector3.zero;
        rot = Quaternion.identity;
        return null;
    }
}

/// <summary>유닛 로컬 Cue 이펙트 라우터. 재료의 구체 종류는 알지 않고 계약만 호출한다.</summary>
[DisallowMultipleComponent]
public class UnitEffectPresenter : MonoBehaviour
{
    private PresentationRuntimeContext _ctx;
    private PresentationRuntimeContext Ctx => _ctx != null ? _ctx : (_ctx = GetComponent<PresentationRuntimeContext>());

    public void PresentationCue(string cueName)
    {
        PresentationRuntimeContext runtime = Ctx;
        if (runtime == null) return;

        string normalizedCue = string.IsNullOrWhiteSpace(cueName) ? string.Empty : cueName.Trim().ToLowerInvariant();
        if (!runtime.TryGetCue(normalizedCue, out RuntimeCue cue)) return;

        SkillEffectContext baseContext = runtime.Current;

        // 부활 대상이 없으면(공격만 수행) ReviveTarget 앵커 Cue는 스폰하지 않는다(엉뚱한 위치 스폰 방지).
        if (cue.Anchor == SpawnAnchor.ReviveTarget && (baseContext == null || baseContext.ReviveTarget == null))
        {
            return;
        }

        Transform anchor = PresentationAnchorUtil.Resolve(baseContext, cue.Anchor, cue.Socket, out Vector3 position, out Quaternion rotation);
        SkillEffectContext effectContext = baseContext != null ? baseContext.CreateSnapshot(anchor, position) : null;

        switch (cue.Operation)
        {
            case CueOperation.Signal:
                if (runtime.TryGetHandle(cue.InstanceKey, out ISkillEffectHandle signalHandle)
                    && signalHandle.Signal(effectContext))
                {
                    runtime.RemoveHandle(cue.InstanceKey);
                }
                return;
            case CueOperation.Stop:
                runtime.StopAndRemoveHandle(cue.InstanceKey);
                return;
        }

        if (cue.EffectPrefabs == null || cue.EffectPrefabs.Count == 0) return;

        var spawnedHandles = new List<ISkillEffectHandle>();

        if (cue.Anchor == SpawnAnchor.EachTarget)
        {
            // 대상 수만큼: 각 타깃 위치에 하나씩 스폰(열의 적이 N명이면 N개).
            IReadOnlyList<BattleCharactor> targets = baseContext?.Targets;
            if (targets != null)
            {
                for (int t = 0; t < targets.Count; t++)
                {
                    BattleCharactor unit = targets[t];
                    if (unit == null || unit.IsDead) continue;
                    Transform tr = unit.transform;
                    SpawnCueInstances(cue, baseContext, tr, tr.position, tr.rotation, spawnedHandles);
                }
            }
        }
        else
        {
            SpawnCueInstances(cue, baseContext, anchor, position, rotation, spawnedHandles);
        }

        // held(InstanceKey): 1개면 그대로, 여러 개면 Composite로 묶어 등록(후속 Signal/Stop이 전부에 전달됨).
        if (!string.IsNullOrEmpty(cue.InstanceKey) && spawnedHandles.Count > 0)
        {
            ISkillEffectHandle held = spawnedHandles.Count == 1
                ? spawnedHandles[0]
                : new CompositeSkillEffectHandle(spawnedHandles);
            runtime.RegisterHandle(cue.InstanceKey, held);
        }
    }

    private void SpawnCueInstances(RuntimeCue cue, SkillEffectContext baseContext,
        Transform anchor, Vector3 position, Quaternion rotation, List<ISkillEffectHandle> handlesOut)
    {
        SkillEffectContext effectContext = baseContext != null ? baseContext.CreateSnapshot(anchor, position) : null;
        for (int i = 0; i < cue.EffectPrefabs.Count; i++)
        {
            GameObject prefab = cue.EffectPrefabs[i];
            if (prefab == null) continue;

            GameObject instance = Instantiate(prefab, position, rotation);
            instance.GetComponent<ISkillEffectBehaviour>()?.Play(effectContext);

            if (!string.IsNullOrEmpty(cue.InstanceKey))
            {
                if (instance.TryGetComponent(out ISkillEffectHandle handle))
                {
                    handlesOut.Add(handle);
                }
                else
                {
                    Debug.LogWarning($"[UnitEffectPresenter] Held InstanceKey '{cue.InstanceKey}' prefab '{prefab.name}' does not implement ISkillEffectHandle.", instance);
                }
            }
        }
    }
}