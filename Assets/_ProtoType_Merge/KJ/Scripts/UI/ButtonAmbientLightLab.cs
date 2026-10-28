using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Button-shaped ambient halo, with an optional existing button target for production UI.</summary>
public sealed class ButtonAmbientLightLab : MonoBehaviour
{
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Shader ambientShader;
    [Tooltip("공방처럼 기존 버튼에 적용할 때 연결합니다. 비어 있으면 EffectLab 샘플을 만듭니다.")]
    [SerializeField] private Image targetButton;
    public Color lightColor = new Color(.2f, .65f, 1f, 1f);
    [Range(0f, 4f)] public float intensity = 1.5f;
    [Range(.02f, .15f)] public float spread = .08f;
    public bool breathe = true;
    private Material material;
    private RawImage targetHalo;
    private bool visible;

    private void Start()
    {
        if (targetButton != null) { SetVisible(visible); return; }
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

    public void SetVisible(bool show)
    {
        visible = show;
        if (targetButton == null || targetButton.sprite == null || ambientShader == null) return;
        if (targetHalo == null)
        {
            var go = new GameObject("CoreSelectedAmbient", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            go.layer = targetButton.gameObject.layer;
            go.transform.SetParent(targetButton.transform.parent, false);
            go.transform.SetSiblingIndex(targetButton.transform.GetSiblingIndex());
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            targetHalo = go.GetComponent<RawImage>();
            targetHalo.raycastTarget = false;
            material = new Material(ambientShader);
            targetHalo.material = material;
        }
        targetHalo.gameObject.SetActive(show && isActiveAndEnabled);
        if (!show) return;
        var sprite = targetButton.sprite;
        targetHalo.texture = sprite.texture;
        var spriteRect = sprite.textureRect;
        material.SetVector("_SpriteRect", new Vector4(spriteRect.x / sprite.texture.width,
            spriteRect.y / sprite.texture.height, spriteRect.width / sprite.texture.width, spriteRect.height / sprite.texture.height));
        var target = targetButton.rectTransform;
        var halo = targetHalo.rectTransform;
        halo.anchorMin = target.anchorMin;
        halo.anchorMax = target.anchorMax;
        halo.pivot = target.pivot;
        halo.localRotation = target.localRotation;
        halo.localScale = target.localScale;
        halo.sizeDelta = target.sizeDelta + target.rect.size * .6f;
        halo.anchoredPosition = target.anchoredPosition + Vector2.Scale(target.rect.size * .6f, target.pivot - Vector2.one * .5f);
        material.SetFloat("_Aspect", target.rect.width / Mathf.Max(1f, target.rect.height));
        Update();
    }

    private void OnDisable()
    {
        if (targetHalo != null) targetHalo.gameObject.SetActive(false);
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
        if (targetHalo != null)
        {
            if (Application.isPlaying) Destroy(targetHalo.gameObject);
            else DestroyImmediate(targetHalo.gameObject);
        }
        if (material != null)
        {
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
    }
}
