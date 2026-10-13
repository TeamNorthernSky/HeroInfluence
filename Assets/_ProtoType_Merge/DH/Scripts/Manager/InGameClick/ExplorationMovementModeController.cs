using UnityEngine;

public enum ExplorationMovementMode
{
    FogLegacy = 0,
    MoveRange = 1
}

public class ExplorationMovementModeController : MonoBehaviour
{
    [SerializeField] private ExplorationMovementMode movementMode = ExplorationMovementMode.FogLegacy;

    public ExplorationMovementMode MovementMode => movementMode;
    public bool UsesMoveRange => movementMode == ExplorationMovementMode.MoveRange;

    public void SetMovementMode(ExplorationMovementMode nextMode)
    {
        movementMode = nextMode;
    }
}
