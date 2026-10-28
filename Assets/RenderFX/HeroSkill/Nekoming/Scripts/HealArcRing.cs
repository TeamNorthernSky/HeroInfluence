using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 힐 오라 E-2: 구체와 독립된 원호(arc) 스윕 링.
    /// 중심각 120~300°의 원호가 빠르게 순간적으로 생겨나 회전하며, 반경 노이즈 + 회전축 ±20° 랜덤 틸트.
    /// 각 원호 = 빠르게 호를 훑는 head + TrailRenderer(리본). 풀링으로 재사용.
    /// 좌표(중심)는 호출자 주입: Play(center). HealOrbitVfx가 오브 궤도와 함께 구동.
    /// </summary>
    public class HealArcRing : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("원호 리본 재질(가산). 없으면 렌더 안 됨.")]
        [SerializeField] private Material trailMaterial;

        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영(새 원호부터).")]
        [SerializeField] private HealArcRingPreset preset;
        [Tooltip("플레이 중 preset 값을 반영. 끄면 아래 필드값 사용.")]
        [SerializeField] private bool livePreview = true;

        [Header("Geometry")]
        [Tooltip("기본 회전 반경(m)")]
        [SerializeField] private float radius = 1.0f;
        [Tooltip("반경 노이즈(±m). 원호마다 랜덤 가감")]
        [SerializeField] private float radiusNoise = 0.08f;
        [Tooltip("링 중심 높이(m)")]
        [SerializeField] private float orbitHeight = 0.35f;
        [Tooltip("원호 중심각 최소(도)")]
        [SerializeField] private float arcSpanMinDeg = 120f;
        [Tooltip("원호 중심각 최대(도)")]
        [SerializeField] private float arcSpanMaxDeg = 300f;
        [Tooltip("회전축 랜덤 틸트 최대(±도). 원호마다 회전면이 흔들림")]
        [SerializeField] private float tiltMaxDeg = 20f;
        [Tooltip("켜면 시계/반시계 랜덤")]
        [SerializeField] private bool bidirectional = true;

        [Header("Timing")]
        [Tooltip("한 원호가 호를 훑는 시간(초). 짧을수록 순간적")]
        [SerializeField] private float sweepDurationMin = 0.28f;
        [Tooltip("한 원호가 호를 훑는 시간 최대(초)")]
        [SerializeField] private float sweepDurationMax = 0.42f;
        [Tooltip("새 원호 생성 간격(초)")]
        [SerializeField] private float spawnIntervalMin = 0.06f;
        [Tooltip("새 요소 생성 간격의 최댓값(초)입니다. 높일수록 생성 사이의 간격이 길어질 수 있습니다.")]
        [SerializeField] private float spawnIntervalMax = 0.16f;
        [Tooltip("동시 최대 원호 수")]
        [SerializeField] private int maxArcs = 12;

        [Header("Look")]
        [Tooltip("리본 잔상 시간 최소(초). 원호마다 min~max 랜덤. 클수록 원호 전체가 오래 보임")]
        [SerializeField] private float trailTimeMin = 0.24f;
        [Tooltip("리본 잔상 시간 최대(초)")]
        [SerializeField] private float trailTimeMax = 0.36f;
        [Tooltip("리본 최대 폭 최소(m). 원호마다 min~max 랜덤 피크폭. 생명주기 절반에서 이 폭, 앞뒤로 0")]
        [SerializeField] private float widthStartMin = 0.045f;
        [Tooltip("리본 최대 폭 최대(m)")]
        [SerializeField] private float widthStartMax = 0.075f;
        [Tooltip("리본 색(머리→꼬리). 알파로 페이드")]
        [SerializeField] private Gradient colorGradient;
        [Tooltip("원호별 전체 투명도 최소(0~1). 원호마다 min~max 랜덤 배수로 알파에 곱함")]
        [Range(0f, 1f)] [SerializeField] private float alphaMin = 0.7f;
        [Tooltip("원호별 전체 투명도 최대(0~1)")]
        [Range(0f, 1f)] [SerializeField] private float alphaMax = 1.0f;

        private Vector3 _center;
        private bool _spawning;
        private float _nextSpawn;
        private readonly List<Arc> _pool = new List<Arc>();

        private class Arc
        {
            public GameObject go;
            public TrailRenderer tr;
            public bool active;
            public float t, dur, a0, span, r, trailTime, width, alpha;
            public int dir;
            public Quaternion plane;
        }

        /// <summary>중심 주입 + 원호 스폰 시작.</summary>
        public void Play(Vector3 center)
        {
            _center = center;
            _spawning = true;
            _nextSpawn = 0f;
        }

        /// <summary>스폰만 멈춤(살아있는 원호는 자연 소멸).</summary>
        public void StopSpawning() => _spawning = false;

        /// <summary>즉시 전체 정지·숨김.</summary>
        public void StopAll()
        {
            _spawning = false;
            foreach (var a in _pool)
            {
                a.active = false;
                if (a.go) a.go.SetActive(false);
            }
        }

        private void Update()
        {
            if (livePreview && preset) PullFromPreset();

            if (_spawning)
            {
                _nextSpawn -= Time.deltaTime;
                if (_nextSpawn <= 0f)
                {
                    Spawn();
                    _nextSpawn = Random.Range(spawnIntervalMin, spawnIntervalMax);
                }
            }

            for (int i = 0; i < _pool.Count; i++)
            {
                var a = _pool[i];
                if (a.active) Advance(a);
            }
        }

        private void PullFromPreset()
        {
            var t = preset.TransformSource;   // ★트랜스폼은 따름 규칙(Alter→Basic), 색·알파는 자기 것
            spawnIntervalMin = t.spawnIntervalMin;
            spawnIntervalMax = t.spawnIntervalMax;
            maxArcs = t.maxArcs;
            sweepDurationMin = t.sweepDurationMin;
            sweepDurationMax = t.sweepDurationMax;
            arcSpanMinDeg = t.arcSpanMinDeg;
            arcSpanMaxDeg = t.arcSpanMaxDeg;
            radius = t.radius;
            radiusNoise = t.radiusNoise;
            orbitHeight = t.orbitHeight;
            tiltMaxDeg = t.tiltMaxDeg;
            bidirectional = t.bidirectional;
            widthStartMin = t.widthStartMin;
            widthStartMax = t.widthStartMax;
            trailTimeMin = t.trailTimeMin;
            trailTimeMax = t.trailTimeMax;
            alphaMin = preset.alphaMin;
            alphaMax = preset.alphaMax;
            if (preset.colorGradient != null) colorGradient = preset.colorGradient;
        }

        private void Advance(Arc a)
        {
            a.t += Time.deltaTime;

            // 폭 = 생명주기 펄스(최소→최대@절반→최소). sin(π·p): 0→1→0
            float life = a.dur + a.trailTime;
            float lifeP = life > 0f ? Mathf.Clamp01(a.t / life) : 1f;
            a.tr.widthMultiplier = a.width * Mathf.Sin(Mathf.PI * lifeP);

            if (a.t <= a.dur)
            {
                // 호를 훑는 구간: easeOut(감속)로 순간적 등장 느낌
                float x = Mathf.Clamp01(a.t / a.dur);
                float p = 1f - (1f - x) * (1f - x);
                float ang = a.a0 + a.dir * a.span * p;
                a.go.transform.position = ArcPos(a, ang);
            }
            else if (a.t >= a.dur + a.trailTime)
            {
                // 잔상까지 소멸 → 재활용
                a.active = false;
                a.go.SetActive(false);
            }
            // dur~dur+trailTime: head 고정, 트레일 자연 페이드
        }

        private Vector3 ArcPos(Arc a, float angleDeg)
        {
            float r = Mathf.Deg2Rad * angleDeg;
            Vector3 baseP = new Vector3(Mathf.Cos(r) * a.r, 0f, Mathf.Sin(r) * a.r);
            return _center + Vector3.up * orbitHeight + a.plane * baseP;
        }

        private void Spawn()
        {
            var a = GetArc();
            if (a == null) return;
            a.active = true;
            a.t = 0f;
            a.dur = Random.Range(sweepDurationMin, sweepDurationMax);
            a.span = Random.Range(arcSpanMinDeg, arcSpanMaxDeg);
            a.r = radius + Random.Range(-radiusNoise, radiusNoise);
            a.dir = (bidirectional && Random.value < 0.5f) ? -1 : 1;
            a.a0 = Random.Range(0f, 360f);
            a.trailTime = Random.Range(trailTimeMin, trailTimeMax);
            a.width = Random.Range(widthStartMin, widthStartMax);
            a.alpha = Random.Range(alphaMin, alphaMax);
            a.plane = Quaternion.Euler(Random.Range(-tiltMaxDeg, tiltMaxDeg), 0f, Random.Range(-tiltMaxDeg, tiltMaxDeg));

            a.go.transform.position = ArcPos(a, a.a0);
            a.go.SetActive(true);
            ConfigureTrail(a.tr, a.trailTime, a.alpha);
            a.tr.Clear();   // 이전 위치에서 이어지는 줄 방지
        }

        private Arc GetArc()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].active) return _pool[i];

            if (_pool.Count >= maxArcs) return null;

            var go = new GameObject("Arc_" + _pool.Count);
            go.transform.SetParent(transform, false);
            var tr = go.AddComponent<TrailRenderer>();
            ConfigureTrail(tr, trailTimeMax, alphaMax);
            go.SetActive(false);
            var a = new Arc { go = go, tr = tr, active = false };
            _pool.Add(a);
            return a;
        }

        private void ConfigureTrail(TrailRenderer tr, float trailTime, float alpha)
        {
            tr.time = trailTime;
            tr.minVertexDistance = 0.01f;
            tr.autodestruct = false;
            tr.emitting = true;
            tr.numCapVertices = 4;
            tr.numCornerVertices = 2;
            tr.alignment = LineAlignment.View;
            tr.textureMode = LineTextureMode.Stretch;
            tr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f); // 균일 폭. 실제 두께는 Advance의 widthMultiplier 펄스로
            tr.widthMultiplier = 0f;                             // 시작 0 → Advance가 매 프레임 펄스 구동
            if (trailMaterial) tr.sharedMaterial = trailMaterial;
            if (colorGradient != null) tr.colorGradient = ScaleGradientAlpha(colorGradient, alpha); // 원호별 전체 투명도
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
        }

        /// <summary>그라디언트 알파 전체에 배수를 곱한 새 그라디언트(색 키는 유지).</summary>
        private static Gradient ScaleGradientAlpha(Gradient src, float mul)
        {
            var ak = src.alphaKeys;
            for (int i = 0; i < ak.Length; i++)
                ak[i].alpha = Mathf.Clamp01(ak[i].alpha * mul);
            var g = new Gradient();
            g.mode = src.mode;
            g.SetKeys(src.colorKeys, ak);
            return g;
        }
    }
}
