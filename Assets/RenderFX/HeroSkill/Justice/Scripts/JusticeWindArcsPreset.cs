using System;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 바람무늬 방사광 — 「흐린 원호 리본 여러 겹」 방식 프리셋 (서브이펙트별 분리 체계).
    ///
    /// ★원통/도넛 메시 방식(_Study 보관)의 후계(260729). 원호가 메시가 아니라 TrailRenderer
    /// 리본이라 항상 카메라를 향해 눕는다(뷰 정렬) — 위/정면/측면 어느 시선축에서도
    /// 같은 「빛의 띠」 질감이 원리적으로 보장되고, 카메라 보정이 일절 필요 없다.
    /// 힐 오라 E-2(HealArcRing)의 검증된 레시피를 승계: 호를 빠르게 훑는 헤드 + 리본,
    /// 중심각·반경·틸트·방향 랜덤, 풀링. 변주는 「넓고 부드러운 리본 + 낮은 알파 = 유체 겹침」.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Justice Wind Arcs Preset (바람무늬 원호 겹)", fileName = "FX_JusticeWindArcs")]
    public class JusticeWindArcsPreset : ScriptableObject
    {
        [Serializable]
        public class TargetSet
        {
            [Tooltip("바람 원호 프리팹(JcWindArcsEffect + 바인더).")]
            public GameObject arcsPrefab;
            [Tooltip("리본 재질 — StrokeCore 계열을 낮은 감쇠로 써서 부드러운 띠를 만든다.")]
            public Material ribbonMaterial;
        }

        [Header("── 대상 자산 ──")]
        [Tooltip("적용·캡처할 프리팹과 재질 연결입니다. 다른 스킬 또는 변종을 지정하면 그 에셋이 수정될 수 있습니다.")]
        public TargetSet targets = new TargetSet();

        [Header("── 색 (원호마다 A↔B 사이 랜덤 배합) ──")]
        [Tooltip("색 A — 흰 쪽.")]
        [ColorUsage(true, true)] public Color colorA = Color.white;
        [Tooltip("색 B — 청 쪽.")]
        [ColorUsage(true, true)] public Color colorB = new Color(0.5f, 0.75f, 1f);
        [Tooltip("발광 배수.")]
        [Range(0f, 8f)] public float emission = 1.6f;

        [Header("── 궤도 (원호마다 랜덤 변주) ──")]
        [Tooltip("기본 회전 반경(m).")]
        [Range(0.2f, 5f)] public float radius = 1.6f;
        [Tooltip("반경 노이즈(±m) — 원호마다 가감되어 겹이 벌어진다.")]
        [Range(0f, 1.5f)] public float radiusNoise = 0.25f;
        [Tooltip("링 중심 높이(m).")]
        [Range(-0.5f, 2f)] public float orbitHeight = 0.35f;
        [Tooltip("높이 노이즈(±m) — 겹이 위아래로도 흩어진다.")]
        [Range(0f, 1f)] public float heightNoise = 0.15f;
        [Tooltip("원호 중심각 최소(도).")]
        [Range(30f, 360f)] public float arcSpanMin = 120f;
        [Tooltip("원호 중심각 최대(도).")]
        [Range(30f, 360f)] public float arcSpanMax = 300f;
        [Tooltip("회전면 랜덤 틸트 최대(±도) — 원호마다 궤도면이 흔들려 유체감이 산다.")]
        [Range(0f, 60f)] public float tiltMax = 18f;
        [Tooltip("켜면 시계/반시계 랜덤.")]
        public bool bidirectional = true;

        [Header("── 타이밍 ──")]
        [Tooltip("한 원호가 호를 훑는 시간 최소(초). 짧을수록 순간적.")]
        [Range(0.05f, 2f)] public float sweepDurMin = 0.28f;
        [Tooltip("한 원호가 호를 훑는 시간 최대(초).")]
        [Range(0.05f, 2f)] public float sweepDurMax = 0.45f;
        [Tooltip("새 원호 생성 간격 최소(초).")]
        [Range(0.01f, 1f)] public float spawnIntMin = 0.05f;
        [Tooltip("새 원호 생성 간격 최대(초).")]
        [Range(0.01f, 1f)] public float spawnIntMax = 0.12f;
        [Tooltip("동시 최대 원호 수(풀 크기).")]
        [Range(1, 32)] public int maxArcs = 14;
        [Tooltip("원호를 뿌리는 창(초). 이후 스폰이 멈추고 잔존 원호는 자연 소멸한다.")]
        [Range(0.1f, 3f)] public float spawnWindow = 0.6f;

        [Header("── 리본 룩 (흐린 겹의 핵심: 넓은 폭 × 낮은 알파) ──")]
        [Tooltip("리본 피크 폭 최소(m). 힐 고리(0.05~0.08)보다 크게 잡아야 흐린 띠가 된다.")]
        [Range(0.01f, 1.5f)] public float widthMin = 0.12f;
        [Tooltip("리본 피크 폭 최대(m). 폭은 일생 절반에서 피크, 앞뒤로 0.")]
        [Range(0.01f, 1.5f)] public float widthMax = 0.3f;
        [Tooltip("리본 잔상 시간 최소(초).")]
        [Range(0.05f, 2f)] public float trailTimeMin = 0.25f;
        [Tooltip("리본 잔상 시간 최대(초).")]
        [Range(0.05f, 2f)] public float trailTimeMax = 0.45f;
        [Tooltip("원호별 알파 최소. 낮게 깔아야 겹친 곳만 진해지는 유체감이 난다.")]
        [Range(0f, 1f)] public float alphaMin = 0.25f;
        [Tooltip("원호별 알파 최대.")]
        [Range(0f, 1f)] public float alphaMax = 0.5f;

        [Header("── 밝은 심 겹 (흐림 속의 가는 심 — 0이면 비활성) ──")]
        [Tooltip("가늘고 밝은 원호가 나올 확률(0~1).")]
        [Range(0f, 1f)] public float brightRatio = 0.25f;
        [Tooltip("밝은 원호의 폭 배율(가늘게).")]
        [Range(0.05f, 1f)] public float brightWidthMul = 0.35f;
        [Tooltip("밝은 원호의 알파 배율(진하게, 1 초과 허용 — 최종은 1로 클램프).")]
        [Range(1f, 4f)] public float brightAlphaMul = 1.8f;

        [Header("── 배치 ──")]
        [Tooltip("스폰 지점(소켓) 기준 오프셋(m).")]
        public Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);

        private void OnValidate()
        {
            if (arcSpanMax < arcSpanMin) arcSpanMax = arcSpanMin;
            if (sweepDurMax < sweepDurMin) sweepDurMax = sweepDurMin;
            if (spawnIntMax < spawnIntMin) spawnIntMax = spawnIntMin;
            if (widthMax < widthMin) widthMax = widthMin;
            if (trailTimeMax < trailTimeMin) trailTimeMax = trailTimeMin;
            if (alphaMax < alphaMin) alphaMax = alphaMin;
        }
    }
}
