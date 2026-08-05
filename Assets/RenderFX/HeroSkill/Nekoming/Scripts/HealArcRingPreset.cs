using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 #3] 힐 오라 E-2 원호 스윕 링(HealArcRing) 튜닝 값.
    /// 에셋 파일명: 3_HealArcRingPreset.asset (인덱스 접두어로 프로젝트 창 정렬).
    /// 커스텀 인스펙터(HealArcRingPresetEditor)의 "프리팹에 적용"으로 HealOrbit 프리팹의 ArcRing에 일괄 반영.
    /// HealArcRing.livePreview가 켜져 있으면 플레이 중 값 변경이 새로 생성되는 원호에 즉시 반영.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/4_Heal Arc Ring Preset", fileName = "4_HealArcRingPreset")]
    public class HealArcRingPreset : ScriptableObject
    {
        [Header("★따름 (Alter 전용 — 공용 프리셋(7_Sub)은 끔 유지)")]
        [Tooltip("켜면 트랜스폼(밀도·반경·폭·타이밍)을 Basic 프리셋에서 읽는다. Alter 는 기본 ON.")]
        public bool followBasic;
        public HealArcRingPreset basicRef;

        /// <summary>트랜스폼 정본 — Alter 가 따름이면 Basic.</summary>
        public HealArcRingPreset TransformSource => followBasic && basicRef != null ? basicRef : this;

        [Header("■ 밀도 / 타이밍")]
        [Tooltip("원호 생성 간격 최소(초). 작을수록 촘촘(밀도↑)")]
        public float spawnIntervalMin = 0.06f;
        [Tooltip("원호 생성 간격 최대(초)")]
        public float spawnIntervalMax = 0.16f;
        [Tooltip("동시 최대 원호 수")]
        public int maxArcs = 12;
        [Tooltip("한 원호가 호를 훑는 시간 최소(초). 짧을수록 순간적")]
        public float sweepDurationMin = 0.28f;
        [Tooltip("한 원호가 호를 훑는 시간 최대(초)")]
        public float sweepDurationMax = 0.42f;

        [Header("■ 중심각 / 반경")]
        [Tooltip("원호 중심각 최소(도)")]
        public float arcSpanMinDeg = 120f;
        [Tooltip("원호 중심각 최대(도)")]
        public float arcSpanMaxDeg = 300f;
        [Tooltip("기본 회전 반경(m)")]
        public float radius = 1.0f;
        [Tooltip("반경 노이즈(±m). 원호마다 랜덤 가감")]
        public float radiusNoise = 0.08f;
        [Tooltip("링 중심 높이(m)")]
        public float orbitHeight = 0.35f;
        [Tooltip("회전축 랜덤 틸트 최대(±도)")]
        public float tiltMaxDeg = 20f;
        [Tooltip("시계/반시계 랜덤")]
        public bool bidirectional = true;

        [Header("■ 룩 (굵기 / 잔상 / 색)")]
        [Tooltip("리본 최대 폭 최소(m). 원호마다 min~max 랜덤 피크폭. 생명주기 절반에서 이 폭, 앞뒤로 0")]
        public float widthStartMin = 0.045f;
        [Tooltip("리본 최대 폭 최대(m)")]
        public float widthStartMax = 0.075f;
        [Tooltip("리본 잔상 시간 최소(초). 원호마다 min~max 랜덤. 클수록 원호 전체가 오래 보임")]
        public float trailTimeMin = 0.24f;
        [Tooltip("리본 잔상 시간 최대(초)")]
        public float trailTimeMax = 0.36f;
        [Tooltip("리본 색(머리→꼬리). 알파로 페이드")]
        public Gradient colorGradient;
        [Tooltip("원호별 전체 투명도 최소(0~1). 원호마다 min~max 랜덤 배수로 알파에 곱함")]
        [Range(0f, 1f)] public float alphaMin = 0.7f;
        [Tooltip("원호별 전체 투명도 최대(0~1)")]
        [Range(0f, 1f)] public float alphaMax = 1.0f;
    }
}
