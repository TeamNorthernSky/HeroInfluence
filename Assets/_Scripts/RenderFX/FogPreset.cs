using UnityEngine;

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

    [Header("안개 색 / 높이별 밀도 (셰이더)")]
    public Color fogColor = new Color(0.75f, 0.78f, 0.85f, 1f);
    [Range(0f, 1f)] public float fogDensityLow = 1f;
    [Range(0f, 1f)] public float fogDensityMid = 0.85f;
    [Range(0f, 1f)] public float fogDensityHigh = 0.5f;

    [Header("노이즈 구름 (셰이더)")]
    public float noiseScaleBase = 6f;
    public float noiseScaleDetail = 18f;
    public float noiseScaleDistortion = 3f;
    public float flowSpeedBase = 0.15f;
    public float flowSpeedDetail = 0.25f;
    public float flowSpeedDistortion = 0.08f;
    public float distortionStrength = 0.3f;
    [Range(0.5f, 8f)] public float noiseContrast = 3.5f;

    [Header("높이 볼륨 (셰이더)")]
    [Range(0.1f, 2f)] public float heightTransition = 0.5f;
    public float fogCeilingY = 2f;

    [Header("밝기 / 경계 (셰이더)")]
    [Range(0.3f, 1.5f)] public float brightnessLow = 0.75f;
    [Range(0.3f, 1.5f)] public float brightnessMid = 1f;
    [Range(0.3f, 1.5f)] public float brightnessHigh = 1.25f;
    [Range(0f, 0.5f)] public float cloudContrast = 0.15f;
    [Range(0.01f, 0.49f)] public float edgeSoftness = 0.18f;
    [Range(0f, 1f)] public float trimStrength = 0.65f;
    [Tooltip("경계 레이어 테이퍼 — 가시 경계에서 high→mid→low 순으로 먼저 걷힘 (0=동시, FogOfWarHI 전용)")]
    [Range(0f, 0.45f)] public float edgeLayerSpread = 0.15f;
    [Tooltip("셰이더 디버그 모드 (가시성 텍스처 표시)")]
    public bool debugMode = false;

    [Header("가시성 단계 값 (FogRenderManager 런타임 반영)")]
    [Range(0f, 1f)] public float unexploredValue = 0f;
    [Range(0f, 1f)] public float foggedValue = 0.5f;
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
