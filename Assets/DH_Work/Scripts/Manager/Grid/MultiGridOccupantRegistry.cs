using System.Collections.Generic;
using UnityEngine;

public class MultiGridOccupantRegistry : MonoBehaviour
{
    private readonly List<MultiGridOccupant> occupants = new List<MultiGridOccupant>();

    public IReadOnlyList<MultiGridOccupant> Occupants => occupants;

    private void Awake()
    {
        RefreshSceneOccupants();
    }

    public void Register(MultiGridOccupant occupant)
    {
        if (occupant == null || occupants.Contains(occupant))
            return;

        occupants.Add(occupant);
    }

    public void Unregister(MultiGridOccupant occupant)
    {
        if (occupant == null)
            return;

        occupants.Remove(occupant);
    }

    [ContextMenu("Refresh Scene Occupants")]
    public void RefreshSceneOccupants()
    {
        occupants.Clear();

        MultiGridOccupant[] sceneOccupants = FindObjectsByType<MultiGridOccupant>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneOccupants.Length; i++)
        {
            MultiGridOccupant occupant = sceneOccupants[i];
            if (occupant == null)
                continue;

            Register(occupant);
        }
    }
}
