using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 #10] 힐 오라 궤도 구체를 따라다니는 스타버스트 반짝임 (260805 신설).
    /// 투사체의 Sparkles 와 동일 사양의 파티클을 궤도 오브가 끌고 다닌다(월드 시뮬레이션 = 궤적에 흩뿌려짐).
    /// 투사체(2번)와 프리셋을 분리해 따로 튜닝한다 — 사용자 지정.
    /// 재질은 투사체 스파클과 공유(스킬 내 재사용 원칙), 색은 라이브 MPB 로 이 프리셋이 정본.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/10_Heal Orbit Sparkle Preset", fileName = "10_HealOrbitSparklePreset")]
    public class HealOrbitSparklePreset : ScriptableObject
    {
        [Header("★따름 (Alter 전용)")]
        [Tooltip("켜면 트랜스폼(밀도·크기·수명·반경)을 Basic 프리셋에서 읽는다. Alter 는 기본 ON.")]
        public bool followBasic;
        [Tooltip("Basic 따름이 켜져 있을 때 위치·형태 기준으로 읽을 프리셋입니다. 연결이 비어 있으면 자기 값을 사용합니다.")]
        public HealOrbitSparklePreset basicRef;

        /// <summary>트랜스폼 정본 — Alter 가 따름이면 Basic.</summary>
        public HealOrbitSparklePreset TransformSource => followBasic && basicRef != null ? basicRef : this;

        [Header("반짝임 (투사체 Sparkles 동일 사양)")]
        [Tooltip("발생 주기(초당 개수)")]
        public float rate = 10f;
        [Tooltip("생성되는 요소 크기의 최솟값(m)입니다. 최댓값과의 사이에서 크기가 선택됩니다.")]
        public float sizeMin = 0.03f;
        [Tooltip("생성되는 요소 크기의 최댓값(m)입니다. 최솟값과 벌릴수록 크기 차이가 커집니다.")]
        public float sizeMax = 0.06f;
        [Tooltip("각 스타 수명(초)")]
        public float lifetime = 0.8f;
        [Tooltip("오브 주변 흩뿌림 반경(m)")]
        public float shapeRadius = 0.15f;

        [Header("색")]
        [Tooltip("이 시각 요소의 색입니다. HDR 색은 발광 강도와 함께 최종 밝기에 영향을 줍니다.")]
        [ColorUsage(true, true)] public Color color = new Color(1f, 0.97f, 0.7f) * 1.8f;
    }
}
