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

        if (cue.EffectPrefabs == null) return;
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
                    runtime.RegisterHandle(cue.InstanceKey, handle);
                }
                else
                {
                    Debug.LogWarning($"[UnitEffectPresenter] Held InstanceKey '{cue.InstanceKey}' prefab '{prefab.name}' does not implement ISkillEffectHandle.", instance);
                }
            }
        }
    }
}