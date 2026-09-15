using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// (ZoneId, BattleKey) → TutorialUiSheet. Flow Registry와 동일 기준(정확 매칭 → -1 와일드카드 → null, Ordinal).
/// </summary>
[CreateAssetMenu(menuName = "Battle/Tutorial/UI Catalog", fileName = "TutorialUiCatalog")]
public sealed class TutorialUiCatalog : ScriptableObject
{
    [SerializeField] private List<TutorialUiSheet> sheets = new List<TutorialUiSheet>();

    public TutorialUiSheet Find(int zoneId, string battleKey)
    {
        if (string.IsNullOrWhiteSpace(battleKey))
        {
            return null;
        }

        string key = battleKey.Trim();
        TutorialUiSheet wildcard = null;
        for (int i = 0; i < sheets.Count; i++)
        {
            TutorialUiSheet s = sheets[i];
            if (s == null || !string.Equals(s.BattleKey, key, StringComparison.Ordinal))
            {
                continue;
            }

            if (s.ZoneId == zoneId)
            {
                return s; // 정확 매칭 우선
            }

            if (s.ZoneId == -1 && wildcard == null)
            {
                wildcard = s;
            }
        }

        return wildcard;
    }

    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < sheets.Count; i++)
        {
            TutorialUiSheet s = sheets[i];
            if (s == null)
            {
                issues.Add($"[{name}] sheets[{i}] 가 null");
                continue;
            }

            string composite = $"{s.ZoneId}|{s.BattleKey}";
            if (!seen.Add(composite))
            {
                issues.Add($"[{name}] 중복 (zone={s.ZoneId}, key={s.BattleKey})");
            }

            s.CollectValidationIssues(issues);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var issues = new List<string>();
        CollectValidationIssues(issues);
        for (int i = 0; i < issues.Count; i++)
        {
            Debug.LogWarning(issues[i], this);
        }
    }
#endif
}
