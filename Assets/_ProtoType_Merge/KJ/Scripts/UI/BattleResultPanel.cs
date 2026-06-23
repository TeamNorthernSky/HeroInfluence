using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleResultPanel : MonoBehaviour
{
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject victoryImage;
    [SerializeField] private GameObject defeatImage;
    [SerializeField] private Transform heroIndex;
    [SerializeField] private HeroInfoResult heroInfoResultWinPrefab;
    [SerializeField] private HeroInfoResult heroInfoResultLosePrefab;
    [SerializeField] private Button acceptButton;

    [Header("Skill Selection")]
    [SerializeField] private SkillSelectionPanel getSkillSlotPrefab;
    [SerializeField] private Transform getSkillSlotParent;

    public event System.Action OnAccepted;

    private readonly List<SkillSelectionResult> skillResults = new List<SkillSelectionResult>();
    private int pendingSlotCount = 0;

    private void Awake()
    {
        // [JC 260622] 자기-비활성화 제거: 이 컴포넌트는 resultPanel(부모) 하위에 있고 resultPanel이 씬에 비활성 저장됨.
        // Awake가 resultPanel을 끄면, Show()가 resultPanel을 켜는 순간 비로소 Awake가 돌며 즉시 다시 꺼버려
        // 결과패널이 표시되지 않는 버그가 있었음. 시작 숨김은 씬의 비활성 저장으로 대체.

        if (acceptButton != null)
        {
            acceptButton.gameObject.SetActive(false);
            acceptButton.onClick.AddListener(() => OnAccepted?.Invoke());
        }
    }

    public void Show(BattleResult result, BattleRewardPlan plan)
    {
        resultPanel.SetActive(true);
        victoryImage.SetActive(result == BattleResult.Victory);
        defeatImage.SetActive(result == BattleResult.Defeat);

        skillResults.Clear();
        pendingSlotCount = 0;

        BuildSlots(plan, result);
        BuildSkillSlots(plan, result);

        if (pendingSlotCount == 0)
            acceptButton.gameObject.SetActive(true);
    }

    private void BuildSlots(BattleRewardPlan plan, BattleResult result)
    {
        HeroInfoResult prefab = result == BattleResult.Victory ? heroInfoResultWinPrefab : heroInfoResultLosePrefab;
        Debug.Log($"[BuildSlots] result={result} prefab={prefab} heroIndex={heroIndex}");
        if (heroIndex == null || prefab == null) return;

        foreach (Transform child in heroIndex)
            Destroy(child.gameObject);

        CombatContext context = FindAnyObjectByType<CombatContext>();
        if (context == null) return;
        var unitIndices = context.CombatParty?.UnitIndices;
        if (unitIndices == null) return;
        Debug.Log($"UnitIndeices={unitIndices.Count}");
        for (int i = 0; i < unitIndices.Count; i++)
        {
            if (unitIndices[i] == 0) continue;

            HeroInfoResult slot = Instantiate(prefab);
            slot.transform.SetParent(heroIndex, false);

            UnitRewardPreview preview = null;
            if (plan != null)
                preview = plan.UnitPreviews.Find(p => p.UnitIndex == unitIndices[i])
                          ?? (i < plan.UnitPreviews.Count ? plan.UnitPreviews[i] : null);

            if (preview != null)
                slot.Apply(preview);
        }
    }

    private void BuildSkillSlots(BattleRewardPlan plan, BattleResult result)
    {
        if (result != BattleResult.Victory || plan == null) return;

        Debug.Log($"[BuildSkillSlots] prefab={getSkillSlotPrefab}, parent={getSkillSlotParent}");

        if (getSkillSlotPrefab == null || getSkillSlotParent == null) return;

        foreach (var preview in plan.UnitPreviews)
        {
            bool hasLevelUp = preview.HasLevelUp;
            int candidateCount = preview.UnlockCandidateSkillIds?.Count ?? 0;
            Debug.Log($"[BuildSkillSlots] {preview.UnitName} hasLevelUp={hasLevelUp} candidates={candidateCount}");

            if (!hasLevelUp || candidateCount == 0)
                continue;

            SkillSelectionPanel slot = Instantiate(getSkillSlotPrefab, getSkillSlotParent, false);
            slot.Setup(preview);
            pendingSlotCount++;

            int unitIndex = preview.UnitIndex;
            slot.OnCompleted += (selectedSkillId) =>
            {
                if (selectedSkillId >= 0)
                {
                    skillResults.Add(new SkillSelectionResult
                    {
                        UnitIndex = unitIndex,
                        SelectedSkillId = selectedSkillId
                    });
                }

                pendingSlotCount--;
                if (pendingSlotCount <= 0)
                    acceptButton.gameObject.SetActive(true);
            };
        }
    }

    public List<SkillSelectionResult> GetSkillResults() => skillResults;
}
