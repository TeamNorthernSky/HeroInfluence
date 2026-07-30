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

    // [KJ 260729] 스킬 획득 창이 전부 사라진 뒤에 결과 내용을 노출한다.
    //   기존에는 결과 패널 위에 스킬 창이 겹쳐 떠 있었다(GetSkillParent가 결과 패널의 자식이고
    //   GetSkillBackground가 ResultBackground와 같은 1440x720 영역을 덮는 구조).
    //   숨긴 대상만 기록해 복원하므로 원래 비활성이던 노드는 건드리지 않는다.
    private readonly List<GameObject> hiddenResultContent = new List<GameObject>();

    public void Show(BattleResult result, BattleRewardPlan plan)
    {
        skillResults.Clear();
        pendingSlotCount = 0;
        skillQueue.Clear();
        currentSkillIndex = 0;
        // [KJ 260729] 같은 인스턴스에서 재호출되어도 이전에 숨긴 노드가 남지 않게 먼저 되돌린다.
        RevealResultContent();

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

        // [KJ 260729] 스킬 획득 창이 있으면 결과 내용을 숨겨 두고, 마지막 창이 닫힐 때 노출한다.
        if (pendingSlotCount > 0)
            HideResultContent(view);
        else if (view.acceptButton != null)
            view.acceptButton.gameObject.SetActive(true);
    }

    /// <summary>[KJ 260729] 스킬 슬롯 부모(및 그 조상)를 제외한 직계 자식을 숨긴다.
    /// 이미 비활성인 노드는 기록하지 않아 복원 시 원래 꺼져 있던 것이 켜지지 않는다.</summary>
    private void HideResultContent(BattleResultView view)
    {
        hiddenResultContent.Clear();
        foreach (Transform child in transform)
        {
            // skillSlotParent가 계층 어디에 있어도 그 조상은 끄지 않는다(스킬 창까지 같이 꺼지므로).
            if (view.skillSlotParent != null && view.skillSlotParent.IsChildOf(child)) continue;
            if (!child.gameObject.activeSelf) continue;

            child.gameObject.SetActive(false);
            hiddenResultContent.Add(child.gameObject);
        }
    }

    /// <summary>[KJ 260729] HideResultContent가 숨긴 노드만 되돌린다.</summary>
    private void RevealResultContent()
    {
        for (int i = 0; i < hiddenResultContent.Count; i++)
            if (hiddenResultContent[i] != null) hiddenResultContent[i].SetActive(true);
        hiddenResultContent.Clear();
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
                if (pendingSlotCount <= 0)
                {
                    // [KJ 260729] 마지막 스킬 창까지 처리된 뒤에 결과 내용을 노출한다.
                    RevealResultContent();
                    if (acceptButton != null) acceptButton.gameObject.SetActive(true);
                }
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
