using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PersistentUnitRepository : MonoBehaviour
{
    public static PersistentUnitRepository Instance { get; private set; }

    [Header("Persistent Units")]
    [SerializeField] private int nextUnitIndex = 1;
    [SerializeField] private List<UnitPersistentData> units = new List<UnitPersistentData>();
    [Header("Persistent Parties")]
    [SerializeField] private List<PartyPersistentData> parties = new List<PartyPersistentData>();
    [Header("Combat Context")]
    [SerializeField] private CombatPartyPersistentData combatParty;

    private readonly Dictionary<int, UnitPersistentData> unitLookup = new Dictionary<int, UnitPersistentData>();
    private readonly Dictionary<string, PartyPersistentData> partyLookup = new Dictionary<string, PartyPersistentData>();

    public IReadOnlyList<UnitPersistentData> Units => units;
    public IReadOnlyList<PartyPersistentData> Parties => parties;
    public CombatPartyPersistentData CombatParty => combatParty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public int CreateUnit()
    {
        return CreateUnit(string.Empty, 1, 0, default, 0, 0, default);
    }

    public int CreateUnit(string unitTemplateKey, int level, int favorability, StatBlock baseStats)
    {
        return CreateUnit(unitTemplateKey, level, favorability, baseStats, 0, 0, default);
    }

    public int CreateUnit(string unitTemplateKey, int level, int favorability, StatBlock baseStats, int currentSkillIndex, int currentWeaponIndex)
    {
        return CreateUnit(unitTemplateKey, level, favorability, baseStats, currentSkillIndex, currentWeaponIndex, default);
    }

    public int CreateUnit(string unitTemplateKey, int level, int favorability, StatBlock baseStats, int currentSkillIndex, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats)
    {
        int unitIndex = Mathf.Max(1, nextUnitIndex);
        nextUnitIndex = unitIndex + 1;

        var data = new UnitPersistentData(unitIndex, unitTemplateKey, level, favorability, baseStats, currentSkillIndex, currentWeaponIndex, currentWeaponStats);
        units.Add(data);
        unitLookup[unitIndex] = data;
        return unitIndex;
    }

    public bool ContainsUnit(int unitIndex)
    {
        return unitLookup.ContainsKey(unitIndex);
    }

    public bool TryGetUnit(int unitIndex, out UnitPersistentData data)
    {
        return unitLookup.TryGetValue(unitIndex, out data);
    }

    public bool RemoveUnit(int unitIndex)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        unitLookup.Remove(unitIndex);
        units.Remove(data);
        return true;
    }

    public void RegisterOrUpdateParty(string partyId, IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(partyId))
            return;

        if (partyLookup.TryGetValue(partyId, out PartyPersistentData existingData))
        {
            existingData.SetUnitIndices(unitIndices);
            return;
        }

        PartyPersistentData newData = new PartyPersistentData(partyId, unitIndices);
        parties.Add(newData);
        partyLookup[partyId] = newData;
    }

    public bool ContainsParty(string partyId)
    {
        return !string.IsNullOrWhiteSpace(partyId) && partyLookup.ContainsKey(partyId);
    }

    public bool TryGetParty(string partyId, out PartyPersistentData data)
    {
        if (string.IsNullOrWhiteSpace(partyId))
        {
            data = null;
            return false;
        }

        return partyLookup.TryGetValue(partyId, out data);
    }

    public bool RemoveParty(string partyId)
    {
        if (string.IsNullOrWhiteSpace(partyId) || !partyLookup.TryGetValue(partyId, out PartyPersistentData data))
            return false;

        partyLookup.Remove(partyId);
        parties.Remove(data);
        return true;
    }

    public void RegisterCombatParty(string partyId, IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(partyId))
            return;

        if (combatParty == null)
        {
            combatParty = new CombatPartyPersistentData(partyId, unitIndices);
            return;
        }

        combatParty.SetPartyId(partyId);
        combatParty.SetUnitIndices(unitIndices);
    }

    public void ClearCombatParty()
    {
        combatParty = null;
    }

    public void ClearAllUnits()
    {
        units.Clear();
        unitLookup.Clear();
        parties.Clear();
        partyLookup.Clear();
        combatParty = null;
        nextUnitIndex = 1;
    }

    private void RebuildLookup()
    {
        unitLookup.Clear();
        partyLookup.Clear();

        int highestIndex = 0;
        for (int i = 0; i < units.Count; i++)
        {
            UnitPersistentData data = units[i];
            if (data == null)
                continue;

            int unitIndex = data.UnitIndex;
            if (unitIndex <= 0)
                continue;

            if (unitLookup.ContainsKey(unitIndex))
            {
                Debug.LogWarning($"PersistentUnitRepository has duplicate unitIndex '{unitIndex}'.", this);
                continue;
            }

            unitLookup.Add(unitIndex, data);
            if (unitIndex > highestIndex)
                highestIndex = unitIndex;
        }

        for (int i = 0; i < parties.Count; i++)
        {
            PartyPersistentData data = parties[i];
            if (data == null || string.IsNullOrWhiteSpace(data.PartyId))
                continue;

            if (partyLookup.ContainsKey(data.PartyId))
            {
                Debug.LogWarning($"PersistentUnitRepository has duplicate partyId '{data.PartyId}'.", this);
                continue;
            }

            partyLookup.Add(data.PartyId, data);
        }

        if (nextUnitIndex <= highestIndex)
            nextUnitIndex = highestIndex + 1;
    }
}
