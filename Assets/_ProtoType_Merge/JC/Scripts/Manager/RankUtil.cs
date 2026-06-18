using UnityEngine;

/// <summary>
/// [JC 260619] 레벨 → 랭크 알파벳 변환(성장 시스템 V6.0 '레벨 경험치 테이블').
/// Lv1=N, Lv2=M, … Lv9=F, Lv10=E, … Lv14=A, Lv15=S.
/// 랭크는 레벨에서 파생되므로 별도 저장 없이 표시 시 계산한다.
/// </summary>
public static class RankUtil
{
    /// <summary>레벨에 대응하는 랭크 문자. Lv 1~14는 N→A(역순), Lv 15+는 S.</summary>
    public static string FromLevel(int level)
    {
        if (level >= 15) return "S";
        int lv = Mathf.Clamp(level, 1, 14);
        // Lv1 → 'N'(='A'+13), Lv14 → 'A'(='A'+0)
        char c = (char)('A' + (14 - lv));
        return c.ToString();
    }
}
