using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Exploration-local selection UI; uses the existing publicity controller through a serialized event.
public sealed class ExplorationPublicitySelection : MonoBehaviour
{
    public RectTransform content;
    public HeroProfileButton itemPrefab;
    public UnityEvent<int> selectHero = new UnityEvent<int>();
    private readonly List<HeroProfileButton> cards = new List<HeroProfileButton>();
    private bool ready;

    private void OnEnable() { ready = false; Rebuild(); }
    private void Update() { if (!ready) Rebuild(); }
    private void OnDisable()
    {
        foreach (var card in cards) if (card != null) Destroy(card.gameObject);
        cards.Clear();
    }
    private void Rebuild()
    {
        var parties = PartyPersistentRepository.Instance;
        var units = PersistentUnitRepository.Instance;
        if (parties == null || units == null || itemPrefab == null || content == null) return;
        if (parties.Parties.Count == 0 || parties.Parties[0] == null) return;
        var party = parties.Parties[0];
        var members = PartyFormation.OrderFrontFirst(party.UnitIndices, party.UnitSlots);
        if (members.Count == 0) return;
        ready = true;
        foreach (int id in members)
        {
            if (!units.TryGetUnit(id, out var unit) || unit == null) continue;
            DHPlayerUnitTemplate template = null;
            if (DHCsvTemplateCatalog.Instance != null)
                DHCsvTemplateCatalog.Instance.TryGetPlayerUnitTemplate(unit.UnitTemplateKey, out template);
            var card = Instantiate(itemPrefab, content);
            card.gameObject.SetActive(true);
            card.BindForSelect(id, Select, unit, template, true);
            cards.Add(card);
        }
        if (cards.Count > 0) Select(cards[0].UnitIndex);
    }
    public static int ResolveFirstPartyUnit()
    {
        var parties = PartyPersistentRepository.Instance;
        var units = PersistentUnitRepository.Instance;
        if (parties == null || units == null || parties.Parties.Count == 0 || parties.Parties[0] == null)
            return -1;
        var party = parties.Parties[0];
        foreach (int id in PartyFormation.OrderFrontFirst(party.UnitIndices, party.UnitSlots))
            if (units.TryGetUnit(id, out var unit) && unit != null) return id;
        return -1;
    }

    private void Select(int unitIndex)
    {
        selectHero.Invoke(unitIndex);
        foreach (var card in cards) if (card != null) card.SetSelected(card.UnitIndex == unitIndex);
    }
}