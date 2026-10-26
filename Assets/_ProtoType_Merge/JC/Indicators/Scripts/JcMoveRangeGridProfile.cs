using UnityEngine;

/// <summary>탐사와 튜토리얼의 이동범위 그리드가 함께 사용하는 영구 설정입니다.</summary>
public sealed class JcMoveRangeGridProfile : ScriptableObject
{
    public const string ResourceName = "JcMoveRangeGridProfile";
    [Header("Movement Grid")]
    [Tooltip("이동 가능 그리드의 색상입니다. 투명도는 아래 항목에서 별도로 조절합니다.")]
    public Color gridColor = new Color(0.15f, 1f, 0.35f, 1f);
    [Range(0f, 1f), Tooltip("그리드의 불투명도입니다. 0은 투명, 1은 불투명입니다.")]
    public float gridOpacity = 0.4f;
    [Min(0f), Tooltip("주변 조명과 무관한 선의 자체 발광 강도입니다. 1보다 큰 HDR 값은 카메라의 Bloom이 켜져 있을 때 빛 번짐에도 반영됩니다.")]
    public float gridEmissionIntensity = 3f;
    [Min(0.001f), Tooltip("그리드 선의 월드 단위 두께입니다. 셀 크기의 1/4 이하로 제한합니다.")]
    public float gridWidth = 0.025f;
    [Range(0f, 0.5f), Tooltip("선 두께 대비 가장자리의 부드러운 감쇠 폭입니다. 0은 기본 픽셀 안티앨리어싱만 적용하며, 높일수록 선 경계가 부드러워집니다.")]
    public float gridEdgeSoftness = 0.15f;
    [Tooltip("셀 지면 위로 띄우는 월드 높이입니다. 최소 0.001이며, 탐사·튜토리얼에 공통 저장됩니다. 연석 전체를 넘기는 높이는 아닙니다.")]
    public float lineYOffset = 0.035f;

    [Header("Light Curtain")]
    [Min(0f), Tooltip("선을 따라 올라오는 빛 장막의 월드 단위 높이입니다. 0이면 장막을 숨깁니다.")]
    public float curtainHeight = 0.18f;
    [Range(0f, 1f), Tooltip("빛 장막의 불투명도입니다. 선의 투명도와 별도로 적용합니다.")]
    public float curtainOpacity = 0.16f;
    [Min(0f), Tooltip("빛 장막의 자체 발광 강도입니다.")]
    public float curtainIntensity = 1.5f;
    [Range(0.5f, 6f), Tooltip("위로 올라갈수록 장막이 흐려지는 정도입니다. 높을수록 윗부분이 더 빨리 사라집니다.")]
    public float curtainSoftness = 2f;


    [Header("화면 크기와 야간 보정")]
    [Tooltip("켜면 거리·시야각·원근에 따라 월드 높이를 보정해 화면에서 커튼 크기를 일정하게 유지합니다. 공통 프로필에 저장됩니다.")]
    public bool uniformCurtainSize = true;
    [Range(.001f, .05f), Tooltip("화면 높이에 대한 커튼의 목표 크기입니다. 0.012는 화면 높이의 1.2%입니다. 기존 Curtain Height가 0이면 표시하지 않습니다.")]
    public float curtainScreenHeight = .012f;
    [Min(.01f), Tooltip("화면 크기 보정 시 허용하는 최대 월드 높이입니다. 수직을 내려다보는 각도 등에서 과도한 확대를 제한합니다.")]
    public float curtainMaxHeight = 1.5f;
    [Tooltip("기존 낮밤 조명의 밝기 비율로 선과 커튼 발광을 자동 조절합니다. 낮밤 시스템이 없는 씬은 원래 강도를 유지합니다.")]
    public bool adaptToDayNight = true;
    [Range(0, 1), Tooltip("완전한 밤의 발광 배율입니다. 낮은 1배이며 해질녘에는 부드럽게 보간합니다. 0은 밤의 발광색을 검정으로 만드므로 시인성을 위해 0보다 큰 값을 권장합니다.")]
    public float nightIntensityMultiplier = .2f;

    private void OnValidate()
    {
        curtainScreenHeight = Mathf.Clamp(curtainScreenHeight, .001f, .05f);
        curtainMaxHeight = Mathf.Max(.01f, curtainMaxHeight);
        nightIntensityMultiplier = Mathf.Clamp01(nightIntensityMultiplier);
        gridOpacity = Mathf.Clamp01(gridOpacity);
        gridEmissionIntensity = Mathf.Max(0, gridEmissionIntensity);
        gridWidth = Mathf.Max(.001f, gridWidth);
        gridEdgeSoftness = Mathf.Clamp(gridEdgeSoftness, 0, .5f);
        lineYOffset = Mathf.Max(.001f, lineYOffset);
        curtainHeight = Mathf.Max(0, curtainHeight);
        curtainOpacity = Mathf.Clamp01(curtainOpacity);
        curtainIntensity = Mathf.Max(0, curtainIntensity);
        curtainSoftness = Mathf.Clamp(curtainSoftness, .5f, 6);
    }
}
