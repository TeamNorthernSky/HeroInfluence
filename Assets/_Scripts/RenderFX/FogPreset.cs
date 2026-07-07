using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// [JC 신설 260706] fog 연출 파라미터 프리셋.
/// DHFogOfWar_PointSoftEdge 셰이더 프로퍼티 + FogRenderManager 가시성 값 + FogGridManager refog 규칙을
/// 한 벌로 묶은 ScriptableObject. RenderFXManager가 씬 진입 시 적용한다.
/// 플레이 중 이 에셋을 인스펙터에서 편집하면 즉시 반영되고(OnValidate→Changed 이벤트),
/// SO 에셋 특성상 플레이 종료 후에도 값이 보존된다 — 튜닝 결과의 저장소.
/// </summary>
[CreateAssetMenu(fileName = "FogPreset_", menuName = "HeroInfluence/RenderFX/Fog Preset")]
public class FogPreset : ScriptableObject
{
    /// <summary>에디터에서 값이 바뀔 때 발화. RenderFXManager가 구독해 활성 프리셋이면 재적용.</summary>
    public static event System.Action<FogPreset> Changed;

    [Header("높이 볼륨 (셰이더)")]
    [Tooltip("높이 대역 경계의 부드러움(월드 유닛). 경계 전후 이 폭에 걸쳐 대역이 섞임.\n※3단(Low/Mid/High)은 합산이 아니라 '지면 Y가 어느 대역인가'의 선택 — 평지 맵은 전부 Low")]
    [Range(0.1f, 2f)] public float heightTransition = 0.5f;
    [Tooltip("Low→Mid 경계 높이(월드 Y). 지면이 이보다 낮으면 Low(볼륨 내부) 취급.\n두 경계를 같게 두면 구 fogCeilingY 단일 경계와 동일. 평지에서 Mid/High를 보려면 지면보다 낮게 내릴 것")]
    public float fogLowTopY = 2f;
    [Tooltip("Mid→High 경계 높이(월드 Y). 지면이 이보다 높으면 High(볼륨 위 돌출) 취급. fogLowTopY 이상 권장")]
    public float fogHighStartY = 2f;

    [Header("안개 색 / 높이별 밀도 (셰이더)")]
    [Tooltip("Low(볼륨 내부) 컬러 — fogLowTopY 아래 지면에 적용. 평지 맵에서는 사실상 안개 전체의 색")]
    public Color fogColor = new Color(0.75f, 0.78f, 0.85f, 1f);
    [Tooltip("Mid(볼륨 표면) 컬러 — 지면 Y가 fogLowTopY~fogHighStartY 대역일 때 발현")]
    public Color fogColorMid = new Color(0.75f, 0.78f, 0.85f, 1f);
    [Tooltip("High(볼륨 위 돌출) 컬러 — 지면 Y가 fogHighStartY 위일 때 발현")]
    public Color fogColorHigh = new Color(0.75f, 0.78f, 0.85f, 1f);
    [Tooltip("Low 대역 불투명도(0=투명~1=완전 불투명). 평지 맵에서는 사실상 안개 전체의 농도")]
    [Range(0f, 1f)] public float fogDensityLow = 1f;
    [Tooltip("Mid 대역 불투명도 — 볼륨 표면(구름 윗면)에 남는 안개의 진하기")]
    [Range(0f, 1f)] public float fogDensityMid = 0.85f;
    [Tooltip("High 대역 불투명도 — 볼륨 위로 돌출한 지형(산봉우리)에 남는 얇은 안개의 진하기")]
    [Range(0f, 1f)] public float fogDensityHigh = 0.5f;

