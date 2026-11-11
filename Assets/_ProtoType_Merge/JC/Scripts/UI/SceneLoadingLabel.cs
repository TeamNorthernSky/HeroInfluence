using TMPro;
using UnityEngine;

/// <summary>검정 로딩 화면에 글자별 파도 연출과 푸른 글로우를 표시한다.</summary>
public sealed class SceneLoadingLabel : MonoBehaviour
{
    private const float WaveHeight = 12f;
    private const float LetterDelay = .09f;
    private const float WaveDuration = .48f;
    private const float WavePause = .32f;
    private RectTransform[] letters;
    private Vector2[] restPositions;
    private float waveTime;
    private Material glowMaterial;
    private SceneLoadingEmblem emblem;
    private bool motionPaused;
    public void Initialize(Transform parent)
    {
        transform.SetParent(parent, false);
        var root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
        root.sizeDelta = new Vector2(420, 100);
        const string text = "Loading...";
        var widths = new float[text.Length];
        letters = new RectTransform[text.Length];
        restPositions = new Vector2[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            var host = new GameObject("Letter " + i, typeof(RectTransform));
            host.transform.SetParent(root, false);
            var rect = (RectTransform)host.transform;
            rect.sizeDelta = new Vector2(45, 90);
            rect.anchoredPosition = new Vector2((i - (text.Length - 1) * .5f) * 28, 0);
            letters[i] = rect;
            var glow = CreateGlyph(rect, text[i], new Color(.12f, .45f, 1f, 1));
            if (glowMaterial == null)
            {
                glowMaterial = new Material(glow.fontSharedMaterial);
                glowMaterial.SetFloat("_OutlineWidth", .45f);
                glowMaterial.SetFloat("_OutlineSoftness", .5f);
                glowMaterial.SetColor("_OutlineColor", new Color(.08f, .35f, 1f, 1));
                glowMaterial.SetFloat("_FaceDilate", .15f);
            }
            glow.fontSharedMaterial = glowMaterial;
            glow.alpha = .4f;
            var face = CreateGlyph(rect, text[i], Color.white);
            widths[i] = face.GetPreferredValues(text[i].ToString()).x + 3;
        }
        float total = 0;
        foreach (float width in widths) total += width;
        float x = -total * .5f;
        for (int i = 0; i < letters.Length; i++)
        {
            letters[i].anchoredPosition = new Vector2(x + widths[i] * .5f, 0);
            restPositions[i] = letters[i].anchoredPosition;
            x += widths[i];
        }
        var emblemObject = new GameObject("Loading hero emblem", typeof(RectTransform), typeof(SceneLoadingEmblem));
        emblem = emblemObject.GetComponent<SceneLoadingEmblem>();
        emblem.Initialize(root);
    }
    public void BeginPresentation()
    {
        SetMotionPaused(false);
        if (emblem != null) emblem.BeginPresentation();
        RestartMotion();
    }
    public void SetMotionPaused(bool paused)
    {
        motionPaused = paused;
        if (emblem != null) emblem.SetMotionPaused(paused);
    }
    public void RestartMotion()
    {
        waveTime = 0f;
        ApplyWave(0f);
        // 전후 대기나 재활성화 때는 텍스트만 재시작한다. 아이콘은 첫 등장 이후 계속 유지한다.
    }
    private void OnEnable()
    {
        RestartMotion();
    }
    private void Update()
    {
        if (letters == null || motionPaused) return;
        // 로딩 중 게임 시간이 멈춰 있어도 동일한 속도로 재생한다.
        float cycleDuration = (letters.Length - 1) * LetterDelay + WaveDuration + WavePause;
        waveTime = Mathf.Repeat(waveTime + Mathf.Min(Time.unscaledDeltaTime, .05f), cycleDuration);
        ApplyWave(waveTime);
    }
    private void OnDisable()
    {
        ApplyWave(0f);
    }
    private void ApplyWave(float time)
    {
        if (letters == null) return;
        for (int i = 0; i < letters.Length; i++)
        {
            float progress = (time - i * LetterDelay) / WaveDuration;
            float height = progress > 0f && progress < 1f
                ? Mathf.Sin(progress * Mathf.PI) * WaveHeight : 0f;
            // 흰 글자와 글로우가 같은 부모를 따라 함께 움직인다.
            letters[i].anchoredPosition = restPositions[i] + Vector2.up * height;
        }
    }
    private static TMP_Text CreateGlyph(RectTransform parent, char value, Color color)
    {
        var go = new GameObject("Glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 42;
        label.alignment = TextAlignmentOptions.Center;
        label.text = value.ToString();
        label.color = color;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        var rect = label.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return label;
    }
    private void OnDestroy()
    {
        if (glowMaterial == null) return;
        if (Application.isPlaying) Destroy(glowMaterial); else DestroyImmediate(glowMaterial);
    }
}
