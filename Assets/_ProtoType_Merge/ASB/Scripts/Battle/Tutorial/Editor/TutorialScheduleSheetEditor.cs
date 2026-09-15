using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TutorialScheduleSheet를 "진행 순서 오버뷰 표"로 보여주는 커스텀 인스펙터.
/// 상단: 읽기용 요약 표(트리거·조건·액션요약 + uiKey 문구 미리보기). 하단: 기본 드로어로 상세 편집.
/// 문구 미리보기는 (ZoneId,BattleKey) 매칭 TutorialUiSheet를 best-effort로 찾아 붙인다(없으면 key만).
/// </summary>
[CustomEditor(typeof(TutorialScheduleSheet))]
public sealed class TutorialScheduleSheetEditor : Editor
{
    private static readonly (string header, float width)[] Columns =
    {
        ("#", 24f),
        ("ruleId", 96f),
        ("트리거", 118f),
        ("조건", 108f),
        ("액션 요약", 240f),
        ("1회", 30f),
        ("purpose", 110f),
    };

    private bool showEditable;
    private TutorialUiSheet cachedUiSheet;
    private int cachedZone = int.MinValue;
    private string cachedKey;

    public override void OnInspectorGUI()
    {
        var sheet = (TutorialScheduleSheet)target;

        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("zoneId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("battleKey"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("규칙 (진행 순서 오버뷰)", EditorStyles.boldLabel);

        IReadOnlyList<TutorialRuleEntry> rules = sheet.Rules;
        if (rules == null || rules.Count == 0)
        {
            EditorGUILayout.HelpBox("규칙이 없습니다. 아래 '규칙 편집(상세)'에서 추가하세요.", MessageType.Info);
        }
        else
        {
            DrawOverview(sheet, rules);
        }

        // 상세 편집: 기본 드로어(중첩 트리거/액션 리스트 편집 그대로)
        EditorGUILayout.Space();
        showEditable = EditorGUILayout.Foldout(showEditable, "규칙 편집 (상세)", true);
        if (showEditable)
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rules"), true);
            serializedObject.ApplyModifiedProperties();
        }

        // 검증(공용 메서드 재사용)
        var issues = new List<string>();
        sheet.CollectValidationIssues(issues);
        if (issues.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(string.Join("\n", issues), MessageType.Warning);
        }
    }

