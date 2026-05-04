using UnityEngine;

public enum SkillEffectType
{
    Damage,
    Heal,
    Taunt,
    Buff,
    Debuff
}

[CreateAssetMenu(fileName = "SkillData", menuName = "ASB/Data/SkillData")]
public class SkillDataAsset : ScriptableObject
{
    [SerializeField] private string skillId;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private int power;
    [SerializeField] private float coolTime;


    public string SkillId => skillId;
    public string DisplayName => displayName;
    public string Description => description;
    public int Power => power;
    public float CoolTime => coolTime;
}
