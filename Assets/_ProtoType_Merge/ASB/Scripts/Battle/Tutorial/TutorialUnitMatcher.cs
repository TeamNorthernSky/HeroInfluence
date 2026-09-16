using System;

/// <summary>동일 템플릿 유닛이 여러 명일 때 어떤 대상을 고를지.</summary>
public enum UnitMatchMode
{
    First,
    All,
    Slot
}

/// <summary>
/// 튜토리얼 유닛 식별. BattleCharactor.UnitId는 인스턴스 접미사(_InstanceID)가 붙으므로
/// 그것으로 매칭하면 안 된다. 안정적인 템플릿 ID로 판정한다:
/// 적=EnemyScript.Data.Index, 아군=CharactorScript.Data.Index, 최종 폴백=UnitName.
/// </summary>
public static class TutorialUnitMatcher
{
    /// <summary>side + templateId로 매칭. templateId가 비면 side만 검사.</summary>
    public static bool MatchesTemplateId(BattleCharactor unit, TutorialUnitSide side, string templateId)
    {
        if (unit == null)
        {
            return false;
        }

        if (side == TutorialUnitSide.Player && !unit.IsPlayer)
        {
            return false;
        }

        if (side == TutorialUnitSide.Enemy && unit.IsPlayer)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(templateId))
        {
            return true;
        }

        string wanted = templateId.Trim();

        // 적: EnemyScript.Data.Index (예: FV20003)
        EnemyScript enemyScript = unit.GetComponent<EnemyScript>();
        if (enemyScript != null && enemyScript.Data != null &&
            !string.IsNullOrWhiteSpace(enemyScript.Data.Index) &&
            string.Equals(enemyScript.Data.Index.Trim(), wanted, StringComparison.Ordinal))
        {
            return true;
        }

        // 아군: CharactorScript.Data.Index (예: 10001)
        CharactorScript charactorScript = unit.GetComponent<CharactorScript>();
        if (charactorScript != null && charactorScript.Data != null &&
            !string.IsNullOrWhiteSpace(charactorScript.Data.Index) &&
            string.Equals(charactorScript.Data.Index.Trim(), wanted, StringComparison.Ordinal))
        {
            return true;
        }

        // 최종 폴백: 접미사 없는 UnitName
        return !string.IsNullOrWhiteSpace(unit.UnitName) &&
               string.Equals(unit.UnitName.Trim(), wanted, StringComparison.Ordinal);
    }
}
