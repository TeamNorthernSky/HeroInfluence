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

    [Header("Animation (Hybrid)")]
    [SerializeField] private string animationTrigger = "Attack";
    [SerializeField] private string stateName;
    [SerializeField] private bool useAnimEvent;
    [SerializeField] private float hitDelay = 0.25f;
    [SerializeField] private float totalDelay = 0.5f;

    public string SkillId => skillId;
    public string DisplayName => displayName;
    public string Description => description;
    public int Power => power;
    public float CoolTime => coolTime;
    public string AnimationTrigger => animationTrigger;
    public string StateName => stateName;
    public bool UseAnimEvent => useAnimEvent;
    public float HitDelay => hitDelay;
    public float TotalDelay => totalDelay;
}
