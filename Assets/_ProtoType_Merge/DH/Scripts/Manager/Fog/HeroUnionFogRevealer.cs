using System.Collections.Generic;
using UnityEngine;

public class HeroUnionFogRevealer : MonoBehaviour
{
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private HeroUnionRegistry heroUnionRegistry;
    [SerializeField, Min(0)] private int revealRadius = 1;
    [SerializeField] private bool revealHeroUnionsOnEnable = true;

    private void OnEnable()
    {
        if (revealHeroUnionsOnEnable)
            RevealAllHeroUnions();
    }

    [ContextMenu("Reveal All HeroUnions")]
    public void RevealAllHeroUnions()
    {
        if (fogGridManager == null || heroUnionRegistry == null)
            return;

        IReadOnlyList<HeroUnionUnit> heroUnions = heroUnionRegistry.HeroUnions;
        for (int i = 0; i < heroUnions.Count; i++)
        {
            HeroUnionUnit heroUnion = heroUnions[i];
            if (heroUnion == null)
                continue;

            RevealHeroUnionArea(heroUnion);
        }
    }

    private void RevealHeroUnionArea(HeroUnionUnit heroUnion)
    {
        if (fogGridManager == null || heroUnion == null)
            return;

        MultiGridOccupant occupant = heroUnion.GetComponent<MultiGridOccupant>();
        if (occupant != null)
        {
            IReadOnlyList<Vector2Int> occupiedCells = occupant.GetOccupiedCells();
            for (int i = 0; i < occupiedCells.Count; i++)
                fogGridManager.RevealArea(occupiedCells[i], revealRadius);

            return;
        }

        fogGridManager.RevealArea(heroUnion.GetCurrentGrid(), revealRadius);
    }
}
