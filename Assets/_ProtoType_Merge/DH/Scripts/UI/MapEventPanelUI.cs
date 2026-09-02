using System;
using UnityEngine;

// [DH/JC seam 260630] 맵이벤트 발행자. UI 직접조작 제거 → 표시 payload + 실행 위임 발행.
public class MapEventPanelUI : MonoBehaviour
{
    [Header("Display Presets")]
    [SerializeField] private MapEventDisplayEntry[] displayEntries = Array.Empty<MapEventDisplayEntry>();

    private void OnEnable()  => MapEventObject.EventInteracted += HandleEventInteracted;
    private void OnDisable() => MapEventObject.EventInteracted -= HandleEventInteracted;

    private void HandleEventInteracted(MapEventObject mapEvent, PartyGridMover party)
    {
        if (mapEvent == null || party == null) return;
        if (mapEvent.EventKind != MapEventKind.Consume)
            return;

        string resourceName = GetResourceDisplayName(mapEvent.RequireResource);
        string effectText = GetEffectDisplayText(mapEvent.EventType, mapEvent.EffectAmount);
        bool canAfford = Game.Economy != null
            && Game.Economy.Has(mapEvent.RequireResource, mapEvent.RequireAmount);

        ExplorationModalEvents.RaiseMapEvent(new MapEventModalRequest {
            description = $"{resourceName}을 '{mapEvent.RequireAmount}' 지불하고\n모든 영웅의 '{effectText}'",
            effectAmountText = mapEvent.EventType == MapEventType.Heal
                ? $"{Mathf.Max(0, mapEvent.EffectAmount)} 만큼 회복"
                : $"+ {Mathf.Max(0, mapEvent.EffectAmount)}",
            costAmountText = Mathf.Max(0, mapEvent.RequireAmount).ToString(),
            resourceIcon = LoadResourceIcon(mapEvent.RequireResource),
            effectIcon = LoadEffectIcon(mapEvent.EventType),
            canAfford = canAfford,
            onConfirm = () => mapEvent.TryExecuteEvent(party),
        });
    }

    // ── 표시 로직 보존 (기존과 동일) ─────────────
    private static Sprite LoadResourceIcon(ResourceType r)
    {
        string n = r switch {
            ResourceType.Money => "UI_icon_money", ResourceType.Chip => "UI_icon_medal",
            ResourceType.Crystal => "UI_icon_crystal", ResourceType.Supply => "UI_icon_block", _ => null };
        return n != null ? Resources.Load<Sprite>("UI_Sprite/UI_HQLobby/Resources/" + n) : null;
    }

    private static Sprite LoadEffectIcon(MapEventType t)
    {
        string n = t switch {
            MapEventType.TrainingAtk => "UI_icon_ATK", MapEventType.TrainingHp => "UI_icon_HP",
            MapEventType.Heal => "UI_icon_HP", _ => null };
        return n != null ? Resources.Load<Sprite>("UI_Sprite/UI_Icon/Status/" + n) : null;
    }

    private static string GetEffectDisplayText(MapEventType t, int amt)
    {
        int a = Mathf.Max(0, amt);
        return t switch {
            MapEventType.TrainingAtk => $"공격력 + {a}", MapEventType.TrainingHp => $"최대 체력 + {a}",
            MapEventType.Heal => "현재 체력 회복", _ => t.ToString() };
    }

    private static string GetResourceDisplayName(ResourceType r)
        => r switch {
            ResourceType.Money => "자금", ResourceType.Chip => "히어로 메달",
            ResourceType.Crystal => "아티펙트 수정", ResourceType.Supply => "건설 자재", _ => r.ToString() };
}

[Serializable]
public struct MapEventDisplayEntry
{
    [SerializeField] private MapEventType eventType;
    [SerializeField] private Sprite eventSprite;

    public MapEventType EventType => eventType;
    public Sprite EventSprite => eventSprite;
}
