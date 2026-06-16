using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillSelectionPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI unitNameText;
    [SerializeField] private TextMeshProUGUI levelUpText;
    [SerializeField] private TMP_Dropdown skillDropdown;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private List<int> candidateSkillIds = new List<int>();

    public event System.Action<int> OnCompleted; // 확인: selectedSkillId, 취소: -1

    private void Awake()
    {
        confirmButton?.onClick.AddListener(OnConfirm);
        cancelButton?.onClick.AddListener(OnCancel);
    }

    public void Setup(UnitRewardPreview preview)
    {
        if (unitNameText != null)
            unitNameText.text = preview.UnitName;

        if (levelUpText != null)
            levelUpText.text = $"Lv.{preview.OldLevel} → {preview.NewLevel}";

        candidateSkillIds.Clear();
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (int skillId in preview.UnlockCandidateSkillIds)
        {
            SkillData skillData = DHCsvTemplateCatalog.Instance?.GetSkillTemplate(skillId);
            string label = skillData != null ? skillData.skillName : $"Skill {skillId}";
            options.Add(new TMP_Dropdown.OptionData(label));
            candidateSkillIds.Add(skillId);
        }

        if (skillDropdown != null)
        {
            skillDropdown.ClearOptions();
            skillDropdown.AddOptions(options);
            skillDropdown.value = 0;
            skillDropdown.RefreshShownValue();
        }
    }

    private void OnConfirm()
    {
        int selectedId = (skillDropdown != null && skillDropdown.value < candidateSkillIds.Count)
            ? candidateSkillIds[skillDropdown.value]
            : -1;
        OnCompleted?.Invoke(selectedId);
        Destroy(gameObject);
    }

    private void OnCancel()
    {
        OnCompleted?.Invoke(-1);
        Destroy(gameObject);
    }
}
