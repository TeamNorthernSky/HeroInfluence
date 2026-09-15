using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>튜토리얼 규칙 하나 = 트리거(언제) + 액션들(무엇). ruleId는 유일(once·효과 scopeId·activeUiRequest 키).</summary>
[Serializable]
public sealed class TutorialRuleEntry
{
    public string ruleId;
    public string purpose;   // 오버뷰 라벨(동작 무관)
    public TutorialTrigger trigger = new TutorialTrigger();
    public List<TutorialAction> actions = new List<TutorialAction>();
    public bool once = true;

    public bool HasPausingAction()
    {
        for (int i = 0; i < actions.Count; i++)
        {
            TutorialAction a = actions[i];
            if (a != null && (a.type == TutorialActionType.ShowBlockingUi || a.type == TutorialActionType.AcquireFlowLock))
            {
                return true;
            }
        }
        return false;
    }
}
