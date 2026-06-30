using System;
using UnityEngine;

// [DH/JC seam 260630] 거점 점령 알림 발행자. UI 직접조작 제거 → 표시 payload 발행.
// displayEntries(스프라이트/이름 프리셋)는 DH 보유 유지. JC OutpostNoticeModalController가 표시.
public class OutpostPanelUI : MonoBehaviour
{
    [Header("Display Presets")]
    [SerializeField] private OutpostUnlockDisplayEntry[] displayEntries = Array.Empty<OutpostUnlockDisplayEntry>();

    private void OnEnable()  => Outpost.OutpostClaimed += HandleOutpostClaimed;
    private void OnDisable() => Outpost.OutpostClaimed -= HandleOutpostClaimed;

    private void HandleOutpostClaimed(Outpost outpost)
    {
        if (outpost == null) return;

        OutpostUnlockDisplayEntry entry = GetDisplayEntry(outpost.OutpostType);
        string name = GetOutpostDisplayName(entry, outpost.OutpostType);
        string resourceName = GetResourceDisplayName(entry, outpost.OutpostType);
        int amount = Mathf.Max(0, outpost.resourcePerTurn);

        ExplorationModalEvents.RaiseOutpostNotice(new OutpostNoticeRequest {
            title       = $"{name} 해방",
            description = $"빌런에게서 '{name}' 해방\n매턴 '{resourceName}' '{amount}' 지급",
            amount         = amount,
            buildingSprite = entry.BuildingSprite,
            resourceSprite = entry.ResourceSprite,
        });
    }

    // ── 표시명/프리셋 해석 (기존 로직 보존) ─────────────────────────────
    private OutpostUnlockDisplayEntry GetDisplayEntry(OutpostType outpostType)
    {
        OutpostType normalizedType = NormalizeDisplayType(outpostType);
        if (displayEntries == null)
            return default;

        for (int i = 0; i < displayEntries.Length; i++)
        {
            OutpostUnlockDisplayEntry entry = displayEntries[i];
            if (NormalizeDisplayType(entry.OutpostType) == normalizedType)
                return entry;
        }

        return default;
    }

    private static OutpostType NormalizeDisplayType(OutpostType outpostType)
    {
        return outpostType == OutpostType.Composite
            ? OutpostType.Library
            : OutpostTypeUtility.Normalize(outpostType);
    }

    private static string GetOutpostDisplayName(OutpostUnlockDisplayEntry entry, OutpostType outpostType)
    {
        if (!string.IsNullOrWhiteSpace(entry.OutpostName))
            return entry.OutpostName;

        return NormalizeDisplayType(outpostType) switch
        {
            OutpostType.Bank        => "은행",
            OutpostType.Library     => "도서관",
            OutpostType.JewelryShop => "보석상",
            OutpostType.BlockStore  => "공구상",
            _                       => "거점"
        };
    }

    private static string GetResourceDisplayName(OutpostUnlockDisplayEntry entry, OutpostType outpostType)
    {
        if (!string.IsNullOrWhiteSpace(entry.ResourceName))
            return entry.ResourceName;

        return NormalizeDisplayType(outpostType) switch
        {
            OutpostType.Bank        => "자금",
            OutpostType.Library     => "히어로 메달",
            OutpostType.JewelryShop => "아티펙트 수정",
            OutpostType.BlockStore  => "건설 자재",
            _                       => "자원"
        };
    }
}

[Serializable]
public struct OutpostUnlockDisplayEntry
{
    [SerializeField] private OutpostType outpostType;
    [SerializeField] private string outpostName;
    [SerializeField] private string resourceName;
    [SerializeField] private Sprite buildingSprite;
    [SerializeField] private Sprite resourceSprite;

    public OutpostType OutpostType => outpostType;
    public string OutpostName      => outpostName;
    public string ResourceName     => resourceName;
    public Sprite BuildingSprite   => buildingSprite;
    public Sprite ResourceSprite   => resourceSprite;
}
