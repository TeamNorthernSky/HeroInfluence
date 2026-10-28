using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「쓰러지면 안돼」(Taosenaiyo, 부활).
    /// [프리셋 T8] 부활 오라 부품(Tao_Revive)의 전체 타이밍 + 착지점 마스터 — B/A 공용(타이밍은 변종 무관).
    /// 통짜 T0 마스터에서 오라 구간만 분리한 부품판(260807). 색·플래시는 부품 프리팹이 변종별로 직렬화.
    /// 에셋명: T8_TaoReviveMaster.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Taosenaiyo/T8_Tao Revive Master Preset", fileName = "T8_TaoReviveMaster")]
    public class TaoReviveMasterPreset : ScriptableObject
    {
        [Header("★착지점 (대상 기준·월드 축)")]
        [Tooltip("오라 전개 오프셋(m). 기본 0 = 대상 발밑.")]
        public Vector3 landOffset = Vector3.zero;

        [Header("오라 타이밍")]
        [Tooltip("복합 오라 페이드인(초)")]
        [Range(0.05f, 1.5f)] public float fadeInTime = 0.35f;
        [Tooltip("유지 시간(초)")]
        [Range(0.2f, 8f)] public float sustainTime = 2.6f;
        [Tooltip("페이드아웃(초)")]
        [Range(0.05f, 2f)] public float fadeOutTime = 0.5f;
    }
}
