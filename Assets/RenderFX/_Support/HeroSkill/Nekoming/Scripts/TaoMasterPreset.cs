using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「쓰러지면 안돼」(Taosenaiyo, 부활).
    /// [프리셋 T0] Taosenaiyo 전체 타이밍/페이드 마스터.
    /// 손 플래시 → 유성 투사체 → 착지 → 복합 오라(페이드인/유지/페이드아웃). 에셋명: T0_TaoMasterPreset.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Taosenaiyo/T0_Tao Master Preset", fileName = "T0_TaoMasterPreset")]
    public class TaoMasterPreset : ScriptableObject
    {
        [Header("발사")]
        [Tooltip("손 플래시 → 발사까지 지연(초)")]
        [Range(0f, 0.5f)] public float launchDelay = 0.12f;
        [Tooltip("착지점 높이(대상 루트 기준, m)")]
        public float hitYOffset = 0.68f;
        [Tooltip("발사 순간 손 플래시 색")]
        [ColorUsage(true, true)] public Color handFlashColor = new Color(1f, 0.92f, 0.55f);
        [Tooltip("착지 순간 플래시 색")]
        [ColorUsage(true, true)] public Color impactFlashColor = new Color(1f, 0.9f, 0.45f);
        [Range(0.2f, 3f)] public float flashSizeMul = 0.9f;

        [Header("오라 타이밍")]
        [Tooltip("복합 오라 페이드인(초)")]
        [Range(0.05f, 1.5f)] public float fadeInTime = 0.35f;
        [Tooltip("유지 시간(초)")]
        [Range(0.2f, 8f)] public float sustainTime = 2.6f;
        [Tooltip("페이드아웃(초)")]
        [Range(0.05f, 2f)] public float fadeOutTime = 0.5f;
    }
}
