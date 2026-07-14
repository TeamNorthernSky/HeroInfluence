using System;
using UnityEngine;

/// <summary>
/// [JC 신설 260706 / 260714 상태별 레이어 재구조화] fog 연출 파라미터 프리셋.
/// 안개 룩(높이 볼륨·색/밀도·노이즈 구름·구름 시트·밝기)을 FogLayerSettings로 묶어
/// Unexplored(미탐)·Fogged(탐사됨·비가시) 두 상태가 각자의 룩을 가진다 — 인스펙터에서 폴드 2개.
/// 경계 SDF·시야 배율·가시성/refog는 두 레이어의 이음새라 공유(루트 레벨).
/// 플레이 중 이 에셋을 인스펙터에서 편집하면 즉시 반영되고(OnValidate→Changed 이벤트),
/// SO 에셋 특성상 플레이 종료 후에도 값이 보존된다 — 튜닝 결과의 저장소.
/// </summary>
[CreateAssetMenu(fileName = "FogPreset_", menuName = "HeroInfluence/RenderFX/Fog Preset")]
public class FogPreset : ScriptableObject
{
    /// <summary>에디터에서 값이 바뀔 때 발화. RenderFXManager가 구독해 활성 프리셋이면 재적용.</summary>
    public static event System.Action<FogPreset> Changed;

    /// <summary>
    /// 안개 상태 1개의 완전한 룩. Unexplored/Fogged가 각자 한 벌씩 가진다.
    /// 필요 없는 파라미터는 튜닝 과정에서 지목 시 프루닝 예정.
    /// </summary>
    [Serializable]
    public class FogLayerSettings
    {
        [Header("높이 볼륨")]
        [Tooltip("높이 대역 경계의 부드러움(월드 유닛). 경계 전후 이 폭에 걸쳐 대역이 섞임")]
        [Range(0.1f, 2f)] public float heightTransition = 0.5f;
        [Tooltip("Low→Mid 경계 높이(월드 Y). 지면이 이보다 낮으면 Low(볼륨 내부) 취급")]
        public float fogLowTopY = 2f;
        [Tooltip("Mid→High 경계 높이(월드 Y). 지면이 이보다 높으면 High(볼륨 위 돌출) 취급. fogLowTopY 이상 권장")]
        public float fogHighStartY = 2f;

        [Header("안개 색 / 높이별 밀도")]
        [Tooltip("Low(볼륨 내부) 컬러 — 평지 맵에서는 사실상 안개 전체의 색")]
        public Color fogColor = new Color(0.75f, 0.78f, 0.85f, 1f);
        [Tooltip("Mid(볼륨 표면) 컬러")]
        public Color fogColorMid = new Color(0.75f, 0.78f, 0.85f, 1f);
        [Tooltip("High(볼륨 위 돌출) 컬러")]
        public Color fogColorHigh = new Color(0.75f, 0.78f, 0.85f, 1f);
        [Tooltip("Low 대역 불투명도. Fogged 레이어에서 0이면 지면 안개가 사라져 오브젝트가 보임")]
        [Range(0f, 1f)] public float fogDensityLow = 1f;
        [Tooltip("Mid 대역 불투명도 — 볼륨 표면(구름 윗면)에 남는 안개의 진하기")]
        [Range(0f, 1f)] public float fogDensityMid = 0.85f;
        [Tooltip("High 대역 불투명도 — 볼륨 위로 돌출한 지형에 남는 얇은 안개의 진하기")]
        [Range(0f, 1f)] public float fogDensityHigh = 0.5f;

        [Header("노이즈 구름")]
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
        [Tooltip("구름 덮음 비율 — 낮출수록 덩어리 사이가 뚫려 듬성듬성해짐 (1=빈틈 없음). base 농도에는 cloudDensityEffect와 조합, 시트에는 직접 적용")]
        [Range(0f, 1f)] public float cloudCoverage = 1f;
        [Tooltip("구름의 농도 관여 — 0=색 명암만(구름 모양이 안개 농도에 영향 없음, 현행), 1=구름 틈은 안개도 투명")]
        [Range(0f, 1f)] public float cloudDensityEffect = 0f;

        [Header("구름 시트 (고정 높이 평면층)")]
        [Tooltip("시트 불투명도 (0=시트 끔 — 노이즈 평가도 생략되어 비용 0)")]
        [Range(0f, 1f)] public float sheetOpacity = 0.6f;
        [Tooltip("시트 평면 높이(월드 Y) — 이보다 높이 솟은 구조물은 시트를 뚫고 나옴")]
        public float sheetHeightY = 2f;
        [Tooltip("시트 색 — base 안개색과 분리 튜닝 가능")]
        public Color sheetColor = new Color(0.75f, 0.78f, 0.85f, 1f);
        [Tooltip("구름 명암 폭 — 노이즈가 구름색을 밝고 어둡게 흔드는 범위 (0=균일). base 색 명암에도 함께 적용")]
        [Range(0f, 0.5f)] public float cloudContrast = 0.15f;
        [Tooltip("시트 범위 확대·축소 — 가장자리를 파티 시야반경 × (계수−1)만큼 밖(>1)/안(<1)으로 이동")]
        [Range(0.1f, 3f)] public float sheetRangeMultiplier = 1f;
        [Tooltip("시트 가장자리 페이드 반폭(월드) — 시트가 이 폭에 걸쳐 서서히 차오름")]
        [Range(0.01f, 10f)] public float sheetFadeWidthWorld = 1f;
        [Tooltip("시차 보정 블렌드 — 0=구름이 실제 높이에 있는 물리적 시차 / 1=시트 구멍을 지면 안개 경계와 화면상 정렬")]
        [Range(0f, 1f)] public float sheetGroundAlign = 0.5f;

