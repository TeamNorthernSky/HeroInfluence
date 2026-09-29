using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ChatRewardResultView : MonoBehaviour
{
    private static readonly Color Blue = new Color32(0, 70, 180, 255);
    private static readonly Color Red = new Color32(210, 30, 40, 255);
    private static readonly string[] StatNames = { "IP", "HP", "ATK", "DEF", "Heal" };

    [Header("Character reward icons")]
    [SerializeField] private Sprite influenceIcon;
    [SerializeField] private Sprite healthIcon;
    [SerializeField] private Sprite attackIcon;
    [SerializeField] private Sprite defenseIcon;

    private sealed class CharacterSlot
    {
        public Transform Root;
        public UnityEngine.UI.Image Portrait;
        public RectTransform Template;
        public readonly List<RectTransform> Rows = new List<RectTransform>();
    }

    private readonly List<CharacterSlot> slots = new List<CharacterSlot>();
    private RectTransform partyRewards;

    public void Bind(DHEventRewardTemplate reward)
    {
        Bind(reward, ResolvePartyKeys());
    }

    // Callers with an explicit party can supply its template keys in display order.
    public void Bind(DHEventRewardTemplate reward, IReadOnlyList<string> partyKeys)
    {
        if (reward == null) return;
        Set("Money", reward.Money);
        Set("Medal", reward.Medal);
        Set("Gem", reward.Gem);
        Set("Supply", reward.Supply);
        TMP_Text exp = FindText("Experience");
        if (exp != null)
        {
            exp.gameObject.SetActive(reward.Exp != 0);
            exp.text = "EXP " + Signed(reward.Exp);
        }
        BindCharacters(reward, partyKeys);
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }

    private static IReadOnlyList<string> ResolvePartyKeys()
    {
        var keys = new List<string>();
        var units = PersistentUnitRepository.Instance;
        if (units == null) return keys;
        var parties = PartyPersistentRepository.Instance;
        var registry = FindFirstObjectByType<PartyRegistry>();
        var player = registry != null ? registry.PlayerParty : null;
        PartyPersistentData party = null;
        if (player != null && parties != null)
        {
            var identity = player.GetComponent<PartyIdentity>();
            if (identity != null) parties.TryGetParty(identity.PartyId, out party);
        }
        // Match the exploration HUD when no scene player party is available (e.g. HQ).
        if (player == null && parties != null && parties.Parties.Count > 0)
            party = parties.Parties[0];
        IReadOnlyList<int> indices;
        if (party != null)
            indices = PartyFormation.OrderFrontFirst(party.UnitIndices, party.UnitSlots);
        else
        {
            var composition = player != null ? player.GetComponent<PartyComposition>() : null;
            indices = PartyFormation.PackFrontFirst(composition != null ? composition.UnitIndices : null);
        }
        foreach (int index in indices)
        {
            // Keep the slot order even if a unit record has not been restored yet.
            keys.Add(units.TryGetUnit(index, out UnitPersistentData unit) && unit != null
                ? unit.UnitTemplateKey : null);
        }
        return keys;
    }

    private bool InitializeSlots()
    {
        if (partyRewards != null) return true;
        Transform card = transform.Find("Card") ?? transform;
        partyRewards = card.Find("Party Rewards") as RectTransform;
        if (partyRewards == null)
        {
            Debug.LogWarning("[ChatRewardResultView] Party Rewards is missing from the prefab.", this);
            return false;
        }
        foreach (Transform child in partyRewards)
        {
            if (!child.name.StartsWith("Character Rewards", System.StringComparison.Ordinal)) continue;
            Transform portrait = child.Find("Frame/Portrait");
            var template = child.Find("Reward") as RectTransform;
            var slot = new CharacterSlot
            {
                Root = child,
                Portrait = portrait != null ? portrait.GetComponent<UnityEngine.UI.Image>() : null,
                Template = template
            };
            if (template != null) slot.Rows.Add(template);
            slots.Add(slot);
        }
        return true;
    }

    private void BindCharacters(DHEventRewardTemplate reward, IReadOnlyList<string> partyKeys)
    {
        if (!InitializeSlots()) return;
        partyRewards.gameObject.SetActive(true);
        for (int i = 0; i < slots.Count; i++)
        {
            CharacterSlot slot = slots[i];
            foreach (RectTransform row in slot.Rows) row.gameObject.SetActive(false);
            string key = partyKeys != null && i < partyKeys.Count ? partyKeys[i] : null;
            bool occupied = !string.IsNullOrWhiteSpace(key);
            slot.Root.gameObject.SetActive(occupied);
            CreatePortrait(slot, key);
            if (!occupied || slot.Template == null) continue;
            var values = new int[StatNames.Length];
            foreach (DHEventCharacterRewardEntry entry in reward.CharacterRewards)
            {
                if (entry.UnitTemplateKey != key) continue;
                values[0] += entry.Influence;
                values[1] += entry.MaxHp;
                values[2] += entry.Atk;
                values[3] += entry.Def;
                values[4] += entry.Heal;
            }
            int count = 0;
            foreach (int value in values) if (value != 0) count++;
            float spacing = GetRowSpacing(slot, count);
            int rowIndex = 0;
            for (int stat = 0; stat < values.Length; stat++)
            {
                if (values[stat] == 0) continue;
                BindStat(slot, rowIndex++, StatNames[stat], values[stat], spacing);
            }
        }
    }

    private static void CreatePortrait(CharacterSlot slot, string key)
    {
        if (slot.Portrait == null) return;
        slot.Portrait.sprite = string.IsNullOrWhiteSpace(key) ? null : Sprites.Portrait.Hero(key);
        slot.Portrait.enabled = slot.Portrait.sprite != null;
        // Frame's mask and both RectTransforms remain exactly as authored in the prefab.
    }

    private float GetRowSpacing(CharacterSlot slot, int count)
    {
        RectTransform template = slot.Template;
        float height = template.rect.height * Mathf.Abs(template.localScale.y);
        if (count <= 1) return height;
        // Use the authored row height; compress only if the rows exceed the party panel.
        // Reserve half a row at the bottom, derived from the prefab rather than pixels.
        Vector3 panelBottom = slot.Root.InverseTransformPoint(partyRewards.TransformPoint(
            new Vector3(partyRewards.rect.center.x, partyRewards.rect.yMin, 0)));
        Bounds rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(slot.Root, template);
        float available = rowBounds.min.y - (panelBottom.y + height / 2);
        return Mathf.Min(height, Mathf.Max(0, available / (count - 1)));
    }

    private void BindStat(CharacterSlot slot, int index, string stat, int value, float spacing)
    {
        while (slot.Rows.Count <= index)
        {
            RectTransform clone = Instantiate(slot.Template, slot.Root, false);
            clone.name = "Reward_" + slot.Rows.Count;
            slot.Rows.Add(clone);
        }
        RectTransform row = slot.Rows[index];
        row.anchoredPosition = slot.Template.anchoredPosition + Vector2.down * (index * spacing);
        row.gameObject.SetActive(true);
        CreateIcon(row, GetStatIcon(stat));
        TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        // Preserve the row typography; use the prefab's resource font only for missing Korean glyphs.
        if (stat == "Heal" && label.font != null &&
            (!label.font.HasCharacter('회') || !label.font.HasCharacter('복')))
        {
            TMP_Text resourceStyle = FindText("Money");
            if (resourceStyle != null && resourceStyle.font != null)
                label.font = resourceStyle.font;
        }
        label.text = (stat == "Heal" ? "회복 " : "") + Signed(value);
        label.color = value < 0 ? Red : Blue;
    }

    private Sprite GetStatIcon(string stat)
    {
        switch (stat)
        {
            case "IP": return influenceIcon;
            case "HP":
            case "Heal": return healthIcon;
            case "ATK": return attackIcon;
            case "DEF": return defenseIcon;
            default: return null;
        }
    }

    private static void CreateIcon(Transform row, Sprite sprite)
    {
        Transform icon = row.Find("Icon");
        UnityEngine.UI.Image image = icon != null ? icon.GetComponent<UnityEngine.UI.Image>() : null;
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void Set(string key, int value)
    {
        TMP_Text text = FindText(key);
        if (text == null) return;
        text.gameObject.SetActive(true);
        text.text = value == 0 ? "-" : Signed(value);
        text.color = value < 0 ? Red : Blue;
    }

    private TMP_Text FindText(string key)
    {
        Transform card = transform.Find("Card") ?? transform;
        Transform child = card.Find(key);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
}
