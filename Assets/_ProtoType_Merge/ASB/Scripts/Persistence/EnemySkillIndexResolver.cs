using System.Globalization;
using System.Reflection;

/// <summary>
/// 영속 적 유닛 → CSV 적 스킬 인덱스 (enemyTemplateId * 10 + 1) 규칙.
/// </summary>
public static class EnemySkillIndexResolver
{
    public static int ResolveSkillIndexFromPersistent(EnemyUnitPersistentData persistentData, EnemyData fallbackData)
    {
        if (persistentData != null)
        {
            PropertyInfo skillIndexProperty = persistentData.GetType().GetProperty("CurrentSkillIndex");
            if (skillIndexProperty != null &&
                skillIndexProperty.GetValue(persistentData) is int reflectedSkillIndex &&
                reflectedSkillIndex > 0)
            {
                return reflectedSkillIndex;
            }

            FieldInfo skillIndexField = persistentData.GetType().GetField(
                "currentSkillIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (skillIndexField != null &&
                skillIndexField.GetValue(persistentData) is int reflectedFieldSkillIndex &&
                reflectedFieldSkillIndex > 0)
            {
                return reflectedFieldSkillIndex;
            }

            if (!string.IsNullOrWhiteSpace(persistentData.UnitTemplateKey) &&
                int.TryParse(
                    persistentData.UnitTemplateKey.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int templateId) &&
                templateId > 0)
            {
                return (templateId * 10) + 1;
            }
        }

        if (fallbackData != null &&
            !string.IsNullOrWhiteSpace(fallbackData.Index) &&
            int.TryParse(
                fallbackData.Index.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int enemyIndexNum))
        {
            return (enemyIndexNum * 10) + 1;
        }

        return 0;
    }
}
