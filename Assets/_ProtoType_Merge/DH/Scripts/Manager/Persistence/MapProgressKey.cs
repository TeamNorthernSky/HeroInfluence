using System.Text;
using UnityEngine;

public static class MapProgressKey
{
    public static string ForGrid(string prefix, Vector2Int grid)
    {
        string normalizedPrefix = NormalizeSegment(prefix);
        if (string.IsNullOrWhiteSpace(normalizedPrefix))
            normalizedPrefix = "key";

        return $"{normalizedPrefix}_{grid.x}_{grid.y}";
    }

    public static string ForItem(Vector2Int grid)
    {
        return ForGrid("item", grid);
    }

    public static string ForEvent(Vector2Int grid, string eventKey)
    {
        string baseKey = ForGrid("event", grid);
        string normalizedEventKey = NormalizeSegment(eventKey);
        return string.IsNullOrWhiteSpace(normalizedEventKey) ? baseKey : $"{baseKey}_{normalizedEventKey}";
    }

    public static string ForOutpost(Vector2Int grid)
    {
        return ForGrid("outpost", grid);
    }

    public static string ForVillainUnion(Vector2Int grid)
    {
        return ForGrid("villain_union", grid);
    }

    public static string ForEnemy(Vector2Int grid)
    {
        return ForGrid("enemy", grid);
    }

    public static string ForSceneEnemy(Vector2Int grid)
    {
        return ForGrid("scene_enemy", grid);
    }

    public static string ForRuntimeEnemy(string sourceKey, int sequence)
    {
        string normalizedSourceKey = NormalizeSegment(sourceKey);
        if (string.IsNullOrWhiteSpace(normalizedSourceKey))
            normalizedSourceKey = "spawn";

        return $"runtime_enemy_{normalizedSourceKey}_{Mathf.Max(1, sequence):000}";
    }

    public static string ForParty(Vector2Int grid)
    {
        return ForGrid("party", grid);
    }

    public static string ForSceneParty(string partyId)
    {
        string normalizedPartyId = NormalizeSegment(partyId);
        if (string.IsNullOrWhiteSpace(normalizedPartyId))
            normalizedPartyId = "party";

        return $"scene_party_{normalizedPartyId}";
    }

    public static string ForRuntimeParty(string sourceKey, int sequence)
    {
        string normalizedSourceKey = NormalizeSegment(sourceKey);
        if (string.IsNullOrWhiteSpace(normalizedSourceKey))
            normalizedSourceKey = "spawn";

        return $"runtime_party_{normalizedSourceKey}_{Mathf.Max(1, sequence):000}";
    }

    public static string NormalizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(trimmed.Length);
        bool lastWasSeparator = false;

        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            bool isAllowed = (c >= 'a' && c <= 'z') ||
                             (c >= '0' && c <= '9') ||
                             c == '-' ||
                             c == '_';

            if (isAllowed)
            {
                builder.Append(c);
                lastWasSeparator = c == '_' || c == '-';
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSeparator)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }

                continue;
            }

            if (!lastWasSeparator)
                builder.Append('_');

            builder.Append('u');
            builder.Append(((int)c).ToString("x4"));
            lastWasSeparator = false;
        }

        return builder.ToString().Trim('_', '-');
    }
}
