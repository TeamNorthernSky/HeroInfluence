using UnityEngine;

/// <summary>로딩 한 번에 실루엣 하나만 선택하고 원 안에서 고유 모션을 재생한다.</summary>
public sealed class SceneLoadingEmblem : MonoBehaviour
{
    private const float EntranceDuration = .6f;
    private const float Diameter = 196f;
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
        iconMaterial = CreateMaterial(shader, 0);
        maskMaterial = CreateMaterial(shader, 1);
        ringMaterial = CreateMaterial(shader, 2);
        flameMaterial = CreateMaterial(shader, 3);

        var mask = CreateImage("Circle clip", root, Vector2.one * Diameter, maskMaterial);
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
        selectedIcon = SelectionRandom.Next(IconRegions.Length);
        var region = IconRegions[selectedIcon];
        icon.uvRect = new Rect(region.x / SheetSize,
            1f - (region.y + region.height) / SheetSize,
            region.width / SheetSize, region.height / SheetSize);
        var size = new Vector2(region.width, region.height) * (140f / Mathf.Max(region.width, region.height));
        icon.rectTransform.sizeDelta = size;
        // Match the two upward-facing barrel openings on the crossed-pistol silhouette.
        flashes[0].rectTransform.anchoredPosition = new Vector2(-size.x * .43f, size.y * .36f);
        flashes[1].rectTransform.anchoredPosition = new Vector2(size.x * .43f, size.y * .36f);
        RestartMotion();
    }

    public void RestartMotion()
    {
        motionTime = 0f;
        ApplyMotion(0f);
    }

    private void Update()
    {
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
        float scale = selectedIcon == 0 ? .9f : 1f;
        float x = 0f, y = 0f, rotation = 0f;
        for (int i = 0; i < flashes.Length; i++) flashes[i].gameObject.SetActive(false);
        if (enter >= 1f)
        {
            switch (selectedIcon)
            {
                case 0: // 왼쪽으로 당긴 뒤 오른쪽으로 빠르게 찌르고 중앙으로 회수한다.
                    float punch = Mathf.Repeat(active, 1.5f);
                    if (punch < .42f)
                        x = Mathf.Lerp(0f, -26f, Mathf.SmoothStep(0f, 1f, punch / .42f));
                    else if (punch < .56f)
                        x = Mathf.Lerp(-26f, 26f, 1f - Mathf.Pow(1f - (punch - .42f) / .14f, 3f));
                    else if (punch < .66f)
                        x = 26f;
                    else if (punch < .96f)
                        x = Mathf.Lerp(26f, 0f, Mathf.SmoothStep(0f, 1f, (punch - .66f) / .3f));
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
