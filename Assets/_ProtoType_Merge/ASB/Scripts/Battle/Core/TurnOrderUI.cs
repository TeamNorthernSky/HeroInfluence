using System.Collections.Generic;
using UnityEngine;

public class TurnOrderUI : MonoBehaviour
{
    [SerializeField] private BattleFlowManager flowManager;
    [SerializeField] private TurnSlotUI slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private int maxDisplayCount = 8;

    private readonly List<TurnSlotUI> slots = new List<TurnSlotUI>();

    private void OnEnable()
    {
        if (flowManager == null) return;
        flowManager.OnTurnStarted += OnTurnStarted;
        flowManager.OnBattleEnded += OnBattleEnded;
    }

    private void OnDisable()
    {
        if (flowManager == null) return;
        flowManager.OnTurnStarted -= OnTurnStarted;
        flowManager.OnBattleEnded -= OnBattleEnded;
    }

    private void OnTurnStarted(int round, BattleCharactor unit) => Refresh();

    private void OnBattleEnded(BattleResult result) => gameObject.SetActive(false);

    private void Refresh()
    {
        List<BattleCharactor> order = flowManager.GetPredictedTurnOrder();
        int displayCount = Mathf.Min(order.Count, maxDisplayCount);

        // 슬롯 부족하면 추가 생성
        while (slots.Count < displayCount)
            slots.Add(Instantiate(slotPrefab, slotParent));

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < displayCount)
            {
                slots[i].gameObject.SetActive(true);
                slots[i].Setup(order[i], isCurrentTurn: i == 0);
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }
    }
}