    private void DrawOverview(TutorialScheduleSheet sheet, IReadOnlyList<TutorialRuleEntry> rules)
    {
        TutorialUiSheet uiSheet = ResolveUiSheet(sheet);

        // 진행 순서로 정렬(effective step → round → 원래 인덱스). 데이터 자체는 건드리지 않는다.
        int[] order = BuildDisplayOrder(rules);

        // 헤더
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        for (int c = 0; c < Columns.Length; c++)
        {
            EditorGUILayout.LabelField(Columns[c].header, EditorStyles.miniBoldLabel, GUILayout.Width(Columns[c].width));
        }
        EditorGUILayout.EndHorizontal();

        int lastStep = int.MinValue;
        for (int r = 0; r < order.Length; r++)
        {
            int i = order[r];
            TutorialRuleEntry rule = rules[i];
            if (rule == null)
            {
                continue;
            }

            // step 그룹 구분선(step이 바뀔 때 얇은 라벨)
            int eff = EffectiveStep(rule.trigger);
            if (eff != lastStep)
            {
                lastStep = eff;
                EditorGUILayout.LabelField(eff == int.MaxValue ? "· step 무관" : $"· step {eff}",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.BeginHorizontal();
            Cell(i.ToString(), Columns[0].width);
            Cell(Safe(rule.ruleId), Columns[1].width);
            Cell(TriggerSummary(rule.trigger), Columns[2].width);
            Cell(ConditionSummary(rule.trigger), Columns[3].width);
            Cell(ActionsSummary(rule.actions, uiSheet), Columns[4].width);
            Cell(rule.once ? "✓" : "–", Columns[5].width);
            Cell(Safe(rule.purpose), Columns[6].width);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.LabelField(
            uiSheet != null ? $"문구 미리보기: {uiSheet.name}" : "문구 미리보기: (매칭 UI 시트 없음 — key만 표시)",
            EditorStyles.miniLabel);
    }

    // 클립되는 라벨 + 전체 내용 툴팁
    private static void Cell(string text, float width)
    {
        EditorGUILayout.LabelField(new GUIContent(text, text), GUILayout.Width(width));
    }

    private static string Safe(string s) => string.IsNullOrWhiteSpace(s) ? "-" : s.Trim();

    private static int[] BuildDisplayOrder(IReadOnlyList<TutorialRuleEntry> rules)
    {
        var order = new int[rules.Count];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (a, b) =>
        {
            int sa = EffectiveStep(rules[a]?.trigger);
            int sb = EffectiveStep(rules[b]?.trigger);
            if (sa != sb) return sa.CompareTo(sb);
            int ra = EffectiveRound(rules[a]?.trigger);
            int rb = EffectiveRound(rules[b]?.trigger);
            if (ra != rb) return ra.CompareTo(rb);
            return a.CompareTo(b);   // 안정성: 원래 순서 유지
        });
        return order;
    }

    private static int EffectiveStep(TutorialTrigger t)
    {
        if (t == null) return int.MaxValue;
        if (t.requiredStep >= 0) return t.requiredStep;
        if (t.minStep >= 0) return t.minStep;
        return int.MaxValue;   // step 무관은 맨 아래 그룹
    }

    private static int EffectiveRound(TutorialTrigger t)
    {
        if (t == null || t.minRound < 0) return int.MaxValue;
        return t.minRound;
    }

    private static string TriggerSummary(TutorialTrigger t)
    {
        if (t == null) return "(none)";
        string s = t.type.ToString();
        switch (t.type)
        {
            case TutorialTriggerType.UnitTurnStarted:
            case TutorialTriggerType.UnitDied:
            case TutorialTriggerType.ParticipantRegistered:
            case TutorialTriggerType.UnitHpAtOrBelow:
                if (!string.IsNullOrWhiteSpace(t.unitTemplateId))
                {
                    s += $"({SideTag(t.side)}{t.unitTemplateId.Trim()})";
                }
                break;
            case TutorialTriggerType.SkillResolved:
                s += $"({(string.IsNullOrWhiteSpace(t.skillKey) ? "*" : t.skillKey.Trim())}{(t.requireTargetDeath ? "→†" : "")})";
                break;
            case TutorialTriggerType.UIAction:
                if (!string.IsNullOrWhiteSpace(t.uiActionId))
                {
                    s += $"({t.uiActionId.Trim()})";
                }
                break;
            case TutorialTriggerType.AliveCountAtOrBelow:
                s += $"({SideTag(t.side)}≤{t.aliveCountAtOrBelow})";
                break;
        }
        return s;
    }

    private static string ConditionSummary(TutorialTrigger t)
    {
        if (t == null) return "-";
        var parts = new List<string>();

        if (t.minRound >= 0 || t.maxRound >= 0)
        {
            parts.Add("R" + Range(t.minRound, t.maxRound));
        }
        if (t.requiredStep >= 0)
        {
            parts.Add("step=" + t.requiredStep);
        }
        else if (t.minStep >= 0 || t.maxStep >= 0)
        {
            parts.Add("step" + Range(t.minStep, t.maxStep));
        }
        if (t.type == TutorialTriggerType.UnitHpAtOrBelow)
        {
            parts.Add($"hp≤{t.hpRatioAtOrBelow:0.##}");
        }
        if (!string.IsNullOrWhiteSpace(t.requireFlag))
        {
            parts.Add("⚑" + t.requireFlag.Trim());
        }
        return parts.Count > 0 ? string.Join(" ", parts) : "-";
    }

    private static string Range(int min, int max)
    {
        string lo = min >= 0 ? min.ToString() : "·";
        string hi = max >= 0 ? max.ToString() : "·";
        if (min >= 0 && min == max) return "=" + min;
        return $"[{lo}..{hi}]";
    }

    private string ActionsSummary(List<TutorialAction> actions, TutorialUiSheet uiSheet)
    {
        if (actions == null || actions.Count == 0) return "-";

        var sb = new StringBuilder();
        for (int i = 0; i < actions.Count; i++)
        {
            if (i > 0) sb.Append("  ·  ");
            sb.Append(ActionShort(actions[i], uiSheet));
        }
        return sb.ToString();
    }

    private string ActionShort(TutorialAction a, TutorialUiSheet uiSheet)
    {
        if (a == null) return "(null)";
        switch (a.type)
        {
            case TutorialActionType.ShowUi:
                return $"UI({Safe(a.uiKey)}){Preview(uiSheet, a.uiKey)}";
            case TutorialActionType.ShowBlockingUi:
                return $"UI⏸({Safe(a.uiKey)}|need:{Safe(a.requiredActionId)}){Preview(uiSheet, a.uiKey)}";
            case TutorialActionType.HideUi:
                return "UI숨김";
            case TutorialActionType.ReleaseRuleEffects:
                return "효과해제";
            case TutorialActionType.AcquireFlowLock:
                return $"⏸잠금({a.lifetime})";
            case TutorialActionType.Spawn:
                return $"소환({Safe(a.spawnUnitId)}@{a.spawnGridNumber})";
            case TutorialActionType.SetHp:
                return $"HP={a.floatValue:0.##}→{Target(a)}";
            case TutorialActionType.SetInfluence:
                return $"IP={a.floatValue:0.##}→{Target(a)}";
            case TutorialActionType.AddMinimumHp:
                return $"최소HP={a.floatValue:0.##}({Target(a)},{a.lifetime})";
            case TutorialActionType.AddStatModifier:
                return $"스탯+({Target(a)},{a.lifetime})";
            case TutorialActionType.SetStep:
                return $"step:={a.intValue}";
            case TutorialActionType.SetFlag:
                return $"⚑{Safe(a.stringValue)}";
            case TutorialActionType.CustomHook:
                return $"훅:{Safe(a.stringValue)}";
            case TutorialActionType.CompleteTutorial:
                return "튜토리얼완료";
            default:
                return a.type.ToString();
        }
    }

    private static string Target(TutorialAction a)
    {
        string side = SideTag(a.targetSide);
        string id = string.IsNullOrWhiteSpace(a.targetTemplateId) ? "" : a.targetTemplateId.Trim();
        string t = (side + id).Trim();
        return string.IsNullOrEmpty(t) ? "?" : t;
    }

    private static string SideTag(TutorialUnitSide side)
    {
        switch (side)
        {
            case TutorialUnitSide.Player: return "P:";
            case TutorialUnitSide.Enemy: return "E:";
            default: return "";
        }
    }

    private static string Preview(TutorialUiSheet uiSheet, string key)
    {
        if (uiSheet == null || string.IsNullOrWhiteSpace(key)) return "";
        TutorialUiEntry e = uiSheet.Get(key);
        if (e == null || string.IsNullOrWhiteSpace(e.message)) return "";

        string m = e.message.Replace("\r", " ").Replace("\n", " ").Trim();
        if (m.Length > 22)
        {
            m = m.Substring(0, 22) + "…";
        }
        return $" “{m}”";
    }

    // (ZoneId,BattleKey) 매칭 UI 시트 best-effort 탐색. (zone/key 변경 시에만 재조회)
    private TutorialUiSheet ResolveUiSheet(TutorialScheduleSheet sheet)
    {
        if (cachedZone == sheet.ZoneId && string.Equals(cachedKey, sheet.BattleKey, StringComparison.Ordinal))
        {
            return cachedUiSheet;
        }

        cachedZone = sheet.ZoneId;
        cachedKey = sheet.BattleKey;
        cachedUiSheet = null;

        if (string.IsNullOrWhiteSpace(sheet.BattleKey))
        {
            return null;
        }

        try
        {
            string[] guids = AssetDatabase.FindAssets("t:TutorialUiSheet");
            TutorialUiSheet wildcard = null;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var s = AssetDatabase.LoadAssetAtPath<TutorialUiSheet>(path);
                if (s == null || !string.Equals(s.BattleKey, sheet.BattleKey, StringComparison.Ordinal))
                {
                    continue;
                }
                if (s.ZoneId == sheet.ZoneId)
                {
                    cachedUiSheet = s;
                    return s;
                }
                if (s.ZoneId == -1 && wildcard == null)
                {
                    wildcard = s;
                }
            }
            cachedUiSheet = wildcard;
        }
        catch (Exception)
        {
            cachedUiSheet = null;   // 미리보기는 편의 기능 — 실패해도 표는 정상
        }
        return cachedUiSheet;
    }
}
