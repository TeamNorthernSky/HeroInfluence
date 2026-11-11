using UnityEngine;

// JC | 2026-11-10 | 방문 여부는 Detector가 전달하고 이 컴포넌트는 Transform 연출만 수행합니다. 씬 검색/입장 판정을 하지 않습니다.
[DisallowMultipleComponent]
public sealed class JcAssociationIndicatorMotion : MonoBehaviour
{
    private JcAssociationIndicatorController controller;
    private Sprite sprite;
    private Vector3 basePosition, baseScale;
    private Quaternion baseRotation;
    private float progress, floatOffset, phase;
    private bool wanted, floatingStarted, initialized;

    public void Initialize(SpriteRenderer renderer, JcAssociationIndicatorController settings)
    {
        controller = settings;
        sprite = renderer.sprite;
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
        baseScale = transform.localScale;
        initialized = true;
        SetVisible(false, true);
    }

    public void SetVisible(bool visible, bool immediate = false)
    {
        if (!initialized) return;
        wanted = visible;
        if (immediate)
        {
            progress = visible ? 1f : 0f;
            floatOffset = phase = 0;
            floatingStarted = false;
        }
        if (visible && !gameObject.activeSelf) gameObject.SetActive(true);
        Advance(0);
    }

    private void LateUpdate()
    {
        var settings = controller ? controller.Draft : null;
        if (settings) Advance(settings.unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
    }

    // JC | 2026-11-10 | 0~1 진행도를 현재값에서 선형 왕복하여 도중 반전에도 위치와 크기가 튀지 않게 합니다.
    public void Advance(float deltaTime)
    {
        if (!initialized || !controller || !sprite) return;
        var settings = controller.Draft;
        if (!settings) return;
        bool animate = settings.animate && controller.isActiveAndEnabled;
        float duration = wanted ? settings.growSeconds : settings.shrinkSeconds;
        progress = !animate || duration <= 0 ? (wanted ? 1f : 0f)
            : Mathf.MoveTowards(progress, wanted ? 1f : 0f, Mathf.Max(0, deltaTime) / duration);
        float height = BaseWorldHeight();
        float amplitude = height * settings.floatHeightRatio;
        if (animate && settings.floating && wanted && progress >= 1f && amplitude > 0)
        {
            if (!floatingStarted)
            {
                phase = Mathf.Asin(Mathf.Clamp(floatOffset / amplitude, -1f, 1f));
                floatingStarted = true;
            }
            phase = Mathf.Repeat(phase + Mathf.Max(0, deltaTime) * Mathf.PI * 2f / Mathf.Max(0.05f, settings.floatPeriod), Mathf.PI * 2f);
            floatOffset = Mathf.Sin(phase) * amplitude;
        }
        else
        {
            floatingStarted = false;
            // JC | 2026-11-10 | 퇴장 시 현재 플로팅 높이에서 중심으로 복귀하여 아래쪽 축소 이동으로 연결합니다.
            floatOffset = !animate || !settings.floating ? 0 : Mathf.MoveTowards(floatOffset, 0,
                Mathf.Max(amplitude, Mathf.Abs(floatOffset)) * Mathf.Max(0, deltaTime) / Mathf.Max(0.01f, settings.shrinkSeconds));
        }
        ApplyPose(settings, height, animate);
        if (!wanted && progress <= 0)
        {
            floatOffset = phase = 0;
            floatingStarted = false;
            gameObject.SetActive(false);
        }
    }

    private float BaseWorldHeight()
    {
        Vector3 localHeight = baseRotation * Vector3.Scale(baseScale, Vector3.up * (sprite.rect.height / sprite.pixelsPerUnit));
        return transform.parent ? transform.parent.TransformVector(localHeight).magnitude : localHeight.magnitude;
    }

    private void ApplyPose(JcAssociationIndicatorProfile settings, float height, bool animate)
    {
        float scale = Mathf.Lerp(settings.minimumScale, settings.maximumScale, progress);
        Vector2 pixels = Vector2.Scale(sprite.rect.size, settings.tipPivot) - sprite.pivot;
        Vector3 tip = new Vector3(pixels.x, pixels.y, 0) / sprite.pixelsPerUnit;
        Vector3 baseTipOffset = baseRotation * Vector3.Scale(baseScale, tip);
        // JC | 2026-11-10 | 피봇 보정은 스케일에만 적용. 별도 월드 Y 이동으로 작은 상태가 아래, 완성 상태가 원래 위치가 됩니다.
        Vector3 vertical = Vector3.up * (animate ? (progress - 1f) * settings.riseHeightRatio * height + floatOffset : 0);
        transform.localRotation = baseRotation;
        transform.localScale = baseScale * scale;
        transform.localPosition = basePosition + baseTipOffset * (1f - scale)
            + (transform.parent ? transform.parent.InverseTransformVector(vertical) : vertical);
    }
}
