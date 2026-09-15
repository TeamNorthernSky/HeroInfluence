using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 1개의 UI 콘텐츠(엔트리 목록). 인스펙터에서 한눈에 보고, flow는 key로 조회한다.
/// 로직(트리거·조건)은 담지 않는다 — 콘텐츠 전용.
/// </summary>
[CreateAssetMenu(menuName = "Battle/Tutorial/UI Sheet", fileName = "TutorialUiSheet")]
public sealed class TutorialUiSheet : ScriptableObject
{
    public const string DefaultViewId = "guide";

    [SerializeField, Min(-1)] private int zoneId = -1;
    [SerializeField] private string battleKey;
    [SerializeField] private List<TutorialUiEntry> entries = new List<TutorialUiEntry>();

    public int ZoneId => zoneId;
    public string BattleKey => battleKey != null ? battleKey.Trim() : string.Empty;
    public IReadOnlyList<TutorialUiEntry> Entries => entries;

    /// <summary>key로 엔트리 조회(Trim + Ordinal). 없으면 null.</summary>
    public TutorialUiEntry Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        string wanted = key.Trim();
        for (int i = 0; i < entries.Count; i++)
        {
            TutorialUiEntry e = entries[i];
            if (e != null && !string.IsNullOrWhiteSpace(e.key) &&
                string.Equals(e.key.Trim(), wanted, StringComparison.Ordinal))
            {
                return e;
            }
        }

        return null;
    }

    /// <summary>검증 이슈를 모은다(테스트·커스텀 인스펙터 공용). 버튼 바인딩 검사는 런타임(씬 UI) 담당.</summary>
    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++)
        {
            TutorialUiEntry e = entries[i];
            if (e == null)
            {
                issues.Add($"[{name}] entries[{i}] 가 null");
                continue;
            }

            string k = e.key != null ? e.key.Trim() : string.Empty;
            if (string.IsNullOrEmpty(k))
            {
                issues.Add($"[{name}] entries[{i}] 빈 key");
            }
            else if (!seen.Add(k))
            {
                issues.Add($"[{name}] key 중복: '{k}'");
            }

            string view = string.IsNullOrWhiteSpace(e.viewId) ? DefaultViewId : e.viewId.Trim();
            if (!string.Equals(view, DefaultViewId, StringComparison.Ordinal))
            {
                issues.Add($"[{name}] '{k}': 미지원 viewId '{view}' (현재 '{DefaultViewId}'만)");
            }

            if (!string.IsNullOrEmpty(e.message))
            {
                try
                {
                    // 형식 오류(중괄호 불균형 등)만 검사. 인자 슬롯은 넉넉히.
                    string.Format(e.message, new object[8]);
                }
                catch (FormatException)
                {
                    issues.Add($"[{name}] '{k}': message 형식 오류(string.Format)");
                }
            }
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
