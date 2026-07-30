using System;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 호 파편(튀는 파편) 프리셋 — 서브이펙트별 분리 체계.
    ///
    /// 회전 톱날이 절삭물과 마찰하며 튀는 불꽃의 궤적을 빌리되, 인상은 불꽃이 아니라 **파편**이다.
    /// 앵커가 호 궤도를 주행하며 그 지점의 접선 방향으로 파편을 쏜다.
    /// 형상은 이등변 삼각형을 절단선으로 깎은 조각(삼각형 또는 사각형)이며,
    /// 절단점은 파티클마다 다른 난수로 정해져 변주가 무한하다(JusticeArcShard 셰이더).
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Justice Arc Shard Preset (호 파편)", fileName = "FX_JusticeArcShard")]
    public class JusticeArcShardPreset : ScriptableObject
    {
        [Serializable]
        public class TargetSet
        {
            [Tooltip("파편 프리팹(JcArcShardEffect + 바인더).")]
            public GameObject shardPrefab;
            [Tooltip("파편 재질(Testbed/Justice/ArcShard).")]
            public Material shardMaterial;
        }

        [Header("── 대상 자산 ──")]
        public TargetSet targets = new TargetSet();

        [Header("── 색 (테두리가 이 색을 쓴다 / 내부는 흰 심) ──")]
        [Tooltip("갓 튄 구간의 테두리 색.")]
        [ColorUsage(true, true)] public Color headColor = new Color(0.85f, 0.95f, 1f);
        [Tooltip("중간 구간의 테두리 색. 파편의 인상을 좌우한다(기본=청, 알트=적).")]
        [ColorUsage(true, true)] public Color midColor = new Color(0.45f, 0.75f, 1f);
        [Tooltip("사라지기 직전의 테두리 색.")]
        [ColorUsage(true, true)] public Color tailColor = new Color(0.20f, 0.45f, 0.9f);
        [Tooltip("테두리 발광 배수.")]
        [Range(0f, 8f)] public float edgeEmission = 1.2f;

        [Tooltip("내부(심) 색 — 빛나는 흰색이 기본.")]
        [ColorUsage(true, true)] public Color innerColor = Color.white;
        [Tooltip("내부 발광 배수.")]
        [Range(0f, 8f)] public float innerEmission = 1.6f;
        [Tooltip("테두리 두께(파편 크기 대비 비율). 0에 가까우면 속이 거의 다 흰 심이 된다.")]
        [Range(0.001f, 0.5f)] public float edgeWidth = 0.08f;
        [Tooltip("형상 경계의 부드러움(안티에일리어싱). 크면 흐릿해진다.")]
        [Range(0.001f, 0.2f)] public float edgeSoft = 0.012f;

        [Header("── 파편 형상 ──")]
        [Tooltip("이등변 삼각형의 밑변 반폭. 작을수록 가늘고 날카로운 파편.")]
        [Range(0.05f, 1f)] public float triWidth = 0.45f;
        [Tooltip("삼각형 높이(반값). 폭 대비 크면 길쭉해진다.")]
        [Range(0.1f, 1f)] public float triHeight = 0.85f;
        [Tooltip("절단점 최소 위치. 0이나 1에 가까우면 거의 깎이지 않고, 0.5 부근에서 가장 많이 깎인다.")]
        [Range(0f, 1f)] public float cutMin = 0.25f;
        [Tooltip("절단점 최대 위치. Min과 벌릴수록 파편마다 모양 차이가 커진다.")]
        [Range(0f, 1f)] public float cutMax = 0.75f;

        [Header("── 궤도 (호 획과 같은 문법 — 겹치려면 값을 맞춘다) ──")]
        [Range(-360f, 720f)] public float angleStart = 210f;
        [Range(-360f, 720f)] public float angleEnd = 30f;
        [Range(0.2f, 6f)] public float radius = 1.6f;
        [Range(0.05f, 3f)] public float sweepDuration = 0.5f;
        [Tooltip("주행 가속 곡선. 1=등속, 클수록 초반이 빠르고 끝에서 감속.")]
        [Range(0.3f, 4f)] public float easeOut = 1f;

        [Header("── 방출 · 발사 ──")]
        [Tooltip("주행 거리당 파편 수. 생성 타이밍은 이 값에 따라 랜덤하게 흩어진다.")]
        [Range(0f, 200f)] public float rateOverDistance = 25f;
        [Tooltip("동시 존재 상한.")]
        [Min(1)] public int maxParticles = 200;
        [Tooltip("발사 속도 최소(m/s). 접선 방향으로 튄다.")]
        [Range(0f, 30f)] public float speedMin = 3f;
        [Tooltip("발사 속도 최대(m/s).")]
        [Range(0f, 30f)] public float speedMax = 8f;
        [Tooltip("발사 방향 산포(도). 접선 기준 원뿔 반각 — 0이면 완전히 접선으로만 튄다.")]
        [Range(0f, 45f)] public float spreadAngle = 8f;
        [Tooltip("발사 지점의 산포 반경(m). 궤도선에 딱 붙지 않게 살짝 흩는다.")]
        [Range(0f, 0.5f)] public float nozzleRadius = 0.05f;
        [Tooltip("★파편 자세의 좌우 랜덤 기울기(도). 진행 방향 기준 ±이 각도만큼 기운다.")]
        [Range(0f, 90f)] public float tiltAngle = 20f;

        [Header("── 물리 ──")]
        [Tooltip("중력 배수. 0이면 직선으로 뻗고, 키우면 포물선으로 떨어진다.")]
        [Range(-2f, 2f)] public float gravity = 0f;
        [Tooltip("공기 저항. 클수록 빨리 감속해 짧게 튄다.")]
        [Range(0f, 10f)] public float drag = 1.5f;

        [Header("── 크기 · 수명 ──")]
        [Tooltip("파편 크기 최소(m).")]
        [Range(0.01f, 1f)] public float sizeMin = 0.06f;
        [Tooltip("파편 크기 최대(m). Min과 벌릴수록 크기가 제각각이다.")]
        [Range(0.01f, 1f)] public float sizeMax = 0.16f;
        [Tooltip("수명 최소(초).")]
        [Range(0.05f, 3f)] public float lifeMin = 0.35f;
        [Tooltip("수명 최대(초).")]
        [Range(0.05f, 3f)] public float lifeMax = 0.7f;

        // ★축소와 페이드는 서로 독립된 시점에 걸 수 있어야 한다(사용자 확정).
        [Header("── 소멸 (축소·페이드 시점 독립) ──")]
        [Tooltip("크기가 줄기 시작하는 수명 비율. 1이면 끝까지 크기 유지.")]
        [Range(0f, 1f)] public float shrinkStart = 0.6f;
        [Tooltip("소멸 시점의 크기 배율. 0이면 점으로 수렴한다.")]
        [Range(0f, 1f)] public float shrinkEndScale = 0f;
        [Tooltip("알파가 빠지기 시작하는 수명 비율. 축소와 다른 시점에 걸 수 있다.")]
        [Range(0f, 1f)] public float fadeStart = 0.75f;

        [Header("── 배치 ──")]
        [Tooltip("스폰 지점(소켓) 기준 오프셋(m).")]
        public Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);
        [Tooltip("주행 종료 후 잔여 파편이 사라질 때까지의 여유(초).")]
        [Range(0f, 3f)] public float extraLinger = 0.5f;

        private void OnValidate()
        {
            if (cutMax < cutMin) cutMax = cutMin;
            if (sizeMax < sizeMin) sizeMax = sizeMin;
            if (lifeMax < lifeMin) lifeMax = lifeMin;
            if (speedMax < speedMin) speedMax = speedMin;
        }
    }
}
