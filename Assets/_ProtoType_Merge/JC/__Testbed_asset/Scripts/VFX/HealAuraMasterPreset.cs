using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 #0] 힐 오라 이펙트 전체(마스터) 타이밍.
    /// 개별 서브이펙트(오브/원호/…) 룩이 아니라 이펙트 전체의 재생 수명과 전후 페이드를 지배.
    /// 에셋 파일명: 0_HealAuraMasterPreset.asset. 오케스트레이터 HealOrbitVfx가 여기서 타이밍을 읽음.
    /// livePreview가 켜져 있으면 플레이 중 값 변경이 즉시 반영.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/0_Heal Aura Master Preset", fileName = "0_HealAuraMasterPreset")]
    public class HealAuraMasterPreset : ScriptableObject
    {
        [Header("■ 전체 재생 타이밍 (이펙트 전체 대상)")]
        [Tooltip("총 재생 시간(초). 0=수동 정지 전까지 무한")]
        public float duration = 3.5f;
        [Tooltip("시작 페이드인 시간(초). 총 재생시간 기준")]
        [Min(0f)] public float fadeInTime = 0.2f;
        [Tooltip("종료 페이드아웃 시간(초). 총 재생시간 끝에서 이 시간만큼 페이드")]
        [Min(0f)] public float fadeOutTime = 0.2f;
    }
}
