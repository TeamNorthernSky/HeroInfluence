using UnityEngine;

public enum SkillButtonType { ClassSkill, WeaponSkill }

[RequireComponent(typeof(HoverTooltip))]
public class SkillButtonTooltip : MonoBehaviour
{
    [SerializeField] private SkillButtonType skillType = SkillButtonType.ClassSkill;
    [SerializeField] private BattleFlowManager battleFlowManager;

    private HoverTooltip hoverTooltip;

    private void Awake()
    {
        hoverTooltip = GetComponent<HoverTooltip>();
        if (battleFlowManager == null)
            battleFlowManager = FindFirstObjectByType<BattleFlowManager>();
    }

    private void OnEnable()
    {
        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted += OnTurnStarted;
    }

    private void OnDisable()
    {
        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted -= OnTurnStarted;
    }

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (unit == null || !unit.IsPlayer) return;

        SkillData skill = null;

        if (skillType == SkillButtonType.ClassSkill)
        {
            unit.ResolveSelectedSkill();
            skill = unit.SelectedSkillData;
        }
        else
        {
            skill = unit.EquippedWeaponData?.ToSkillData();
        }

        if (skill == null) return;
        hoverTooltip.SetContent(skill.skillName, SkillDescriptionBuilder.Build(skill));
    }
}
