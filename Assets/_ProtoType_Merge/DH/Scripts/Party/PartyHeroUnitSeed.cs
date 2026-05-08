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
    [SerializeField] private int initialSkillIndex;
    [SerializeField] private int initialWeaponIndex;

    public string UnitTemplateKey => unitTemplateKey;
    public int Level => Mathf.Max(1, level);
    public int Favorability => Mathf.Max(0, favorability);
    public int InitialSkillIndex => Mathf.Max(0, initialSkillIndex);
    public int InitialWeaponIndex => Mathf.Max(0, initialWeaponIndex);

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(unitTemplateKey) && legacyJobIndex > 0)
            unitTemplateKey = legacyJobIndex.ToString();
    }
}
