using UnityEngine;

[DisallowMultipleComponent]
public class EnemyIdentity : MonoBehaviour
{
    [SerializeField] private string enemyId;

    public string EnemyId => enemyId;

    public void SetEnemyId(string nextEnemyId)
    {
        if (string.IsNullOrWhiteSpace(nextEnemyId))
            return;

        enemyId = nextEnemyId;
    }
}
