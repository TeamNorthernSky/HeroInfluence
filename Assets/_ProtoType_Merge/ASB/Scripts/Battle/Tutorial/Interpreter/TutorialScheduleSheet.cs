using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>튜토리얼 1개의 규칙 목록(트리거+액션). DataDrivenTutorialFlow가 해석·실행.</summary>
[CreateAssetMenu(menuName = "Battle/Tutorial/Schedule Sheet", fileName = "TutorialScheduleSheet")]
public sealed class TutorialScheduleSheet : ScriptableObject
{
    [SerializeField, Min(-1)] private int zoneId = -1;
    [SerializeField] private string battleKey;
    [SerializeField] private List<TutorialRuleEntry> rules = new List<TutorialRuleEntry>();

    public int ZoneId => zoneId;
    public string BattleKey => battleKey != null ? battleKey.Trim() : string.Empty;
    public IReadOnlyList<TutorialRuleEntry> Rules => rules;

    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < rules.Count; i++)
        {
            TutorialRuleEntry rule = rules[i];
            if (rule == null)
            {
                issues.Add($"[{name}] rules[{i}] 가 null");
                continue;
            }

            string id = rule.ruleId != null ? rule.ruleId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                issues.Add($"[{name}] rules[{i}] 빈 ruleId");
            }
            else if (!seen.Add(id))
            {
                issues.Add($"[{name}] ruleId 중복: '{id}'");
            }

            ValidateActions(rule, id, issues);
            ValidateImmediate(rule, id, issues);
        }
    }

    private void ValidateActions(TutorialRuleEntry rule, string id, List<string> issues)
    {
        if (rule.actions == null)
        {
            return;
        }

        for (int j = 0; j < rule.actions.Count; j++)
        {
            TutorialAction a = rule.actions[j];
            if (a == null)
            {
                continue;
            }

            switch (a.type)
            {
                case TutorialActionType.ShowUi:
                case TutorialActionType.ShowBlockingUi:
                    if (string.IsNullOrWhiteSpace(a.uiKey))
                    {
                        issues.Add($"[{name}] '{id}': {a.type} 인데 uiKey 비어있음");
                    }
                    break;
                case TutorialActionType.SetHp:
                case TutorialActionType.SetInfluence:
                case TutorialActionType.AddMinimumHp:
                case TutorialActionType.AddStatModifier:
                    if (string.IsNullOrWhiteSpace(a.targetTemplateId) && a.targetSide == TutorialUnitSide.Any)
                    {
                        issues.Add($"[{name}] '{id}': {a.type} 대상(targetSide/targetTemplateId) 미지정");
                    }
                    break;
                case TutorialActionType.Spawn:
                    if (string.IsNullOrWhiteSpace(a.spawnUnitId))
                    {
                        issues.Add($"[{name}] '{id}': Spawn 인데 spawnUnitId 비어있음");
                    }
                    break;
                case TutorialActionType.CustomHook:
                    if (string.IsNullOrWhiteSpace(a.stringValue))
                    {
                        issues.Add($"[{name}] '{id}': CustomHook 인데 hookId(stringValue) 비어있음");
                    }
                    break;
            }
        }
    }

    private void ValidateImmediate(TutorialRuleEntry rule, string id, List<string> issues)
    {
        if (rule.trigger == null || rule.trigger.type != TutorialTriggerType.Immediate)
        {
            return;
        }

        if (!rule.once)
        {
            issues.Add($"[{name}] '{id}': Immediate 규칙은 once=false면 무한루프 위험(경고)");
        }

        bool advances = false;
        if (rule.actions != null)
        {
            for (int j = 0; j < rule.actions.Count; j++)
            {
                TutorialAction a = rule.actions[j];
                if (a != null && (a.type == TutorialActionType.SetStep ||
                                  a.type == TutorialActionType.SetFlag ||
                                  a.type == TutorialActionType.CompleteTutorial))
                {
                    advances = true;
                    break;
                }
            }
        }

        if (!advances)
        {
            issues.Add($"[{name}] '{id}': Immediate 규칙에 SetStep/SetFlag/Complete 가 없음(연쇄 종료 불명확)");
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
