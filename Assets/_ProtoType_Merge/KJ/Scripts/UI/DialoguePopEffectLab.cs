using UnityEngine;

/// <summary>Independent EffectLab dialogue preview; no exploration event dependencies.</summary>
public sealed class DialoguePopEffectLab : MonoBehaviour
{
    [SerializeField] private DialogueBubblePopEffect bubble;
    [SerializeField] private DialogueBubblePopEffect growBubble;
    [Min(0.5f)] public float replayInterval = 2.5f;
    public bool loop = true;
    private float elapsed;

    public void Configure(DialogueBubblePopEffect effect, DialogueBubblePopEffect growEffect = null)
    {
        bubble = effect;
        growBubble = growEffect;
    }
    private void OnEnable() => Replay();

    public void Replay()
    {
        elapsed = 0f;
        ReplayBubble(bubble);
        ReplayBubble(growBubble);
    }

    private static void ReplayBubble(DialogueBubblePopEffect effect)
    {
        if (effect == null) return;
        if (!effect.gameObject.activeSelf) effect.gameObject.SetActive(true);
        else effect.Play();
    }

    private void Update()
    {
        if (!loop || bubble == null) return;
        elapsed += Time.unscaledDeltaTime;
        float duration = Mathf.Max(PreviewDuration(bubble), PreviewDuration(growBubble));
        if (elapsed >= Mathf.Max(replayInterval, duration + 1f)) Replay();
    }

    private static float PreviewDuration(DialogueBubblePopEffect effect)
    {
        if (effect == null) return 0f;
        var typewriter = effect.GetComponent<DialogueTypewriterEffect>();
        return Mathf.Max(effect.duration, typewriter != null ? typewriter.Duration : 0f);
    }

    public void SetLoop(bool value) => loop = value;
}
