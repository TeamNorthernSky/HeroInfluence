using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프로토타입 스킬 데이터: 식별 정보와 사거리(상대 좌표)만 담당합니다. 실행은 BattleManager가 담당합니다.
/// </summary>
[Serializable]
public class Skill
{
    [SerializeField] private string skillId;

    [Tooltip("스킬 배율(%). BattleManager.ExecuteGridSkill(Skill)에서 사용됩니다.")]
    public float skillPercent = 100f;

    [Tooltip("시전자 셀 기준 상대 그리드 오프셋 목록.")]
    public List<Vector2Int> skillBoundary = new List<Vector2Int>();

    public string SkillId => skillId;

    public bool ContainsRelativeOffset(Vector2Int relativeOffset)
    {
        if (skillBoundary == null)
        {
            return false;
        }

        return skillBoundary.Contains(relativeOffset);
    }

    /// <summary>인스펙터/코드에서 기본 인접 스킬 데이터를 만들 때 사용합니다.</summary>
    public static Skill CreateDefaultBasic()
    {
        return new Skill
        {
            skillId = "default_adjacent_skill",
            skillPercent = 100f,
            skillBoundary = new List<Vector2Int>
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            }
        };
    }
}
