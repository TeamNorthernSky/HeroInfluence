using UnityEngine;

public sealed partial class BattleCellBorderVisual
{
    // JC | 2026-11-04 | 이웃한 셀은 하나의 영역으로 표시하고 단일 전투 셀은 기존 테두리 경로를 사용합니다.
    private BattleCellBorderMesh.Region region;
    private float regionInset;
    private Color? regionOuterColor, regionInnerColor;
    // JC | 2026-11-04 | 부위별 원래 알파와 가림 배율을 별도로 전달해 꺼진 선이 투과로 다시 나타나지 않게 합니다.
    private float regionBuildingOpacity=.15f, regionSidewalkOpacity=.05f, regionFillSidewalkOpacity=.25f;

    /// <summary>기존 전투 테두리의 재질·표현과 같은 경로로 셀 합집합을 표시합니다.</summary>
    public void SetRegion(BattleCellBorderMesh.Region value)
    {
        region = value;
        hasGeometry = false;
    }
    public void RenderRegion(BattleCellBorderConfiguration config, float clock, Color? outerColor = null, Color? innerColor = null, float buildingOpacity = .15f, float sidewalkOpacity = .05f, float fillSidewalkOpacity = .25f)
    {
        regionBuildingOpacity = Mathf.Clamp01(buildingOpacity); regionSidewalkOpacity = Mathf.Clamp01(sidewalkOpacity);
        regionFillSidewalkOpacity = Mathf.Clamp01(fillSidewalkOpacity);
        regionOuterColor = outerColor;
        regionInnerColor = innerColor;
        RenderProfile(config, BattleCellVisualState.CurrentTurn, 1, clock);
    }
}
