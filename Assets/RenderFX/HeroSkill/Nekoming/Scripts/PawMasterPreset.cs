using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「널 위해 준비했어」(PawForYou) 계열.
    /// [프리셋 P0] PawForYou 전체 타이밍/배치 마스터.
    /// 시퀀스: 등장(시전자 우상단)→유지→워프소멸→잔상스트릭→재등장(대상 머리 위)→광선신장→유지→페이드.
    /// 커스텀 인스펙터의 "프리팹에 적용"으로 PawForYou 프리팹의 PawForYouVfx에 반영. 에셋명: P0_PawMasterPreset.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/PawForYou/P0_Paw Master Preset", fileName = "P0_PawMasterPreset")]
    public class PawMasterPreset : ScriptableObject
    {
        [Header("페이즈 타이밍 (초)")]
        [Tooltip("발 등장 팝 시간")]
        [Range(0.05f, 1f)] public float appearTime = 0.25f;
        [Tooltip("등장 후 유지(워프 전 대기)")]
        [Range(0f, 3f)] public float holdTime = 0.5f;
        [Tooltip("워프 소멸(스쿼시 축소) 시간")]
        [Range(0.03f, 0.5f)] public float warpOutTime = 0.12f;
        [Tooltip("워프 공백(소멸→재등장 사이) 시간. 블링크의 사라져 있는 시간")]
        [Range(0.03f, 0.5f)] public float warpTravelTime = 0.1f;
        [Tooltip("대상 머리 위 재등장 팝 시간")]
        [Range(0.05f, 0.6f)] public float warpInTime = 0.15f;
        [Tooltip("광선 신장(발→대상) 시간")]
        [Range(0.03f, 0.6f)] public float beamExtendTime = 0.12f;
        [Tooltip("광선 유지 시간")]
        [Range(0.1f, 5f)] public float beamSustainTime = 1.2f;
        [Tooltip("종료 페이드아웃 시간")]
        [Range(0.05f, 1.5f)] public float fadeOutTime = 0.35f;

        [Header("팝 연출")]
        [Tooltip("등장/재등장 스케일 easeOutBack 오버슛 강도. 0=오버슛 없음")]
        [Range(0f, 4f)] public float popOvershoot = 1.7f;

        [Header("배치 오프셋")]
        [Tooltip("시전자 로컬 기준 발 위치(우상단). x=우, y=상, z=전방")]
        public Vector3 casterOffset = new Vector3(0.55f, 1.7f, 0.15f);
        [Tooltip("대상 머리 위 높이(대상 루트 기준, m)")]
        public float targetHeadOffset = 1.9f;
        [Tooltip("광선 끝 높이(대상 루트 기준, m). 몸통에 박히는 지점")]
        public float beamEndOffsetY = 0.35f;
        [Tooltip("발 아래에서 광선이 시작되는 간격(m)")]
        [Range(0f, 0.6f)] public float pawBeamGap = 0.12f;

        [Header("플래시")]
        [Tooltip("워프/착탄 플래시 크기 배수")]
        [Range(0.2f, 3f)] public float flashSizeMul = 1f;
    }
}
