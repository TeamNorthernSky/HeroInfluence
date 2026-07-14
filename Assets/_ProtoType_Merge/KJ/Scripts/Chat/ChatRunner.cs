using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [KJ 260714] 대화 진행 상태머신 (F008, MonoBehaviour 아님). 상태 = 현재 노드 하나.
/// 선형 진행(NextChatId) / 분기 확정(조건 필터 + A접미 대체) / 효과 실행(Set_Flag_* 내부, 그 외 이벤트 발행).
/// UI는 ChatModalController가 담당 — 러너는 데이터 순회만.
/// </summary>
public class ChatRunner
{
    private readonly ChatDatabase db;

    public ChatNode Current { get; private set; }

    /// <summary>Set_Flag_* 외의 효과(Start_Battle_* 등) 발행. 구독자 없으면 경고 로그만 (전투 연결은 범위 외).</summary>
    public static event Action<string> OnExternalEffect;

    public ChatRunner(ChatDatabase database)
    {
        db = database;
    }

    /// <summary>시작 노드로 이동. 실패(미존재 ID) 시 false.</summary>
    public bool Start(int chatId)
    {
        return MoveTo(chatId);
    }

    /// <summary>선형 다음 노드로. NextChatId 0/미존재 = false(종료).</summary>
    public bool AdvanceLinear()
    {
        return Current != null && MoveTo(Current.NextChatId);
    }

    /// <summary>
    /// 현재 노드의 분기 후보를 확정해 반환. 규칙:
    /// ①조건 불통과 항목 제외 ②같은 번호(숫자부)에서 A접미 변형이 통과하면 무접미 기본을 대체.
    /// 분기 없는 노드면 빈 목록.
    /// </summary>
    public List<BranchOption> ResolveOptions()
    {
        var result = new List<BranchOption>();
        if (Current == null || Current.BranchGroupId <= 0 || db == null) return result;

        IReadOnlyList<BranchOption> all = db.GetBranch(Current.BranchGroupId);
        if (all.Count == 0)
        {
            Debug.LogWarning($"[ChatRunner] Branch_Group_ID {Current.BranchGroupId} 데이터 없음 (노드 {Current.Id})");
            return result;
        }

        var chosenByNumber = new Dictionary<string, BranchOption>();
        var numberOrder = new List<string>();
        foreach (BranchOption option in all)
        {
            if (option == null || !ChatFlagStore.EvaluateCondition(option.Condition)) continue;

            string number = NumericPart(option.SelectionIndex);
            bool isVariant = (option.SelectionIndex?.Trim().Length ?? 0) > number.Length; // "1A" 등 접미 보유

            if (!chosenByNumber.ContainsKey(number))
            {
                chosenByNumber[number] = option;
                numberOrder.Add(number);
            }
            else if (isVariant)
            {
                chosenByNumber[number] = option; // 조건 통과한 변형이 기본을 대체
            }
        }

        for (int i = 0; i < numberOrder.Count; i++)
            result.Add(chosenByNumber[numberOrder[i]]);
        return result;
    }

    /// <summary>선택 실행: 효과 처리 후 TargetTalkId로 이동. 이동 대상 없음(0) = false(대화 종료).</summary>
    public bool Choose(BranchOption option)
    {
        if (option == null) return false;
        ApplyEffect(option.Effect);
        return MoveTo(option.TargetTalkId);
    }

    private bool MoveTo(int chatId)
    {
        Current = null;
        if (chatId <= 0 || db == null) return false;
        if (!db.TryGetNode(chatId, out ChatNode node))
        {
            Debug.LogWarning($"[ChatRunner] Chat_ID {chatId} 미존재 — 대화 종료 처리");
            return false;
        }

        Current = node;
        return true;
    }

    private static void ApplyEffect(string effect)
    {
        if (string.IsNullOrWhiteSpace(effect)) return;

        effect = effect.Trim();
        // "Set_Flag_X" → 플래그 "Flag_X"를 1로 (조건 문법 "Flag_X==1"과 키 일치)
        if (effect.StartsWith("Set_", StringComparison.Ordinal))
        {
            ChatFlagStore.Set(effect.Substring(4));
            return;
        }

        if (OnExternalEffect != null) OnExternalEffect.Invoke(effect);
        else Debug.LogWarning($"[ChatRunner] 외부 효과 '{effect}' 구독자 없음 — 무시 (전투 연결은 후속 작업)");
    }

    /// <summary>"1A" → "1". 숫자 접두부만 추출(없으면 원문 그대로).</summary>
    private static string NumericPart(string selectionIndex)
    {
        if (string.IsNullOrWhiteSpace(selectionIndex)) return string.Empty;
        selectionIndex = selectionIndex.Trim();
        int end = 0;
        while (end < selectionIndex.Length && char.IsDigit(selectionIndex[end])) end++;
        return end > 0 ? selectionIndex.Substring(0, end) : selectionIndex;
    }
}
