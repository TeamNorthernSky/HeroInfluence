using UnityEngine;

/// <summary>
/// [JC 신설 260706] 그리드 영역 지정 안개 제어 단위.
/// RenderFXManager.SetFogZones()에 넘기면 _FogZoneTex로 구워져 FogOfWarHI 셰이더가 소비한다.
/// 지정 영역 밖은 기본값(밀도 1, 3레이어 전부 활성).
/// </summary>
[System.Serializable]
public struct FogZone
{
    [Tooltip("셀 단위 영역 (FogGridManager 그리드 좌표계)")]
    public RectInt cells;

    [Tooltip("안개 밀도 배율 (1=기본, 0=해당 영역 안개 없음)")]
    [Range(0f, 1f)] public float densityMultiplier;

    [Tooltip("low(볼륨 내부) 레이어 활성")]
    public bool lowEnabled;
    [Tooltip("mid(볼륨 표면) 레이어 활성")]
    public bool midEnabled;
    [Tooltip("high(볼륨 상단) 레이어 활성")]
    public bool highEnabled;

    public static FogZone Create(RectInt cells, float densityMultiplier = 1f,
        bool low = true, bool mid = true, bool high = true)
    {
        return new FogZone
        {
            cells = cells,
            densityMultiplier = Mathf.Clamp01(densityMultiplier),
            lowEnabled = low,
            midEnabled = mid,
            highEnabled = high
        };
    }
}
