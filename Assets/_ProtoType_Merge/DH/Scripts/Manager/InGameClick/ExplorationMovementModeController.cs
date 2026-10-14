using UnityEngine;

public enum ExplorationMovementMode
{
    DirectPath = 0,
    MoveRange = 1
}

public class ExplorationMovementModeController : MonoBehaviour
{
    [SerializeField] private ExplorationMovementMode movementMode = ExplorationMovementMode.DirectPath;

    public ExplorationMovementMode MovementMode => movementMode;
    public bool UsesMoveRange => movementMode == ExplorationMovementMode.MoveRange;

    public void SetMovementMode(ExplorationMovementMode nextMode)
    {
        movementMode = nextMode;
    }
}
