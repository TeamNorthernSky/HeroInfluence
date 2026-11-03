using UnityEngine;

namespace JC.BattleTesting
{
    // Position the grid roots before the spawners cache their world-space cells in Awake.
    [DefaultExecutionOrder(-20000)]
    public sealed class JcBossHallFormation : MonoBehaviour
    {
        [SerializeField] private Transform enemyPlace;
        [SerializeField] private Vector3 hallOffset;

        private void Awake()
        {
            var boss = JcBossBattleBackground.Load();
            if (!enemyPlace || !boss || !boss.Matches(CombatContext.Instance)) return;
            transform.position += hallOffset;
            enemyPlace.position += hallOffset;
        }
    }
}
