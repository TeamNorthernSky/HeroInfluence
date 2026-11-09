using UnityEngine;

[DisallowMultipleComponent]
public sealed class TitleExplosionSfx : MonoBehaviour
{
    private const string SfxKey = "Title_Explore";
    private const float PlaybackSeconds = 3.3f;
    private bool played;
    private AudioManager audioManager;
    private string sceneName;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        sceneName = gameObject.scene.name;
        audioManager = AudioManager.Instance;
        audioManager?.DeferSceneBgm(sceneName);
    }

    public void PlayExplosionSfx()
    {
        if (!Application.isPlaying || played)
            return;

        played = true;
        if (audioManager == null) audioManager = AudioManager.Instance;
        if (audioManager != null && !audioManager.PlaySfxForDuration(SfxKey, PlaybackSeconds, FinishExplosionSfx))
            FinishExplosionSfx();
    }

    private void FinishExplosionSfx()
    {
        if (this != null && isActiveAndEnabled)
            audioManager?.ResumeSceneBgm(sceneName);
    }

    private void OnDisable()
    {
        audioManager?.ResumeSceneBgm(sceneName, false);
    }
}