        [Header("밝기")]
        [Tooltip("Low(볼륨 내부) 밝기 배율 — 해당 대역 안개색에 곱해짐")]
        [Range(0.3f, 1.5f)] public float brightnessLow = 0.75f;
        [Tooltip("Mid(볼륨 표면) 밝기 배율")]
        [Range(0.3f, 1.5f)] public float brightnessMid = 1f;
        [Tooltip("High(볼륨 위 돌출) 밝기 배율")]
        [Range(0.3f, 1.5f)] public float brightnessHigh = 1.25f;
    }

    /// <summary>
    /// 두 레이어가 공유하는 항목 — 경계는 상태 간 이음새라 한 벌이어야 크로스페이드가 성립한다.
    /// 가시성 단계 값(구 농도 3종)은 SDF 파이프라인에서 무효라 260714 삭제 —
    /// 폴백 경로는 씬 FogRenderManager(DH)의 자체 인스펙터 값을 그대로 쓴다.
    /// </summary>
    [Serializable]
    public class SharedFogSettings
    {
        [Header("시야 배율 (시각 전용)")]
        [Tooltip("시야 범위 계수 — 데이터(탐사 로직)의 공개 범위는 그대로 두고, 렌더 경계만 파티 시야반경 × (계수−1)만큼 밖(>1)/안(<1)으로 이동. SDF 경로 전용")]
        [Range(0.1f, 3f)] public float sightRangeMultiplier = 1f;

        [Header("경계 SDF (상태 간 이음새, 전부 월드 유닛)")]
        [Tooltip("경계 폭(블러) — 가시↔안개가 이 폭에 걸쳐 부드럽게 넘어감 (SDF 거리 기준 smoothstep 반폭). 작을수록 칼같은 경계")]
        [Range(0.01f, 10f)] public float edgeWidthWorld = 1f;
        [Tooltip("경계 거칠기 — 경계선을 안팎으로 흔드는 노이즈 진폭 (0=매끈한 등고선)")]
        [Range(0f, 5f)] public float edgeNoiseStrength = 0.5f;
        [Tooltip("경계 거칠기 노이즈 스케일 — 클수록 잘게, 작을수록 큼직하게 굽이침")]
        public float edgeNoiseScale = 0.35f;
        [Tooltip("경계 거칠기 노이즈 유속 — 경계 굴곡이 일렁이는 속도 (0=정지)")]
        public float edgeNoiseSpeed = 0.1f;
        [Tooltip("경계 농도 램프 — 안개가 비치기 시작하는 지점부터 안쪽으로 이 거리에 걸쳐 농도가 차오름 (0=끔)")]
        [Range(0f, 20f)] public float edgeFadeWidth = 3f;
        [Tooltip("경계 레이어 테이퍼 — high→mid→low 순으로 이 간격만큼 안쪽에서 걷힘 (0=동시)")]
        [Range(0f, 10f)] public float edgeLayerSpreadWorld = 1f;
        [Tooltip("경계 라운딩 — 경계선 모서리를 이 반경만큼 둥글림 (0=끔). SDF에 가우시안 블러를 걸어 사각 공개 모서리·격자 각짐을 완화")]
        [Range(0f, 10f)] public float edgeRoundingWorld = 2f;
        [Tooltip("상태 전이 폭 — Fogged↔Unexplored 룩이 크로스페이드되는 반폭(월드). 경계 블러(edgeWidthWorld)와 분리된 값 — 구획 경계를 완만하게 하려면 이 값을 키울 것")]
        [Range(0.01f, 20f)] public float stateBlendWidthWorld = 1f;
        [Tooltip("※SDF 미바인딩 시 폴백 전용 — 셀 경계 smoothstep 반폭(셀 비율). SDF가 활성인 평상시엔 효과 없고, 경계 폭은 edgeWidthWorld가 담당")]
        [Range(0.01f, 0.49f)] public float fallbackEdgeSoftness = 0.18f;

        [Header("디버그")]
        [Tooltip("셰이더 디버그 모드 — 안개 합성 대신 가시성 값(visLow)을 흑백으로 표시")]
        public bool debugMode = false;
    }

    [Tooltip("Unexplored(미탐) 지역의 안개 룩 — 기본 불투명 안개")]
    public FogLayerSettings unexplored = new FogLayerSettings();
    [Tooltip("Fogged(탐사됨·비가시) 지역의 안개 룩 — 지형·오브젝트는 보이고 적만 숨는 지역. fogDensityLow 0이면 지면 안개 없음")]
    public FogLayerSettings fogged = new FogLayerSettings { fogDensityLow = 0f, sheetOpacity = 0f };
    [Tooltip("공통 — 시야 배율·경계 SDF·디버그. 경계는 두 레이어의 이음새라 한 벌")]
    public SharedFogSettings shared = new SharedFogSettings();

    /// <summary>외부 경로(JSON 불러오기 등)로 값이 바뀌었을 때 재적용 통지용 — 플레이 중이면 RenderFXManager가 즉시 반영.</summary>
    public void NotifyChanged()
    {
        Changed?.Invoke(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NotifyChanged();
    }
#endif
}
