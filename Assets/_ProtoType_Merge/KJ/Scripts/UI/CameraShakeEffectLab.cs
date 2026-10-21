using UnityEngine;

/// <summary>EffectLab-only camera preview; does not modify bubble demonstrations.</summary>
public sealed class CameraShakeEffectLab : MonoBehaviour
{
    [Min(0f), Tooltip("흔들리는 시간(초). 변경 후 Play Shake를 누르면 적용됩니다.")]
    public float duration = 0.45f;
    [Min(0f), Tooltip("흔들림 강도. 변경 후 Play Shake를 누르면 적용됩니다.")]
    public float intensity = 0.15f;
    private CameraShakeEffect shake;
    private RenderTexture preview;
    private GameObject previewRoot;
    [SerializeField] private UnityEngine.UI.RawImage previewImage;

    private void Start()
    {
        preview = new RenderTexture(960, 540, 16);
        preview.name = "CameraShakeLabPreview";
        previewRoot = new GameObject("CameraShakePreviewWorld");
        previewRoot.transform.position = new Vector3(10000f, 10000f, 10000f);
        var cameraObject = new GameObject("Preview Camera");
        cameraObject.transform.SetParent(previewRoot.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 3f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.09f, 0.14f);
        camera.cullingMask = 1 << 31;
        camera.targetTexture = preview;
        shake = cameraObject.AddComponent<CameraShakeEffect>();
        // SpriteRenderer works with URP without depending on a custom material.
        for (int y = -2; y <= 2; y++)
            for (int x = -4; x <= 4; x++)
            {
                var marker = new GameObject("Grid Marker");
                marker.layer = 31;
                marker.transform.SetParent(previewRoot.transform, false);
                marker.transform.localPosition = new Vector3(x, y, 0f);
                marker.transform.localScale = Vector3.one * 0.18f;
                var sprite = marker.AddComponent<SpriteRenderer>();
                sprite.sprite = MarkerSprite;
                sprite.color = x == 0 && y == 0 ? Color.yellow : new Color(0.3f, 0.8f, 0.9f);
            }
        if (previewImage != null) previewImage.texture = preview;
    }

    private Sprite markerSprite;
    private Sprite MarkerSprite
    {
        get
        {
            if (markerSprite == null)
                markerSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                    new Vector2(0.5f, 0.5f), 1f);
            return markerSprite;
        }
    }

    public void PlayShake()
    {
        if (shake == null) return;
        shake.Duration = duration;
        shake.Intensity = intensity;
        shake.PlayShake();
    }

    public void StopShake() { if (shake != null) shake.StopShake(); }

    private void OnEnable() { if (previewRoot != null) previewRoot.SetActive(true); }

    private void OnDisable()
    {
        if (shake != null) shake.StopShake();
        if (previewRoot != null) previewRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (previewRoot != null) Destroy(previewRoot);
        if (preview != null) { preview.Release(); Destroy(preview); }
        if (markerSprite != null) Destroy(markerSprite);
    }
}
