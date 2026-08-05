using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// PawForYou 요소: 원샷 플래시 풀(셰이더 JC/VFX/PawFlash).
    /// 워프 소멸/재등장·광선 착탄 순간에 Flash(위치, 색) 호출 → 짧게 확 밝았다가 easeOut으로 사라짐.
    /// 색은 호출부가 주입(발/광선 변형 색 매칭). 겹침 대비 소형 풀(3).
    /// </summary>
    public class PawFlash : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("플래시 재질(JC/VFX/PawFlash, 가산)")]
        [SerializeField] private Material flashMaterial;

        [Header("플래시")]
        [Tooltip("플래시 월드 크기(m)")]
        [SerializeField] private float size = 0.9f;
        [Tooltip("지속(초)")]
        [Range(0.05f, 1f)] [SerializeField] private float duration = 0.25f;
        [Tooltip("시작 스케일 비율(작게 시작해 팝)")]
        [Range(0.1f, 1f)] [SerializeField] private float growFrom = 0.5f;

        /// <summary>플래시 수명(초) — 오케스트레이터가 정리 대기 시간으로 읽어간다.</summary>
        public float Duration => duration;

        private Mesh _quad;
        private Camera _cam;
        private readonly List<Entry> _pool = new List<Entry>();

        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        private class Entry
        {
            public GameObject go;
            public MeshRenderer mr;
            public MaterialPropertyBlock mpb;
            public bool active;
            public float t;
            public float sizeMul;
        }

        private void Awake()
        {
            _quad = BuildQuad();
        }

        /// <summary>원샷 플래시 재생. 색은 변형 매칭용으로 호출부가 주입.</summary>
        public void Flash(Vector3 pos, Color color, float sizeMul = 1f)
        {
            var e = GetEntry();
            if (e == null) return;
            e.active = true;
            e.t = 0f;
            e.sizeMul = sizeMul;
            e.go.transform.position = pos;
            e.go.transform.localScale = Vector3.one * (size * sizeMul * growFrom);
            e.mr.GetPropertyBlock(e.mpb);
            e.mpb.SetColor(ColorID, color);
            e.mpb.SetFloat(FadeMulID, 1f);
            e.mr.SetPropertyBlock(e.mpb);
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
            if (_cam == null) _cam = Camera.main;
            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.active) continue;
                e.t += Time.deltaTime;
                float u = Mathf.Clamp01(e.t / Mathf.Max(duration, 1e-4f));
                if (u >= 1f) { e.active = false; e.go.SetActive(false); continue; }

                float fade = (1f - u) * (1f - u);                   // easeOut 감쇠
                float grow = Mathf.Lerp(growFrom, 1f, 1f - fade);   // 팝 확장
                e.go.transform.localScale = Vector3.one * (size * e.sizeMul * grow);
                if (_cam) e.go.transform.rotation = _cam.transform.rotation;

                e.mr.GetPropertyBlock(e.mpb);
                e.mpb.SetFloat(FadeMulID, fade);
                e.mr.SetPropertyBlock(e.mpb);
            }
        }

        private Entry GetEntry()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].active) return _pool[i];
            if (_pool.Count >= 3) return _pool[0];   // 포화 시 가장 오래된 것 재사용

            var go = new GameObject("Flash_" + _pool.Count);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _quad;
            var mr = go.AddComponent<MeshRenderer>();
            if (flashMaterial) mr.sharedMaterial = flashMaterial;
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
            var m = new Mesh { name = "PawFlashQuad" };
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
