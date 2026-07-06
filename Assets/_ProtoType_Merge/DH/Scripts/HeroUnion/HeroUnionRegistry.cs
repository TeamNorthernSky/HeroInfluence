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
}
