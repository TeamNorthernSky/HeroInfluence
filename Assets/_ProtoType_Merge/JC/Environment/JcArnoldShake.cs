using System.Collections;
using UnityEngine;

public sealed class JcArnoldShake : MonoBehaviour
{
    [InspectorName("저장 대상 프로필"), Tooltip("조절값을 불러오거나 저장할 에셋입니다. 다른 프로필을 선택한 뒤 ‘프로필 불러와 적용’을 누르면 그 값을 사용합니다.")] public JcArnoldShakeProfile profile;
    [InspectorName("현재 조절값"), Tooltip("펼쳐서 발걸음과 채팅을 조절합니다. 플레이 중 수정한 값은 다음 발걸음이나 박자부터 반영되며, 보관하려면 프로필에 저장해야 합니다.")] public ArnoldShakeValues draft = ArnoldShakeValues.Default;
    private static JcArnoldShake active;
    private Coroutine preview;
    private GameObject previewWindow;
    private CameraShakeEffect previewCamera;

    private void OnEnable()
    {
        active = this;
        LoadProfile();
    }

    private void OnDisable()
    {
        StopPreview();
        if (active == this) active = null;
    }

    public void LoadProfile() => draft = profile != null ? profile.values : ArnoldShakeValues.Default;

    public static ArnoldShakeValues Values => active != null ? active.draft : ArnoldShakeValues.Default;

    public static void Footstep(Camera camera, Object owner)
    {
        var settings = Values;
        CameraShakeEffect.ForCamera(camera)?.PlayFootstep(owner, settings.footStrength, settings.footDuration, settings.footReboundPercent);
    }

    public static IEnumerator ChatBeats(CameraShakeEffect camera, RectTransform frame, Object owner)
    {
        for (int beat = 0; beat < 4; beat++)
        {
            var settings = Values;
            float blend = beat / 3f;
            float duration = Mathf.Max(.01f, settings.chatDuration);
            float start = Time.unscaledTime;
            if (camera != null)
                camera.PlayImpact(owner, Mathf.Lerp(settings.cameraStart, settings.cameraEnd, blend), duration);
            do
            {
                if (frame != null)
                    frame.anchoredPosition = CameraShakeEffect.ImpactOffset(Time.unscaledTime - start, duration)
                        * Mathf.Lerp(settings.frameStart, settings.frameEnd, blend);
                yield return null;
            }
            while (Time.unscaledTime - start < (beat == 3 ? duration : Mathf.Max(duration, settings.interval)));
        }
        if (frame != null) frame.anchoredPosition = Vector2.zero;
    }

    private void StopPreview()
    {
        if (preview != null) StopCoroutine(preview);
        if (previewCamera != null) previewCamera.CancelImpact(this);
        if (previewWindow != null)
        {
            previewWindow.SetActive(false);
            Destroy(previewWindow);
            previewWindow = null;
        }
        preview = null;
        previewCamera = null;
    }

    public void Preview(bool chat)
    {
        StopPreview();
        if (chat && ChatModalController.PreviewArnoldRumble()) return;
        previewCamera = CameraShakeEffect.ForCamera(Camera.main);
        if (chat) preview = StartCoroutine(ChatBeats(previewCamera, null, this));
        else Footstep(Camera.main, this);
    }

    public void TestChatWindow()
    {
        StopPreview();
        previewWindow = ChatModalController.CreateRumblePreview(out RectTransform frame);
        if (previewWindow == null) return;
        previewCamera = CameraShakeEffect.ForCamera(Camera.main);
        preview = StartCoroutine(TestWindowBeats(frame));
    }

    private IEnumerator TestWindowBeats(RectTransform frame)
    {
        yield return new WaitForSecondsRealtime(.35f);
        yield return ChatBeats(previewCamera, frame, this);
        yield return new WaitForSecondsRealtime(.4f);
        Destroy(previewWindow);
        previewWindow = null;
        preview = null;
        previewCamera = null;
    }
}
