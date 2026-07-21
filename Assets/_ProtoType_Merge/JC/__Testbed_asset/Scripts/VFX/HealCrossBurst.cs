using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 힐 오라 E-4: 캐릭터 주변 바닥에서 위로 솟는 녹색 십자 빌보드 버스트.
    /// 각 십자 = 절차 셰이더(JC/VFX/HealCross) 쿼드. 스크립트 풀링(HealArcRing과 동형).
    /// ★포보보봉: 스폰 시 스케일 easeOutBack 오버슛 팝 + 상승 오버슛(위로 튀었다 정착) + 미세 bob + 시차 스폰.
    /// 좌표(중심=착지점 지면)는 호출자 주입: Play(center). HealOrbitVfx가 오브/원호/광채와 함께 구동.
    /// 마스터 페이드아웃 시 StopSpawning(새 십자 중단, 기존은 자연 소멸) — 원호와 동일.
    /// </summary>
    public class HealCrossBurst : MonoBehaviour
    {
        const float TAU = Mathf.PI * 2f;

        [Header("References")]
        [Tooltip("십자 빌보드 재질(가산). 없으면 렌더 안 됨.")]
        [SerializeField] private Material crossMaterial;

        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영(새 십자부터).")]
        [SerializeField] private HealCrossPreset preset;
        [SerializeField] private bool livePreview = true;

        [Header("스폰")]
        [SerializeField] private int maxCrosses = 14;
        [SerializeField] private float spawnIntervalMin = 0.06f;
        [SerializeField] private float spawnIntervalMax = 0.16f;
        [SerializeField] private float lifetimeMin = 0.9f;
        [SerializeField] private float lifetimeMax = 1.3f;

        [Header("배치 / 상승")]
        [SerializeField] private float radius = 0.7f;
        [SerializeField] private float baseYOffset = 0.05f;
        [SerializeField] private float riseSpeedMin = 1.2f;
        [SerializeField] private float riseSpeedMax = 1.8f;
        [Range(1f, 8f)] [SerializeField] private float riseAccelMul = 3.0f;
        [Range(0f, 1.5f)] [SerializeField] private float riseAccelTime = 0.25f;

        [Header("바운스 팝 / bob / 스핀")]
        [Range(0f, 4f)] [SerializeField] private float popOvershoot = 2.0f;
        [Range(0.05f, 0.6f)] [SerializeField] private float popFrac = 0.25f;
        [Range(0.05f, 0.6f)] [SerializeField] private float endFrac = 0.3f;
        [Range(0f, 0.3f)] [SerializeField] private float bobAmp = 0.05f;
        [Range(0f, 6f)] [SerializeField] private float bobFreq = 2.0f;
        [Range(0f, 180f)] [SerializeField] private float spinMax = 25f;
        [Tooltip("빌보드 Y축만: 수직 유지·수평만 카메라 향함. 끄면 카메라 완전 정면")]
        [SerializeField] private bool billboardYOnly = true;

        [Header("크기 / 페이드")]
        [SerializeField] private float sizeMin = 0.28f;
        [SerializeField] private float sizeMax = 0.44f;
        [Range(0f, 0.5f)] [SerializeField] private float fadeInFrac = 0.12f;
        [Range(0f, 0.6f)] [SerializeField] private float fadeOutFrac = 0.3f;

        [Header("룩")]
        [ColorUsage(true, true)] [SerializeField] private Color color = new Color(0.35f, 1f, 0.45f);
        [Range(0f, 8f)] [SerializeField] private float intensity = 2.2f;
        [Range(0.02f, 0.5f)] [SerializeField] private float barWidth = 0.13f;
        [Range(0.1f, 0.5f)] [SerializeField] private float barLength = 0.42f;
        [Range(0.001f, 0.3f)] [SerializeField] private float softness = 0.06f;

        private Vector3 _center;
        private bool _spawning;
        private float _nextSpawn;
        private Mesh _quad;
        private Camera _cam;
        private readonly List<Cross> _pool = new List<Cross>();

        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int BarWidthID = Shader.PropertyToID("_BarWidth");
        private static readonly int BarLengthID = Shader.PropertyToID("_BarLength");
        private static readonly int SoftnessID = Shader.PropertyToID("_Softness");
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        private class Cross
        {
            public GameObject go;
            public MeshRenderer mr;
            public MaterialPropertyBlock mpb;
            public bool active;
            public float t, dur, riseSpeed, size, bobPhase, spin;
            public Vector3 basePos;
        }

        private void Awake()
        {
            _quad = BuildQuad();
        }

        /// <summary>중심 주입 + 십자 스폰 시작.</summary>
        public void Play(Vector3 center)
        {
            _center = center;
            _spawning = true;
            _nextSpawn = 0f;
        }

        /// <summary>스폰만 멈춤(살아있는 십자는 자연 소멸).</summary>
        public void StopSpawning() => _spawning = false;

        /// <summary>즉시 전체 정지·숨김.</summary>
        public void StopAll()
        {
            _spawning = false;
            foreach (var c in _pool)
            {
                c.active = false;
                if (c.go) c.go.SetActive(false);
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
                if (_pool[i].active) Advance(_pool[i]);
        }

        private void PullFromPreset()
        {
            maxCrosses = preset.maxCrosses;
            spawnIntervalMin = preset.spawnIntervalMin;
            spawnIntervalMax = preset.spawnIntervalMax;
            lifetimeMin = preset.lifetimeMin;
            lifetimeMax = preset.lifetimeMax;
            radius = preset.radius;
            baseYOffset = preset.baseYOffset;
            riseSpeedMin = preset.riseSpeedMin;
            riseSpeedMax = preset.riseSpeedMax;
            riseAccelMul = preset.riseAccelMul;
            riseAccelTime = preset.riseAccelTime;
            popOvershoot = preset.popOvershoot;
            popFrac = preset.popFrac;
            endFrac = preset.endFrac;
            bobAmp = preset.bobAmp;
            bobFreq = preset.bobFreq;
            spinMax = preset.spinMax;
            billboardYOnly = preset.billboardYOnly;
            sizeMin = preset.sizeMin;
            sizeMax = preset.sizeMax;
            fadeInFrac = preset.fadeInFrac;
            fadeOutFrac = preset.fadeOutFrac;
            color = preset.color;
            intensity = preset.intensity;
            barWidth = preset.barWidth;
            barLength = preset.barLength;
            softness = preset.softness;
        }

        private void Advance(Cross c)
        {
            c.t += Time.deltaTime;
            float u = c.dur > 0f ? c.t / c.dur : 1f;
            if (u >= 1f) { c.active = false; c.go.SetActive(false); return; }

            // 상승: 속도 적분. v(t)=최종속도 + 초반부스트×decay(선형 1→0, riseAccelTime 동안).
            //   → 초반엔 최종속도×riseAccelMul로 빠르게 솟다가 riseAccelTime에 걸쳐 최종속도로 정착.
            float boost = c.riseSpeed * (riseAccelMul - 1f);
            float at = Mathf.Max(riseAccelTime, 1e-4f);
            float boostInt = c.t < at ? (c.t - c.t * c.t / (2f * at)) : (at * 0.5f);   // ∫ decay dt
            float riseY = c.riseSpeed * c.t + boost * boostInt;
            float bob = bobAmp * Mathf.Sin((c.t * bobFreq + c.bobPhase) * TAU);
            c.go.transform.position = c.basePos + Vector3.up * (riseY + bob);

            // 빌보드 + 면내 스핀(roll)
            if (_cam == null) _cam = Camera.main;
            if (_cam)
            {
                Quaternion look;
                if (billboardYOnly)
                {
                    // Y축만: 수평으로만 카메라를 향하고 수직 유지
                    Vector3 f = c.go.transform.position - _cam.transform.position;
                    f.y = 0f;
                    look = f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f.normalized, Vector3.up) : c.go.transform.rotation;
                }
                else look = _cam.transform.rotation;   // 카메라 완전 정면
                if (Mathf.Abs(c.spin) > 0.01f) look = look * Quaternion.AngleAxis(c.spin * c.t, Vector3.forward);
                c.go.transform.rotation = look;
            }

            // 스케일: 팝 오버슛(스폰) × 종료 축소
            float pop = u < popFrac ? EaseOutBack01(u / Mathf.Max(popFrac, 1e-4f), popOvershoot) : 1f;
            float endS = 1f - Mathf.Clamp01((u - (1f - endFrac)) / Mathf.Max(endFrac, 1e-4f));
            c.go.transform.localScale = Vector3.one * (c.size * pop * endS);

            // 알파(수명 대비 페이드인/아웃)
            float aIn = Mathf.Clamp01(u / Mathf.Max(fadeInFrac, 1e-4f));
            float aOut = 1f - Mathf.Clamp01((u - (1f - fadeOutFrac)) / Mathf.Max(fadeOutFrac, 1e-4f));
            float alpha = aIn * aOut;

            if (c.mpb == null) c.mpb = new MaterialPropertyBlock();
            c.mr.GetPropertyBlock(c.mpb);
            if (livePreview && preset)
            {
                c.mpb.SetColor(ColorID, color);
                c.mpb.SetFloat(IntensityID, intensity);
                c.mpb.SetFloat(BarWidthID, barWidth);
                c.mpb.SetFloat(BarLengthID, barLength);
                c.mpb.SetFloat(SoftnessID, softness);
            }
            c.mpb.SetFloat(FadeMulID, alpha);
            c.mr.SetPropertyBlock(c.mpb);
        }

        private void Spawn()
        {
            var c = GetCross();
            if (c == null) return;
            c.active = true;
            c.t = 0f;
            c.dur = Random.Range(lifetimeMin, lifetimeMax);

            // 원판 내 균일 랜덤(√ 보정)
            float ang = Random.Range(0f, TAU);
            float rr = radius * Mathf.Sqrt(Random.value);
            Vector3 off = new Vector3(Mathf.Cos(ang) * rr, 0f, Mathf.Sin(ang) * rr);
            c.basePos = _center + Vector3.up * baseYOffset + off;

            c.riseSpeed = Random.Range(riseSpeedMin, riseSpeedMax);
            c.size = Random.Range(sizeMin, sizeMax);
            c.bobPhase = Random.value;
            c.spin = Random.Range(-spinMax, spinMax);

            c.go.transform.position = c.basePos;
            c.go.transform.localScale = Vector3.zero;   // 0에서 팝
            c.go.SetActive(true);
        }

        private Cross GetCross()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].active) return _pool[i];

            if (_pool.Count >= maxCrosses) return null;

            var go = new GameObject("Cross_" + _pool.Count);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _quad;
            var mr = go.AddComponent<MeshRenderer>();
            if (crossMaterial) mr.sharedMaterial = crossMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.SetActive(false);
            var c = new Cross { go = go, mr = mr, mpb = new MaterialPropertyBlock(), active = false };
            _pool.Add(c);
            return c;
        }

        /// <summary>easeOutBack: 0→(오버슛 &gt;1)→1. s=오버슛 강도(0=오버슛 없음).</summary>
        private static float EaseOutBack01(float x, float s)
        {
            float c1 = s;
            float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "HealCrossQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f),
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
            };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }
    }
}
