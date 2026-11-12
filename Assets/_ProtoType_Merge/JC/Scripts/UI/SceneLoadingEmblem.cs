using UnityEngine;

/// <summary>로딩 한 번에 실루엣 하나만 선택하고 원 안에서 고유 모션을 재생한다.</summary>
public sealed class SceneLoadingEmblem : MonoBehaviour
{
    private const float EntranceDuration = .6f;
    private const float Diameter = 196f;
    private const float SafeRadius = 82f;
    private const float SheetSize = 1280f;
    private static readonly System.Random SelectionRandom = new System.Random();
    private static readonly Rect[] IconRegions =
    {
        // Approved sheet coordinates, measured from its top left. Labels are outside these regions.
        new Rect(690, 75, 485, 415), // Justice (revised fist)
        new Rect(80, 75, 520, 415),  // Black Bullet
        new Rect(80, 600, 520, 520), // Lumina
        new Rect(714, 661, 470, 440) // Nekoming: centered on the visible paw, including all toe/pad edges.
    };
    private RectTransform iconMotion;
    private UnityEngine.UI.RawImage icon;
    private UnityEngine.UI.RawImage[] flashes;
    private Material iconMaterial, maskMaterial, ringMaterial, flameMaterial;
    private Texture2D atlas;
    private int selectedIcon;
    private float motionTime;
    private bool motionPaused;
    private float contentRadius;
    // JC | 2026-11-12 | 비교 씬에서만 선택 아이콘·수정안을 고정합니다. 실제 로딩 호출의 무작위 선택과 기존 동작은 유지합니다.
    private bool comparisonOnly, comparisonRevision;
    private int comparisonIcon;
    private Shader comparisonBurstShader;
    // JC | 2026-11-12 | 비교 시안의 주먹만 조절하며, 1배는 기존 수정안의 초기/최종 크기를 뜻합니다.
    private Vector2 justiceStart = new Vector2(-30f, 0f), justiceEnd = new Vector2(6f, 0f);
    private float justiceInitialSize = 1f, justiceFinalSize = 1f;

    public void SetJusticeComparison(Vector2 start, Vector2 end, float initialSize, float finalSize)
    {
        if (!comparisonOnly || !comparisonRevision || comparisonIcon != 0) return;
        justiceStart = start; justiceEnd = end;
        justiceInitialSize = Mathf.Clamp(initialSize, .25f, 2f);
        justiceFinalSize = Mathf.Clamp(finalSize, .25f, 2f);
    }

    public void InitializeComparison(Transform parent, int iconIndex, bool revised, Shader burstShader)
    {
        comparisonOnly = true;
        comparisonIcon = Mathf.Clamp(iconIndex, 0, IconRegions.Length - 1);
        comparisonRevision = revised;
        comparisonBurstShader = burstShader;
        Initialize(parent);
        SetMotionPaused(true);
    }

    // JC | 2026-11-12 | 원본/수정안을 같은 시간으로 평가하여 편집 모드에서도 정지 프레임과 움직임을 비교합니다.
    public void SampleComparison(float time)
    {
        if (!comparisonOnly) return;
        motionTime = Mathf.Max(0f, time);
        ApplyMotion(motionTime);
    }

    public void SetMotionPaused(bool paused) => motionPaused = paused;

    public void Initialize(Transform parent)
    {
        transform.SetParent(parent, false);
        var root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
        root.sizeDelta = Vector2.one * Diameter;
        root.anchoredPosition = new Vector2(0f, 140f);
        atlas = Resources.Load<Texture2D>("UI_Sprite/Loading/LoadingHeroSilhouettes");
        var shader = Resources.Load<Shader>("UI_Sprite/Loading/LoadingSilhouette");
        if (atlas == null || shader == null)
        {
            Debug.LogError("Loading silhouette resources are missing.");
            gameObject.SetActive(false);
            return;
        }
        // JC | 2026-11-12 | 수정 루미나만 대칭 도형 셰이더를 사용합니다. 원본 아틀라스·임포터·로딩 셰이더는 변경하지 않습니다.
        iconMaterial = CreateMaterial(comparisonRevision && comparisonIcon == 2 ? comparisonBurstShader : shader, 0);
        maskMaterial = CreateMaterial(shader, 1);
        ringMaterial = CreateMaterial(shader, 2);
        flameMaterial = CreateMaterial(shader, 3);

        var mask = CreateImage("Circle clip", root, Vector2.one * ((SafeRadius + 4f) * 2f), maskMaterial);
        mask.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
        var motion = new GameObject("Icon motion", typeof(RectTransform));
        motion.transform.SetParent(mask.transform, false);
        iconMotion = (RectTransform)motion.transform;
        iconMotion.sizeDelta = Vector2.one * 140f;
        icon = CreateImage("Selected silhouette", iconMotion, Vector2.one * 140f, iconMaterial);
        icon.texture = atlas;
        flashes = new UnityEngine.UI.RawImage[2];
        for (int i = 0; i < flashes.Length; i++)
        {
            flashes[i] = CreateImage("Muzzle flame " + i, iconMotion, new Vector2(22f, 38f), flameMaterial);
            flashes[i].color = new Color(1f, .7f, .18f, 0f);
            flashes[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 43f : -43f);
            var core = CreateImage("Flame core", flashes[i].transform, new Vector2(10f, 23f), flameMaterial);
            core.color = new Color(1f, .98f, .82f, 1f);
        }
        var ring = CreateImage("Blue circle", root, Vector2.one * Diameter, ringMaterial);
        ring.color = new Color(.23f, .65f, 1f, .95f);
        BeginPresentation();
    }

