using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "ASB/Data/Enemy Data")]
public class EnemyDataSO : ScriptableObject, IUnitData
{
    [Header("Identity")]
    [SerializeField] private string _unitId;
    [SerializeField] private string _unitName;

    [Header("Combat Stats")]
    [SerializeField] private float _maxHp = 1f;
    [SerializeField] private float _attack = 1f;
    [SerializeField] private float _defense;
    [SerializeField] private float _speed;

    [Header("Enemy Extra")]
    [SerializeField] private int enemyAIIndex;
    [SerializeField] private string dropTable;
    [SerializeField] private string enemyConcept;

    public string unitId => _unitId;
    public string unitName => _unitName;
    public float maxHp => Mathf.Max(1f, _maxHp);
    public float attack => Mathf.Max(1f, _attack);
    public float defense => Mathf.Max(0f, _defense);
    public float speed => Mathf.Max(0f, _speed);
    public TeamType teamType => TeamType.Enemy;

    public int EnemyAIIndex => enemyAIIndex;
    public string DropTable => dropTable;
    public string EnemyConcept => enemyConcept;
}
