using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleScenarioType
{
    None,
    HostageRescue
}

public enum HostageBattleState
{
    Safe,
    Injured
}

[Serializable]
public sealed class BattleScenarioConfig
{
    public BattleScenarioType ScenarioType;
    public HostageScenarioConfig HostageRescue;

    public bool IsHostageRescue =>
        ScenarioType == BattleScenarioType.HostageRescue &&
        HostageRescue != null &&
        HostageRescue.Hostages != null &&
        HostageRescue.Hostages.Count > 0;
}

[Serializable]
public sealed class HostageScenarioConfig
{
    public List<HostageSpawnConfig> Hostages = new List<HostageSpawnConfig>();
    public List<string> ThreatEnemyUnitKeys = new List<string>();
    public float FullHealthAggroGain = 1f;
    public float DamagedAggroGain = 2f;
    public float ThreatDamage = 10f;
    public float ThreatAggroReduction = 2f;
    public float ThreatChancePerTotalAggro = 0.1f;
    public float MaximumThreatChance = 1f;
    public float ThreatWindupSeconds = 0.25f;
    public float ThreatRecoverySeconds = 0.25f;

    public bool ContainsHostageUnit(string unitKey)
    {
        string normalized = BattleScenarioUnitKey.Normalize(unitKey);
        if (string.IsNullOrEmpty(normalized))
            return false;

        for (int i = 0; i < Hostages.Count; i++)
        {
            HostageSpawnConfig hostage = Hostages[i];
            if (hostage != null &&
                string.Equals(BattleScenarioUnitKey.Normalize(hostage.SourceUnitKey), normalized, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool CanEnemyThreaten(string unitKey)
    {
        string normalized = BattleScenarioUnitKey.Normalize(unitKey);
        if (string.IsNullOrEmpty(normalized))
            return false;

        for (int i = 0; i < ThreatEnemyUnitKeys.Count; i++)
        {
            if (string.Equals(
                    BattleScenarioUnitKey.Normalize(ThreatEnemyUnitKeys[i]),
                    normalized,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

[Serializable]
public sealed class HostageSpawnConfig
{
    public string HostageId;
    public string SourceUnitKey;
    public int Slot;
    public float MaxHp = 50f;
    public float InitialHp = 50f;
    public float Defense = 5f;
    public float InitialAggro;
    public string PrefabResourcePath;
}

public static class BattleScenarioUnitKey
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim().ToUpperInvariant();
        int firstDigit = -1;
        for (int i = 0; i < trimmed.Length; i++)
        {
            if (!char.IsDigit(trimmed[i]))
                continue;

            firstDigit = i;
            break;
        }

        return firstDigit >= 0 ? trimmed.Substring(firstDigit) : trimmed;
    }
}
