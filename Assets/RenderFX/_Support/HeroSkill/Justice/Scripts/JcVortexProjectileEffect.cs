using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 용권풍 — 저스티스 대쉬(1030)의 관통형 직선 투사체. ASB 연출 규약(ISkillEffectBehaviour) 구현체.
    ///
    /// 「장풍이 나가는 느낌」 — 공격 궤적에서 분화하는 것이 아니라, 회전하는 덩어리가
    /// 진행 방향으로 일정 속도로 날아간다. 회전면은 바닥 평행(xz), 회전축은 수직(y)이라
    /// 기존 호 이펙트의 각도 문법이 그대로 맞는다.
    ///
    /// ★이 컴포넌트는 형상을 하나도 만들지 않는다. 하는 일은 두 가지뿐이다:
    ///   1) 대쉬 서브이펙트(호 획·포인트 획·바람 원호)를 스폰하고 궤도 파라미터를 덮어쓴다
    ///   2) 매 프레임 자기 위치를 전진시키고, 자식들의 위치를 거기에 맞춘다
    ///
    /// ★연속 회전에 루프·재스폰이 필요 없다 — 자식들의 MoveAnchor가 LerpUnclamped(각도 래핑 없음)라
    ///   Angle End = Angle Start ± 360×회전수를 넣으면 그대로 N바퀴를 돈다. Ease Out 1 = 등속.
    ///
    /// ★자식을 부모로 묶지 않는다. 자식들의 Play() 첫 줄이 SetParent(null)이라 어차피 떨어지고,
    ///   소켓 lossyScale 오염 규칙도 그대로 지켜야 한다. 대신 Play 직후의 상대 위치를 기록해 두고
    ///   매 프레임 「내 위치 + 기록한 오프셋」으로 밀어 준다. 자식 파티클이 Local 시뮬이라
    ///   루트가 움직이면 이미 그어진 획까지 강체처럼 함께 이동한다 = 회전하는 덩어리의 비행.
    ///
    /// ★소멸에 별도 코드가 없다. 자식 스윕을 「지연 + 비행 시간」으로 주입하므로,
    ///   비행이 끝나는 순간 자식들이 스스로 방출을 끄고 잔상만 페이드한다(자멸 예약도 스윕 기준).
    ///
    /// ★책임 분리: 이 컨테이너는 「시간축·경로」만 정하고, 「룩」(반경·두께·개수·색·투명도)은
    ///   전부 자식 프리셋이 전담한다. 그래서 자식 프리팹·프리셋·재질은 대쉬와 공유하지 않고
    ///   용권풍 전용 사본을 쓴다(JC_JusticeVortexArc / Point / WindArcs).
    ///   초기 구현의 배수 방식(radiusScale·thicknessScale)은 폐기했다 — 단일 배수로는 두 층의
    ///   반경 관계를 바꿀 수 없고, startSize가 두 상수 모드라 크기 배수가 최대값에만 걸렸으며,
    ///   알파는 이미 구워진 그라디언트라 진입점 자체가 없었다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcVortexProjectileEffect : MonoBehaviour, ISkillEffectBehaviour
    {
        [Header("자식 구성 (끄거나 비우면 그 층은 생략)")]
        [Tooltip("호 획 층을 쓴다.")]
        [SerializeField] private bool useStroke = true;
        [Tooltip("호 획 프리팹(JcArcStrokeEffect). 대쉬용을 그대로 가리켜도 되고, 룩을 분리하려면 복제본을 쓴다.")]
        [SerializeField] private GameObject strokePrefab;

        [Tooltip("포인트 획 층을 쓴다.")]
        [SerializeField] private bool usePoint = true;
        [Tooltip("포인트 획 프리팹(JcArcPointEffect).")]
        [SerializeField] private GameObject pointPrefab;

        [Tooltip("바람 원호 층을 쓴다.")]
        [SerializeField] private bool useWindArcs = true;
        [Tooltip("바람 원호 프리팹(JcWindArcsEffect).")]
        [SerializeField] private GameObject windArcsPrefab;

        [Tooltip("호 파편 층을 쓴다. ★파편은 농도(Opacity)의 영향을 받지 않는다 — 본체를 옅게 해도 또렷하게 남는다.")]
        [SerializeField] private bool useShard;
        [Tooltip("호 파편 프리팹(JcArcShardEffect).")]
        [SerializeField] private GameObject shardPrefab;

        [Header("발사")]
        [Tooltip("스폰 후 전진을 시작하기까지의 지연(초). 이 동안에도 회전은 이미 돌고 있다.")]
        [SerializeField, Min(0f)] private float launchDelay = 0.05f;
        [Tooltip("전진 속도(m/s). 일정 속도 — 가감속 없음.")]
        [SerializeField, Min(0.1f)] private float speed = 8f;
        [Tooltip("전진 거리(m). 이 거리에 닿으면 멈춘다(관통 연출이므로 타깃 위치와 무관한 고정값).")]
        [SerializeField, Min(0.1f)] private float distance = 6f;
        [Tooltip("발사 원점(소켓) 기준 오프셋(m). ★자식 프리셋에도 각자 오프셋이 있어 최종 위치는 둘의 합이다.")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.6f, 0f);

        [Header("회전 (바닥 평행 · 수직축)")]
        [Tooltip("회전 시작각(도). 타깃 방향이 12시 — 대쉬 호와 같은 문법.")]
        [SerializeField] private float angleStart = 210f;
        [Tooltip("켜면 시계 방향. 끄면 반시계(대쉬 호와 같은 방향).")]
        [SerializeField] private bool clockwise;
        [Tooltip("전체 수명 동안 도는 바퀴 수. 소수 허용(1.5 = 한 바퀴 반).")]
        [SerializeField, Min(0.25f)] private float turns = 3f;
        [Tooltip("★획 각도 보존 — 획 하나가 덮는 「각도」를 프리셋 기준으로 유지한다.\n" +
                 "획 수명이 절대 초라, 끄면 거리·회전수를 바꿀 때마다 각속도가 달라져 획 길이가 같이 변한다\n" +
                 "(거리를 줄이면 획이 여러 바퀴를 덮어 링이 꽉 차고 굵어 보인다).\n" +
                 "켜면 자식 프리셋의 스윕·각도폭을 「기준 각속도」로 읽어 수명을 비례 보정한다.")]
        [SerializeField] private bool preserveStrokeArc = true;

        [Header("농도")]
        [Tooltip("용권풍 전체 투명도(1 = 프리셋 그대로, 낮출수록 흐려짐).\n" +
                 "재질이 가산(Blend SrcAlpha One)이라 최종 출력이 rgb×알파다 — 알파를 낮추면 그대로 옅어진다.\n" +
                 "자식 프리셋의 색·발광은 건드리지 않고 층 전체에 한 번만 곱한다.")]
        [SerializeField, Range(0.02f, 1f)] private float opacity = 1f;

        [Header("정리")]
        [Tooltip("비행 종료 후 자식 잔상이 사라질 때까지 기다리는 여유(초).")]
        [SerializeField, Min(0f)] private float tailLinger = 1.2f;

        [Tooltip("생성·재생·종료 과정을 Console에 기록합니다. 효과 외형에는 영향을 주지 않습니다.")]
        [SerializeField] private bool logLifecycle;

        /// <summary>자식 서브스텝 상한(16) × 목표 이동(0.15m). 프레임당 이 값을 넘으면 직선 글리치가 부활한다.</summary>
        const float SubStepBudgetPerFrame = 16f * 0.15f;

        // 프리셋 바인더가 밀어넣는 값
        public bool UseStroke { get => useStroke; set => useStroke = value; }
        public GameObject StrokePrefab { get => strokePrefab; set => strokePrefab = value; }
        public bool UsePoint { get => usePoint; set => usePoint = value; }
        public GameObject PointPrefab { get => pointPrefab; set => pointPrefab = value; }
        public bool UseWindArcs { get => useWindArcs; set => useWindArcs = value; }
        public GameObject WindArcsPrefab { get => windArcsPrefab; set => windArcsPrefab = value; }
        public bool UseShard { get => useShard; set => useShard = value; }
        public GameObject ShardPrefab { get => shardPrefab; set => shardPrefab = value; }
        public float LaunchDelay { get => launchDelay; set => launchDelay = Mathf.Max(0f, value); }
        public float Speed { get => speed; set => speed = Mathf.Max(0.1f, value); }
        public float Distance { get => distance; set => distance = Mathf.Max(0.1f, value); }
        public Vector3 SpawnOffset { get => spawnOffset; set => spawnOffset = value; }
        public float AngleStart { get => angleStart; set => angleStart = value; }
        public bool Clockwise { get => clockwise; set => clockwise = value; }
        public float Turns { get => turns; set => turns = Mathf.Max(0.25f, value); }
        public bool PreserveStrokeArc { get => preserveStrokeArc; set => preserveStrokeArc = value; }
        public float Opacity { get => opacity; set => opacity = Mathf.Clamp(value, 0.02f, 1f); }
        public float TailLinger { get => tailLinger; set => tailLinger = Mathf.Max(0f, value); }

        private readonly List<Transform> children = new List<Transform>();
        private readonly List<Vector3> offsets = new List<Vector3>();
        private float elapsed;
        private float travelled;
        private float life;
        private bool flying;

        public void Play(SkillEffectContext ctx)
        {
            // 어떤 경우에도 소켓 자식이 되지 않는다(스케일 오염 차단).
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;

            Vector3 origin = transform.position + spawnOffset;
            if (ctx != null)
            {
                Transform socket = ctx.SocketTransform;
                // ★TransformPoint 금지 — 소켓 스케일이 오프셋에 곱해진다.
                origin = (socket != null ? socket.position : ctx.SpawnPosition) + spawnOffset;
            }
            transform.position = origin;
            transform.rotation = Quaternion.LookRotation(ResolveForward(ctx), Vector3.up);

            float flightTime = distance / Mathf.Max(speed, 0.1f);
            life = launchDelay + flightTime;     // 자식 스윕 = 전체 수명(지연 중에도 회전은 돈다)

            elapsed = 0f;
            travelled = 0f;
            flying = true;

            children.Clear();
            offsets.Clear();

            // 자식은 소켓이 아니라 「내 위치」에서 태어난다 — SocketTransform을 비운 스냅샷을 넘긴다.
            // Caster/PrimaryTarget은 보존되므로 자식의 전진축 계산(ResolveForward)도 나와 같아진다.
            SkillEffectContext childCtx = ctx != null ? ctx.CreateSnapshot(null, transform.position) : null;

            if (useStroke) SpawnChild(strokePrefab, childCtx);
            if (usePoint) SpawnChild(pointPrefab, childCtx);
            if (useWindArcs) SpawnChild(windArcsPrefab, childCtx);
            if (useShard) SpawnChild(shardPrefab, childCtx);

            Destroy(gameObject, life + tailLinger);

            if (logLifecycle)
                Debug.Log("[JcVortex] Play  origin=" + origin.ToString("F2") +
                          "  life=" + life.ToString("F2") + "s  자식=" + children.Count, this);
        }

        /// <summary>전진 기준(시계 12시 방향) = 시전자→타깃 수평. 없으면 시전자 정면.</summary>
        private static Vector3 ResolveForward(SkillEffectContext ctx)
        {
            Vector3 dir = Vector3.forward;
            if (ctx != null)
            {
                Transform target = ctx.PrimaryTarget != null ? ctx.PrimaryTarget.transform : null;
                if (target != null && ctx.Caster != null) dir = target.position - ctx.Caster.transform.position;
                else if (ctx.Caster != null) dir = ctx.Caster.transform.forward;
            }
            dir.y = 0f;
            return dir.sqrMagnitude < 1e-6f ? Vector3.forward : dir.normalized;
        }

        private void SpawnChild(GameObject prefab, SkillEffectContext childCtx)
        {
            if (prefab == null) return;

            GameObject go = Instantiate(prefab, transform.position, transform.rotation);

            // ★순서 — Instantiate 시점의 Awake에서 바인더가 프리셋을 바른다.
            //   궤도 오버라이드는 반드시 그 뒤여야 하고, Play는 오버라이드를 읽어야 하므로 마지막이다.
            ApplyOrbit(go);
            go.GetComponent<ISkillEffectBehaviour>()?.Play(childCtx);

            // Play가 자기 spawnOffset을 더한 뒤이므로, 결과 위치와의 차이를 그대로 상대 오프셋으로 삼는다.
            children.Add(go.transform);
            offsets.Add(go.transform.position - transform.position);
        }

        /// <summary>
        /// 자식의 궤도를 「N바퀴 등속 회전 · 전체 수명」으로 덮어쓴다.
        /// 자식 코드는 한 줄도 고치지 않는다 — 전부 기존 public 프로퍼티다.
        ///
        /// ★여기서 덮어쓰는 것은 시간축·경로뿐이다. 반경·두께·개수·색·투명도 같은 「룩」은
        ///   전부 자식 프리셋의 관할이다(용권풍 전용 사본을 쓴다). 배수로 덧씌우던 초기 구현은
        ///   폐기했다 — 두 층의 반경 관계를 바꾸지 못하고, startSize가 두 상수 모드라
        ///   크기 배수가 최대값에만 걸리며, 알파는 구워진 그라디언트라 진입점 자체가 없었다.
        /// </summary>
        private void ApplyOrbit(GameObject go)
        {
            float angleEnd = angleStart + (clockwise ? 1f : -1f) * 360f * turns;

            var stroke = go.GetComponent<JcArcStrokeEffect>();
            if (stroke != null)
            {
                // ★수명 보정은 각도·스윕을 덮어쓰기 「전」에 — 프리셋이 잡은 기준 각속도를 읽어야 한다.
                float k = ArcPreserveFactor(stroke.AngleStart, stroke.AngleEnd, stroke.SweepDuration);
                stroke.StrokeLifeMin *= k;
                stroke.StrokeLifeMax *= k;

                stroke.AngleStart = angleStart;
                stroke.AngleEnd = angleEnd;
                stroke.SweepDuration = life;
                stroke.EaseOut = 1f;                 // 등속 — 「일정하게 날아간다」
                WarnIfTooFast(stroke.Radius, "호 획");
            }

            var point = go.GetComponent<JcArcPointEffect>();
            if (point != null)
            {
                float k = ArcPreserveFactor(point.AngleStart, point.AngleEnd, point.SweepDuration);
                point.StrokeLifeMin *= k;
                point.StrokeLifeMax *= k;

                point.AngleStart = angleStart;
                point.AngleEnd = angleEnd;
                point.SweepDuration = life;
                point.EaseOut = 1f;
                // 포인트의 서브스텝은 반경이 아니라 「반경 + 바깥 오프셋」으로 계산된다 —
                // 그래서 호 획보다 먼저 상한에 걸린다. 실제 한도는 이쪽이 정한다.
                WarnIfTooFast(point.Radius + Mathf.Max(point.OffsetMin, point.OffsetMax), "포인트 획");

                // 포인트의 선별 토글은 프리셋 관할이지만, 끄면 오버런이 켜져 비행이 끝난 뒤에도
                // 앵커가 연장 주행한다 — 용권풍에서는 사라진 뒤 궤적이 이어지는 그림이 된다.
                if (!point.SkipShortRemainder)
                    Debug.LogWarning("[JcVortex] 포인트 획의 Skip Short Remainder가 꺼져 있습니다 — " +
                                     "비행 종료 후에도 대시가 연장 주행합니다. 프리셋에서 켜는 것을 권합니다.", this);
            }

            var arcs = go.GetComponent<JcWindArcsEffect>();
            if (arcs != null)
            {
                arcs.FollowRoot = true;              // ★궤도 중심이 내 위치를 따라오게 한다(기본은 재생 시 고정)
                arcs.SpawnWindow = life;

                // 원호는 파티클이 아니라 TrailRenderer라 색 그라디언트를 자기가 만든다 — 알파 범위에 곱한다.
                arcs.AlphaMin *= opacity;
                arcs.AlphaMax *= opacity;
            }

            var shard = go.GetComponent<JcArcShardEffect>();
            if (shard != null)
            {
                shard.AngleStart = angleStart;
                shard.AngleEnd = angleEnd;
                shard.SweepDuration = life;
                shard.EaseOut = 1f;
                // ★파편은 농도에서 제외한다. 본체를 옅게 만드는 목적이 「파편이 묻히지 않게」라서,
                //   같이 옅어지면 상대 밝기가 그대로라 아무 것도 달라지지 않는다.
                return;
            }

            ApplyOpacity(go);
        }

        /// <summary>
        /// 층 전체 농도 — 파티클의 시작 색 알파에 한 번만 곱한다.
        ///
        /// 최종 입자 색 = 시작 색 × 수명 그라디언트라, 시작 색의 알파를 낮추면 그라디언트가 만든
        /// 색·발광 관계를 그대로 둔 채 전체 농도만 균일하게 내려간다. 재질이 가산(Blend SrcAlpha One)이고
        /// 셰이더가 rgb×알파를 뱉으므로 알파가 곧 출력 세기다.
        /// 프리셋의 색·발광을 건드리지 않는 것이 요점 — 색을 일일이 손대지 않고 한 손잡이로 다스린다.
        /// </summary>
        private void ApplyOpacity(GameObject go)
        {
            if (opacity >= 0.999f) return;

            ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                var main = systems[i].main;
                var sc = main.startColor;

                switch (sc.mode)
                {
                    case ParticleSystemGradientMode.Color:
                        main.startColor = Fade(sc.color);
                        break;
                    case ParticleSystemGradientMode.TwoColors:
                        main.startColor = new ParticleSystem.MinMaxGradient(Fade(sc.colorMin), Fade(sc.colorMax));
                        break;
                    default:
                        // 그라디언트 모드는 키를 통째로 다시 구워야 해 건드리지 않는다(현재 자산에는 없다).
                        Debug.LogWarning("[JcVortex] '" + systems[i].name + "'의 시작 색이 그라디언트 모드라 " +
                                         "농도 적용을 건너뜁니다.", this);
                        break;
                }
            }
        }

        private Color Fade(Color c)
        {
            c.a *= opacity;
            return c;
        }

        /// <summary>
        /// ★획 각도 보존 계수 — 획 수명에 곱해 「획 하나가 덮는 각도」를 프리셋 기준으로 유지한다.
        ///
        /// 획 수명이 절대 초라, 각속도가 바뀌면 획이 덮는 호 길이가 그대로 따라 변한다.
        /// 그래서 비행 거리를 줄이면(=수명 단축=각속도 상승) 획 하나가 몇 바퀴를 덮어 링이 꽉 차고,
        /// 리본 폭까지 더해져 「반경이 커진 것처럼」 보인다 — 거리와 룩이 엮이는 지점이었다.
        ///
        /// 계수 = 프리셋 각속도 ÷ 용권풍 각속도.
        /// 프리셋의 스윕·각도폭은 용권풍에서 덮어써지지만, 여기서 「이 수명 값이 의도된 기준 각속도」로
        /// 되살아난다. 덕분에 새 파라미터 없이 거리·회전수와 룩이 분리된다.
        /// </summary>
        private float ArcPreserveFactor(float srcAngleStart, float srcAngleEnd, float srcSweep)
        {
            if (!preserveStrokeArc) return 1f;

            float srcSpan = Mathf.Abs(srcAngleEnd - srcAngleStart);
            if (srcSpan < 1f || srcSweep < 0.001f)
            {
                Debug.LogWarning("[JcVortex] 자식 프리셋의 각도폭·스윕이 비어 있어 획 각도 보존을 건너뜁니다.", this);
                return 1f;
            }

            float srcDegPerSec = srcSpan / srcSweep;
            float vortexDegPerSec = 360f * turns / Mathf.Max(life, 0.01f);
            return srcDegPerSec / vortexDegPerSec;
        }

        /// <summary>
        /// 회전이 지나치게 빠르면 자식의 서브스텝 상한(16)에 걸려 첫 세그먼트 직선 글리치가 부활한다.
        /// 조용히 망가지면 튜닝 함정이므로 미리 알린다(실사용 범위에서는 여유가 크다).
        /// </summary>
        private void WarnIfTooFast(float radius, string layer)
        {
            float perFrame = 2f * Mathf.PI * radius * turns / Mathf.Max(life, 0.01f) / 60f;
            if (perFrame <= SubStepBudgetPerFrame) return;

            float limit = SubStepBudgetPerFrame * 60f * life / (2f * Mathf.PI * Mathf.Max(radius, 0.01f));
            Debug.LogWarning("[JcVortex] " + layer + "의 회전이 너무 빠릅니다 — 프레임당 호 이동 " +
                             perFrame.ToString("F2") + "m > 서브스텝 한도 " +
                             SubStepBudgetPerFrame.ToString("F2") + "m. 이 층의 회전수 한도는 약 " +
                             limit.ToString("F1") + "바퀴입니다(현재 " + turns.ToString("F1") + ").", this);
        }

        private void Update()
        {
            if (!flying) return;

            float dt = Time.deltaTime;
            elapsed += dt;

            if (elapsed >= launchDelay && travelled < distance)
            {
                float step = Mathf.Min(speed * dt, distance - travelled);
                travelled += step;
                transform.position += transform.forward * step;
            }

            // 자식은 부모가 아니다 — 위치만 동기화한다.
            // ★자식 파티클은 Local 시뮬이라 루트 이동이 곧 「그려진 획 전체의 이동」이다.
            //   (방출이 거리 기반이므로 전진이 밀도에 얹힐 수 있다 — 실측 후 필요하면 보정한다.)
            for (int i = children.Count - 1; i >= 0; i--)
            {
                if (children[i] == null)
                {
                    children.RemoveAt(i);
                    offsets.RemoveAt(i);
                    continue;
                }
                children[i].position = transform.position + offsets[i];
            }
        }
    }
}
