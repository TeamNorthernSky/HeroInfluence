using System.Collections.Generic;
using UnityEngine;

public class HeroUnionRegistry : MonoBehaviour
{
    private readonly List<HeroUnionUnit> heroUnions = new List<HeroUnionUnit>();

    public IReadOnlyList<HeroUnionUnit> HeroUnions => heroUnions;

    private void Awake()
    {
        RefreshSceneHeroUnions();
    }

    public void Register(HeroUnionUnit heroUnion)
    {
        if (heroUnion == null || heroUnions.Contains(heroUnion))
            return;

        heroUnions.Add(heroUnion);
    }

    public void Unregister(HeroUnionUnit heroUnion)
    {
        if (heroUnion == null)
            return;

        heroUnions.Remove(heroUnion);
    }

    [ContextMenu("Refresh Scene HeroUnions")]
    public void RefreshSceneHeroUnions()
    {
        heroUnions.Clear();

        HeroUnionUnit[] sceneHeroUnions = FindObjectsByType<HeroUnionUnit>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneHeroUnions.Length; i++)
        {
            HeroUnionUnit heroUnion = sceneHeroUnions[i];
            if (heroUnion == null)
                continue;

            Register(heroUnion);
        }
    }

    public bool TryGetByZoneId(string zoneId, out HeroUnionUnit heroUnion)
    {
        string normalizedZoneId = NormalizeZoneId(zoneId);
        heroUnion = null;

        for (int i = 0; i < heroUnions.Count; i++)
        {
            HeroUnionUnit candidate = heroUnions[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.ZoneId, normalizedZoneId, System.StringComparison.Ordinal))
                continue;

            heroUnion = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetClaimedByZoneId(string zoneId, out HeroUnionUnit heroUnion)
    {
        if (TryGetByZoneId(zoneId, out heroUnion) && heroUnion != null && heroUnion.IsClaimedByHero)
            return true;

        heroUnion = null;
        return false;
    }

    public bool TryGetFirstClaimed(out HeroUnionUnit heroUnion)
    {
        heroUnion = null;

        for (int i = 0; i < heroUnions.Count; i++)
        {
            HeroUnionUnit candidate = heroUnions[i];
            if (candidate == null || !candidate.IsClaimedByHero)
                continue;

            heroUnion = candidate;
            return true;
        }

        return false;
    }

    public HeroUnionUnit GetClosestHeroUnion(Vector2Int grid)
    {
        HeroUnionUnit closest = null;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < heroUnions.Count; i++)
        {
            HeroUnionUnit heroUnion = heroUnions[i];
            if (heroUnion == null)
                continue;

            int distance = GridManager.GridDistance(grid, heroUnion.GetCurrentGrid());
            if (distance >= closestDistance)
                continue;

            closest = heroUnion;
            closestDistance = distance;
        }

        return closest;
    }

    public HeroUnionUnit GetClosestClaimedHeroUnion(Vector2Int grid)
    {
        HeroUnionUnit closest = null;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < heroUnions.Count; i++)
        {
            HeroUnionUnit heroUnion = heroUnions[i];
            if (heroUnion == null || !heroUnion.IsClaimedByHero)
                continue;

            int distance = GridManager.GridDistance(grid, heroUnion.GetCurrentGrid());
            if (distance >= closestDistance)
                continue;

            closest = heroUnion;
            closestDistance = distance;
        }

        return closest;
    }

    private static string NormalizeZoneId(string zoneId)
    {
        string normalized = MapProgressKey.NormalizeSegment(zoneId);
        return string.IsNullOrWhiteSpace(normalized) ? "zone_001" : normalized;
    }
}