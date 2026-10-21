using TMPro;
using UnityEngine;

/// <summary>Reveals the dialogue body without changing its layout or speaker label.</summary>
[DisallowMultipleComponent]
public sealed class DialogueTypewriterEffect : MonoBehaviour
{
    [SerializeField] private TMP_Text message;
    [Min(1f)] public float charactersPerSecond = 28f;
    [Min(0f)] public float startDelay = 0.3f;
    private float elapsed;
    private int characterCount;
    private bool running;
    public bool IsPlaying => running && message != null;

    public float Duration => Mathf.Max(0f, startDelay) + characterCount / Mathf.Max(1f, charactersPerSecond);

    public void Play()
    {
        if (message == null) return;
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
}
