using UnityEngine;

[DisallowMultipleComponent]
public class UnitSoundPresenter : MonoBehaviour
{
    private PresentationRuntimeContext context;
    private PresentationRuntimeContext Context =>
        context != null ? context : context = GetComponent<PresentationRuntimeContext>();

    private PresentationCueDriver driver;
    private PresentationCueDriver Driver =>
        driver != null ? driver : driver = GetComponent<PresentationCueDriver>();

    public void PresentationCue(string cueName, bool bypassStateGate = false)
    {
        PresentationRuntimeContext runtimeContext = Context;
        if (runtimeContext == null)
            return;

        string normalizedName = string.IsNullOrWhiteSpace(cueName)
            ? string.Empty
            : cueName.Trim().ToLowerInvariant();
        if (!runtimeContext.TryGetCue(normalizedName, out RuntimeCue cue))
            return;

        FireCue(cue, normalizedName, bypassStateGate);
    }

    public void PresentationCueById(string cueId, bool bypassStateGate = true)
    {
        PresentationRuntimeContext runtimeContext = Context;
        if (runtimeContext == null)
            return;

        if (!runtimeContext.TryGetCueById(cueId, out RuntimeCue cue))
            return;

        FireCue(cue, cue.NormalizedCueName, bypassStateGate);
    }

    private void FireCue(RuntimeCue cue, string normalizedCueName, bool bypassStateGate)
    {
        if (cue == null || cue.SoundIds == null)
            return;

        if (!bypassStateGate)
        {
            PresentationCueDriver cueDriver = Driver;
            if (cueDriver != null && !cueDriver.IsCueAllowed(normalizedCueName))
                return;
        }

        // Legacy ASB SoundManager was removed. This component remains only to keep existing cue hooks safe.
    }
}