    public void BeginPresentation()
    {
        if (icon == null) return;
        // Do not consume gameplay's UnityEngine.Random sequence for a cosmetic choice.
        selectedIcon = comparisonOnly ? comparisonIcon : SelectionRandom.Next(IconRegions.Length);
        var region = IconRegions[selectedIcon];
        icon.uvRect = new Rect(region.x / SheetSize,
            1f - (region.y + region.height) / SheetSize,
            region.width / SheetSize, region.height / SheetSize);
        var size = new Vector2(region.width, region.height) * (140f / Mathf.Max(region.width, region.height));
        icon.rectTransform.sizeDelta = size;
        // Match the two upward-facing barrel openings on the crossed-pistol silhouette.
        flashes[0].rectTransform.anchoredPosition = new Vector2(-size.x * .43f, size.y * .36f);
        flashes[1].rectTransform.anchoredPosition = new Vector2(size.x * .43f, size.y * .36f);
        if (comparisonRevision && selectedIcon == 2)
            icon.uvRect = new Rect(0f, 0f, 1f, 1f);
        // JC | 2026-11-12 | 불꽃 중심이 아닌 꼬리 끝을 실제 총구(원본 시트의 검은 개구부 중심)에 연결합니다.
        // 자식 코어도 같은 시작점에 고정해 확대/반동 중 총구와 불꽃 사이가 벌어지지 않습니다.
        if (comparisonRevision && selectedIcon == 1)
        {
            var muzzlePixels = new[] { new Vector2(126f, 138f), new Vector2(548f, 138f) };
            for (int i = 0; i < flashes.Length; i++)
            {
                var rect = flashes[i].rectTransform;
                rect.pivot = new Vector2(.5f, 0f);
                rect.anchoredPosition = new Vector2((muzzlePixels[i].x - region.x) / region.width - .5f,
                    .5f - (muzzlePixels[i].y - region.y) / region.height) * size;
                var core = (RectTransform)rect.GetChild(0);
                core.anchorMin = core.anchorMax = core.pivot = new Vector2(.5f, 0f);
                core.anchoredPosition = Vector2.zero;
            }
        }
        // Reserve space for the entire image, including rotated corners and the largest muzzle flame.
        contentRadius = size.magnitude * .5f;
        if (selectedIcon == 1)
            contentRadius = Mathf.Max(contentRadius, flashes[0].rectTransform.anchoredPosition.magnitude +
                flashes[0].rectTransform.sizeDelta.magnitude * .5f * 1.25f);
        if (comparisonRevision && selectedIcon == 1)
        {
            // JC | 2026-11-12 | 총구 기준으로 앞쪽에 배치된 불꽃의 최대 확대 네 모서리까지 계산해 원 테두리 잘림을 방지합니다.
            foreach (var flash in flashes)
                for (int corner = 0; corner < 4; corner++)
                {
                    var rect = flash.rectTransform;
                    var local = (new Vector2((corner & 1) == 0 ? 0 : 1, (corner & 2) == 0 ? 0 : 1) - rect.pivot) * rect.sizeDelta * 1.25f;
                    var point = (Vector3)rect.anchoredPosition + rect.localRotation * (Vector3)local;
                    contentRadius = Mathf.Max(contentRadius, point.magnitude);
                }
        }
        RestartMotion();
    }

    public void RestartMotion()
    {
        motionTime = 0f;
        ApplyMotion(0f);
    }

    private void Update()
    {
        if (motionPaused) return;
        // A long scene-load frame must not skip the visible entrance or motion.
        motionTime += Mathf.Min(Time.unscaledDeltaTime, .05f);
        ApplyMotion(motionTime);
    }

