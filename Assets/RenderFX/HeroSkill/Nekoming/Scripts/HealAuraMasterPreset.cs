using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「아픈 거 다 날아가라」(HealSkill).
    /// [프리셋 #0] 힐 오라 이펙트 전체(마스터) 타이밍.
    /// 개별 서브이펙트(오브/원호/…) 룩이 아니라 이펙트 전체의 재생 수명과 전후 페이드를 지배.
    /// 에셋 파일명: 0_HealAuraMasterPreset.asset. 오케스트레이터 HealOrbitVfx가 여기서 타이밍을 읽음.
    /// livePreview가 켜져 있으면 플레이 중 값 변경이 즉시 반영.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/9_Heal Aura Master Preset", fileName = "9_HealAuraMasterPreset")]
    public class HealAuraMasterPreset : ScriptableObject
    {
        [Header("★묶음 트랜스폼 (오라 조립체 전체 — 카메라 각도 보정 인프라)")]
        [Tooltip("조립체 전체 오프셋(m). 착지점 기준 — 전 부품이 함께 이동한다.")]
        public Vector3 rootOffset = Vector3.zero;
        [Tooltip("조립체 전체 스케일 배수. 1=원본. ★1차 적용 범위: 궤도 반경·오브 크기 + 루트 트랜스폼.\n" +
                 "월드 좌표로 직접 그리는 부품(원호 반경 등)은 각 프리셋의 반경으로 조절 — 향후 확장.")]
        [Range(0.2f, 3f)] public float rootScale = 1f;
        [Tooltip("조립체 전체 회전(오일러 도). ★1차 적용 범위: 루트 트랜스폼(메시 기반 자식).\n" +
                 "빌보드·3D 트레일이 카메라 각도와 어긋날 때 보정용 — 만약을 대비한 자리.")]
        public Vector3 rootEuler = Vector3.zero;

        [Header("■ 전체 재생 타이밍 (이펙트 전체 대상)")]
        [Tooltip("총 재생 시간(초). 0=수동 정지 전까지 무한")]
        public float duration = 3.5f;
        [Tooltip("시작 페이드인 시간(초). 총 재생시간 기준")]
        [Min(0f)] public float fadeInTime = 0.2f;
        [Tooltip("종료 페이드아웃 시간(초). 총 재생시간 끝에서 이 시간만큼 페이드")]
        [Min(0f)] public float fadeOutTime = 0.2f;
    }
}
