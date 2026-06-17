using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260617] 버튼 호버 오버레이를 "버튼 sprite 형상을 따르는 흰색 반투명"으로 구성한다.
/// 기존 BaseHoverOverlay(노란 사각형 quad, sprite=null)를 대체하는 색/형상 구성기.
/// 호버 시 켜고 끄는 토글은 기존 ButtonEffectActiveToggle.hoverObjects가 그대로 담당하고,
/// 본 컴포넌트는 오버레이의 sprite/형상/색만 맞춘다.
///
/// UIButtonBase는 IOF가 버튼별 sprite Variant를 생성하므로, 프리팹에 sprite를 고정하면
/// 변종마다 형상이 어긋난다. → 부모 버튼의 sprite를 런타임에 복사해 형상을 일치시킨다.
/// </summary>
[DisallowMultipleComponent]
public class HoverOverlayTint : MonoBehaviour
{
    [Tooltip("틴트를 적용할 오버레이 Image. 비우면 자식 'BaseHoverOverlay'에서 탐색.")]
    [SerializeField] private Image overlay;
    [Tooltip("형상(sprite)을 따올 원본. 비우면 이 GameObject의 Image.")]
    [SerializeField] private Image source;
    [Tooltip("오버레이 색(흰색 반투명). 알파가 베일 농도.")]
    [SerializeField] private Color tint = new Color(1f, 1f, 1f, 0.30f);
    [Tooltip("RGB를 흰색으로 강제하고 sprite 알파를 형상 마스크로 쓰는 UI 머티리얼(UI/WhiteSilhouette). 비우면 sprite 곱셈이라 흰색이 무효.")]
    [SerializeField] private Material overlayMaterial;

    private void Awake() => Apply();
    private void OnEnable() => Apply();

    private void Apply()
    {
        if (source == null) source = GetComponent<Image>();
        if (overlay == null)
        {
            var t = transform.Find("BaseHoverOverlay");
            if (t != null) overlay = t.GetComponent<Image>();
        }
        if (overlay == null) return;

        if (source != null)
        {
            overlay.sprite = source.sprite;
            overlay.type = source.type;
            overlay.preserveAspect = source.preserveAspect;
            if (source.type == Image.Type.Sliced || source.type == Image.Type.Tiled)
                overlay.fillCenter = true;
        }
        if (overlayMaterial != null) overlay.material = overlayMaterial;
        overlay.color = tint;
    }
}
