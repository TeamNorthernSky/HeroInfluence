using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillSelectionPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI unitNameText;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI levelUpText;
    [SerializeField] private TMP_Dropdown skillDropdown;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI prevSkillText;
    [SerializeField] private TextMeshProUGUI nextSkillText;
    [SerializeField] private Transform portrait;

    private List<int> candidateSkillIds = new List<int>();

    public event System.Action<int> OnCompleted; // 확인: selectedSkillId, 취소: -1

    private void Awake()
    {
        if (prevSkillText == null)
        {
            Transform t = transform.Find("PrevSkill/Explanation/Text");
            if (t != null) prevSkillText = t.GetComponent<TextMeshProUGUI>();
        }

        if (nextSkillText == null)
        {
            Transform t = transform.Find("NextSkill/Explanation/Text");
            if (t != null) nextSkillText = t.GetComponent<TextMeshProUGUI>();
        }

        confirmButton?.onClick.AddListener(OnConfirm);
        cancelButton?.onClick.AddListener(OnCancel);
        skillDropdown?.onValueChanged.AddListener(OnDropdownChanged);
    }

    public void Setup(UnitRewardPreview preview)
    {
        if (unitNameText != null)
            unitNameText.text = preview.UnitName;

        if (rankText != null)
            rankText.text = UnitRankLookup.GetRank(preview.NewLevel);

        if (levelUpText != null)
            levelUpText.text = $"Lv.{preview.OldLevel} → {preview.NewLevel}";

        // 초상화
        if (portrait != null)
        {
            // [JC 260621] 포트레이트 = PortraitLibrary(키=HeroIndex).
            Sprite sp = Sprites.Portrait.HeroByUnit(preview.UnitIndex);
            if (sp != null)
            {
                GameObject rawObj = new GameObject("Portrait_Image", typeof(RectTransform), typeof(Image));
                rawObj.transform.SetParent(portrait, false);
                rawObj.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
                RectTransform rt = rawObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rawObj.GetComponent<Image>().sprite = sp;
            }
        }

        // 현재 ClassSkill 정보 표시
        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;

        DHClassSkillTemplate currentSkill = null;
        catalog?.TryGetClassSkillTemplate(preview.CurrentClassSkillId, out currentSkill);
        if (prevSkillText != null)
            prevSkillText.text = currentSkill != null ? SkillDescriptionBuilder.Build(currentSkill) : "-";

        candidateSkillIds.Clear();
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (int skillId in preview.UnlockCandidateSkillIds)
        {
            DHClassSkillTemplate skillTemplate = null;
            catalog?.TryGetClassSkillTemplate(skillId, out skillTemplate);
            string label = skillTemplate != null ? skillTemplate.SkillName : $"Skill {skillId}";
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

        // 첫 번째 후보 스킬 정보 표시
        UpdateNextSkillText(0);
    }

    private void OnDropdownChanged(int index)
    {
        UpdateNextSkillText(index);
    }

    private void UpdateNextSkillText(int index)
    {
        if (nextSkillText == null || index < 0 || index >= candidateSkillIds.Count) return;

        DHClassSkillTemplate nextSkill = null;
        DHCsvTemplateCatalog.Instance?.TryGetClassSkillTemplate(candidateSkillIds[index], out nextSkill);
        nextSkillText.text = nextSkill != null ? SkillDescriptionBuilder.Build(nextSkill) : "-";
    }

    private bool completed = false;

    private void OnConfirm()
    {
        if (completed) return;
        completed = true;
        int selectedId = (skillDropdown != null && skillDropdown.value < candidateSkillIds.Count)
            ? candidateSkillIds[skillDropdown.value]
            : -1;
        // [KJ 260729] Destroy는 프레임 끝에 처리되므로 루트를 먼저 끈다.
        //   그러지 않으면 OnCompleted가 켜는 다음 창과 한 프레임 겹쳐 보인다.
        gameObject.SetActive(false);
        OnCompleted?.Invoke(selectedId);
        Destroy(gameObject);
    }

    private void OnCancel()
    {
        if (completed) return;
        completed = true;
        // [KJ 260729] OnConfirm과 동일 — 다음 창과의 1프레임 겹침 방지.
        gameObject.SetActive(false);
        OnCompleted?.Invoke(-1);
        Destroy(gameObject);
    }
}
