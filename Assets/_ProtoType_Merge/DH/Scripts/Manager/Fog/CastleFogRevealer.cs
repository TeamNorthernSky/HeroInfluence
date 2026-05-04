using System.Collections.Generic;
using UnityEngine;

public class CastleFogRevealer : MonoBehaviour
{
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private CastleRegistry castleRegistry;
    [SerializeField, Min(0)] private int revealRadius = 1;
    [SerializeField] private bool revealCastlesOnEnable = true;

    private void OnEnable()
    {
        if (revealCastlesOnEnable)
            RevealAllCastles();
    }

    [ContextMenu("Reveal All Castles")]
    public void RevealAllCastles()
    {
        if (fogGridManager == null || castleRegistry == null)
            return;

        IReadOnlyList<CastleUnit> castles = castleRegistry.Castles;
        for (int i = 0; i < castles.Count; i++)
        {
            CastleUnit castle = castles[i];
            if (castle == null)
                continue;

            RevealCastleArea(castle);
        }
    }

    private void RevealCastleArea(CastleUnit castle)
    {
        if (fogGridManager == null || castle == null)
            return;

        MultiGridOccupant occupant = castle.GetComponent<MultiGridOccupant>();
        if (occupant != null)
        {
            IReadOnlyList<Vector2Int> occupiedCells = occupant.GetOccupiedCells();
            for (int i = 0; i < occupiedCells.Count; i++)
                fogGridManager.RevealArea(occupiedCells[i], revealRadius);

            return;
        }

        fogGridManager.RevealArea(castle.GetCurrentGrid(), revealRadius);
    }
}
