using UnityEngine;

/// <summary>
/// 교환소(자원 교환) 데이터·환산 로직. 기획 'H.I UI 기획서 V3.0 > 협회(교환소)',
/// 교환비 정본 'H.I 자원 데이터 테이블 V1.4 > 협회-교환소' 기준.
/// 추후 CSV로 갈아끼울 수 있도록 순수 static 테이블로 분리(런타임 상태 없음).
///
/// 핵심 규칙 — 기준 묶음(배치) 단위 교환:
///  - 지불 기준 묶음 단위: 자금(Money) 100 / 그 외(메달·수정·자재) 5
///  - 1묶음당 수령량 = 표 기본값(Lv.1) + 강화 보너스(묶음당).
///  - 방향성(기획 V3.0): 자금 → 타자원 가능 / 타자원 → 자금 불가.
///  - 교환소 강화 레벨(HQDepartment.Exchange, 0=미해금 / 1~3)에 따라 효율 증가.
///
/// 자원 매핑: 자금=Money, 메달=Chip, 수정=Crystal, 자재=Supply.
/// </summary>
public static class ExchangeData
{
    /// <summary>지불 재화별 기준 묶음 단위(한 번에 지불하는 최소 단위).</summary>
    public static int GetBatchUnit(ResourceType give)
        => give == ResourceType.Money ? 100 : 5;

    /// <summary>
    /// 1묶음 지불 시 기본 수령량(교환소 Lv.1) [지불][수령]. 0 = 교환 불가.
    /// 데이터 정본: 'H.I 자원 데이터 테이블 V1.4 > 협회-교환소'.
    /// 표(대각선=자기 재화 불가, → 자금(타자원→자금)도 불가):
    ///  자금100 → 메달3 / 수정5 / 자재10
    ///  메달5   → 수정3 / 자재3
    ///  수정5   → 메달2 / 자재10
    ///  자재5   → 메달1 / 수정3
    /// </summary>
    public static int GetBaseReceive(ResourceType give, ResourceType receive)
    {
        // 타자원 → 자금 교환 불가 (기획 V3.0)
        if (receive == ResourceType.Money) return 0;
        switch (give)
        {
            case ResourceType.Money:
                switch (receive) { case ResourceType.Chip: return 3; case ResourceType.Crystal: return 5; case ResourceType.Supply: return 10; }
                break;
            case ResourceType.Chip:
                switch (receive) { case ResourceType.Crystal: return 3; case ResourceType.Supply: return 3; }
                break;
            case ResourceType.Crystal:
                switch (receive) { case ResourceType.Chip: return 2; case ResourceType.Supply: return 10; }
                break;
            case ResourceType.Supply:
                switch (receive) { case ResourceType.Chip: return 1; case ResourceType.Crystal: return 3; }
                break;
        }
        return 0;
    }

    /// <summary>
    /// 강화 보너스(묶음당, 누계값). 교환소 레벨 1~3. 자금 수령은 항상 0.
    /// 데이터 정본: 'H.I 자원 데이터 테이블 V1.4 > 협회-교환소'.
    ///  Lv.1: +0 (기본값 그대로)
    ///  Lv.2: 메달+1 / 수정+1 / 자재+1
    ///  Lv.3: 메달+3 / 수정+4 / 자재+5
    /// </summary>
    public static int GetUpgradeBonus(int exchangeLevel, ResourceType receive)
    {
        if (receive == ResourceType.Money) return 0;
        switch (exchangeLevel)
        {
            case 1: return 0;
            case 2: return 1;
            case 3: return receive == ResourceType.Chip ? 3 : (receive == ResourceType.Crystal ? 4 : 5);
            default: return 0; // 0 이하(미해금) 또는 범위 밖
        }
    }

    /// <summary>교환 가능한 (지불, 수령) 조합인지. 자기 재화·타자원→자금은 불가.</summary>
    public static bool CanExchange(ResourceType give, ResourceType receive)
        => give != receive && receive != ResourceType.Money && GetBaseReceive(give, receive) > 0;

    /// <summary>1묶음당 실제 수령량(기본 + 강화 보너스). 교환 불가면 0.</summary>
    public static int GetReceivePerBatch(ResourceType give, ResourceType receive, int exchangeLevel)
    {
        int b = GetBaseReceive(give, receive);
        if (b <= 0) return 0;
        return b + GetUpgradeBonus(exchangeLevel, receive);
    }
}
