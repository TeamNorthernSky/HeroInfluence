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
    private readonly List<(ResourceGainNumberMotion motion, TMP_Text label, int value)> numbers =
        new List<(ResourceGainNumberMotion, TMP_Text, int)>();
    private Coroutine numberRoutine;
    public sealed class ValueSnapshot
    {
        public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public static ValueSnapshot Capture()
        {
            var snapshot = new ValueSnapshot();
            var economy = GameManager.Instance != null ? GameManager.Instance.Economy : null;
            if (economy != null)
            {
                snapshot.Values["Money"] = economy.Get(ResourceType.Money);
                snapshot.Values["Medal"] = economy.Get(ResourceType.Chip);
                snapshot.Values["Gem"] = economy.Get(ResourceType.Crystal);
                snapshot.Values["Supply"] = economy.Get(ResourceType.Supply);
            }
            var repository = PersistentUnitRepository.Instance;
            if (repository != null)
                foreach (var unit in repository.Units)
                {
                    if (unit == null || string.IsNullOrEmpty(unit.UnitTemplateKey)) continue;
                    string key = unit.UnitTemplateKey + "/";
                    if (snapshot.Values.ContainsKey(key + "IP")) continue;
                    snapshot.Values[key + "IP"] = Mathf.RoundToInt(unit.CurrentInfluence);
                    snapshot.Values[key + "HP"] = Mathf.RoundToInt(unit.IngameStats.HP);
                    snapshot.Values[key + "ATK"] = Mathf.RoundToInt(unit.IngameStats.Atk);
                    snapshot.Values[key + "DEF"] = Mathf.RoundToInt(unit.IngameStats.DEF);
                    snapshot.Values[key + "Heal"] = Mathf.RoundToInt(unit.CurrentHp);
                }
            return snapshot;
        }
    }
    private ValueSnapshot beforeValues;
    private readonly Dictionary<TMP_Text, string> valueKeys = new Dictionary<TMP_Text, string>();
    public void SetBeforeValues(ValueSnapshot snapshot) => beforeValues = snapshot;

    private void QueueNumber(TMP_Text label, int value, string prefix = "", string key = null)
    {
        // EXP is a per-unit grant, not a shared balance: keep its reward amount.
        if (key == null) { label.text = prefix + Signed(value); return; }
        var current = ValueSnapshot.Capture();
        int start = beforeValues != null && beforeValues.Values.TryGetValue(key, out int oldValue)
            ? oldValue : current.Values.TryGetValue(key, out int currentValue) ? currentValue : value;
        var motion = new ResourceGainNumberMotion(n => prefix + n.ToString("N0"), showNotices: false, animateColor: false);
        motion.Set(label, start);
        valueKeys[label] = key;
        numbers.Add((motion, label, value));
    }

    private System.Collections.IEnumerator PlayNumbersAfterEntrance()
    {
        yield return null; // Let layout and the modal entrance initialize first.
        var after = ValueSnapshot.Capture(); // Dispatch listeners have now all applied the reward.
        var modal = GetComponentInParent<ChatModalController>();
        while (modal != null && modal.IsEntrancePlaying) yield return null;
        foreach (var number in numbers)
            if (after.Values.TryGetValue(valueKeys[number.label], out int total))
                number.motion.Set(number.label, total);
        numberRoutine = null;
    }

    private void Update()
    {
        foreach (var number in numbers) number.motion.Advance(Time.unscaledDeltaTime);
    }

    private void ClearNumbers()
    {
        if (numberRoutine != null) StopCoroutine(numberRoutine);
        numberRoutine = null;
        foreach (var number in numbers) number.motion.Reset();
        numbers.Clear();
        valueKeys.Clear();
    }

    private void OnDisable() => ClearNumbers();

    public void Bind(DHEventRewardTemplate reward)
    {
        Bind(reward, ResolvePartyKeys());
    }

    // Callers with an explicit party can supply its template keys in display order.
    public void Bind(DHEventRewardTemplate reward, IReadOnlyList<string> partyKeys)
    {
        if (reward == null) return;
        ClearNumbers();
        Set("Money", reward.Money);
        Set("Medal", reward.Medal);
        Set("Gem", reward.Gem);
        Set("Supply", reward.Supply);
        TMP_Text exp = FindText("Experience");
        if (exp != null)
        {
            exp.gameObject.SetActive(reward.Exp != 0);
            if (reward.Exp != 0) QueueNumber(exp, reward.Exp, "EXP ");
        }
        BindCharacters(reward, partyKeys);
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        if (isActiveAndEnabled) numberRoutine = StartCoroutine(PlayNumbersAfterEntrance());
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
                BindStat(slot, rowIndex++, StatNames[stat], values[stat], spacing, key);
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

    private void BindStat(CharacterSlot slot, int index, string stat, int value, float spacing, string unitKey)
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
        label.color = value < 0 ? Red : Blue;
        QueueNumber(label, value, stat == "Heal" ? "회복 " : "", unitKey + "/" + stat);
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
        text.color = value < 0 ? Red : Blue;
        if (value == 0) text.text = "-";
        else QueueNumber(text, value, key: key);
    }

    private TMP_Text FindText(string key)
    {
        Transform card = transform.Find("Card") ?? transform;
        Transform child = card.Find(key);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
}
