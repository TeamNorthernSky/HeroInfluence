// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/TurnOrderUI.cs
// 원본 객체: TurnOrderUI -> JcTurnOrderUI
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System.Collections.Generic;
using UnityEngine;

public class JcTurnOrderUI : MonoBehaviour
{
    [Tooltip("현재 행동 유닛과 예측 턴 순서를 제공하는 전투 흐름입니다.")]
    [SerializeField] private JcBattleFlowManager flowManager;
    [Tooltip("씬에 미리 배치한 슬롯을 표시 순서대로 연결합니다. 하나라도 등록하면 실행 중 슬롯을 생성하지 않습니다. 위치와 크기는 각 RectTransform에서 조절합니다.")]
    [SerializeField] private TurnSlotUI[] sceneSlots = new TurnSlotUI[0];

    [Header("기존 씬 호환 — 씬 슬롯이 비어 있을 때만 사용")]
    [Tooltip("아직 개편하지 않은 씬에서 생성할 슬롯 프리팹입니다. 씬 슬롯이 등록되어 있으면 사용하지 않습니다.")]
    [SerializeField] private TurnSlotUI slotPrefab;
    [Tooltip("기존 씬에서 생성한 슬롯을 배치할 부모입니다. 씬 슬롯이 등록되어 있으면 사용하지 않습니다.")]
    [SerializeField] private Transform slotParent;
    [Tooltip("최대 표시 슬롯 수입니다. 씬 배치 방식에서는 등록된 슬롯 수를 넘겨 생성하지 않습니다. 0 이하는 모든 슬롯을 숨깁니다.")]
    [SerializeField] private int maxDisplayCount = 5;

    private readonly List<TurnSlotUI> slots = new List<TurnSlotUI>();

    // 원본 함수 대응: TurnOrderUI.OnEnable (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/TurnOrderUI.cs)

    private void OnEnable()
    {
        if (flowManager == null) return;
        flowManager.OnTurnStarted += OnTurnStarted;
        flowManager.OnBattleEnded += OnBattleEnded;
        Refresh();
    }

    // 원본 함수 대응: TurnOrderUI.OnDisable (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/TurnOrderUI.cs)

    private void OnDisable()
    {
        if (flowManager == null) return;
        flowManager.OnTurnStarted -= OnTurnStarted;
        flowManager.OnBattleEnded -= OnBattleEnded;
    }

    private void OnTurnStarted(int round, BattleCharactor unit) => Refresh();

    private void OnBattleEnded(BattleResult result) => gameObject.SetActive(false);

    // 원본 함수 대응: TurnOrderUI.ResolvePortrait (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/TurnOrderUI.cs)

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

    // 원본 함수 대응: TurnOrderUI.Refresh (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/TurnOrderUI.cs)

    private void Refresh()
    {
        if (flowManager == null) return;

        List<BattleCharactor> order = flowManager.GetPredictedTurnOrder();
        IReadOnlyList<TurnSlotUI> displaySlots = GetDisplaySlots();
        int count = Mathf.Max(0, maxDisplayCount);

        for (int i = 0; i < displaySlots.Count; i++)
        {
            TurnSlotUI slot = displaySlots[i];
            if (slot == null) continue;
            if (i < count && order.Count > 0)
            {
                // 인원이 슬롯 수보다 적으면 기존처럼 다음 순환을 반복해 표시합니다.
                BattleCharactor unit = order[i % order.Count];
                Sprite portrait = ResolvePortrait(unit);
                slot.Setup(unit, isCurrentTurn: i == 0, portraitSprite: portrait);
            }
            else
            {
                slot.Setup(null, false);
            }
        }
    }

    // 원본 함수 대응: TurnOrderUI.GetDisplaySlots (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/TurnOrderUI.cs)

    private IReadOnlyList<TurnSlotUI> GetDisplaySlots()
    {
        if (sceneSlots != null && sceneSlots.Length > 0) return sceneSlots;

        // BattleSimulationScene_Legacy 등의 기존 참조가 이관될 때까지 생성 경로를 유지합니다.
        if (slotPrefab != null && slotParent != null)
        {
            while (slots.Count < maxDisplayCount)
                slots.Add(Instantiate(slotPrefab, slotParent));
        }
        return slots;
    }
}
