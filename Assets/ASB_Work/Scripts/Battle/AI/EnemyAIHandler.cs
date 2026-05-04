using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAIHandler : MonoBehaviour
{
    [SerializeField] private int enemyAIIndex;

    public int EnemyAIIndex => enemyAIIndex;

    public void Configure(EnemyDataSO data)
    {
        if (data == null)
        {
            enemyAIIndex = 0;
            return;
        }

        enemyAIIndex = data.EnemyAIIndex;
        // AI Index 기반 행동 트리 선택 지점.
    }
}
