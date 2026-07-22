using UnityEngine;

/// <summary>Cue의 Anchor/SocketName으로 이펙트 생성 위치를 해석한다(배치는 레시피가 결정).</summary>
public static class PresentationAnchorUtil
{
    public static Transform Resolve(SkillEffectContext ctx, SpawnAnchor anchor, string socketName,
        out Vector3 pos, out Quaternion rot)
    {
        Transform t = null;
        if (ctx != null)
        {
            switch (anchor)
            {
                case SpawnAnchor.Caster:
                    t = ctx.Caster != null ? ctx.Caster.transform : null;
                    break;
                case SpawnAnchor.CasterSocket:
                    UnitVisualProfile prof = ctx.Caster != null ? ctx.Caster.GetComponent<UnitVisualProfile>() : null;
                    t = prof != null ? prof.GetSocket(socketName)
                                     : (ctx.Caster != null ? ctx.Caster.transform : null);
                    break;
                case SpawnAnchor.Target:
                    if (ctx.PrimaryTarget != null && !ctx.PrimaryTarget.IsDead)
                    {
                        t = ctx.PrimaryTarget.transform;
                    }
                    break;
                case SpawnAnchor.TargetCell:
                    break; // 위치 스냅샷만 사용
            }
        }

        if (t != null)
        {
            pos = t.position;
            rot = t.rotation;
            return t;
        }

        pos = ctx != null ? ctx.TargetPosition : Vector3.zero;
        rot = Quaternion.identity;
        return null;
    }
}

/// <summary>
/// 유닛 로컬 이펙트 프리젠터. AniEvent_PresentationCue(cue) 수신 시 그 cue의 EffectPrefabs를
/// Anchor 위치에 생성하고 Play(ctx). cue/컨텍스트 없으면 no-op. 사운드는 UnitSoundPresenter가 담당.
/// </summary>
[DisallowMultipleComponent]
public class UnitEffectPresenter : MonoBehaviour
{
    private PresentationRuntimeContext _ctx;
    private PresentationRuntimeContext Ctx => _ctx != null ? _ctx : (_ctx = GetComponent<PresentationRuntimeContext>());

    public void PresentationCue(string cueName)
    {
        PresentationRuntimeContext ctx = Ctx;
        if (ctx == null)
        {
            return;
        }

        string norm = string.IsNullOrWhiteSpace(cueName) ? string.Empty : cueName.Trim().ToLowerInvariant();
        if (!ctx.TryGetCue(norm, out RuntimeCue cue) || cue.EffectPrefabs == null)
        {
            return;
        }

        SkillEffectContext baseCtx = ctx.Current;
        for (int i = 0; i < cue.EffectPrefabs.Count; i++)
        {
            GameObject prefab = cue.EffectPrefabs[i];
            if (prefab == null)
            {
                continue;
            }

            Transform at = PresentationAnchorUtil.Resolve(baseCtx, cue.Anchor, cue.SocketName, out Vector3 pos, out Quaternion rot);
            GameObject inst = Instantiate(prefab, pos, rot);

            if (baseCtx != null)
            {
                baseCtx.SocketTransform = at; // 재료가 소켓을 참조할 경우 대비
            }
            inst.GetComponent<ISkillEffectBehaviour>()?.Play(baseCtx);
        }
    }
}
