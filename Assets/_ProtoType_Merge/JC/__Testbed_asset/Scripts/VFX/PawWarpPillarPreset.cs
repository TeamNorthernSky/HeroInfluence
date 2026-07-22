using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 P5] 워프 블링크 빛기둥(PawWarpPillar)의 튜닝 값.
    /// 록맨 텔레포트풍: 한줄기 수직 빛기둥이 반짝 → 하단부터 위로 슈릭 소멸.
    /// SF 차원이동풍 색감은 coreColor/glowColor로. 에셋명: P5_PawWarpPillar.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/PawForYou/P5_Paw Warp Pillar Preset", fileName = "P5_PawWarpPillar")]
    public class PawWarpPillarPreset : ScriptableObject
    {
        [Header("배치")]
        [Tooltip("기둥 폭(m)")]
        public float width = 0.5f;
        [Tooltip("기둥 높이(m)")]
        public float height = 4f;
        [Tooltip("기둥 하단의 기준점 대비 오프셋(m). -면 발 아래까지 내려옴")]
        public float bottomOffset = -0.3f;

        [Header("타임라인")]
        [Tooltip("버스트 수명(초)")]
        [Range(0.1f, 2f)] public float duration = 0.35f;
        [Tooltip("플래시 구간 비율(수명 대비). 이 동안 기둥 전체가 반짝")]
        [Range(0.05f, 0.8f)] public float flashFrac = 0.35f;
        [Tooltip("플래시 시작 밝기 부스트(1+이 값에서 1로 감쇠)")]
        [Range(0f, 3f)] public float flashBoost = 1.2f;
        [Tooltip("슈릭 구간에서 폭이 줄어드는 정도(0=유지)")]
        [Range(0f, 1f)] public float narrow = 0.4f;

        [Header("색 / 밝기 (SF 차원이동풍)")]
        [Tooltip("코어(고휘도 중심) 색")]
        [ColorUsage(true, true)] public Color coreColor = Color.white;
        [Tooltip("글로우(외곽 번짐) 색")]
        [ColorUsage(true, true)] public Color glowColor = new Color(0.45f, 0.8f, 1f);
        [Tooltip("전체 밝기")]
        [Range(0f, 10f)] public float intensity = 3f;

        [Header("단면 / 경계")]
        [Tooltip("코어(고휘도 중심) 폭 비율")]
        [Range(0.01f, 0.6f)] public float coreWidth = 0.22f;
        [Tooltip("글로우 감쇠 지수(클수록 좁고 진하게)")]
        [Range(0.5f, 8f)] public float glowFalloff = 2.2f;
        [Tooltip("코어 가장자리 소프트")]
        [Range(0.005f, 0.3f)] public float edgeSoft = 0.08f;
        [Tooltip("슈릭 하단 경계 소프트(빠져나가는 꼬리의 부드러움)")]
        [Range(0.01f, 0.6f)] public float sweepSoft = 0.18f;
        [Tooltip("상단 페이드 폭")]
        [Range(0.01f, 0.6f)] public float topFade = 0.2f;
    }
}
