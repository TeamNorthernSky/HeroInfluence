using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class PartyHeroUnitSeed : MonoBehaviour
{
    [Header("Initial Unit Data")]
    [SerializeField] private string unitTemplateKey;
    [FormerlySerializedAs("jobIndex")]
    [SerializeField, HideInInspector] private int legacyJobIndex;
    [SerializeField] private int level = 1;
    [SerializeField] private int favorability;
    [SerializeField] private StatBlock baseStats;

    public string UnitTemplateKey => unitTemplateKey;
    public int Level => Mathf.Max(1, level);
    public int Favorability => Mathf.Max(0, favorability);
    public StatBlock BaseStats => baseStats;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(unitTemplateKey) && legacyJobIndex > 0)
            unitTemplateKey = legacyJobIndex.ToString();
    }
}
