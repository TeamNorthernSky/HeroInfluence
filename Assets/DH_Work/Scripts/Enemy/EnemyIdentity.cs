using UnityEngine;

[DisallowMultipleComponent]
public class EnemyIdentity : MonoBehaviour
{
    [SerializeField] private int enemyId;

    public int EnemyId => enemyId;

    public void SetEnemyId(int nextEnemyId)
    {
        if (nextEnemyId <= 0)
            return;

        enemyId = nextEnemyId;
    }
}