    [Header("노이즈 구름 (셰이더)")]
    [Tooltip("베이스 구름 무늬 스케일 — 큰 덩어리 패턴의 반복 밀도(맵 크기 기준 정규화). 클수록 덩어리가 잘게 쪼개짐")]
    public float noiseScaleBase = 6f;
    [Tooltip("디테일 잔결 스케일 — 베이스 위에 겹치는 미세 무늬. 보통 베이스의 2~4배 값")]
    public float noiseScaleDetail = 18f;
    [Tooltip("왜곡장 스케일 — 구름 무늬를 휘게 만드는 노이즈의 크기. 작을수록 넓고 완만하게 휨")]
    public float noiseScaleDistortion = 3f;
    [Tooltip("베이스 구름이 떠가는 속도 (0=정지)")]
    public float flowSpeedBase = 0.15f;
    [Tooltip("디테일 잔결의 흐름 속도 — 베이스와 다른 방향으로 흘러 뒤섞이는 느낌을 만듦")]
    public float flowSpeedDetail = 0.25f;
    [Tooltip("왜곡장의 흐름 속도 — 휘어짐 자체가 서서히 변하는 속도 (느릴수록 자연스러움)")]
    public float flowSpeedDistortion = 0.08f;
    [Tooltip("왜곡 강도 — 구름 무늬를 소용돌이치듯 휘게 하는 정도 (0=휘어짐 없음)")]
    public float distortionStrength = 0.3f;
    [Tooltip("노이즈 대비 — 클수록 덩어리·틈이 뚜렷한 뭉게구름, 작을수록 균일하게 뿌연 안개")]
    [Range(0.5f, 8f)] public float noiseContrast = 3.5f;

    [Header("구름 시트 (셰이더 — 고정 높이 평면층)")]
    [Tooltip("시트 불투명도 (0=시트 끔). 표면에 칠하는 base 안개 위에, 고정 높이 평면 기준의 반투명 구름층을 한 겹 덮어 벽면 밀착 티를 가림. 구름 질감을 시트에 맡기려면 base의 cloudContrast를 낮출 것")]
    [Range(0f, 1f)] public float sheetOpacity = 0.6f;
    [Tooltip("시트 평면 높이(월드 Y) — 이보다 높이 솟은 구조물은 시트를 뚫고 나옴")]
    public float sheetHeightY = 2f;
    [Tooltip("시트 색 — base 안개색과 분리 튜닝 가능. 노이즈 스케일/유속 파라미터는 base와 공유")]
    public Color sheetColor = new Color(0.75f, 0.78f, 0.85f, 1f);
    [Tooltip("구름 명암 폭 — 노이즈가 구름색을 밝고 어둡게 흔드는 범위 (0=균일). ※공유 파라미터: base 안개색 명암에도 함께 적용됨")]
    [Range(0f, 0.5f)] public float cloudContrast = 0.15f;
    [Tooltip("시트 범위 확대·축소 — 가장자리를 파티 시야반경 × (계수−1)만큼 밖(>1)/안(<1)으로 이동. sightRangeMultiplier의 시트 전용판")]
    [Range(0.1f, 3f)] public float sheetRangeMultiplier = 1f;
    [Tooltip("시트 가장자리 페이드 반폭(월드) — 시트가 이 폭에 걸쳐 서서히 차오름. base 경계 파라미터와 독립")]
    [Range(0.01f, 10f)] public float sheetFadeWidthWorld = 1f;
    [Tooltip("시차 보정 블렌드 — 0=구름이 실제 높이에 있는 물리적 시차 / 1=시트 구멍을 지면 안개 경계와 화면상 정렬. 카메라 각도에 따른 밀림 보정")]
    [Range(0f, 1f)] public float sheetGroundAlign = 0.5f;

    [Header("밝기 (셰이더)")]
    [Tooltip("Low(볼륨 내부) 밝기 배율 — 해당 대역 안개색에 곱해짐")]
    [Range(0.3f, 1.5f)] public float brightnessLow = 0.75f;
    [Tooltip("Mid(볼륨 표면) 밝기 배율")]
    [Range(0.3f, 1.5f)] public float brightnessMid = 1f;
    [Tooltip("High(볼륨 위 돌출) 밝기 배율")]
    [Range(0.3f, 1.5f)] public float brightnessHigh = 1.25f;

