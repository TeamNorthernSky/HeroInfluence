using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Independent next-turn button samples using the existing UI glow shaders.</summary>
public sealed class TurnButtonEffectLab : MonoBehaviour
{
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Material glowTemplate;
    [SerializeField] private Material orbitTemplate;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Shader scatteredGlowShader;
    [Min(.1f)] public float orbitPeriod = 2.4f;
    [Range(0f, 8f)] public float glowIntensity = 2f;
    private readonly List<Material> materials = new List<Material>();
    private Material orbit;
    private Material hover;
    private Material backlight;
    private Image orbitImage;
    private float started;
    private bool built;
    private readonly Vector3[] corners = new Vector3[4];

    private void OnEnable() => started = Time.unscaledTime;
    private void OnDisable()
    {
        var overlay = transform.Find("Sample2/NextTurnButton/HoverLight");
        if (overlay != null) overlay.gameObject.SetActive(false);
    }
    private void Start() => Build();

    public void Build()
    {
        if (built) return;
        built = true;
        Text("다음 턴 버튼 · 빛 효과", transform, new Vector2(0, 330), new Vector2(1500, 70), 38);
        Text("각 효과를 비교하세요 · 오른쪽 버튼 위에 마우스를 올려 보세요", transform,
            new Vector2(0, 265), new Vector2(1500, 50), 23);
        for (int i = 0; i < 3; i++)
        {
            var root = new GameObject("Sample" + i, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(transform, false);
            rect.sizeDelta = new Vector2(440, 300);
            rect.anchoredPosition = new Vector2((i - 1) * 510, 0);
            Text(new[] { "1. 뒤로 새어 나오는 빛", "2. 테두리를 도는 두 빛", "3. 마우스를 올리면 빛" }[i],
                rect, new Vector2(0, 170), new Vector2(470, 55), 26);
            if (i == 0)
            {
                var back = Picture("BackGlow", rect);
                back.sprite = null;
                back.rectTransform.sizeDelta = new Vector2(650, 440);
                backlight = new Material(scatteredGlowShader);
                materials.Add(backlight);
                back.material = backlight;
            }
            var image = Picture("NextTurnButton", rect);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            if (i == 1)
            {
                orbitImage = Picture("OrbitLight", rect);
                orbit = Copy(orbitTemplate);
                orbit.SetFloat("_SelectedMotion", 1);
                orbit.SetFloat("_OrbitTrail", .22f);
                orbit.SetFloat("_OrbitWhiteRim", 1);
                orbit.SetFloat("_SelectedSweepIntensity", 0);
                orbit.SetFloat("_BloomIntensity", 0);
                orbitImage.material = orbit;
            }
            if (i == 2)
            {
                var overlay = Picture("HoverLight", image.transform);
                hover = Copy(glowTemplate);
                overlay.material = hover;
                var trigger = image.gameObject.AddComponent<EventTrigger>();
                AddHover(trigger, EventTriggerType.PointerEnter, () => { started = Time.unscaledTime; overlay.gameObject.SetActive(true); });
                AddHover(trigger, EventTriggerType.PointerExit, () => overlay.gameObject.SetActive(false));
                overlay.gameObject.SetActive(false);
            }
            Text(new[] { "버튼 뒤에서 바깥으로 퍼지는 빛", "시계방향 · 마주 보는 두 빛", "마우스를 벗어나면 꺼집니다" }[i],
                rect, new Vector2(0, -170), new Vector2(470, 50), 22);
        }
    }

    private void Update()
    {
        if (backlight != null)
        {
            backlight.SetFloat("_EffectTime", Time.unscaledTime - started);
            backlight.SetFloat("_Intensity", glowIntensity);
        }
        if (hover != null) hover.SetFloat("_SweepTime", Time.unscaledTime - started);
        if (orbit == null) return;
        orbit.SetFloat("_SelectedTime", Time.unscaledTime);
        orbit.SetFloat("_OrbitPeriod", orbitPeriod);
        orbit.SetFloat("_GlowIntensity", glowIntensity);
        orbitImage.rectTransform.GetWorldCorners(corners);
        var canvas = orbitImage.canvas;
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        float width = Vector2.Distance(min, RectTransformUtility.WorldToScreenPoint(camera, corners[3]));
        float height = Vector2.Distance(min, RectTransformUtility.WorldToScreenPoint(camera, corners[1]));
        orbit.SetVector("_OrbitUVPerPixel", new Vector4(1 / Mathf.Max(1, width), 1 / Mathf.Max(1, height), 0, 0));
    }

    private Material Copy(Material source)
    {
        var result = new Material(source);
        materials.Add(result);
        return result;
    }

    private Image Picture(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        var image = go.GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        float aspect = buttonSprite.rect.width / buttonSprite.rect.height;
        image.rectTransform.sizeDelta = new Vector2(410, 410 / aspect);
        image.sprite = buttonSprite;
        image.raycastTarget = false;
        return image;
    }

    private void Text(string value, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        var go = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    private static void AddHover(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private void OnDestroy()
    {
        foreach (var material in materials) if (material != null) Destroy(material);
    }
}
