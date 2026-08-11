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

    private PresentationCueDriver _driver;
    private PresentationCueDriver Driver => _driver != null ? _driver : (_driver = GetComponent<PresentationCueDriver>());

    /// <param name="bypassStateGate">
    /// 시퀀서가 직접 부르는 Cue(예: 부활, 애니 없는 MovePrepare)는 아직 기대 state에 진입하지 않은 시점에
    /// 호출되므로 State 게이트를 우회한다. 게이트의 목적은 "이전 상태의 늦은 Animation Event"를 막는 것이지
    /// 의도된 직접 호출을 막는 것이 아니다.
    /// </param>
    public void PresentationCue(string cueName, bool bypassStateGate = false)
    {
        PresentationRuntimeContext runtime = Ctx;
        if (runtime == null) return;

        string normalizedCue = string.IsNullOrWhiteSpace(cueName) ? string.Empty : cueName.Trim().ToLowerInvariant();
        if (!runtime.TryGetCue(normalizedCue, out RuntimeCue cue)) return;

        // 등록되어 있다는 이유만으로 발화하지 않는다 — 기대 Animator state에 실제로 진입했을 때만.
        if (!bypassStateGate)
        {
            PresentationCueDriver driver = Driver;
            if (driver != null && !driver.IsCueAllowed(normalizedCue)) return;
        }

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
                    SpawnCueInstances(runtime, cue, baseContext, tr, tr.position, tr.rotation, spawnedHandles);
                }
            }
        }
        else
        {
            SpawnCueInstances(runtime, cue, baseContext, anchor, position, rotation, spawnedHandles);
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

    private void SpawnCueInstances(PresentationRuntimeContext runtime, RuntimeCue cue, SkillEffectContext baseContext,
        Transform anchor, Vector3 position, Quaternion rotation, List<ISkillEffectHandle> handlesOut)
    {
        SkillEffectContext effectContext = baseContext != null ? baseContext.CreateSnapshot(anchor, position) : null;
        for (int i = 0; i < cue.EffectPrefabs.Count; i++)
        {
            GameObject prefab = cue.EffectPrefabs[i];
            if (prefab == null) continue;

            bool held = !string.IsNullOrEmpty(cue.InstanceKey);

            // Held cues remain attached to their resolved anchor until a later sequence consumes them.
            //
            // 부모를 지정한 Instantiate 오버로드를 쓴다. 예전에는 부모 없이 스폰한 뒤
            // SetParent(anchor, false)로 붙였는데, 부모가 없을 때는 localPosition == 월드 좌표라
            // worldPositionStays:false로 붙이면 그 값이 로컬로 유지되어 월드 위치가
            // anchor.TransformPoint(position)이 됐다 — 앵커 좌표가 한 번 더 더해져 2배 지점에 스폰.
            bool attachToAnchor = held && anchor != null;
            GameObject instance = attachToAnchor
                ? Instantiate(prefab, position, rotation, anchor)
                : Instantiate(prefab, position, rotation);
            if (!held)
            {
                PresentationCueEffectLifetimeNotifier notifier =
                    instance.GetComponent<PresentationCueEffectLifetimeNotifier>();
                if (notifier == null)
                {
                    notifier = instance.AddComponent<PresentationCueEffectLifetimeNotifier>();
                }
                notifier.Bind(runtime);
            }


            instance.GetComponent<ISkillEffectBehaviour>()?.Play(effectContext);

            if (!held)
            {
                // InstanceKey가 없는 Cue는 후속 Signal/Stop이 오지 않는다(핸들로 등록되지 않으므로
                // StopAllHandles 대상도 아니다). 자체 정리를 하지 않는 재료는 시전마다 하나씩 누적되므로
                // 스폰한 쪽에서 수명 상한을 보장한다.
                EffectFallbackRelease.Ensure(instance);
                continue;
            }

            if (instance.TryGetComponent(out ISkillEffectHandle handle))
            {
                handlesOut.Add(handle);
                continue;
            }

            // 핸들 등록 실패: 경고만 남기고 살려두면 소켓에 붙은 채 아무도 회수하지 않는다.
            // 수명 상한을 걸어 누수를 끊는다(즉시 파괴하면 의도된 연출이 아예 보이지 않으므로 OneShot 취급).
            Debug.LogWarning(
                $"[UnitEffectPresenter] Held InstanceKey '{cue.InstanceKey}' prefab '{prefab.name}'이 " +
                "ISkillEffectHandle을 구현하지 않는다. 후속 Signal/Stop으로 회수할 수 없어 OneShot 수명으로 폴백한다.",
                instance);
            EffectFallbackRelease.Ensure(instance);
        }
    }
}