using System;
using UnityEngine;

public static class CheatCommands
{
    public const int ResourceCap = 999999;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        CheatCommandRegistry.Register("Tetra Anax", TetraAnax);
        CheatCommandRegistry.Register("Ain Soph Aur", AinSophAur);
        CheatCommandRegistry.Register("Logos", Logos);
        CheatCommandRegistry.Register("Malkuth", Malkuth);
    }

    private static string TetraAnax(string[] args)
    {
        var economy = GameManager.Instance != null ? GameManager.Instance.Economy : null;
        if (economy == null) return "[err] EconomyManager unavailable";

        const int delta = 99999;
        var sb = new System.Text.StringBuilder("Tetra Anax →");
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            int before = economy.Get(type);
            int target = Mathf.Min(ResourceCap, before + delta);
            economy.Set(type, target);
            sb.Append($" {type}:{before}→{target}");
        }
        return sb.ToString();
    }

    private static string AinSophAur(string[] args)
    {
        var economy = GameManager.Instance != null ? GameManager.Instance.Economy : null;
        if (economy == null) return "[err] EconomyManager unavailable";

        economy.ResetAll();
        return "Ain Soph Aur → all resources reset to 0";
    }

    private static string Logos(string[] args)
    {
        var bc = GameManager.Instance != null ? GameManager.Instance.Publicity : null;
        if (bc == null) return "[err] PublicityManager unavailable";

        const int delta = 100;
        bc.AddIPToAllHeroes(delta);
        return $"Logos → all heroes IP +{delta}";
    }

    private static string Malkuth(string[] args)
    {
        var hq = GameManager.Instance != null ? GameManager.Instance.HQ : null;
        if (hq == null) return "[err] HQStateManager unavailable";

        int n = hq.DebugUnlockAllFacilities();
        return $"Malkuth → 모든 시설 해금 (신규 {n}개)";
    }
}