    private void ApplyMotion(float time)
    {
        if (iconMotion == null) return;
        float enter = Mathf.Clamp01(time / EntranceDuration);
        float entranceY = Mathf.Lerp(-210f, 0f, 1f - Mathf.Pow(1f - enter, 3f));
        float active = Mathf.Max(0f, time - EntranceDuration);
        float scale = selectedIcon == 0 ? .72f : 1f;
        float x = 0f, y = 0f, rotation = 0f;
        // JC | 2026-11-12 | 수정 주먹은 등장 때부터 왼쪽의 작은 준비 자세로 시작합니다.
        if (comparisonRevision && selectedIcon == 0) { x = justiceStart.x; y = justiceStart.y; scale = .46f * justiceInitialSize; }
        for (int i = 0; i < flashes.Length; i++) flashes[i].gameObject.SetActive(false);
        if (enter >= 1f)
        {
            switch (selectedIcon)
            {
                case 0: // 오른쪽에서 유지한 뒤 바로 왼쪽으로 당겨 다음 펀치를 준비한다.
                    float punch = Mathf.Repeat(active, 1.5f);
                    if (comparisonRevision)
                    {
                        // JC | 2026-11-12 | 조절한 시작/종료 중심 사이를 이동합니다. 시작점에서 왼쪽 6만큼 당기는 준비 동작과 타이밍은 보존합니다.
                        Vector2 position = justiceEnd;
                        Vector2 pulled = justiceStart + Vector2.left * 6f;
                        scale = .72f * justiceFinalSize;
                        if (punch < .42f)
                        {
                            float pull = Mathf.SmoothStep(0f, 1f, punch / .42f);
                            position = Vector2.Lerp(active < 1.5f ? justiceStart : justiceEnd, pulled, pull);
                            scale = Mathf.Lerp(active < 1.5f ? .46f * justiceInitialSize : .72f * justiceFinalSize, .46f * justiceInitialSize, pull);
                        }
                        else if (punch < .56f)
                        {
                            float strike = 1f - Mathf.Pow(1f - (punch - .42f) / .14f, 3f);
                            position = Vector2.Lerp(pulled, justiceEnd, strike);
                            scale = Mathf.Lerp(.46f * justiceInitialSize, .72f * justiceFinalSize, strike);
                        }
                        x = position.x; y = position.y;
                        break;
                    }
                    x = 14f;
                    if (punch < .42f)
                    {
                        float pull = Mathf.SmoothStep(0f, 1f, punch / .42f);
                        x = Mathf.Lerp(active < 1.5f ? 0f : 14f, -24f, pull);
                        scale = Mathf.Lerp(.72f, .46f, pull);
                    }
                    else if (punch < .56f)
                    {
                        float strike = 1f - Mathf.Pow(1f - (punch - .42f) / .14f, 3f);
                        x = Mathf.Lerp(-24f, 14f, strike);
                        scale = Mathf.Lerp(.46f, .72f, strike);
                    }
                    break;
                case 1: // Alternating shots with a brief flame and opposite recoil.
                    int shot = Mathf.FloorToInt(active / .48f);
                    float fire = Mathf.Repeat(active, .48f);
                    float recoil = 1f - Mathf.Clamp01(fire / .22f);
                    y = -7f * recoil;
                    rotation = (shot % 2 == 0 ? -1f : 1f) * 3.5f * recoil;
                    if (fire < .1f)
                    {
                        var flash = flashes[shot % 2];
                        flash.gameObject.SetActive(true);
                        var c = flash.color;
                        c.a = 1f - fire / .1f;
                        flash.color = c;
                        flash.rectTransform.localScale = Vector3.one * (1f + .25f * Mathf.Sin(fire * 90f));
                    }
                    break;
                case 2:
                    rotation = -active * 100f; // Clockwise in UI space.
                    break;
                case 3:
                    scale = 1f - .3f * Mathf.Sin(Mathf.Repeat(active, 1.4f) / 1.4f * Mathf.PI);
                    break;
            }
        }
        // Keep all motion inside the ring rather than relying on clipping at the rim.
        // Entrance still rises through the circular mask, without replaying after the real load.
        if (contentRadius > 0f)
            scale = Mathf.Min(scale, Mathf.Max(0f, SafeRadius - new Vector2(x, y).magnitude) / contentRadius);
        iconMotion.anchoredPosition = new Vector2(x, entranceY + y);
        iconMotion.localScale = Vector3.one * scale;
        iconMotion.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }

    private static Material CreateMaterial(Shader shader, float shape)
    {
        var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        material.SetFloat("_Shape", shape);
        return material;
    }

    private static UnityEngine.UI.RawImage CreateImage(string name, Transform parent, Vector2 size, Material material)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.RawImage));
        host.transform.SetParent(parent, false);
        var image = host.GetComponent<UnityEngine.UI.RawImage>();
        image.rectTransform.sizeDelta = size;
        image.material = material;
        image.raycastTarget = false;
        return image;
    }

    private void OnDestroy()
    {
        Release(iconMaterial);
        Release(maskMaterial);
        Release(ringMaterial);
        Release(flameMaterial);
    }

    private static void Release(Material material)
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
    }
}
