using UnityEngine;

[DisallowMultipleComponent]
public sealed class TitleExplosionSfx : MonoBehaviour
{
    private const string SfxKey = "Title_Explore";
    private const float PlaybackSeconds = 3.3f;
    private const float BgmDelaySeconds = 1f;
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
        audioManager?.PlaySfxForDuration(SfxKey, PlaybackSeconds);
        StartCoroutine(StartBgmAfterDelay());
    }

    private System.Collections.IEnumerator StartBgmAfterDelay()
    {
        yield return new WaitForSecondsRealtime(BgmDelaySeconds);
        audioManager?.ResumeSceneBgm(sceneName);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        audioManager?.ResumeSceneBgm(sceneName, false);
    }
}
