using System;
using UnityEngine;

public enum JcBuildingOutlineWidthMode
{
    [InspectorName("게임 공간 기준 (줌 연동)")] WorldSpace = 0,
    [InspectorName("화면 픽셀 고정")] ScreenPixels = 1
}

[Serializable]
public struct JcBuildingSilhouetteSettings
{
    [Tooltip("일반 건물이 이미 반투명일 때 파티 이동 중에는 불투명 상태로 복원하지 않습니다. 새로 가려진 건물은 즉시 반투명화합니다. 건물 외 장식에는 적용하지 않습니다.")]
    public bool holdBuildingFadeWhileMoving;
    [Tooltip("일반 건물의 가림이 해소된 상태가 연속으로 유지되어야 하는 시간(초)입니다. 기본 0.3초. 이동 중 유지가 켜져 있으면 정지 후부터 셉니다. 다시 가려지면 초기화합니다. 0이면 다음 검사에서 즉시 복원하며, 시간은 일시정지 배율과 무관합니다.")]
    [Min(0f)] public float buildingRestoreDelay;
    [Tooltip("일반 건물 19종에 실제 메시 교차 판정을 적용합니다. 끄면 기존 Bounds 판정입니다. 울타리·산·다리 등은 이 설정과 무관하게 기존 판정을 유지합니다.")]
    public bool preciseBuildingOcclusion;
    [Tooltip("파티 위치에서 판정 상자 중심까지의 월드 좌표 오프셋입니다. 파티 회전/크기를 따르지 않습니다. Y를 높이면 몸통 위쪽을 검사합니다.")]
    public Vector3 occlusionBoxOffset;
    [Tooltip("파티를 감싸는 가상 상자의 전체 가로(X)·높이(Y)·깊이(Z)입니다. 월드 단위이며 축별 최소 0.01입니다. 상자는 월드 축에 고정됩니다.")]
    public Vector3 occlusionBoxSize;
    [Tooltip("켜면 상자 중심이 건물 메시에 가려지는 즉시 반투명화합니다. 끄면 꼭지점 개수만 사용합니다. 가느다란 물체가 중심만 가려도 켰을 때는 반투명화됩니다.")]
    public bool occlusionCenterPriority;
    [Tooltip("고정된 꼭지점 8개 중 같은 건물에 가려져야 하는 최소 개수입니다. 1은 민감하고 5는 더 많이 가려져야 적용됩니다. 정면에서 겹쳐 보이는 앞뒤 꼭지점도 각각 셉니다.")]
    [Range(1, 5)] public int occlusionRequiredCorners;
    [Tooltip("이 오브젝트를 선택한 상태에서 Scene 뷰에 파티 판정 상자와 중심을 표시합니다. 게임 화면에는 표시하지 않습니다.")]
    public bool showOcclusionBox;
    [Tooltip("건물 내부 전체를 채우는 단색입니다. 원래 건물 재질과 조명 색은 섞지 않습니다.")]
    [ColorUsage(false)] public Color fillColor;

    [Tooltip("0 = 완전히 투명, 1 = 완전히 불투명. 이 컴포넌트가 켜져 있으면 DH의 occludedAlpha보다 우선합니다.")]
    [Range(0f, 1f)] public float opacity;

    [Tooltip("합쳐진 실루엣 경계의 윤곽선 색상입니다. 투명도는 별도의 윤곽선 불투명도에서 조절합니다.")]
    [ColorUsage(false)] public Color outlineColor;

    [Tooltip("채움과 독립적인 윤곽선 영역의 불투명도입니다. 0 = 투명, 1 = 완전히 불투명. 채움 불투명도가 0이어도 윤곽선은 남길 수 있습니다.")]
    [Range(0f, 1f)] public float outlineOpacity;

    [Tooltip("게임 공간 기준: 건물 표면의 거리에 맞춰 화면 두께가 변합니다(줌인 시 두껍게, 줌아웃 시 얇게). 화면 픽셀 고정: 카메라 줌과 무관하게 같은 픽셀 두께를 유지합니다.")]
    public JcBuildingOutlineWidthMode outlineWidthMode;

    [Tooltip("게임 공간 기준 모드의 두께입니다. 월드 단위이며 0이면 윤곽선을 끕니다. 기본 0.025. 건물 표면 깊이와 카메라 투영으로 화면 두께를 계산합니다.")]
    [Range(0f, 0.5f)] public float outlineWorldWidth;

    [Tooltip("화면 픽셀 고정 모드의 두께입니다. 0이면 윤곽선을 끕니다. 줌/렌더 스케일과 무관하게 최종 화면 픽셀 두께를 유지합니다.")]
    [Range(0f, 12f)] public float outlineWidth;

    [Tooltip("화면의 같은 지점에서 건물과 앞쪽 물체의 카메라 깊이를 비교할 때 허용하는 오차입니다.\n" +
             "기본 0.002 월드 단위, 범위 0~0.05. 0이면 엄격히 비교하며, 높이면 경계가 잘리는 현상을 줄일 수 있지만 앞쪽 물체 위로 건물 색이 비칠 수 있습니다.\n" +
             "파티의 가림 판정 거리나 반투명 적용 범위는 바꾸지 않습니다. 경계 문제가 없다면 기본값을 유지하세요.")]
    [Range(0f, 0.05f)] public float depthBias;

    public static JcBuildingSilhouetteSettings Default => new JcBuildingSilhouetteSettings
    {
        holdBuildingFadeWhileMoving = true,
        buildingRestoreDelay = 0.3f,
        preciseBuildingOcclusion = true,
        occlusionBoxOffset = new Vector3(0f, 0.8f, 0f),
        occlusionBoxSize = new Vector3(1.6f, 1.6f, 1.6f),
        occlusionCenterPriority = true,
        occlusionRequiredCorners = 3,
        showOcclusionBox = true,
        fillColor = new Color(0.38f, 0.48f, 0.58f, 1f),
        opacity = 0.45f,
        outlineColor = new Color(0.04f, 0.055f, 0.07f, 1f),
        outlineOpacity = 0.45f,
        outlineWidthMode = JcBuildingOutlineWidthMode.WorldSpace,
        outlineWorldWidth = 0.025f,
        outlineWidth = 2f,
        depthBias = 0.002f
    };

    public JcBuildingSilhouetteSettings Sanitized()
    {
        var value = this;
        value.buildingRestoreDelay = Mathf.Max(0f, value.buildingRestoreDelay);
        value.occlusionRequiredCorners = Mathf.Clamp(value.occlusionRequiredCorners, 1, 5);
        value.occlusionBoxSize = Vector3.Max(value.occlusionBoxSize, Vector3.one * 0.01f);
        value.fillColor.a = 1f;
        value.outlineColor.a = 1f;
        value.outlineOpacity = Mathf.Clamp01(value.outlineOpacity);
        value.outlineWorldWidth = Mathf.Clamp(value.outlineWorldWidth, 0f, 0.5f);
        value.outlineWidth = Mathf.Clamp(value.outlineWidth, 0f, 12f);
        value.opacity = Mathf.Clamp01(value.opacity);
        value.depthBias = Mathf.Clamp(value.depthBias, 0f, 0.05f);
        return value;
    }
}

[CreateAssetMenu(menuName = "JC Environment/건물 반투명 프리셋", fileName = "BUILDING_Silhouette")]
public sealed class JcBuildingSilhouettePreset : ScriptableObject
{
    public JcBuildingSilhouetteSettings settings = JcBuildingSilhouetteSettings.Default;
}
