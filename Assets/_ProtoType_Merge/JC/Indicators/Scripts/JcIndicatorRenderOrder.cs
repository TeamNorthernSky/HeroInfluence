namespace JC.Indicators
{
    /// <summary>바닥 안내 표시끼리의 순서를 유지하면서 기본 월드 UI(3000)보다 먼저 그립니다.</summary>
    internal static class JcIndicatorRenderOrder
    {
        // JC | 2026-11-06 | 트리거→이동 범위→건물 입장 강조→이동 표식→화살표 순서입니다.
        // 앞의 두 표시는 전용 패스에서 건물 강조보다 먼저 합성하며 월드 UI(3000)는 최상위를 유지합니다.
        internal const int MoveRangeGrid = 2850;
        internal const int TriggerFill = 2800;
        internal const int TriggerMain = 2801;
        internal const int TriggerOutline = 2802;
        internal const int MovementShadow = 2950;
        internal const int Movement = 2951;
        internal const int GuidanceArrow = 2980;
        // JC | 2026-11-04 | 건물 뒤 이벤트 메시를 바닥 안내보다 위, 기본 말풍선/UI보다 아래에 합성합니다.
        internal const int EventOcclusion = 2970;
    }
}
