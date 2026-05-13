using UnityEngine;

[DisallowMultipleComponent]
public class EnemyIdentity : MonoBehaviour
{
    [SerializeField] private string enemyId;
    // [JC 추가 260513] EnemyPartyPool 인스턴스 식별자 (씬 떠나도 풀이 보존)
    [SerializeField] private string instanceId;

    public string EnemyId => enemyId;
    public string InstanceId => instanceId;

    public void SetEnemyId(string nextEnemyId)
    {
        if (string.IsNullOrWhiteSpace(nextEnemyId))
            return;

        enemyId = nextEnemyId;
    }

    public void SetInstanceId(string nextInstanceId)
    {
        instanceId = string.IsNullOrWhiteSpace(nextInstanceId) ? string.Empty : nextInstanceId;
    }
}
