using System;
using System.Collections.Generic;
using UnityEngine;

public enum LevelZoneType
{
    MainZone,
    Connector,
    Fixed
}

[Serializable]
public class LevelZoneSlot
{
    private const int DefaultCandidateCount = 3;

    [SerializeField] private string zoneId = "zone_001";
    [SerializeField] private LevelZoneType zoneType = LevelZoneType.MainZone;
    [SerializeField] private Vector2Int anchor = Vector2Int.zero;
    [SerializeField] private Vector2Int size = new Vector2Int(50, 30);
    [SerializeField] private bool optional;
    [SerializeField] private LevelData[] candidates = new LevelData[DefaultCandidateCount];

    public string ZoneId => zoneId;
    public LevelZoneType ZoneType => zoneType;
    public Vector2Int Anchor => anchor;
    public Vector2Int Size => size;
    public bool Optional => optional;
    public IReadOnlyList<LevelData> Candidates => candidates;
    public RectInt Bounds => new RectInt(anchor, size);

    public bool Contains(Vector2Int grid)
    {
        return grid.x >= anchor.x
            && grid.y >= anchor.y
            && grid.x < anchor.x + size.x
            && grid.y < anchor.y + size.y;
    }

    public bool TryGetCandidate(int index, out LevelData candidate)
    {
        candidate = null;
        if (candidates == null || index < 0 || index >= candidates.Length)
            return false;

        candidate = candidates[index];
        return candidate != null;
    }

    public int GetValidCandidateCount()
    {
        if (candidates == null)
            return 0;

        int count = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null)
                count++;
        }

        return count;
    }

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            zoneId = "zone_001";

        anchor = new Vector2Int(Mathf.Max(0, anchor.x), Mathf.Max(0, anchor.y));
        size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));

        if (candidates == null || candidates.Length == 0)
            candidates = new LevelData[DefaultCandidateCount];
    }
}

[CreateAssetMenu(
    fileName = "LevelZoneLayoutData",
    menuName = "DH Work/Level Editor/Level Zone Layout Data")]
public class LevelZoneLayoutData : ScriptableObject
{
    [Header("Meta")]
    [SerializeField] private string layoutId = "layout_001";
    [SerializeField] private string displayName = "New Zone Layout";

    [Header("Grid")]
    [SerializeField] private Vector2Int totalGridSize = new Vector2Int(130, 100);

    [Header("Zones")]
    [SerializeField] private List<LevelZoneSlot> zones = new List<LevelZoneSlot>();

    public string LayoutId => layoutId;
    public string DisplayName => displayName;
    public Vector2Int TotalGridSize => totalGridSize;
    public IReadOnlyList<LevelZoneSlot> Zones => zones;

    public bool IsInsideGrid(Vector2Int grid)
    {
        return grid.x >= 0
            && grid.y >= 0
            && grid.x < totalGridSize.x
            && grid.y < totalGridSize.y;
    }

    public bool TryGetZone(string zoneId, out LevelZoneSlot zone)
    {
        zone = null;
        if (string.IsNullOrWhiteSpace(zoneId))
            return false;

        for (int i = 0; i < zones.Count; i++)
        {
            LevelZoneSlot candidate = zones[i];
            if (candidate != null && candidate.ZoneId == zoneId)
            {
                zone = candidate;
                return true;
            }
        }

        return false;
    }

    public bool ValidateLayout(List<string> errors)
    {
        if (errors == null)
            throw new ArgumentNullException(nameof(errors));

        errors.Clear();
        Normalize();

        for (int i = 0; i < zones.Count; i++)
        {
            LevelZoneSlot zone = zones[i];
            if (zone == null)
            {
                errors.Add($"Zone {i} is null.");
                continue;
            }

            ValidateZoneBounds(zone, i, errors);
            ValidateZoneCandidates(zone, i, errors);

            for (int otherIndex = i + 1; otherIndex < zones.Count; otherIndex++)
            {
                LevelZoneSlot other = zones[otherIndex];
                if (other == null)
                    continue;

                if (zone.Bounds.Overlaps(other.Bounds))
                    errors.Add($"Zone '{zone.ZoneId}' overlaps zone '{other.ZoneId}'.");
            }
        }

        return errors.Count == 0;
    }

    private void ValidateZoneBounds(LevelZoneSlot zone, int index, List<string> errors)
    {
        RectInt bounds = zone.Bounds;
        if (bounds.xMin < 0 || bounds.yMin < 0 || bounds.xMax > totalGridSize.x || bounds.yMax > totalGridSize.y)
            errors.Add($"Zone '{zone.ZoneId}' at index {index} is outside total grid size.");
    }

    private void ValidateZoneCandidates(LevelZoneSlot zone, int index, List<string> errors)
    {
        int validCandidateCount = zone.GetValidCandidateCount();
        if (!zone.Optional && validCandidateCount == 0)
            errors.Add($"Zone '{zone.ZoneId}' at index {index} has no candidates.");

        IReadOnlyList<LevelData> candidates = zone.Candidates;
        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            LevelData candidate = candidates[candidateIndex];
            if (candidate == null)
                continue;

            if (candidate.GridSize != zone.Size)
            {
                errors.Add(
                    $"Zone '{zone.ZoneId}' candidate '{candidate.name}' grid size {candidate.GridSize} does not match zone size {zone.Size}.");
            }
        }
    }

    private void Normalize()
    {
        if (string.IsNullOrWhiteSpace(layoutId))
            layoutId = "layout_001";

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "New Zone Layout";

        totalGridSize = new Vector2Int(Mathf.Max(1, totalGridSize.x), Mathf.Max(1, totalGridSize.y));

        if (zones == null)
            zones = new List<LevelZoneSlot>();

        for (int i = 0; i < zones.Count; i++)
            zones[i]?.Normalize();
    }

    private void OnValidate()
    {
        Normalize();
    }
}
