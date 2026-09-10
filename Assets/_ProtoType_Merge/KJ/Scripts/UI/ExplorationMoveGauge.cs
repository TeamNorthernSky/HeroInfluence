using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Screen-space view of the player party's existing movement budget.</summary>
public sealed class ExplorationMoveGauge : MonoBehaviour
{
    [SerializeField] private RectTransform segmentContainer;
    [SerializeField] private Image segmentTemplate;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private float inactiveAlpha = 0;
    [SerializeField] private Sprite greenFill;
    [SerializeField] private Sprite orangeFill;
    [SerializeField] private Sprite redFill;

    private readonly List<Image> segments = new List<Image>();
    private readonly Dictionary<Canvas, bool> hiddenWorldCanvases = new Dictionary<Canvas, bool>();
    private PartyRegistry registry;
    private PartyGridMover party;
    private float nextSearch;
    private int lastRemaining = -1;
    private int lastMaximum = -1;
    private float lastWidth = -1;

    private void OnEnable()
    {
        nextSearch = 0;
        lastRemaining = lastMaximum = -1;
        if (segmentTemplate != null) segmentTemplate.gameObject.SetActive(false);
        Refresh();
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (segmentContainer == null || segmentTemplate == null || amountText == null || visibility == null)
            return;

        if (registry == null && Time.unscaledTime >= nextSearch)
            registry = FindFirstObjectByType<PartyRegistry>();

        PartyGridMover current = registry != null ? registry.PlayerParty : null;
        if (party != current)
        {
            RestoreWorldBars();
            party = current;
            nextSearch = 0;
            lastRemaining = lastMaximum = -1;
        }

        bool visible = party != null && party.gameObject.activeInHierarchy &&
                       !DefeatedPartyReturnController.IsPartyWaiting(party);
        visibility.alpha = visible ? 1f : 0f;
        visibility.interactable = false;
        visibility.blocksRaycasts = false;

        if (Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + 0.5f;
            if (party != null)
            {
                foreach (var world in FindObjectsByType<PartyMovePointWorldBillboardGauge>(FindObjectsSortMode.None))
                {
                    if (world.TargetParty != party) continue;
                    var canvas = world.GetComponentInChildren<Canvas>(true);
                    if (canvas == null) continue;
                    if (!hiddenWorldCanvases.ContainsKey(canvas)) hiddenWorldCanvases.Add(canvas, canvas.enabled);
                    canvas.enabled = false;
                }
            }
        }
        if (party == null) return;

        int maximum = Mathf.Max(0, party.MaxMovePoints);
        int remaining = Mathf.Clamp(party.RemainingMovePoints, 0, maximum);
        float width = segmentContainer.rect.width;
        if (maximum == lastMaximum && remaining == lastRemaining && Mathf.Approximately(width, lastWidth)) return;

        while (segments.Count < maximum)
        {
            var image = Instantiate(segmentTemplate, segmentContainer);
            image.name = "MovePoint_" + (segments.Count + 1);
            image.raycastTarget = false;
            segments.Add(image);
        }
        var layout = segmentContainer.GetComponent<HorizontalLayoutGroup>();
        float spacing = layout != null ? layout.spacing : 0;
        float padding = layout != null ? layout.padding.horizontal : 0;
        float cellWidth = maximum > 0 ? Mathf.Max(0, (width - padding - spacing * (maximum - 1)) / maximum) : 0;
        Sprite fill = remaining >= 8 ? greenFill : remaining >= 4 ? orangeFill : redFill;
        for (int i = 0; i < segments.Count; i++)
        {
            var image = segments[i];
            image.gameObject.SetActive(i < maximum);
            image.sprite = i < remaining && fill != null ? fill : segmentTemplate.sprite;
            //image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellWidth);
            var color = segmentTemplate.color;
            color.a *= i < remaining ? 1f : inactiveAlpha;
            image.color = color;
        }
        amountText.text = remaining + "/" + maximum;
        lastMaximum = maximum;
        lastRemaining = remaining;
        lastWidth = width;
    }

    private void OnDisable()
    {
        RestoreWorldBars();
        if (visibility != null) visibility.alpha = 0;
    }

    private void RestoreWorldBars()
    {
        foreach (var entry in hiddenWorldCanvases)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        hiddenWorldCanvases.Clear();
    }
}
