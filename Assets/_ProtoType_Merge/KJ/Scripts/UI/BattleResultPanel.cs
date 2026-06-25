using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleResultPanel : MonoBehaviour
{
    [Header("Skill Selection")]
    [SerializeField] private SkillSelectionPanel getSkillSlotPrefab;

    public event System.Action OnAccepted;

    private readonly List<SkillSelectionResult> skillResults = new List<SkillSelectionResult>();
    private int pendingSlotCount = 0;

    public void Show(BattleResult result, BattleRewardPlan plan)
    {
        skillResults.Clear();
        pendingSlotCount = 0;

        BattleResultView view = GetComponent<BattleResultView>();
        if (view == null) return;

        if (view.acceptButton != null)
        {
            view.acceptButton.gameObject.SetActive(false);
            view.acceptButton.onClick.AddListener(() => OnAccepted?.Invoke());
        }

        BuildSlots(plan, view);
        BuildSkillSlots(plan, result, view);

        if (pendingSlotCount == 0 && view.acceptButton != null)
            view.acceptButton.gameObject.SetActive(true);
    }

    private void BuildSlots(BattleRewardPlan plan, BattleResultView view)
    {
        if (view.heroIndex == null || view.heroInfoResultPrefab == null) return;

        foreach (Transform child in view.heroIndex)
            Destroy(child.gameObject);

        CombatContext context = FindAnyObjectByType<CombatContext>();
        if (context == null) return;
        var unitIndices = context.CombatParty?.UnitIndices;
        if (unitIndices == null) return;

        for (int i = 0; i < unitIndices.Count; i++)
        {
            if (unitIndices[i] == 0) continue;

            HeroInfoResult slot = Instantiate(view.heroInfoResultPrefab, view.heroIndex, false);

            UnitRewardPreview preview = null;
            if (plan != null)
                preview = plan.UnitPreviews.Find(p => p.UnitIndex == unitIndices[i])
                          ?? (i < plan.UnitPreviews.Count ? plan.UnitPreviews[i] : null);

            if (preview != null)
                slot.Apply(preview);
        }
    }

    private void BuildSkillSlots(BattleRewardPlan plan, BattleResult result, BattleResultView view)
    {
        if (result != BattleResult.Victory || plan == null) return;
        if (getSkillSlotPrefab == null || view.skillSlotParent == null) return;

        foreach (var preview in plan.UnitPreviews)
        {
            if (!preview.HasLevelUp || (preview.UnlockCandidateSkillIds?.Count ?? 0) == 0)
                continue;

            SkillSelectionPanel slot = Instantiate(getSkillSlotPrefab, view.skillSlotParent, false);
            slot.Setup(preview);
            pendingSlotCount++;

            int unitIndex = preview.UnitIndex;
            Button acceptButton = view.acceptButton;
            slot.OnCompleted += (selectedSkillId) =>
            {
                if (selectedSkillId >= 0)
                    skillResults.Add(new SkillSelectionResult { UnitIndex = unitIndex, SelectedSkillId = selectedSkillId });

                pendingSlotCount--;
                if (pendingSlotCount <= 0 && acceptButton != null)
                    acceptButton.gameObject.SetActive(true);
            };
        }
    }

    public List<SkillSelectionResult> GetSkillResults() => skillResults;
}
