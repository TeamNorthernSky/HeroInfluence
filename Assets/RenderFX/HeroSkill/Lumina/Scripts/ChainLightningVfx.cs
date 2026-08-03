using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「체인 라이팅」 스킬 연출 오케스트레이터.
    /// 기획서 스텝(슬라이드 15): 발동 → 대상1에게 빛 줄기 → (연쇄 지연) → 연쇄 대상에게 빛 줄기 → 감전 잔류.
    /// 광선 방식: 볼트는 트랜스폼(시작=피벗, X축=방향, scale.x=길이) + MPB(_Progress 스윕/_Seed 플리커)로 그린다.
    /// 연쇄 대상은 호출자가 주입 — 실전은 그리드를 아는 스킬 시스템, 테스트베드는 트리거의 반경 스캔.
    /// 리스크(멘탈 붕괴)는 chainTargets를 비우면 그대로 재현(연쇄 생략).
    /// </summary>
    public class ChainLightningVfx : VfxEffect
    {
        [Header("References")]
        [Tooltip("머즐 구체(작은 번개 구체, ShockAura 소형 재사용).")]
        [SerializeField] private Renderer muzzleRenderer;
        [Tooltip("본볼트(시전→대상1) 렌더러.")]
        [SerializeField] private Renderer mainBoltRenderer;
        [Tooltip("연쇄 볼트 템플릿(비활성). 연쇄 대상 수만큼 복제.")]
        [SerializeField] private Renderer chainBoltTemplate;
        [Tooltip("감전 잔류 템플릿(비활성, LightningShock). 피격 대상마다 복제.")]
        [SerializeField] private LightningShock shockTemplate;

        [Header("Muzzle")]
        [Tooltip("머즐 구체 위치(캐스터 로컬 오프셋). 캐릭터 전방.")]
        [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 1.15f, 0.5f);
        [Tooltip("머즐 팝(등장) 시간(초).")]
        [SerializeField] private float muzzleTime = 0.12f;
        [Tooltip("머즐 구체 월드 크기(m).")]
        [SerializeField] private float muzzleSize = 0.4f;

        [Header("Bolt Timing")]
        [Tooltip("본볼트가 그어지는 시간(초). 빠르게 긋는 선.")]
        [SerializeField] private float drawTime = 0.08f;
        [Tooltip("본볼트 유지(플리커) 시간(초).")]
        [SerializeField] private float holdTime = 0.25f;
        [Tooltip("볼트·머즐 페이드 시간(초).")]
        [SerializeField] private float fadeTime = 0.15f;
        [Tooltip("플리커 리롤 빈도(회/초). 볼트·아우라 형상이 이 주기로 명멸.")]
        [SerializeField] private float flickerRate = 18f;

        [Header("Chain")]
        [Tooltip("피격 후 연쇄까지 지연(초). 기획 0.1~1.0.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float chainDelay = 0.35f;
        [Tooltip("연쇄 볼트가 그어지는 시간(초).")]
        [SerializeField] private float chainDrawTime = 0.06f;
        [Tooltip("연쇄 볼트 유지 시간(초).")]
        [SerializeField] private float chainHoldTime = 0.2f;

        [Header("Shock (감전 잔류)")]
        [Tooltip("감전 잔류 시간(초). 기획 기본 1초.")]
        [SerializeField] private float shockDuration = 1f;
        [Tooltip("감전 페이드 시간(초, 잔류 시간의 끝부분).")]
        [SerializeField] private float shockFadeTime = 0.25f;
        [Tooltip("감전 아우라 월드 크기(m).")]
        [SerializeField] private float shockSize = 1.7f;
        [Tooltip("피격점 높이(m, 대상 피벗 기준). 볼트 도착점·감전 중심.")]
        [SerializeField] private float targetHeight = 0.8f;

        private Coroutine _co;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private MaterialPropertyBlock _mpb;
        private static readonly int ProgressID = Shader.PropertyToID("_Progress");
        private static readonly int SeedID = Shader.PropertyToID("_Seed");
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");

        public float MuzzleTime { get => muzzleTime; set => muzzleTime = value; }
        public float MuzzleSize { get => muzzleSize; set => muzzleSize = value; }
        public Vector3 MuzzleOffset { get => muzzleOffset; set => muzzleOffset = value; }
        public float DrawTime { get => drawTime; set => drawTime = value; }
        public float HoldTime { get => holdTime; set => holdTime = value; }
        public float FadeTime { get => fadeTime; set => fadeTime = value; }
        public float FlickerRate { get => flickerRate; set => flickerRate = value; }
        public float ChainDelay { get => chainDelay; set => chainDelay = Mathf.Clamp(value, 0.1f, 1f); }
        public float ChainDrawTime { get => chainDrawTime; set => chainDrawTime = value; }
        public float ChainHoldTime { get => chainHoldTime; set => chainHoldTime = value; }
        public float ShockDuration { get => shockDuration; set => shockDuration = value; }
        public float ShockFadeTime { get => shockFadeTime; set => shockFadeTime = value; }
        public float ShockSize { get => shockSize; set => shockSize = value; }
        public float TargetHeight { get => targetHeight; set => targetHeight = value; }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (muzzleRenderer != null) muzzleRenderer.gameObject.SetActive(false);
            if (mainBoltRenderer != null) mainBoltRenderer.gameObject.SetActive(false);
            if (chainBoltTemplate != null) chainBoltTemplate.gameObject.SetActive(false);
            if (shockTemplate != null) shockTemplate.gameObject.SetActive(false);
        }

        public override void Play(Transform origin, Transform target) => Play(origin, target, null);

        /// <summary>연쇄 대상 주입형 재생. chainTargets가 비면 연쇄 생략(리스크 연출).</summary>
        public void Play(Transform origin, Transform target, IReadOnlyList<Transform> chainTargets)
        {
            if (origin == null || target == null) return;
            if (_co != null) StopCoroutine(_co);
            CleanupSpawned();
            _co = StartCoroutine(Run(origin, target, chainTargets));
        }

        public override void Stop()
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
            if (muzzleRenderer != null) muzzleRenderer.gameObject.SetActive(false);
            if (mainBoltRenderer != null) mainBoltRenderer.gameObject.SetActive(false);
            CleanupSpawned();
            IsPlaying = false;
        }

        private void CleanupSpawned()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
        }

        private float Seed => Mathf.Floor(Time.time * Mathf.Max(flickerRate, 0.01f));

        private void PushBolt(Renderer r, float progress, float opacity)
        {
            if (r == null) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(ProgressID, progress);
            _mpb.SetFloat(SeedID, Seed);
            _mpb.SetFloat(OpacityID, opacity);
            r.SetPropertyBlock(_mpb);
        }

        private static void AimBolt(Transform t, Vector3 start, Vector3 end)
        {
            var dir = end - start;
            float len = dir.magnitude;
            t.position = start;
            t.rotation = len > 1e-4f ? Quaternion.FromToRotation(Vector3.right, dir / len) : Quaternion.identity;
            var s = t.localScale;
            t.localScale = new Vector3(len, s.y, s.z);
        }

        private LightningShock SpawnShock(Transform target)
        {
            if (shockTemplate == null) return null;
            var inst = Instantiate(shockTemplate, transform);
            _spawned.Add(inst.gameObject);
            inst.Init(target, Vector3.up * targetHeight, shockSize, shockDuration, shockFadeTime, flickerRate);
            return inst;
        }

        /// <summary>볼트 1개의 긋기→유지→페이드 수명. 렌더러는 호출자가 배치·활성화해 둔 상태.</summary>
        private IEnumerator DriveBolt(Renderer r, float draw, float hold, float fade)
        {
            float t = 0f;
            while (t < draw)
            {
                t += Time.deltaTime;
                PushBolt(r, Mathf.Clamp01(t / Mathf.Max(draw, 0.01f)), 1f);
                yield return null;
            }
            t = 0f;
            while (t < hold)
            {
                t += Time.deltaTime;
                PushBolt(r, 1f, 1f);
                yield return null;
            }
            t = 0f;
            while (t < fade)
            {
                t += Time.deltaTime;
                PushBolt(r, 1f, 1f - Mathf.Clamp01(t / Mathf.Max(fade, 0.01f)));
                yield return null;
            }
            r.gameObject.SetActive(false);
        }

        private IEnumerator Run(Transform origin, Transform target, IReadOnlyList<Transform> chainTargets)
        {
            IsPlaying = true;

            Vector3 muzzlePos = origin.TransformPoint(muzzleOffset);
            Vector3 hitPos = target.position + Vector3.up * targetHeight;

            // 머즐 팝
            if (muzzleRenderer != null)
            {
                var mt = muzzleRenderer.transform;
                mt.position = muzzlePos;
                muzzleRenderer.gameObject.SetActive(true);
                float t = 0f;
                while (t < muzzleTime)
                {
                    t += Time.deltaTime;
                    float g = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / muzzleTime), 3f);
                    mt.localScale = new Vector3(muzzleSize * g, muzzleSize * g, 1f);
                    PushBolt(muzzleRenderer, 1f, 1f);
                    yield return null;
                }
            }

            // 본볼트: 빠르게 긋기 → 유지(플리커)
            if (mainBoltRenderer != null)
            {
                AimBolt(mainBoltRenderer.transform, muzzlePos, hitPos);
                PushBolt(mainBoltRenderer, 0f, 1f);
                mainBoltRenderer.gameObject.SetActive(true);
            }
            float bt = 0f;
            while (bt < drawTime)
            {
                bt += Time.deltaTime;
                PushBolt(mainBoltRenderer, Mathf.Clamp01(bt / Mathf.Max(drawTime, 0.01f)), 1f);
                PushBolt(muzzleRenderer, 1f, 1f);
                yield return null;
            }

            // 명중: 대상1 감전 시작
            SpawnShock(target);
            float hitTime = Time.time;

            // 유지(플리커) 후 본볼트·머즐 페이드
            bt = 0f;
            while (bt < holdTime)
            {
                bt += Time.deltaTime;
                PushBolt(mainBoltRenderer, 1f, 1f);
                PushBolt(muzzleRenderer, 1f, 1f);
                yield return null;
            }
            bt = 0f;
            while (bt < fadeTime)
            {
                bt += Time.deltaTime;
                float f = 1f - Mathf.Clamp01(bt / Mathf.Max(fadeTime, 0.01f));
                PushBolt(mainBoltRenderer, 1f, f);
                PushBolt(muzzleRenderer, 1f, f);
                yield return null;
            }
            if (mainBoltRenderer != null) mainBoltRenderer.gameObject.SetActive(false);
            if (muzzleRenderer != null) muzzleRenderer.gameObject.SetActive(false);

            // 연쇄: 피격 시점 기준 chainDelay 후, 대상1 → 각 연쇄 대상
            float lastShockStart = hitTime;
            if (chainTargets != null && chainTargets.Count > 0)
            {
                float wait = chainDelay - (Time.time - hitTime);
                if (wait > 0f) yield return new WaitForSeconds(wait);

                var boltCos = new List<Coroutine>();
                foreach (var ct in chainTargets)
                {
                    if (ct == null) continue;
                    var r = Instantiate(chainBoltTemplate, transform);
                    _spawned.Add(r.gameObject);
                    AimBolt(r.transform, hitPos, ct.position + Vector3.up * targetHeight);
                    PushBolt(r, 0f, 1f);
                    r.gameObject.SetActive(true);
                    boltCos.Add(StartCoroutine(DriveBolt(r, chainDrawTime, chainHoldTime, fadeTime)));
                }
                yield return new WaitForSeconds(chainDrawTime);
                foreach (var ct in chainTargets)
                    if (ct != null) SpawnShock(ct);
                lastShockStart = Time.time;
                foreach (var c in boltCos) yield return c;
            }

            // 마지막 감전이 끝날 때까지 대기
            float remain = shockDuration - (Time.time - lastShockStart);
            if (remain > 0f) yield return new WaitForSeconds(remain);

            IsPlaying = false;
            _co = null;
            RaiseFinished();
        }
    }
}
