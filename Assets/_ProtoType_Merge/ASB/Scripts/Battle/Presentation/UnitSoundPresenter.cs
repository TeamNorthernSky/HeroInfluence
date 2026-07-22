using UnityEngine;

/// <summary>
/// 유닛 로컬 사운드 프리젠터. AniEvent_PresentationCue(cue) 수신 시 그 cue의 SoundIds를 재생.
/// cue/컨텍스트/사운드 없으면 no-op.
/// </summary>
[DisallowMultipleComponent]
public class UnitSoundPresenter : MonoBehaviour
{
    private PresentationRuntimeContext _ctx;
    private PresentationRuntimeContext Ctx => _ctx != null ? _ctx : (_ctx = GetComponent<PresentationRuntimeContext>());

    public void PresentationCue(string cueName)
    {
        PresentationRuntimeContext ctx = Ctx;
        if (ctx == null || SoundManager.Instance == null)
        {
            return;
        }

        string norm = string.IsNullOrWhiteSpace(cueName) ? string.Empty : cueName.Trim().ToLowerInvariant();
        if (!ctx.TryGetCue(norm, out RuntimeCue cue) || cue.SoundIds == null)
        {
            return;
        }

        Vector3 pos = ctx.Current != null && ctx.Current.Caster != null
            ? ctx.Current.Caster.transform.position
            : transform.position;

        for (int i = 0; i < cue.SoundIds.Count; i++)
        {
            int id = cue.SoundIds[i];
            if (id != 0)
            {
                SoundManager.Instance.PlayById(id, pos);
            }
        }
    }
}
