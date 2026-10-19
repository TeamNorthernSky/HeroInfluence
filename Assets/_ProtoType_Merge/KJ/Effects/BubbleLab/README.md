# Bubble EffectLab — 2026-10-15

Source: user-provided recordings `181225.mp4`, `181309.mp4`, `181330.mp4`.
Scene: `Assets/_ProtoType_Merge/Scenes/EffectLab.unity`.

These are chat-bubble UI effects, not particle bubbles. Each sample prefab includes
its own viewport, messages, sequence controller and replay button. Enter Play Mode
to preview; the top button restarts all three samples. The production chat is unchanged.

## Reference observations and reconstruction

| Sample | Visible evidence | Reconstructed preset |
|---|---|---|
| 01 / 181225 | Phone chat appears at ~0.87s; third message rises/fades in around 1.20–1.40s. Rounded yellow outgoing bubbles; older lines move upward when another line arrives. | 0.30s cubic ease-out rise by 65 UI units, alpha entrance; 0.80s message spacing; previous bubbles shift by 80. |
| 02 / 181309 | Blue chat field; right yellow and left white messages. Incoming lines appear from below and push existing conversation upward. | 0.26s rise by 50; scale 0.84 → slight overshoot → 1; 1.30s interval, 100-unit stack shift. |
| 03 / 181330 | Single yellow overlay over live footage. First line is already present at recording start. A cut at ~1.53s is followed by a new line around 1.60s. | 0.18s quick lift by 45; scale 0.96 → 1; 1.60s interval, replace previous line. |

The third clip does not contain a complete first entrance; its timing and quick-lift
curve are an interpretation of the short visible replacement. All timings above
are reproducible implementation presets, not claims of exact original animation
curves. Footage, player controls and real people's portraits are not copied into assets.

## Components

- `BubbleRiseEffect`: independent entrance on a RectTransform and CanvasGroup;
  `Play(target)` / `Hide()` / `SetDestination(target)`; unscaled time.
- `BubbleEffectLabDemo`: sample sequence, stack movement, replacement, repeat.
- `BubbleShape.png`: procedural white rounded bubble, tinted at display time.
- `BubbleSample_01..03.prefab`: serialized references and replay callbacks saved by Unity.

At 1920×1080, previews are side by side; CanvasScaler follows the screen size.
The demos have no save-game, network, lobby-manager or production-chat dependency.
