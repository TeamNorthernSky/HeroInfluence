using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    private static Sprite ResolvePortrait(BattleCharactor unit)
    {
        if (unit == null) return null;
        // [JC 260621] 플레이어 = PortraitLibrary Hero(키=HeroIndex).
        if (unit.IsPlayer && unit.SourceData != null)
            return Sprites.Portrait.Hero(unit.SourceData.UnitTemplateKey);
        // [JC 260622] 적 = PortraitLibrary Enemy(키=적 UnitTemplateKey, 미수록/키없음 시 범용 zako).
        if (!unit.IsPlayer)
            return Sprites.Portrait.Enemy(unit.SourceEnemyData != null ? unit.SourceEnemyData.UnitTemplateKey : null);
        return null;
    }

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
                BattleCharactor unit = order[i % order.Count];
                Sprite portrait = ResolvePortrait(unit);
                slots[i].gameObject.SetActive(true);
                slots[i].Setup(unit, isCurrentTurn: i == 0, portraitSprite: portrait);
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }
    }
}
