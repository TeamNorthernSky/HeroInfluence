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

    private PresentationCueDriver _driver;
    private PresentationCueDriver Driver => _driver != null ? _driver : (_driver = GetComponent<PresentationCueDriver>());

    /// <param name="bypassStateGate">시퀀서 직접 호출용. <see cref="UnitEffectPresenter.PresentationCue"/> 참조.</param>
    public void PresentationCue(string cueName, bool bypassStateGate = false)
    {
        PresentationRuntimeContext ctx = Ctx;
        if (ctx == null) return;

        string norm = string.IsNullOrWhiteSpace(cueName) ? string.Empty : cueName.Trim().ToLowerInvariant();
        if (!ctx.TryGetCue(norm, out RuntimeCue cue)) return;

        FireCue(ctx, cue, norm, bypassStateGate);
    }

    /// <summary>CueId로 사운드를 발화한다(Path A Timeline Signal 경로). Timeline이 시점을 소유하므로 State 게이트를 기본 우회한다.</summary>
    public void PresentationCueById(string cueId, bool bypassStateGate = true)
    {
        PresentationRuntimeContext ctx = Ctx;
        if (ctx == null) return;
        if (!ctx.TryGetCueById(cueId, out RuntimeCue cue)) return;

        FireCue(ctx, cue, cue.NormalizedCueName, bypassStateGate);
    }

    private void FireCue(PresentationRuntimeContext ctx, RuntimeCue cue, string normalizedCueName, bool bypassStateGate)
    {
        if (SoundManager.Instance == null || cue.SoundIds == null) return;

        // 등록되어 있다는 이유만으로 발화하지 않는다 — 기대 Animator state에 실제로 진입했을 때만.
        if (!bypassStateGate)
        {
            PresentationCueDriver driver = Driver;
            if (driver != null && !driver.IsCueAllowed(normalizedCueName)) return;
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
