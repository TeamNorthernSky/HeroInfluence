using System;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 용권풍 프리셋 — 저스티스 대쉬(1030)의 관통형 직선 투사체 (서브이펙트별 분리 체계).
    ///
    /// 형상을 직접 만들지 않는다. 서브이펙트 3종(호 획·포인트 획·바람 원호)을 스폰하고
    /// 「몇 바퀴 · 어디로 얼마나 빨리」만 정한다 — 즉 시간축과 경로만 담당한다.
    ///
    /// ★룩(반경·두께·개수·색·투명도)은 자식 프리셋의 관할이다. 자식은 대쉬와 자산을 공유하지 않고
    ///   전용 사본을 쓴다(FX_JusticeVortexStrokeBlue / FX_JusticeVortexWindArcsBlue).
    ///   대쉬 프리셋은 「180° 한 번 긋고 끝」을 전제로 잡힌 값이라, N바퀴 연속 회전에 그대로 쓰면
    ///   포인트의 탄생 링(반경+오프셋)이 몸통보다 큰 고리로 상시 노출되는 등 전제가 어긋난다.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Justice Vortex Preset (용권풍 투사체)", fileName = "FX_JusticeVortex")]
    public class JusticeVortexPreset : ScriptableObject
    {
        [Serializable]
        public class TargetSet
        {
            [Tooltip("용권풍 컨테이너 프리팹(JcVortexProjectileEffect + 바인더). 「적용」의 기록 대상.")]
            public GameObject vortexPrefab;
            [Tooltip("호 획 프리팹(JcArcStrokeEffect). 대쉬용을 그대로 가리켜도 된다.")]
            public GameObject strokePrefab;
            [Tooltip("포인트 획 프리팹(JcArcPointEffect).")]
            public GameObject pointPrefab;
            [Tooltip("바람 원호 프리팹(JcWindArcsEffect).")]
            public GameObject windArcsPrefab;
            [Tooltip("호 파편 프리팹(JcArcShardEffect).")]
            public GameObject shardPrefab;
        }

        [Header("── 대상 자산 ──")]
        public TargetSet targets = new TargetSet();

        [Header("── 자식 구성 (끄면 그 층은 생략) ──")]
        [Tooltip("호 획 층 — 용권풍의 몸통이 되는 굵은 궤적.")]
        public bool useStroke = true;
        [Tooltip("포인트 획 층 — 바깥에서 궤도로 수렴하는 액센트 대시.")]
        public bool usePoint = true;
        [Tooltip("바람 원호 층 — 흐린 리본이 겹치는 방사광. 리본은 월드 공간이라 뒤로 흘러 꼬리가 된다.")]
        public bool useWindArcs = true;
        [Tooltip("호 파편 층 — 궤도 접선으로 튀는 조각.\n" +
                 "★파편은 농도(Opacity)의 영향을 받지 않는다 — 본체를 옅게 해도 또렷하게 남는다.")]
        public bool useShard;

        [Header("── 발사 ──")]
        [Tooltip("스폰 후 전진을 시작하기까지의 지연(초). 이 동안에도 회전은 이미 돌고 있다.")]
        [Range(0f, 1f)] public float launchDelay = 0.05f;
        [Tooltip("전진 속도(m/s). 일정 속도 — 가감속 없음.")]
        [Range(0.5f, 30f)] public float speed = 8f;
        [Tooltip("전진 거리(m). 이 거리에 닿으면 멈춘다(관통 연출이라 타깃 위치와 무관한 고정값).")]
        [Range(0.5f, 20f)] public float distance = 6f;
        [Tooltip("발사 원점(소켓) 기준 오프셋(m). ★자식 프리셋에도 각자 오프셋이 있어 최종 위치는 둘의 합이다.")]
        public Vector3 spawnOffset = new Vector3(0f, 0.6f, 0f);

        [Header("── 회전 (바닥 평행 · 수직축) ──")]
        [Tooltip("회전 시작각(도). 타깃 방향이 12시 — 대쉬 호와 같은 문법.")]
        [Range(0f, 360f)] public float angleStart = 210f;
        [Tooltip("켜면 시계 방향. 끄면 반시계(대쉬 호와 같은 방향).")]
        public bool clockwise;
        [Tooltip("전체 수명 동안 도는 바퀴 수. 소수 허용(1.5 = 한 바퀴 반). 회전 속도 = 바퀴 수 ÷ 수명.\n" +
                 "★한도: 자식의 서브스텝 상한(프레임당 2.4m) 때문에 너무 빠르면 획의 첫 세그먼트가\n" +
                 "직선으로 찍히기 시작한다. 넘으면 재생 시 경고가 뜨며, 실제 열화는 완만하다.")]
        [Range(0.25f, 60f)] public float turns = 3f;
        [Tooltip("★획 각도 보존 — 획 하나가 덮는 「각도」를 프리셋 기준으로 유지한다.\n" +
                 "획 수명이 절대 초라, 끄면 거리·회전수를 바꿀 때마다 각속도가 달라져 획 길이가 같이 변한다\n" +
                 "(거리를 줄이면 획이 여러 바퀴를 덮어 링이 꽉 차고 굵어 보인다).\n" +
                 "켜면 자식 프리셋의 스윕·각도폭을 「기준 각속도」로 읽어 수명을 비례 보정한다.")]
        public bool preserveStrokeArc = true;
        [Header("── 농도 ──")]
        [Tooltip("용권풍 전체 투명도(1 = 프리셋 그대로, 낮출수록 흐려짐).\n" +
                 "재질이 가산(Blend SrcAlpha One)이라 최종 출력이 rgb×알파다 — 알파를 낮추면 그대로 옅어진다.\n" +
                 "자식 프리셋의 색·발광은 손대지 않고 세 층 전체에 한 번만 곱한다.")]
        [Range(0.02f, 1f)] public float opacity = 1f;

        [Header("── 정리 ──")]
        [Tooltip("비행 종료 후 자식 잔상이 사라질 때까지 기다리는 여유(초).")]
        [Range(0f, 3f)] public float tailLinger = 1.2f;
    }
}
