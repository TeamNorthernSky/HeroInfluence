using UnityEngine;

/// <summary>
/// 교환소(자원 교환) 데이터·환산 로직. 기획서 'H.I_협회 시스템 2.0v.xlsx > 교환소' 기준.
/// 추후 CSV로 갈아끼울 수 있도록 순수 static 테이블로 분리(런타임 상태 없음).
///
/// 핵심 규칙 — 기준 묶음(배치) 단위 교환:
///  - 지불 기준 묶음 단위: 골드(Money) 100 / 그 외(메달·수정·자재) 5
///  - 1묶음당 수령량 = 표 기본값 + 강화 보너스(묶음당). 골드 수령은 보너스 없음.
///  - 손실은 비율 자체에 내재(별도 수수료 없음).
///  - 교환소 강화 레벨(HQDepartment.Exchange, 0=미해금 / 1~3)에 따라 효율 증가.
///
/// 자원 매핑: 골드=Money, 메달=Chip, 수정=Crystal, 자재=Supply.
/// </summary>
public static class ExchangeData
{
    /// <summary>지불 재화별 기준 묶음 단위(한 번에 지불하는 최소 단위).</summary>
    public static int GetBatchUnit(ResourceType give)
        => give == ResourceType.Money ? 100 : 5;

    /// <summary>
    /// 1묶음 지불 시 기본 수령량 [지불][수령]. 0 = 교환 불가(자기 자신 또는 미정의).
    /// 표(대각선=자기 재화=교환 불가):
    ///  골드100 → 메달3 / 수정5 / 자재10
    ///  메달5   → 골드50 / 수정3 / 자재3
    ///  수정5   → 골드50 / 메달2 / 자재10
    ///  자재5   → 골드50 / 메달1 / 수정3
    /// </summary>
    public static int GetBaseReceive(ResourceType give, ResourceType receive)
    {
        switch (give)
        {
            case ResourceType.Money:
                switch (receive) { case ResourceType.Chip: return 3; case ResourceType.Crystal: return 5; case ResourceType.Supply: return 10; }
                break;
            case ResourceType.Chip:
                switch (receive) { case ResourceType.Money: return 50; case ResourceType.Crystal: return 3; case ResourceType.Supply: return 3; }
                break;
            case ResourceType.Crystal:
                switch (receive) { case ResourceType.Money: return 50; case ResourceType.Chip: return 2; case ResourceType.Supply: return 10; }
                break;
            case ResourceType.Supply:
                switch (receive) { case ResourceType.Money: return 50; case ResourceType.Chip: return 1; case ResourceType.Crystal: return 3; }
                break;
        }
        return 0;
    }

    /// <summary>
    /// 강화 보너스(묶음당). 교환소 레벨 1~3. 골드 수령은 항상 0.
    ///  Lv.1: 메달+1 / 수정+1 / 자재+1
    ///  Lv.2: 메달+1 / 수정+2 / 자재+3
    ///  Lv.3: 메달+1 / 수정+3 / 자재+5
    /// </summary>
    public static int GetUpgradeBonus(int exchangeLevel, ResourceType receive)
    {
        if (receive == ResourceType.Money) return 0;
        switch (exchangeLevel)
        {
            case 1: return 1;
            case 2: return receive == ResourceType.Chip ? 1 : (receive == ResourceType.Crystal ? 2 : 3);
            case 3: return receive == ResourceType.Chip ? 1 : (receive == ResourceType.Crystal ? 3 : 5);
            default: return 0; // 0 이하(미해금) 또는 범위 밖
        }
    }

    /// <summary>교환 가능한 (지불, 수령) 조합인지.</summary>
    public static bool CanExchange(ResourceType give, ResourceType receive)
        => give != receive && GetBaseReceive(give, receive) > 0;

    /// <summary>1묶음당 실제 수령량(기본 + 강화 보너스). 교환 불가면 0.</summary>
    public static int GetReceivePerBatch(ResourceType give, ResourceType receive, int exchangeLevel)
    {
        int b = GetBaseReceive(give, receive);
        if (b <= 0) return 0;
        return b + GetUpgradeBonus(exchangeLevel, receive);
    }
}
