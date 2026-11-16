using TMPro;
using UnityEngine;

/// <summary>Reveals the dialogue body without changing its layout or speaker label.</summary>
[DisallowMultipleComponent]
public sealed class DialogueTypewriterEffect : MonoBehaviour
{
    [SerializeField] private TMP_Text message;
    [SerializeField] private TMP_FontAsset missingGlyphFont;
    [Min(1f)] public float charactersPerSecond = 28f;
    [Min(0f)] public float startDelay = 0.3f;
    private float elapsed;
    private int characterCount;
    private bool running;
    private VisibleHangulPreprocessor missingGlyphPreprocessor;
    public bool IsPlaying => running && message != null;

    public float Duration => Mathf.Max(0f, startDelay) + characterCount / Mathf.Max(1f, charactersPerSecond);

    private void EnsureVisibleGlyphs()
    {
        if (message != null && missingGlyphFont != null)
        {
            if (missingGlyphPreprocessor == null)
                missingGlyphPreprocessor = new VisibleHangulPreprocessor(message, missingGlyphFont);
            message.textPreprocessor = missingGlyphPreprocessor;
        }
    }

    public void Play()
    {
        if (message == null) return;
        EnsureVisibleGlyphs();
        message.maxVisibleCharacters = int.MaxValue;
        message.ForceMeshUpdate(true, true);
        characterCount = message.textInfo.characterCount;
        elapsed = 0f;
        running = true;
        message.maxVisibleCharacters = 0;
    }

    private void Update()
    {
        if (!running || message == null) return;
        elapsed += Time.unscaledDeltaTime;
        RevealAt(elapsed);
    }

    private void RevealAt(float seconds)
    {
        float time = Mathf.Max(0f, seconds - Mathf.Max(0f, startDelay));
        int visible = Mathf.Min(characterCount, Mathf.FloorToInt(time * Mathf.Max(1f, charactersPerSecond)));
        message.maxVisibleCharacters = visible;
        if (visible >= characterCount) running = false;
    }

    private void OnDisable()
    {
        running = false;
        if (message != null) message.maxVisibleCharacters = int.MaxValue;
    }
    // 일부 넷마블 글자는 존재한다고 보고되지만 실제 윤곽과 글자 폭이 모두 0입니다.
    // TMP 기본 폴백은 이 경우를 놓치므로 해당 글자에만 보조 폰트를 지정합니다.
    private sealed class VisibleHangulPreprocessor : ITextPreprocessor
    {
        private readonly TMP_Text target;
        private readonly TMP_FontAsset fallback;

        public VisibleHangulPreprocessor(TMP_Text target, TMP_FontAsset fallback)
        {
            this.target = target;
            this.fallback = fallback;
            MaterialReferenceManager.AddFontAsset(fallback);
        }

        public string PreprocessText(string text)
        {
            if (string.IsNullOrEmpty(text) || target.font == null) return text;
            var output = new System.Text.StringBuilder(text.Length);
            bool inTag = false;
            foreach (char character in text)
            {
                if (character == '<') inTag = true;
                bool replace = false;
                if (!inTag && character >= '\uAC00' && character <= '\uD7A3')
                {
                    target.font.HasCharacter(character, false, true);
                    replace = !target.font.characterLookupTable.TryGetValue(character, out var glyph) ||
                        (glyph.glyph.metrics.width <= 0 && glyph.glyph.metrics.height <= 0);
                }
                if (replace && fallback.HasCharacter(character, false, true))
                    output.Append("<font=\"").Append(fallback.name).Append("\">").Append(character).Append("</font>");
                else output.Append(character);
                if (character == '>') inTag = false;
            }
            return output.ToString();
        }
    }

}
