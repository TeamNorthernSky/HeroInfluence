using UnityEngine;

// JC | 2026-11-10 | 협회 방문 표시 전용 연출값. 원본 위치/크기에 대한 비율이므로 건물 배율을 따라갑니다.
[CreateAssetMenu(menuName = "JC/Indicators/Association Visit Profile")]
public sealed class JcAssociationIndicatorProfile : ScriptableObject
{
    [InspectorName("확대·축소 연출 사용")] public bool animate = true;
    [InspectorName("확대 시간 (초)"), Min(0)] public float growSeconds = 0.2f;
    [InspectorName("축소 시간 (초)"), Min(0)] public float shrinkSeconds = 0.15f;
    [InspectorName("최소 크기 배율"), Min(0)] public float minimumScale = 0.15f;
    [InspectorName("최대 크기 배율"), Min(0.001f)] public float maximumScale = 1f;
    [InspectorName("등장 상승량 / 표시 높이"), Min(0)] public float riseHeightRatio = 0.06f;
    [Tooltip("원본 이미지의 좌하단=0,0 / 우상단=1,1. 협회 방문 이미지의 아래 뾰족한 끝을 기본으로 사용합니다.")]
    [InspectorName("스케일 피봇 (이미지 비율)")] public Vector2 tipPivot = new Vector2(0.463f, 0.002f);
    [InspectorName("체류 플로팅 사용")] public bool floating = true;
    [InspectorName("플로팅 주기 (초)"), Min(0.05f)] public float floatPeriod = 1.6f;
    [InspectorName("플로팅 진폭 / 표시 높이"), Min(0)] public float floatHeightRatio = 0.02f;
    [InspectorName("시간 배율에 영향받지 않음")] public bool unscaledTime = true;

    // JC | 2026-11-10 | 플레이 중 숫자 직접 입력도 허용하되 0 나눗셈과 역전된 크기를 방지합니다.
    public void Sanitize()
    {
        growSeconds = Mathf.Max(0, growSeconds);
        shrinkSeconds = Mathf.Max(0, shrinkSeconds);
        maximumScale = Mathf.Max(0.001f, maximumScale);
        minimumScale = Mathf.Clamp(minimumScale, 0, maximumScale);
        riseHeightRatio = Mathf.Max(0, riseHeightRatio);
        floatPeriod = Mathf.Max(0.05f, floatPeriod);
        floatHeightRatio = Mathf.Max(0, floatHeightRatio);
        tipPivot = new Vector2(Mathf.Clamp01(tipPivot.x), Mathf.Clamp01(tipPivot.y));
    }
    private void OnValidate() => Sanitize();
}
