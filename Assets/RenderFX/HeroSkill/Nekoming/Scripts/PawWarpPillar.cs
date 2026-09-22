using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// PawForYou 요소: 워프 블링크 빛기둥(셰이더 JC/VFX/PawWarpPillar). 록맨 텔레포트풍.
    /// Burst(위치) 호출 → 한줄기 수직 빛기둥이 반짝(플래시) → 하단부터 위로 슈릭 빠져나가며 소멸.
    /// 소멸/재등장 지점 양쪽에서 같은 연출. 두 버스트 겹침 대비 소형 풀(2). Y축 빌보드.
    /// ★런타임 프리뷰: preset + livePreview 켜면 재생 중 값 즉시 반영.
    /// </summary>
    public class PawWarpPillar : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("빛기둥 재질(JC/VFX/PawWarpPillar, 가산)")]
        [SerializeField] private Material pillarMaterial;

        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영.")]
        [SerializeField] private PawWarpPillarPreset preset;
        [Tooltip("켜면 변경한 설정을 실행 중인 이 효과에 갱신합니다. 파일 저장과는 별개이며 이미 시작된 시간표는 재시전하여 확인합니다.")]
        [SerializeField] private bool livePreview = true;

        [Header("배치 / 타임라인 (livePreview 시 preset 사용)")]
        [Tooltip("기둥 폭(m)")]
        [SerializeField] private float width = 0.5f;
        [Tooltip("기둥 높이(m)")]
        [SerializeField] private float height = 4f;
        [Tooltip("기둥 하단의 기준점 대비 오프셋(m). -면 발 아래까지 내려옴")]
        [SerializeField] private float bottomOffset = -0.3f;
        [Tooltip("버스트 수명(초)")]
        [Range(0.1f, 2f)] [SerializeField] private float duration = 0.35f;
        [Tooltip("플래시 구간 비율(수명 대비). 이 동안 기둥 전체가 반짝")]
        [Range(0.05f, 0.8f)] [SerializeField] private float flashFrac = 0.35f;
        [Tooltip("플래시 시작 밝기 부스트(1+이 값에서 1로 감쇠)")]
        [Range(0f, 3f)] [SerializeField] private float flashBoost = 1.2f;
        [Tooltip("슈릭 구간에서 폭이 줄어드는 정도(0=유지)")]
        [Range(0f, 1f)] [SerializeField] private float narrow = 0.4f;

        [Header("룩 (livePreview 시 preset 사용)")]
        [Tooltip("중심 코어의 색입니다. 테두리 색과 별도로 중심부의 인상을 조절합니다.")]
        [ColorUsage(true, true)] [SerializeField] private Color coreColor = Color.white;
        [Tooltip("글로우(외곽 번짐) 색")]
        [ColorUsage(true, true)] [SerializeField] private Color glowColor = new Color(0.45f, 0.8f, 1f);
        [Tooltip("전체 밝기")]
        [Range(0f, 10f)] [SerializeField] private float intensity = 3f;

        private Mesh _quad;
        private Camera _cam;
        private readonly List<Entry> _pool = new List<Entry>();

        private static readonly int CoreColorID = Shader.PropertyToID("_CoreColor");
        private static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int CoreWidthID = Shader.PropertyToID("_CoreWidth");
        private static readonly int GlowFalloffID = Shader.PropertyToID("_GlowFalloff");
        private static readonly int EdgeSoftID = Shader.PropertyToID("_EdgeSoft");
        private static readonly int SweepID = Shader.PropertyToID("_Sweep");
        private static readonly int SweepSoftID = Shader.PropertyToID("_SweepSoft");
        private static readonly int TopFadeID = Shader.PropertyToID("_TopFade");
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        private class Entry
        {
            public GameObject go;
            public MeshRenderer mr;
            public MaterialPropertyBlock mpb;
            public bool active;
            public float t;
            public Vector3 basePos;
        }

        private void Awake()
        {
            EnsureQuad();
        }

        /// <summary>★지연 초기화 — pre-Awake 호출 시 풀 엔트리에 null 메시가 영구 대입되는 것 방지(VFX 부품 공통 규격).</summary>
        private void EnsureQuad()
        {
            if (_quad == null) _quad = BuildQuad();
        }

        /// <summary>변형 프리셋 런타임 교체.</summary>
        public void SetPreset(PawWarpPillarPreset p) => preset = p;

        /// <summary>현재 버스트 수명(초) — P5 duration 이 유일 정본. 오케스트레이터가 정리 대기 시간으로 읽어간다.</summary>
        public float CurrentDuration => livePreview && preset ? preset.duration : duration;

        /// <summary>지정 위치에서 빛기둥 블링크 재생(자체 수명 소멸).</summary>
        public void Burst(Vector3 pos)
        {
            EnsureQuad();
            if (livePreview && preset) PullFromPreset();
            var e = GetEntry();
            if (e == null) return;
            e.active = true;
            e.t = 0f;
            e.basePos = pos;
            e.go.SetActive(true);
        }

        /// <summary>즉시 전체 숨김.</summary>
        public void StopAll()
        {
            foreach (var e in _pool)
            {
                e.active = false;
                if (e.go) e.go.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (livePreview && preset) PullFromPreset();
            if (_cam == null) _cam = Camera.main;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.active) continue;
                e.t += Time.deltaTime;
                float u = Mathf.Clamp01(e.t / Mathf.Max(duration, 1e-4f));
                if (u >= 1f) { e.active = false; e.go.SetActive(false); continue; }

                // 타임라인: 플래시(전체 반짝) → 슈릭(하단 경계 easeIn으로 상승 + 폭 축소)
                float sweep, boost, wMul;
                if (u < flashFrac)
                {
                    float uf = u / Mathf.Max(flashFrac, 1e-4f);
                    sweep = 0f;
                    boost = 1f + flashBoost * (1f - uf);
                    wMul = 1f;
                }
                else
                {
                    float us = (u - flashFrac) / Mathf.Max(1f - flashFrac, 1e-4f);
                    sweep = us * us;   // easeIn: 가속하며 위로 빠져나감
                    boost = 1f;
                    wMul = 1f - narrow * us;
                }

                e.go.transform.position = e.basePos + Vector3.up * (bottomOffset + height * 0.5f);
                e.go.transform.localScale = new Vector3(width * wMul, height, 1f);

                // Y축 빌보드: 수직 유지, 수평만 카메라
                if (_cam)
                {
                    Vector3 f = e.go.transform.position - _cam.transform.position;
                    f.y = 0f;
                    if (f.sqrMagnitude > 1e-6f)
                        e.go.transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
                }

                e.mr.GetPropertyBlock(e.mpb);
                if (livePreview && preset)
                {
                    e.mpb.SetColor(CoreColorID, coreColor);
                    e.mpb.SetColor(GlowColorID, glowColor);
                    e.mpb.SetFloat(IntensityID, intensity);
                    e.mpb.SetFloat(CoreWidthID, preset.coreWidth);
                    e.mpb.SetFloat(GlowFalloffID, preset.glowFalloff);
                    e.mpb.SetFloat(EdgeSoftID, preset.edgeSoft);
                    e.mpb.SetFloat(SweepSoftID, preset.sweepSoft);
                    e.mpb.SetFloat(TopFadeID, preset.topFade);
                }
                e.mpb.SetFloat(SweepID, sweep);
                e.mpb.SetFloat(FadeMulID, boost);
                e.mr.SetPropertyBlock(e.mpb);
            }
        }

        private void PullFromPreset()
        {
            width = preset.width;
            height = preset.height;
            bottomOffset = preset.bottomOffset;
            duration = preset.duration;
            flashFrac = preset.flashFrac;
            flashBoost = preset.flashBoost;
            narrow = preset.narrow;
            coreColor = preset.coreColor;
            glowColor = preset.glowColor;
            intensity = preset.intensity;
        }

        private Entry GetEntry()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].active) return _pool[i];
            if (_pool.Count >= 2) return _pool[0];   // 포화 시 가장 오래된 것 재사용

            var go = new GameObject("WarpPillar_" + _pool.Count);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _quad;
            var mr = go.AddComponent<MeshRenderer>();
            if (pillarMaterial) mr.sharedMaterial = pillarMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.SetActive(false);
            var e = new Entry { go = go, mr = mr, mpb = new MaterialPropertyBlock(), active = false };
            _pool.Add(e);
            return e;
        }

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "PawPillarQuad" };
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
