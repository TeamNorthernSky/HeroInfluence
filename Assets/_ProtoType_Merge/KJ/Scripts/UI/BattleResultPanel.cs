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

    // [JC 260628] 스킬 획득 창 순차 표시용(한 번에 하나씩, 전열→후열 순)
    private readonly List<SkillSelectionPanel> skillQueue = new List<SkillSelectionPanel>();
    private int currentSkillIndex = 0;

    public void Show(BattleResult result, BattleRewardPlan plan)
    {
        skillResults.Clear();
        pendingSlotCount = 0;
        skillQueue.Clear();
        currentSkillIndex = 0;

        BattleResultView view = GetComponent<BattleResultView>();
        if (view == null) return;

        if (view.acceptButton != null)
        {
            view.acceptButton.gameObject.SetActive(false);
            view.acceptButton.onClick.AddListener(() => OnAccepted?.Invoke());
        }

        // [JC 260628] 아군 표시 순서 = 전열→후열(PartyFormation 공용 규약). 전력평가·탐사 HeroBtn과 일치.
        CombatContext context = FindAnyObjectByType<CombatContext>();
        var ordered = PartyFormation.PackFrontFirst(context != null ? context.CombatParty?.UnitIndices : null);

        BuildSlots(plan, view, ordered);
        BuildSkillSlots(plan, result, view, ordered);

        if (pendingSlotCount == 0 && view.acceptButton != null)
            view.acceptButton.gameObject.SetActive(true);
    }

    private void BuildSlots(BattleRewardPlan plan, BattleResultView view, IReadOnlyList<int> orderedUnitIndices)
    {
        if (view.heroIndex == null || view.heroInfoResultPrefab == null) return;

        foreach (Transform child in view.heroIndex)
            Destroy(child.gameObject);

        if (orderedUnitIndices == null) return;

        for (int i = 0; i < orderedUnitIndices.Count; i++)
        {
            int unitIndex = orderedUnitIndices[i];
            if (unitIndex == 0) continue;

            HeroInfoResult slot = Instantiate(view.heroInfoResultPrefab, view.heroIndex, false);

            UnitRewardPreview preview = null;
            if (plan != null && plan.UnitPreviews != null)
                preview = plan.UnitPreviews.Find(p => p.UnitIndex == unitIndex);

            if (preview != null)
                slot.Apply(preview);
        }
    }

    private void BuildSkillSlots(BattleRewardPlan plan, BattleResult result, BattleResultView view, IReadOnlyList<int> orderedUnitIndices)
    {
        if (result != BattleResult.Victory || plan == null || plan.UnitPreviews == null) return;
        if (getSkillSlotPrefab == null || view.skillSlotParent == null) return;

        // [JC 260628] 결과 슬롯과 동일하게 전열→후열 순으로 정렬(누락 방지: ordered에 없는 preview는 뒤에 보존).
        var sortedPreviews = new List<UnitRewardPreview>(plan.UnitPreviews.Count);
        if (orderedUnitIndices != null)
            foreach (int ui in orderedUnitIndices)
            {
                var pv = plan.UnitPreviews.Find(p => p.UnitIndex == ui);
                if (pv != null && !sortedPreviews.Contains(pv)) sortedPreviews.Add(pv);
            }
        foreach (var pv in plan.UnitPreviews)
            if (!sortedPreviews.Contains(pv)) sortedPreviews.Add(pv);

        // [JC 260628] 스킬 획득 창을 한 번에 하나씩 순차 팝업(전열→후열 순). 이전엔 4개를 동시 생성·스택해
        // 최상단(마지막 생성)만 보이고 역순으로 처리되던 구조 → 현재 창만 활성, 완료 시 다음 창 활성.
        // (SkillSelectionPanel은 OnEnable이 없고 완료 시 스스로 Destroy하므로 Setup 후 비활성/재활성 안전.)
        foreach (var preview in sortedPreviews)
        {
            if (!preview.HasLevelUp || (preview.UnlockCandidateSkillIds?.Count ?? 0) == 0)
                continue;

            SkillSelectionPanel slot = Instantiate(getSkillSlotPrefab, view.skillSlotParent, false);
            slot.Setup(preview);
            slot.gameObject.SetActive(false); // 자기 차례에만 활성

            pendingSlotCount++;

            int unitIndex = preview.UnitIndex;
            Button acceptButton = view.acceptButton;
            slot.OnCompleted += (selectedSkillId) =>
            {
                if (selectedSkillId >= 0)
                    skillResults.Add(new SkillSelectionResult { UnitIndex = unitIndex, SelectedSkillId = selectedSkillId });

                ActivateNextSkillSlot(); // 다음 창 팝업(현재 창은 SkillSelectionPanel이 스스로 Destroy)

                pendingSlotCount--;
                if (pendingSlotCount <= 0 && acceptButton != null)
                    acceptButton.gameObject.SetActive(true);
            };
            skillQueue.Add(slot);
        }

        // 첫 창만 팝업
        if (skillQueue.Count > 0 && skillQueue[0] != null)
            skillQueue[0].gameObject.SetActive(true);
    }

    private void ActivateNextSkillSlot()
    {
        currentSkillIndex++;
        if (currentSkillIndex < skillQueue.Count && skillQueue[currentSkillIndex] != null)
            skillQueue[currentSkillIndex].gameObject.SetActive(true);
    }

    public List<SkillSelectionResult> GetSkillResults() => skillResults;
}
