namespace JC.Indicators
{
    /// <summary>바닥 안내 표시끼리의 순서를 유지하면서 기본 월드 UI(3000)보다 먼저 그립니다.</summary>
    internal static class JcIndicatorRenderOrder
    {
        internal const int MoveRangeGrid = 2850;
        internal const int TriggerFill = 2900;
        internal const int TriggerMain = 2901;
        internal const int TriggerOutline = 2902;
        internal const int MovementShadow = 2950;
        internal const int Movement = 2951;
        // JC | 2026-11-04 | 건물 뒤 이벤트 메시를 바닥 안내보다 위, 기본 말풍선/UI보다 아래에 합성합니다.
        internal const int EventOcclusion = 2970;
    }
}
