using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// Taosenaiyo 요소: 휘날리는 깃털 버스트(셰이더 JC/VFX/TaoFeather, 스크립트 풀 — HealCrossBurst 동형).
    /// 각 깃털 = 절차 SDF 빌보드. 모션 = 나선 상승(중심 둘레 각도 드리프트) + 플러터(사인 흔들림) + 자전(roll).
    /// Play(center)/StopSpawning/StopAll. 마스터 페이드아웃 시 StopSpawning(잔여 자연 소멸).
    /// </summary>
    public class TaoFeatherBurst : MonoBehaviour
    {
        const float TAU = Mathf.PI * 2f;

        [Header("References")]
        [Tooltip("깃털 재질(JC/VFX/TaoFeather, 가산)")]
        [SerializeField] private Material featherMaterial;

        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영(새 깃털부터).")]
        [SerializeField] private TaoFeatherPreset preset;
        [Tooltip("켜면 변경한 설정을 실행 중인 이 효과에 갱신합니다. 파일 저장과는 별개이며 이미 시작된 시간표는 재시전하여 확인합니다.")]
        [SerializeField] private bool livePreview = true;

        [Header("스폰")]
        [Tooltip("동시 최대 깃털 수")]
        [SerializeField] private int maxFeathers = 10;
        [Tooltip("새 요소 생성 간격의 최솟값(초)입니다. 낮을수록 더 자주 생성될 수 있습니다.")]
        [SerializeField] private float spawnIntervalMin = 0.12f;
        [Tooltip("새 요소 생성 간격의 최댓값(초)입니다. 높일수록 생성 사이의 간격이 길어질 수 있습니다.")]
        [SerializeField] private float spawnIntervalMax = 0.3f;
        [Tooltip("생성되는 요소 수명의 최솟값(초)입니다. 최댓값과의 사이에서 지속 시간이 선택됩니다.")]
        [SerializeField] private float lifetimeMin = 1.5f;
        [Tooltip("생성되는 요소 수명의 최댓값(초)입니다. 높일수록 일부 요소가 오래 남습니다.")]
        [SerializeField] private float lifetimeMax = 2.3f;
        [Tooltip("스폰 반경(m). 중심 주위 원판 내 랜덤")]
        [SerializeField] private float spawnRadius = 0.8f;
        [Tooltip("시작 높이(중심 기준, m)")]
        [SerializeField] private float baseYOffset = 0.15f;

        [Header("모션")]
        [Tooltip("상승 속도 최소(m/s)")]
        [SerializeField] private float riseSpeedMin = 0.45f;
        [Tooltip("상승 속도 최대(m/s)")]
        [SerializeField] private float riseSpeedMax = 0.8f;
        [Tooltip("중심 둘레 나선 회전(도/초)")]
        [Range(-180f, 180f)] [SerializeField] private float spiralSpeed = 40f;
        [Tooltip("좌우 플러터 진폭(m)")]
        [Range(0f, 0.5f)] [SerializeField] private float swayAmp = 0.12f;
        [Tooltip("플러터 빈도(Hz)")]
        [Range(0f, 6f)] [SerializeField] private float swayFreq = 1.4f;
        [Tooltip("자전(roll) 최대(±도/초)")]
        [Range(0f, 360f)] [SerializeField] private float spinMax = 90f;

        [Header("크기 / 페이드")]
        [Tooltip("생성되는 요소 크기의 최솟값(m)입니다. 최댓값과의 사이에서 크기가 선택됩니다.")]
        [SerializeField] private float sizeMin = 0.18f;
        [Tooltip("생성되는 요소 크기의 최댓값(m)입니다. 최솟값과 벌릴수록 크기 차이가 커집니다.")]
        [SerializeField] private float sizeMax = 0.3f;
        [Tooltip("페이드인 구간 비율(수명 대비)")]
        [Range(0f, 0.5f)] [SerializeField] private float fadeInFrac = 0.15f;
        [Tooltip("페이드아웃 구간 비율(수명 대비)")]
        [Range(0.05f, 0.9f)] [SerializeField] private float fadeOutFrac = 0.4f;

        [Header("룩 (깃털 셰이더)")]
        [Tooltip("이 시각 요소의 색입니다. HDR 색은 발광 강도와 함께 최종 밝기에 영향을 줍니다.")]
        [ColorUsage(true, true)] [SerializeField] private Color color = new Color(1f, 0.9f, 0.55f);
        [Tooltip("전체 밝기")]
        [Range(0f, 8f)] [SerializeField] private float intensity = 2.2f;

        private Vector3 _center;
        private bool _spawning;
        private float _nextSpawn;
        private Mesh _quad;
        private Camera _cam;
        private readonly List<Feather> _pool = new List<Feather>();

        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int BendID = Shader.PropertyToID("_Bend");
        private static readonly int BarbFreqID = Shader.PropertyToID("_BarbFreq");
        private static readonly int BarbAmountID = Shader.PropertyToID("_BarbAmount");
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        private class Feather
        {
            public GameObject go;
            public MeshRenderer mr;
            public MaterialPropertyBlock mpb;
            public bool active;
            public float t, dur, ang0, r0, riseSpeed, size, swayPhase, spin, bendSign;
        }

        private void Awake()
        {
            _quad = BuildQuad();
        }

        public void SetPreset(TaoFeatherPreset p) => preset = p;

        /// <summary>중심 주입 + 깃털 스폰 시작.</summary>
        public void Play(Vector3 center)
        {
            _center = center;
            _spawning = true;
            _nextSpawn = 0f;
        }

        /// <summary>스폰만 멈춤(살아있는 깃털은 자연 소멸).</summary>
        public void StopSpawning() => _spawning = false;

        /// <summary>즉시 전체 정지·숨김.</summary>
        public void StopAll()
        {
            _spawning = false;
            foreach (var f in _pool)
            {
                f.active = false;
                if (f.go) f.go.SetActive(false);
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
            // ★라이브 따름(260807) — 스폰·거동·형태는 TransformSource(Alter→Basic), 색·밝기만 자기 것.
            var t = preset.TransformSource;
            maxFeathers = t.maxFeathers;
            spawnIntervalMin = t.spawnIntervalMin;
            spawnIntervalMax = t.spawnIntervalMax;
            lifetimeMin = t.lifetimeMin;
            lifetimeMax = t.lifetimeMax;
            spawnRadius = t.spawnRadius;
            baseYOffset = t.baseYOffset;
            riseSpeedMin = t.riseSpeedMin;
            riseSpeedMax = t.riseSpeedMax;
            spiralSpeed = t.spiralSpeed;
            swayAmp = t.swayAmp;
            swayFreq = t.swayFreq;
            spinMax = t.spinMax;
            sizeMin = t.sizeMin;
            sizeMax = t.sizeMax;
            fadeInFrac = t.fadeInFrac;
            fadeOutFrac = t.fadeOutFrac;
            color = preset.color;
            intensity = preset.intensity;
        }

        private void Advance(Feather f)
        {
            f.t += Time.deltaTime;
            float u = f.dur > 0f ? f.t / f.dur : 1f;
            if (u >= 1f) { f.active = false; f.go.SetActive(false); return; }

            // 나선 상승 + 플러터
            float ang = f.ang0 + Mathf.Deg2Rad * spiralSpeed * f.t;
            Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * f.r0;
            Vector3 pos = _center + radial + Vector3.up * (baseYOffset + f.riseSpeed * f.t);

            if (_cam == null) _cam = Camera.main;
            if (_cam)
            {
                Vector3 sway = _cam.transform.right * (Mathf.Sin((f.t * swayFreq + f.swayPhase) * TAU) * swayAmp);
                pos += sway;
                f.go.transform.rotation = _cam.transform.rotation * Quaternion.AngleAxis(f.spin * f.t, Vector3.forward);
            }
            f.go.transform.position = pos;
            f.go.transform.localScale = new Vector3(f.size * 0.55f, f.size, 1f);   // 깃털은 세로로 긴 쿼드

            float aIn = Mathf.Clamp01(u / Mathf.Max(fadeInFrac, 1e-4f));
            float aOut = 1f - Mathf.Clamp01((u - (1f - fadeOutFrac)) / Mathf.Max(fadeOutFrac, 1e-4f));
            float alpha = aIn * aOut;

            if (f.mpb == null) f.mpb = new MaterialPropertyBlock();
            f.mr.GetPropertyBlock(f.mpb);
            if (livePreview && preset)
            {
                var t = preset.TransformSource;   // ★따름 — 깃털 형태(벤드·깃가지)는 Basic 정본
                f.mpb.SetColor(ColorID, color);
                f.mpb.SetFloat(IntensityID, intensity);
                f.mpb.SetFloat(BendID, t.bend * f.bendSign);
                f.mpb.SetFloat(BarbFreqID, t.barbFreq);
                f.mpb.SetFloat(BarbAmountID, t.barbAmount);
            }
            f.mpb.SetFloat(FadeMulID, alpha);
            f.mr.SetPropertyBlock(f.mpb);
        }

        private void Spawn()
        {
            var f = GetFeather();
            if (f == null) return;
            f.active = true;
            f.t = 0f;
            f.dur = Random.Range(lifetimeMin, lifetimeMax);
            f.ang0 = Random.Range(0f, TAU);
            f.r0 = spawnRadius * Mathf.Sqrt(Random.value);   // 원판 균일
            f.riseSpeed = Random.Range(riseSpeedMin, riseSpeedMax);
            f.size = Random.Range(sizeMin, sizeMax);
            f.swayPhase = Random.value;
            f.spin = Random.Range(-spinMax, spinMax);
            f.bendSign = Random.value < 0.5f ? -1f : 1f;
            f.go.transform.localScale = Vector3.zero;
            f.go.SetActive(true);
        }

        private Feather GetFeather()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].active) return _pool[i];
            if (_pool.Count >= maxFeathers) return null;

            var go = new GameObject("Feather_" + _pool.Count);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _quad;
            var mr = go.AddComponent<MeshRenderer>();
            if (featherMaterial) mr.sharedMaterial = featherMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.SetActive(false);
            var f = new Feather { go = go, mr = mr, mpb = new MaterialPropertyBlock(), active = false };
            _pool.Add(f);
            return f;
        }

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "TaoFeatherQuad" };
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
