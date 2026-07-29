using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BattleScenarioCatalog", menuName = "ASB/Battle/Event Battle Scenario Catalog")]
public sealed class BattleScenarioCatalog : ScriptableObject
{
    public const string DefaultResourcesPath = "BattleScenario/BattleScenarioCatalog";

    [SerializeField] private List<HostageScenarioDefinition> hostageScenarios =
        new List<HostageScenarioDefinition>();

    public IReadOnlyList<HostageScenarioDefinition> HostageScenarios => hostageScenarios;

    public bool TryGetHostageScenario(int zoneId, string battleKey, out HostageScenarioDefinition definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(battleKey))
            return false;

        string normalizedBattleKey = battleKey.Trim();
        for (int i = 0; i < hostageScenarios.Count; i++)
        {
            HostageScenarioDefinition candidate = hostageScenarios[i];
            if (candidate == null || candidate.ZoneId != zoneId)
                continue;

            if (!string.Equals(candidate.BattleKey?.Trim(), normalizedBattleKey, StringComparison.OrdinalIgnoreCase))
                continue;

            definition = candidate;
            return true;
        }

        return false;
    }

    public static BattleScenarioCatalog LoadDefault()
    {
        return Resources.Load<BattleScenarioCatalog>(DefaultResourcesPath);
    }
}

[Serializable]
public sealed class HostageScenarioDefinition
{
    public int ZoneId = 1;
    public string BattleKey;
    [Range(0f, 1f)] public float InitialHpRatio = 1f;
    public List<HostageDefinitionEntry> Hostages = new List<HostageDefinitionEntry>();
    public List<string> ThreatEnemyUnitKeys = new List<string>();
    public float FullHealthAggroGain = 1f;
    public float DamagedAggroGain = 2f;
    public float ThreatDamage = 10f;
    public float ThreatAggroReduction = 2f;
    public float ThreatChancePerTotalAggro = 0.1f;
    public float MaximumThreatChance = 1f;
    public float ThreatWindupSeconds = 0.25f;
    public float ThreatRecoverySeconds = 0.25f;
}

[Serializable]
public sealed class HostageDefinitionEntry
{
    public string HostageId;
    public string SourceUnitKey;
    public string PrefabResourcePath;
}
