using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Button-shaped ambient halo preview, independent of production UI.</summary>
public sealed class ButtonAmbientLightLab : MonoBehaviour
{
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Shader ambientShader;
    public Color lightColor = new Color(.2f, .65f, 1f, 1f);
    [Range(0f, 4f)] public float intensity = 1.5f;
    [Range(.02f, .15f)] public float spread = .08f;
    public bool breathe = true;
    private Material material;

    private void Start()
    {
        if (buttonSprite == null || ambientShader == null) return;
        Caption("버튼 효과 2 · Ambient Light", new Vector2(0, 330), 38);
        Caption("버튼 모양을 따라 바깥으로 부드럽게 퍼지는 빛", new Vector2(0, 260), 24);
        float aspect = buttonSprite.rect.width / buttonSprite.rect.height;
        var size = new Vector2(360, 360 / aspect);
        var halo = new GameObject("AmbientLight", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        halo.transform.SetParent(transform, false);
        halo.gameObject.layer = gameObject.layer;
        halo.rectTransform.sizeDelta = size * 1.6f;
        halo.texture = buttonSprite.texture;
        halo.raycastTarget = false;
        material = new Material(ambientShader);
        var rect = buttonSprite.textureRect;
        material.SetVector("_SpriteRect", new Vector4(rect.x / buttonSprite.texture.width,
            rect.y / buttonSprite.texture.height, rect.width / buttonSprite.texture.width,
            rect.height / buttonSprite.texture.height));
        material.SetFloat("_Aspect", aspect);
        halo.material = material;
        var image = new GameObject("Button", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(transform, false);
        image.gameObject.layer = gameObject.layer;
        image.rectTransform.sizeDelta = size;
        image.sprite = buttonSprite;
        image.raycastTarget = false;
        Caption("색상 · 밝기 · 퍼지는 범위는 Inspector에서 조절", new Vector2(0, -260), 23);
    }

    private void Update()
    {
        if (material == null) return;
        material.SetColor("_Color", lightColor);
        material.SetFloat("_Intensity", intensity * (breathe ? .85f + .15f * Mathf.Sin(Time.unscaledTime * 1.5f) : 1f));
        material.SetFloat("_Spread", spread);
    }

    private void Caption(string value, Vector2 position, float size)
    {
        var text = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(transform, false);
        text.gameObject.layer = gameObject.layer;
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = new Vector2(1500, 70);
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
