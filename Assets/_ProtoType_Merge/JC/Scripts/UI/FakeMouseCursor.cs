using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 프리팹 연결을 보존하며 독립 UI Canvas에서 기본/스크롤 커서를 표시한다.</summary>
[DisallowMultipleComponent]
public class FakeMouseCursor : MonoBehaviour
{
    [Tooltip("기본 커서 이미지와 클릭 기준점을 읽을 기존 RectTransform입니다. 원본512px를 재사용하며 독립 UI Canvas에 커서를 표시합니다. 크기는 게임 렌더 높이에 비례하고 이동은 프레임률의 영향을 받습니다.")]
    [SerializeField] private RectTransform _cursorRect;
    private static FakeMouseCursor Instance;
    private Image sourceImage;
    private bool imageWasEnabled;
    private Texture2D normalTexture;
    private Texture2D[] arrows;
    private Vector2 normalHotspot;
    private int applied = int.MinValue;
    private JcRelativeCursor presentation = new JcRelativeCursor();
    private const int ArrowSize = 512;

    private void Reset() => _cursorRect = transform as RectTransform;
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    private void OnEnable()
    {
        if (Instance != this) return;
        if (_cursorRect == null) _cursorRect = transform as RectTransform;
        sourceImage = _cursorRect != null ? _cursorRect.GetComponent<Image>() : null;
        if (sourceImage != null)
        {
            imageWasEnabled = sourceImage.enabled;
            if (normalTexture == null && sourceImage.sprite != null)
            {
                try { normalTexture = CopyCursor(sourceImage); }
                catch (System.Exception e) { Debug.LogWarning("기본 커서 변환 실패: OS 기본 커서를 사용합니다. " + e.Message, this); }
            }
            sourceImage.enabled = false;
        }
        if (arrows == null)
        {
            arrows = new Texture2D[8];
            for (int i = 0; i < arrows.Length; i++) arrows[i] = CreateArrow(i);
        }
        applied = int.MinValue;
    }
    private void OnApplicationFocus(bool focus)
    {
        if (Instance != this) return;
        applied = int.MinValue;
        if (!focus) ReleaseCursor();
    }
    // 카메라 판정 이후 모양을 교체한다. 위치 표시는 Unity 커서 경로 한 곳에서 처리한다.
    private void LateUpdate()
    {
        if (Instance != this) return;
        if (!JcPointerInput.CanControl) { ReleaseCursor(); return; }
        Vector2 direction = JcPointerInput.ScrollDirection;
        if (!JcPointerInput.Inside && direction.sqrMagnitude < .01f) { ReleaseCursor(); return; }
        int index = direction.sqrMagnitude < .01f ? -1
            : ((Mathf.RoundToInt(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg / 45f) % 8) + 8) % 8;
        Texture2D texture = index < 0 ? normalTexture : arrows[index];
        Vector2 hotspot = index < 0 ? normalHotspot : ArrowHotspot(index);
        var profile = JcPointerProfile.Current;
        float size = index < 0 ? profile.normalCursorSize : profile.scrollCursorSize;
        if (texture == null) { ReleaseCursor(); return; }
        presentation.Show(texture, hotspot, size, index >= 0);
        applied = index;
    }
    private void ReleaseCursor()
    {
        if (applied == -2) return;
        presentation.Release();
        Cursor.visible = true;
        applied = -2;
    }
    private void OnDisable()
    {
        if (Instance != this) return;
        ReleaseCursor();
        presentation.Dispose();
        if (sourceImage != null) sourceImage.enabled = imageWasEnabled;
    }
    private void OnDestroy()
    {
        if (Instance != this) return;
        ReleaseCursor();
        presentation.Dispose();
        if (normalTexture != null) Destroy(normalTexture);
        if (arrows != null) foreach (var texture in arrows) if (texture != null) Destroy(texture);
        Instance = null;
    }
    private Texture2D CopyCursor(Image image)
    {
        Sprite sprite = image.sprite;
        Rect rect = sprite.textureRect;
        // 원본 importer의 Read/Write + 무압축 픽셀을 그대로 복사한다.
        // RT 색공간 변환, UI 크기 축소, Image tint를 적용하지 않는다.
        int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
        if (!sprite.texture.isReadable)
            throw new System.InvalidOperationException("커서 원본의 Read/Write 설정을 켜 주세요.");
        var source = sprite.texture.GetPixels32();
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            System.Array.Copy(source, (Mathf.RoundToInt(rect.y) + y) * sprite.texture.width + Mathf.RoundToInt(rect.x), pixels, y * width, width);
        var result = NewTexture(width, height, "JC Default Cursor");
        result.SetPixels32(pixels);
        result.Apply(false, false);
        normalHotspot = new Vector2(Mathf.Clamp(_cursorRect.pivot.x * width, 0, width - 1),
            Mathf.Clamp((1 - _cursorRect.pivot.y) * height, 0, height - 1));
        return result;
    }
    private static Texture2D NewTexture(int width, int height, string name)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name, alphaIsTransparency = true, filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
        };
    }
    public static Vector2 ArrowHotspot(int direction)
    {
        float angle = direction * Mathf.PI / 4;
        return new Vector2((.5f + .42f * Mathf.Cos(angle)) * ArrowSize, (.5f - .42f * Mathf.Sin(angle)) * ArrowSize);
    }
    // 512px 원본. 외곽선 없이 청백색 면과 고정 광원의 은은한 하이라이트를 사용한다.
    // 경계 픽셀만 4x4 면적 샘플링하여 대각선/끝점의 앨리어싱을 완화한다.
    public static Texture2D CreateArrow(int direction)
    {
        var texture = NewTexture(ArrowSize, ArrowSize, "JC Scroll " + direction);
        var pixels = new Color[ArrowSize * ArrowSize];
        float angle = direction * Mathf.PI / 4;
        Vector2 forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 side = new Vector2(-forward.y, forward.x);
        for (int y = 0; y < ArrowSize; y++)
        for (int x = 0; x < ArrowSize; x++)
        {
            Vector2 screen = new Vector2((x + .5f) / ArrowSize - .5f, (y + .5f) / ArrowSize - .5f);
            Vector2 local = new Vector2(Vector2.Dot(screen, forward), Vector2.Dot(screen, side));
            float distance = ShapeDistance(local);
            float alpha = distance < -1f / ArrowSize ? 1 : 0;
            if (Mathf.Abs(distance) <= 1f / ArrowSize)
            {
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                {
                    Vector2 p = screen + new Vector2(((sx + .5f) / 4 - .5f) / ArrowSize, ((sy + .5f) / 4 - .5f) / ArrowSize);
                    if (ShapeDistance(new Vector2(Vector2.Dot(p, forward), Vector2.Dot(p, side))) <= 0) alpha += 1f / 16;
                }
            }
            float light = Mathf.Clamp01(.55f + screen.y * .75f - screen.x * .3f);
            Color color = Color.Lerp(new Color(.48f, .68f, .88f), new Color(.91f, .97f, 1f), light);
            float band = (screen.y - .12f + screen.x * .3f) / .09f;
            float highlight = Mathf.Exp(-band * band) * .24f;
            float bevel = (1 - Mathf.Clamp01(-distance / .014f)) * .16f * light;
            color = Color.Lerp(color, Color.white, highlight + bevel);
            color.a = alpha;
            pixels[y * ArrowSize + x] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
    private static float ShapeDistance(Vector2 p)
    {
        float triangle = Mathf.Max(.04f - p.x, (Mathf.Abs(p.y) + p.x - .42f) / Mathf.Sqrt(2));
        float bars = float.PositiveInfinity;
        for (int i = 0; i < 3; i++)
        {
            float center = -.065f - .13f * i;
            float halfHeight = .32f - .09f * i;
            bars = Mathf.Min(bars, Mathf.Max(Mathf.Abs(p.x - center) - .035f, Mathf.Abs(p.y) - halfHeight));
        }
        return Mathf.Min(triangle, bars);
    }
}
