using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「우리 다 같이 힘내자」(LetsFightingLove).
    /// [프리셋 L0] LetsFightingLove 전체 타이밍/배치/연쇄 마스터.
    /// 시퀀스: 양손 차징 오브 2개 → 가슴 앞 합체(플래시) → 낮은 포물선 비행 → 착지 힐 오라 →
    ///         상하좌우 1칸 이웃 전원에게 연쇄 투사체(1홉) → 각자 힐 오라.
    /// 에셋명: L0_LFLMasterPreset.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/LetsFightingLove/L0_LFL Master Preset", fileName = "L0_LFLMasterPreset")]
    public class LFLMasterPreset : ScriptableObject
    {
        [Header("차징 / 합체")]
        [Tooltip("양손 차징 시간(초)")]
        [Range(0.1f, 3f)] public float handChargeTime = 0.7f;
        [Tooltip("합류점 수렴 이동 시간(초)")]
        [Range(0.05f, 1f)] public float convergeTime = 0.25f;
        [Tooltip("합류점(시전자 로컬: x=우, y=상, z=전방)")]
        public Vector3 mergeLocalOffset = new Vector3(0f, 1.0f, 0.55f);
        [Tooltip("합체 플래시 색")]
        [ColorUsage(true, true)] public Color mergeFlashColor = new Color(1f, 0.95f, 0.6f);
        [Tooltip("합체 플래시 크기 배수")]
        [Range(0.2f, 3f)] public float flashSizeMul = 0.8f;
        [Tooltip("합체 후 발사까지 지연(초)")]
        [Range(0f, 0.5f)] public float launchDelay = 0.06f;

        [Header("비행 / 착지")]
        [Tooltip("착지점 높이(대상 루트 기준, m). 투사체가 도달하는 지점")]
        public float hitYOffset = 0.68f;

        [Header("연쇄 (1홉 고정)")]
        [Tooltip("본 착지 → 연쇄 발사까지 지연(초)")]
        [Range(0f, 1f)] public float chainDelay = 0.25f;
        [Tooltip("연쇄 투사체 출발 높이(본 대상 루트 기준, m)")]
        public float chainSpawnYOffset = 0.9f;
        [Tooltip("연쇄 다발 발사 시차(초/발)")]
        [Range(0f, 0.5f)] public float chainStagger = 0.08f;
        [Tooltip("1칸 크기(m). 상하좌우 이웃 판정 거리")]
        [Range(0.5f, 6f)] public float cellSize = 2.0f;
        [Tooltip("십자 판정 허용오차(m). 수직축 어긋남 + 거리 여유")]
        [Range(0.05f, 1.5f)] public float axisTol = 0.6f;
        [Tooltip("최대 연쇄 대상 수(가까운 순)")]
        [Range(1, 8)] public int maxChainTargets = 4;
    }
}
