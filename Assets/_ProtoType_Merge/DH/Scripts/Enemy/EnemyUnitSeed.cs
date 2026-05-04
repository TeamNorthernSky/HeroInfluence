using UnityEngine;

[DisallowMultipleComponent]
public class EnemyUnitSeed : MonoBehaviour
{
    [Header("Initial Unit Data")]
    [SerializeField] private string unitTemplateKey;
    [SerializeField] private int level = 1;
    [SerializeField] private StatBlock baseStats;

    public string UnitTemplateKey => unitTemplateKey;
    public int Level => Mathf.Max(1, level);
    public StatBlock BaseStats => baseStats;
}
