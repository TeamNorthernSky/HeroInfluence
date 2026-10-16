using UnityEngine;

/// <summary>EffectLab-only looping reference sequence, independent of game data.</summary>
[DisallowMultipleComponent]
public sealed class BubbleEffectLabDemo : MonoBehaviour
{
    [SerializeField] private BubbleRiseEffect[] bubbles;
    [SerializeField] private Vector2[] restingPositions;
    [SerializeField, Min(0.1f)] private float messageInterval = 1.15f;
    [SerializeField, Min(0f)] private float stackSpacing = 86f;
    [SerializeField, Min(0.01f)] private float stackDuration = 0.28f;
    [SerializeField, Min(0f)] private float holdDuration = 2f;
    [SerializeField] private bool replacePrevious;
    [SerializeField] private bool loop = true;
    private float clock;
    private float shiftClock;
    private int shown;
    private Vector2[] shiftFrom;
    private Vector2[] shiftTo;

    public void Configure(BubbleRiseEffect[] items, Vector2[] positions, float interval,
        float spacing, bool replace)
    {
        bubbles = items;
        restingPositions = positions;
        messageInterval = Mathf.Max(0.1f, interval);
        stackSpacing = Mathf.Max(0f, spacing);
        replacePrevious = replace;
    }

    private bool IsConfigured => bubbles != null && bubbles.Length > 0 &&
        restingPositions != null && restingPositions.Length == bubbles.Length;

    private void OnEnable()
    {
        if (IsConfigured) Replay();
    }

    public void Replay()
    {
        if (!IsConfigured) return;
        shiftFrom = new Vector2[bubbles.Length];
        shiftTo = new Vector2[bubbles.Length];
        for (int i = 0; i < bubbles.Length; i++)
        {
            if (bubbles[i] == null) continue;
            bubbles[i].Hide();
            shiftFrom[i] = shiftTo[i] = restingPositions[i];
        }
        shown = 0;
        clock = -0.35f;
        shiftClock = stackDuration;
    }

    private void Update()
    {
        if (!IsConfigured || shiftFrom == null) return;
        clock += Time.unscaledDeltaTime;
        shiftClock = Mathf.Min(shiftClock + Time.unscaledDeltaTime, stackDuration);
        float t = 1f - Mathf.Pow(1f - shiftClock / stackDuration, 3f);
        for (int i = 0; i < shown; i++)
            if (bubbles[i] != null && bubbles[i].gameObject.activeSelf)
                bubbles[i].SetDestination(Vector2.LerpUnclamped(shiftFrom[i], shiftTo[i], t));

        if (shown < bubbles.Length && clock >= shown * messageInterval)
        {
            for (int i = 0; i < shown; i++)
            {
                if (bubbles[i] == null) continue;
                if (replacePrevious) bubbles[i].Hide();
                shiftFrom[i] = Vector2.LerpUnclamped(shiftFrom[i], shiftTo[i], t);
                shiftTo[i] += Vector2.up * stackSpacing;
            }
            shiftClock = 0f;
            if (bubbles[shown] != null) bubbles[shown].Play(restingPositions[shown]);
            shown++;
        }
        var lastBubble = bubbles[bubbles.Length - 1];
        float lastDuration = lastBubble != null ? lastBubble.Duration : 0f;
        if (loop && shown == bubbles.Length &&
            clock >= (bubbles.Length - 1) * messageInterval + lastDuration + holdDuration)
            Replay();
    }

    public void SetLoop(bool value) => loop = value;
}
