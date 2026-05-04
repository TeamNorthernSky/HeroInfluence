using System;
using UnityEngine;


/// <summary>
/// 이번 턴에 "누가(Actor) 누구(Target)에게 어떤 행동(ActionType)을 하는지"를 표현하는 데이터.
/// 전투 실행 로직은 BattleManager가 담당합니다.
/// </summary>
[Serializable]
public sealed class BattleAction
{
    public BattleCharactor Actor { get; }
    public BattleCharactor Target { get; }
    public BattleActionType ActionType { get; }

    /// <summary>
    /// ActionType이 Skill일 때만 사용(선택)
    /// </summary>
    public SkillDataAsset SkillData { get; }

    public BattleAction(
        BattleCharactor actor,
        BattleCharactor target,
        BattleActionType actionType,
        SkillDataAsset skillData = null)
    {
        Actor = actor;
        Target = target;
        ActionType = actionType;
        SkillData = skillData;
    }
}

