using System.Collections.Generic;
using UnityEngine;

public class TurnOrderUI : MonoBehaviour
{
    [SerializeField] private BattleFlowManager flowManager;
    [SerializeField] private TurnSlotUI slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private int maxDisplayCount = 5;

    private readonly List<TurnSlotUI> slots = new List<TurnSlotUI>();

    private void OnEnable()
    {
        if (flowManager == null) return;
        flowManager.OnTurnStarted += OnTurnStarted;
        flowManager.OnBattleEnded += OnBattleEnded;
        Refresh();
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
        if (flowManager == null || slotPrefab == null || slotParent == null)
            return;

        List<BattleCharactor> order = flowManager.GetPredictedTurnOrder();
        if (order.Count == 0) return;

        // 슬롯 부족하면 추가 생성
        while (slots.Count < maxDisplayCount)
            slots.Add(Instantiate(slotPrefab, slotParent));

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < maxDisplayCount)
            {
                slots[i].gameObject.SetActive(true);
                slots[i].Setup(order[i % order.Count], isCurrentTurn: i == 0);
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }
    }
}
