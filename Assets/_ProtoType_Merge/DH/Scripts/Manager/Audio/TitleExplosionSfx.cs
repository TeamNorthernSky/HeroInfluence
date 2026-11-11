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
    private Animator titleAnimator;
    private float playbackSpeed;

    private void Awake()
    {
        titleAnimator = GetComponent<Animator>();
        if (titleAnimator != null) playbackSpeed = titleAnimator.speed;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        sceneName = gameObject.scene.name;
        audioManager = AudioManager.Instance;
        audioManager?.DeferSceneBgm(sceneName);
        // 폭발과 효과음 이벤트가 로딩 화면 뒤에서 먼저 소모되지 않도록 첫 프레임에서 대기한다.
        if (titleAnimator != null) titleAnimator.speed = 0f;
        StartCoroutine(PlayAfterLoading());
    }

    private System.Collections.IEnumerator PlayAfterLoading()
    {
        // OnEnable은 sceneLoaded 이전이므로 페이드 컨트롤러가 진입 처리를 시작할 때까지 기다린다.
        yield return null;
        while (SceneFadeController.IsInputBlocked)
            yield return null;
        if (titleAnimator != null) titleAnimator.speed = playbackSpeed;
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
        if (titleAnimator != null) titleAnimator.speed = playbackSpeed;
        audioManager?.ResumeSceneBgm(sceneName, false);
    }
}
