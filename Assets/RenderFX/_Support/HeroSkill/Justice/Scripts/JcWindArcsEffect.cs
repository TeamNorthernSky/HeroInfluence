using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 바람무늬 방사광 — 흐린 원호 리본 여러 겹. ASB 연출 규약(ISkillEffectBehaviour) 구현체.
    ///
    /// 힐 오라 E-2(HealArcRing)의 검증된 레시피 승계: 원호 = 호를 빠르게 훑는 헤드 +
    /// TrailRenderer 리본(풀링). 중심각·반경·높이·틸트·방향을 원호마다 랜덤 변주하고,
    /// 넓은 폭 × 낮은 알파의 부드러운 리본이 겹치며 「빛의 플루이드」를 만든다.
    /// ★리본은 뷰 정렬(항상 카메라를 향해 눕는다) — 위/정면/측면 어느 시선축에서도
    /// 같은 질감이 원리적으로 보장되어 카메라 보정이 필요 없다(원통 메시 방식의 실패 지점).
    ///
    /// 생애: Play 시 스폰 창 동안 원호를 뿌리고 정지 → 잔존 원호 자연 소멸 → 자체 파괴.
    /// 위치는 재생 순간 고정(궤도 중심) — 소켓 비부모 규칙.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcWindArcsEffect : MonoBehaviour, ISkillEffectBehaviour
    {
        [Header("색 (원호마다 A↔B 랜덤 배합) — 바인더가 프리셋에서 채운다")]
        [Tooltip("색 A — 흰 쪽.")]
        [SerializeField, ColorUsage(true, true)] private Color colorA = Color.white;
        [Tooltip("색 B — 청 쪽.")]
        [SerializeField, ColorUsage(true, true)] private Color colorB = new Color(0.5f, 0.75f, 1f);
        [Tooltip("본체 발광 배수.")]
        [SerializeField, Min(0f)] private float emission = 1.6f;

        [Header("궤도")]
        [Tooltip("이 효과가 배치되거나 회전하는 반경(m)입니다. 높일수록 중심에서 멀어집니다.")]
        [SerializeField, Min(0.05f)] private float radius = 1.6f;
        [Tooltip("반경 노이즈(±m). 원호마다 랜덤 가감")]
        [SerializeField, Min(0f)] private float radiusNoise = 0.25f;
        [Tooltip("링 중심 높이(m)")]
        [SerializeField] private float orbitHeight = 0.35f;
        [Tooltip("높이 노이즈(±m) — 겹이 위아래로도 흩어진다.")]
        [SerializeField, Min(0f)] private float heightNoise = 0.15f;
        [Tooltip("원호 중심각 최소(도).")]
        [SerializeField, Range(30f, 360f)] private float arcSpanMin = 120f;
        [Tooltip("원호 중심각 최대(도).")]
        [SerializeField, Range(30f, 360f)] private float arcSpanMax = 300f;
        [Tooltip("회전면 랜덤 틸트 최대(±도) — 원호마다 궤도면이 흔들려 유체감이 산다.")]
        [SerializeField, Range(0f, 60f)] private float tiltMax = 18f;
        [Tooltip("켜면 시계/반시계 랜덤")]
        [SerializeField] private bool bidirectional = true;

        [Header("타이밍")]
        [Tooltip("한 원호가 호를 훑는 시간 최소(초). 짧을수록 순간적.")]
        [SerializeField, Min(0.05f)] private float sweepDurMin = 0.28f;
        [Tooltip("한 원호가 호를 훑는 시간 최대(초).")]
        [SerializeField, Min(0.05f)] private float sweepDurMax = 0.45f;
        [Tooltip("새 원호 생성 간격 최소(초).")]
        [SerializeField, Min(0.01f)] private float spawnIntMin = 0.05f;
        [Tooltip("새 원호 생성 간격 최대(초).")]
        [SerializeField, Min(0.01f)] private float spawnIntMax = 0.12f;
        [Tooltip("동시 최대 원호 수")]
        [SerializeField, Range(1, 32)] private int maxArcs = 14;
        [Tooltip("원호를 뿌리는 창(초). 이후 스폰이 멈추고 잔존 원호는 자연 소멸한다.")]
        [SerializeField, Min(0.1f)] private float spawnWindow = 0.6f;

        [Header("리본 룩")]
        [Tooltip("최대 두께 최소(m).")]
        [SerializeField, Min(0.01f)] private float widthMin = 0.12f;
        [Tooltip("최대 두께 최대(m). Min과 벌릴수록 굵기가 제각각인 획이 된다.")]
        [SerializeField, Min(0.01f)] private float widthMax = 0.3f;
        [Tooltip("리본 잔상 시간 최소(초). 원호마다 min~max 랜덤. 클수록 원호 전체가 오래 보임")]
        [SerializeField, Min(0.05f)] private float trailTimeMin = 0.25f;
        [Tooltip("리본 잔상 시간 최대(초)")]
        [SerializeField, Min(0.05f)] private float trailTimeMax = 0.45f;
        [Tooltip("원호별 전체 투명도 최소(0~1). 원호마다 min~max 랜덤 배수로 알파에 곱함")]
        [SerializeField, Range(0f, 1f)] private float alphaMin = 0.25f;
        [Tooltip("원호별 전체 투명도 최대(0~1)")]
        [SerializeField, Range(0f, 1f)] private float alphaMax = 0.5f;

        [Header("밝은 심 겹")]
        [Tooltip("가늘고 밝은 원호가 나올 확률(0~1).")]
        [SerializeField, Range(0f, 1f)] private float brightRatio = 0.25f;
        [Tooltip("밝은 원호의 폭 배율(가늘게).")]
        [SerializeField, Range(0.05f, 1f)] private float brightWidthMul = 0.35f;
        [Tooltip("밝은 원호의 알파 배율(진하게, 1 초과 허용 — 최종은 1로 클램프).")]
        [SerializeField, Range(1f, 4f)] private float brightAlphaMul = 1.8f;

        [Header("배치 · 재질")]
        [Tooltip("스폰 지점(소켓) 기준 오프셋(m). 소켓 스케일은 무시된다.")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);
        [Tooltip("리본 재질 — StrokeCore 계열을 낮은 감쇠로 써서 부드러운 띠를 만든다.")]
        [SerializeField] private Material ribbonMaterial;

        [Tooltip("켜면 궤도 중심이 매 프레임 이 오브젝트의 위치를 따라간다(용권풍처럼 이동하는 모체에 얹을 때).\n" +
                 "기본은 꺼짐 — 재생 순간의 위치에 고정된다(제자리 방사광).")]
        [SerializeField] private bool followRoot;

        [Tooltip("생성·재생·종료 과정을 Console에 기록합니다. 효과 외형에는 영향을 주지 않습니다.")]
        [SerializeField] private bool logLifecycle;

        // 프리셋 바인더가 밀어넣는 값
        public Color ColorA { get => colorA; set => colorA = value; }
        public Color ColorB { get => colorB; set => colorB = value; }
        public float Emission { get => emission; set => emission = Mathf.Max(0f, value); }
        public float Radius { get => radius; set => radius = Mathf.Max(0.05f, value); }
        public float RadiusNoise { get => radiusNoise; set => radiusNoise = Mathf.Max(0f, value); }
        public float OrbitHeight { get => orbitHeight; set => orbitHeight = value; }
        public float HeightNoise { get => heightNoise; set => heightNoise = Mathf.Max(0f, value); }
        public float ArcSpanMin { get => arcSpanMin; set => arcSpanMin = Mathf.Clamp(value, 30f, 360f); }
        public float ArcSpanMax { get => arcSpanMax; set => arcSpanMax = Mathf.Clamp(value, 30f, 360f); }
        public float TiltMax { get => tiltMax; set => tiltMax = Mathf.Clamp(value, 0f, 60f); }
        public bool Bidirectional { get => bidirectional; set => bidirectional = value; }
        public float SweepDurMin { get => sweepDurMin; set => sweepDurMin = Mathf.Max(0.05f, value); }
        public float SweepDurMax { get => sweepDurMax; set => sweepDurMax = Mathf.Max(0.05f, value); }
        public float SpawnIntMin { get => spawnIntMin; set => spawnIntMin = Mathf.Max(0.01f, value); }
        public float SpawnIntMax { get => spawnIntMax; set => spawnIntMax = Mathf.Max(0.01f, value); }
        public int MaxArcs { get => maxArcs; set => maxArcs = Mathf.Clamp(value, 1, 32); }
        public float SpawnWindow { get => spawnWindow; set => spawnWindow = Mathf.Max(0.1f, value); }
        public float WidthMin { get => widthMin; set => widthMin = Mathf.Max(0.01f, value); }
        public float WidthMax { get => widthMax; set => widthMax = Mathf.Max(0.01f, value); }
        public float TrailTimeMin { get => trailTimeMin; set => trailTimeMin = Mathf.Max(0.05f, value); }
        public float TrailTimeMax { get => trailTimeMax; set => trailTimeMax = Mathf.Max(0.05f, value); }
        public float AlphaMin { get => alphaMin; set => alphaMin = Mathf.Clamp01(value); }
        public float AlphaMax { get => alphaMax; set => alphaMax = Mathf.Clamp01(value); }
        public float BrightRatio { get => brightRatio; set => brightRatio = Mathf.Clamp01(value); }
        public float BrightWidthMul { get => brightWidthMul; set => brightWidthMul = Mathf.Clamp(value, 0.05f, 1f); }
        public float BrightAlphaMul { get => brightAlphaMul; set => brightAlphaMul = Mathf.Clamp(value, 1f, 4f); }
        public Vector3 SpawnOffset { get => spawnOffset; set => spawnOffset = value; }
        public Material RibbonMaterial { get => ribbonMaterial; set => ribbonMaterial = value; }
        public bool FollowRoot { get => followRoot; set => followRoot = value; }

        private class Arc
        {
            public GameObject go;
            public TrailRenderer tr;
            public bool active;
            public float t, dur, a0, span, r, h, width, alpha, trailTime;
            public float doneT;          // 스윕 종료 후 잔상 소멸 대기 경과
            public int dir;
            public Quaternion plane;
        }

        private readonly List<Arc> pool = new List<Arc>();
        private Vector3 center;
        private float elapsed;
        private float nextSpawn;
        private bool playing;

        public void Play(SkillEffectContext ctx)
        {
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;

            if (ctx != null)
            {
                Transform socket = ctx.SocketTransform;
                transform.position = (socket != null ? socket.position : ctx.SpawnPosition) + spawnOffset;
            }
            center = transform.position;

            elapsed = 0f;
            nextSpawn = 0f;
            playing = true;

            Destroy(gameObject, spawnWindow + sweepDurMax + trailTimeMax + 0.3f);

            if (logLifecycle)
                Debug.Log("[JcWindArcs] Play  origin=" + center.ToString("F2"), this);
        }

        private void Update()
        {
            if (!playing) return;
            float dt = Time.deltaTime;
            elapsed += dt;

            // 이동하는 모체에 얹힌 경우에만 중심을 갱신한다. 리본(TrailRenderer)은 월드 공간이라
            // 이미 그려진 띠는 제자리에 남아 뒤로 흐르고, 새 원호만 새 중심에서 태어난다.
            if (followRoot) center = transform.position;

            if (elapsed < spawnWindow)
            {
                nextSpawn -= dt;
                if (nextSpawn <= 0f)
                {
                    Spawn();
                    nextSpawn = Random.Range(spawnIntMin, spawnIntMax);
                }
            }

            for (int i = 0; i < pool.Count; i++)
                if (pool[i].active) Advance(pool[i], dt);
        }

        private void Spawn()
        {
            Arc a = null;
            for (int i = 0; i < pool.Count; i++)
                if (!pool[i].active) { a = pool[i]; break; }
            if (a == null)
            {
                if (pool.Count >= maxArcs) return;   // 풀 상한 — 가장 오래된 것을 뺏지 않고 이번 스폰을 거른다
                a = CreateArc();
                pool.Add(a);
            }

            a.active = true;
            a.t = 0f;
            a.doneT = 0f;
            a.dur = Random.Range(sweepDurMin, sweepDurMax);
            a.a0 = Random.Range(0f, 360f);
            a.span = Random.Range(Mathf.Min(arcSpanMin, arcSpanMax), Mathf.Max(arcSpanMin, arcSpanMax));
            a.dir = bidirectional && Random.value < 0.5f ? -1 : 1;
            a.r = radius + Random.Range(-radiusNoise, radiusNoise);
            a.h = orbitHeight + Random.Range(-heightNoise, heightNoise);
            a.trailTime = Random.Range(trailTimeMin, trailTimeMax);
            a.plane = Quaternion.Euler(Random.Range(-tiltMax, tiltMax), 0f, Random.Range(-tiltMax, tiltMax));

            a.width = Random.Range(widthMin, widthMax);
            a.alpha = Random.Range(alphaMin, alphaMax);
            if (Random.value < brightRatio)
            {
                a.width *= brightWidthMul;                      // 가늘고
                a.alpha = Mathf.Min(a.alpha * brightAlphaMul, 1f);   // 밝게 — 흐림 속의 심
            }

            // 색: A↔B 랜덤 배합 × 발광, 꼬리로 갈수록 알파 페이드
            Color hue = Color.Lerp(colorA, colorB, Random.value) * emission;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(hue, 0f), new GradientColorKey(hue, 1f) },
                new[] { new GradientAlphaKey(a.alpha, 0f), new GradientAlphaKey(a.alpha * 0.55f, 0.5f), new GradientAlphaKey(0f, 1f) });
            a.tr.colorGradient = g;
            a.tr.time = a.trailTime;
            a.tr.widthMultiplier = 0f;

            a.go.transform.position = ArcPos(a, 0f);
            a.tr.Clear();
            a.tr.emitting = true;
            a.go.SetActive(true);
        }

        private Arc CreateArc()
        {
            var go = new GameObject("WindArc");
            go.transform.SetParent(transform, false);
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = ribbonMaterial;
            tr.alignment = LineAlignment.View;                  // ★뷰 정렬 — 시선축 불변 질감의 핵심
            tr.textureMode = LineTextureMode.Stretch;
            tr.minVertexDistance = 0.02f;
            tr.numCapVertices = 4;
            tr.numCornerVertices = 4;   // 서브스텝으로 점이 촘촘해진 만큼 모서리도 함께 둥글린다
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            tr.emitting = false;
            go.SetActive(false);
            return new Arc { go = go, tr = tr };
        }

        private Vector3 ArcPos(Arc a, float k)
        {
            float ang = (a.a0 + a.dir * a.span * k) * Mathf.Deg2Rad;
            Vector3 local = new Vector3(Mathf.Sin(ang) * a.r, a.h, Mathf.Cos(ang) * a.r);
            return center + a.plane * local;
        }

        /// <summary>
        /// ★서브스텝 목표 이동 거리(m). 한 프레임에 이보다 많이 움직이면 중간 점을 직접 찍는다.
        ///
        /// TrailRenderer는 **프레임당 한 점**만 기록한다. 원호가 183~338도를 0.05~0.14초에 훑으므로
        /// 60fps에서 3~8프레임밖에 안 지나가고, 궤적이 서너 점짜리 다각형이 되어 각져 보였다.
        /// minVertexDistance를 줄여도 소용없다 — 제약이 거리가 아니라 **프레임 수**이기 때문이다.
        /// `AddPosition`으로 중간 점을 직접 넣어 해소한다(호 획의 서브스텝과 같은 발상).
        /// </summary>
        const float TrailStepMeters = 0.08f;

        /// <summary>한 프레임에 찍을 수 있는 점의 상한 — 폭주 방지.</summary>
        const int MaxTrailSteps = 24;

        private void Advance(Arc a, float dt)
        {
            float prevT = a.t;
            a.t += dt;
            float k = Mathf.Clamp01(a.t / a.dur);
            float eased = 1f - (1f - k) * (1f - k);             // 촤악 — 초반 빠르고 말미 감속

            // 이번 프레임의 실이동량으로 서브스텝 수를 정하고, 중간 점을 직접 찍는다.
            // (마지막 점은 아래에서 transform.position으로 들어가므로 여기서는 제외한다)
            if (a.tr.emitting)
            {
                float prevK = Mathf.Clamp01(prevT / a.dur);
                float prevEased = 1f - (1f - prevK) * (1f - prevK);
                float moved = Mathf.Abs(eased - prevEased) * a.span * Mathf.Deg2Rad * a.r;
                int steps = Mathf.Clamp(Mathf.CeilToInt(moved / TrailStepMeters), 1, MaxTrailSteps);

                for (int s = 1; s < steps; s++)
                {
                    float mid = Mathf.Lerp(prevEased, eased, s / (float)steps);
                    a.tr.AddPosition(ArcPos(a, mid));
                }
            }

            a.go.transform.position = ArcPos(a, eased);
            // 폭: 일생 절반 피크(사인 엔벨로프). widthMultiplier는 리본 전체 라이브 스케일 —
            // 힐 고리에서 검증된 맥동 질감이라 그대로 쓴다.
            a.tr.widthMultiplier = a.width * Mathf.Sin(Mathf.Clamp01(a.t / (a.dur + a.trailTime)) * Mathf.PI);

            if (k >= 1f)
            {
                a.tr.emitting = false;
                a.doneT += dt;
                if (a.doneT >= a.trailTime)
                {
                    a.active = false;
                    a.go.SetActive(false);
                }
            }
        }
    }
}