    [Header("시야 배율 (셰이더 — 시각 전용)")]
    [Tooltip("시야 범위 계수 — 데이터(탐사 로직)의 공개 범위는 그대로 두고, 렌더 경계만 파티 시야반경 × (계수−1)만큼 밖(>1)/안(<1)으로 이동. SDF 경로 전용")]
    [Range(0.1f, 3f)] public float sightRangeMultiplier = 1f;

    [Header("경계 SDF (FogOfWarHI 전용, 전부 월드 유닛)")]
    [Tooltip("경계 폭(블러) — 가시↔안개가 이 폭에 걸쳐 부드럽게 넘어감 (SDF 거리 기준 smoothstep 반폭). 작을수록 칼같은 경계")]
    [Range(0.01f, 10f)] public float edgeWidthWorld = 1f;
    [Tooltip("경계 거칠기 — 경계선을 안팎으로 흔드는 노이즈 진폭 (0=매끈한 등고선)")]
    [Range(0f, 5f)] public float edgeNoiseStrength = 0.5f;
    [Tooltip("경계 거칠기 노이즈 스케일 — 클수록 잘게, 작을수록 큼직하게 굽이침")]
    public float edgeNoiseScale = 0.35f;
    [Tooltip("경계 거칠기 노이즈 유속 — 경계 굴곡이 일렁이는 속도 (0=정지)")]
    public float edgeNoiseSpeed = 0.1f;
    [Tooltip("경계 농도 램프 — 안개가 비치기 시작하는 지점부터 안쪽으로 이 거리에 걸쳐 농도가 차오름 (0=끔). 평지에서도 보이는 경계 완화")]
    [Range(0f, 20f)] public float edgeFadeWidth = 3f;
    [Tooltip("경계 레이어 테이퍼 — high→mid→low 순으로 이 간격만큼 안쪽에서 걷힘 (0=동시). ※지형 높낮이가 있고 mid/high 밀도>0일 때만 시각화됨")]
    [Range(0f, 10f)] public float edgeLayerSpreadWorld = 1f;
    [Tooltip("경계 라운딩 — 경계선 모서리를 이 반경만큼 둥글림 (0=끔). SDF에 가우시안 블러를 걸어 사각 공개 모서리·격자 각짐을 완화")]
    [Range(0f, 10f)] public float edgeRoundingWorld = 2f;
    [Tooltip("※SDF 미바인딩 시 폴백 전용 — 셀 경계 smoothstep 반폭(셀 비율). SDF가 활성인 평상시엔 효과 없고, 경계 폭은 edgeWidthWorld가 담당")]
    [FormerlySerializedAs("edgeSoftness")]
    [Range(0.01f, 0.49f)] public float fallbackEdgeSoftness = 0.18f;

    [Header("디버그 (셰이더)")]
    [Tooltip("셰이더 디버그 모드 — 안개 합성 대신 가시성 값(visLow)을 흑백으로 표시")]
    public bool debugMode = false;

    [Header("가시성 단계 값 (FogRenderManager 런타임 반영)")]
    [Range(0f, 1f)] public float unexploredValue = 0f;
    [Tooltip("탐색 후(시야 밖) 값. 1=안개 완전 소멸(기획 확정 260706) / 0.5=반투명(구 DH 시각)")]
    [Range(0f, 1f)] public float foggedValue = 1f;
    [Range(0f, 1f)] public float visibleValue = 1f;

    [Header("재안개화 규칙 (FogGridManager 런타임 반영)")]
    public bool enableRefogByDay = false;
    [Min(1)] public int refogDelayDays = 3;

#if UNITY_EDITOR
    private void OnValidate()
    {
        Changed?.Invoke(this);
    }
#endif
}
