using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「우리 다 같이 힘내자」(LetsFightingLove)의 오케스트레이터.
    /// LetsFightingLove 스킬 VFX 오케스트레이터.
    /// 시퀀스: 양손 앞 치유 오브 2개 차징(ChargeOrb 재사용) → 가슴 앞 합류점으로 수렴·합체(플래시) →
    ///         아군에게 낮은 포물선 비행(ProjectileVfx 재사용, arcHeight 낮춤 프리팹) → 착지 힐 오라(HealOrbit 재사용) →
    ///         본 대상의 상하좌우 1칸 이웃 전원에게 연쇄 투사체 1홉(재연쇄 없음) → 각 착지마다 힐 오라.
    /// ★연쇄 대상을 정하는 방법은 두 가지고, <b>주입이 우선</b>이다.
    ///   ① <see cref="SetTargets"/> — 스킬이 이미 결정한 대상을 그대로 받는다. <b>실전 경로.</b>
    ///      「누가 인접인가」는 격자 인덱스를 아는 스킬이 판단해야 정확하다.
    ///   ② <see cref="SetChainCandidates"/> + 자체 십자 판정 — <b>주입이 없을 때만</b> 도는 폴백.
    ///      테스트베드·프리뷰 씬이 스킬 로직 없이도 굴러가게 하려고 남겨 둔 것이다.
    ///   Play(caster, target) 2점 주입은 두 경로 공통.
    /// 손 위치 = 시전자 계층에서 소켓 이름 검색(Socket_L/R_VFX), 소켓 lossyScale(100) 오프셋 보정.
    /// </summary>
    public class LetsFightingLoveVfx : VfxEffect
    {
        [Header("References (프리팹 에셋 — 런타임 인스턴스화)")]
        [Tooltip("양손 차징 오브(힐 ChargeOrb 재사용)")]
        [SerializeField] private ChargeOrbVfx chargeOrbPrefab;
        [Tooltip("본 발사 투사체(낮은 포물선 베이크 프리팹)")]
        [SerializeField] private ProjectileVfx projectilePrefab;
        [Tooltip("연쇄 투사체(더 낮은 포물선 베이크 프리팹)")]
        [SerializeField] private ProjectileVfx chainProjectilePrefab;
        [Tooltip("착지 힐 오라(힐 HealOrbit 재사용). 대상 수만큼 풀링")]
        [SerializeField] private HealOrbitVfx healAuraPrefab;
        [Tooltip("합체 플래시(자식, PawFlash 재사용)")]
        [SerializeField] private PawFlash flash;

        [Header("런타임 프리뷰")]
        [Tooltip("전체 타이밍/배치/연쇄 마스터 프리셋. 없으면 아래 필드값 사용.")]
        [SerializeField] private LFLMasterPreset masterPreset;
        [SerializeField] private bool livePreview = true;

        [Header("소켓 (시전자 계층에서 이름 검색)")]
        [SerializeField] private string socketLName = "Socket_L_VFX";
        [SerializeField] private string socketRName = "Socket_R_VFX";
        [Tooltip("소켓 로컬 오프셋(월드 단위 m, lossyScale 자동 보정). z+ = 손바닥 앞")]
        [SerializeField] private Vector3 socketLocalOffsetWorld = new Vector3(0f, 0f, 0.04f);

        [Header("차징 / 합체 (livePreview 시 preset 사용)")]
        [Range(0.1f, 3f)] [SerializeField] private float handChargeTime = 0.7f;
        [Range(0.05f, 1f)] [SerializeField] private float convergeTime = 0.25f;
        [SerializeField] private Vector3 mergeLocalOffset = new Vector3(0f, 1.0f, 0.55f);
        [ColorUsage(true, true)] [SerializeField] private Color mergeFlashColor = new Color(1f, 0.95f, 0.6f);
        [Range(0.2f, 3f)] [SerializeField] private float flashSizeMul = 0.8f;
        [Range(0f, 0.5f)] [SerializeField] private float launchDelay = 0.06f;

        [Header("비행 / 연쇄")]
        [SerializeField] private float hitYOffset = 0.68f;
        [Range(0f, 1f)] [SerializeField] private float chainDelay = 0.25f;
        [SerializeField] private float chainSpawnYOffset = 0.9f;
        [Range(0f, 0.5f)] [SerializeField] private float chainStagger = 0.08f;
        [Tooltip("폴백 판정용 1칸 크기(m). 실측 격자 간격 = 3.30 (Grid_c_r 의 x·z 모두 동일).")]
        [Range(0.5f, 6f)] [SerializeField] private float cellSize = 3.3f;
        [Range(0.05f, 1.5f)] [SerializeField] private float axisTol = 0.6f;
        [Range(1, 8)] [SerializeField] private int maxChainTargets = 4;

        private enum Phase { Idle, HandCharge, Converge, Flight, ChainDelay, ChainFlight, AuraWait }
        private Phase _phase = Phase.Idle;
        private float _phaseT;

        private Transform _caster, _target;
        private Transform[] _candidates;
        private readonly List<VfxTarget> _injected = new List<VfxTarget>();
        private bool _hasInjected;
        private readonly List<Transform> _fallbackExclude = new List<Transform>(2);
        private readonly List<Transform> _fallbackPicked = new List<Transform>(4);
        private ChargeOrbVfx _orbL, _orbR;
        private Vector3 _convStartL, _convStartR;
        private ProjectileVfx _mainProj;
        private readonly List<ProjectileVfx> _chainProjs = new List<ProjectileVfx>();
        private readonly List<HealOrbitVfx> _auraPool = new List<HealOrbitVfx>();
        private readonly List<VfxTarget> _chainTargets = new List<VfxTarget>();

        private bool _launched, _mainLanded;
        private int _chainLaunchedCount, _chainPending, _aurasPlaying;

        /// <summary>
        /// ★실전 경로 — 스킬이 <b>이미 결정한</b> 연쇄 대상을 그대로 받는다. Play 전에 호출.
        /// 여기에 무엇이 들어오든 이펙트는 되묻지 않는다. 시전자·본 대상 제외도 부르는 쪽 책임이다.
        /// 빈 목록을 주면 「연쇄 없음」이고, 아예 호출하지 않으면 폴백 판정이 돈다 — <b>둘은 다르다.</b>
        /// </summary>
        public override void SetTargets(IReadOnlyList<VfxTarget> targets)
        {
            _injected.Clear();
            _hasInjected = targets != null;
            if (targets == null) return;
            for (int i = 0; i < targets.Count; i++) _injected.Add(targets[i]);
        }

        /// <summary>
        /// 폴백용 연쇄 후보(아군 목록) 주입. <b><see cref="SetTargets"/> 를 쓴 경우 무시된다.</b>
        /// 스킬 로직 없이 도는 테스트베드·프리뷰 씬 전용이라고 보면 된다.
        /// 시전자/본 대상은 내부 판정에서 제외한다.
        /// </summary>
        public void SetChainCandidates(Transform[] candidates) => _candidates = candidates;

        /// <summary>시전자/대상 주입 재생 — 실 게임 결선용 시그니처.</summary>
        public override void Play(Transform origin, Transform target)
        {
            if (origin == null || target == null)
            {
                Debug.LogWarning("[LetsFightingLoveVfx] caster/target 없이 Play 호출 — 무시");
                return;
            }
            StopInternal();
            _caster = origin;
            _target = target;
            if (livePreview && masterPreset) PullFromMaster();

            _orbL = SpawnChargeOrb(FindSocket(origin, socketLName));
            _orbR = SpawnChargeOrb(FindSocket(origin, socketRName));
            if (_orbL) _orbL.Play();
            if (_orbR) _orbR.Play();

            EnterPhase(Phase.HandCharge);
            IsPlaying = true;
        }

        public override void Stop() => StopInternal();

        /// <summary>진행 중이면 부드럽게 종료 — 투사체는 즉시 숨김, 힐 오라는 페이드아웃 대기.</summary>
        public void StopGraceful()
        {
            if (!IsPlaying) return;
            KillChargeOrbs();
            if (_mainProj) _mainProj.Stop();
            foreach (var p in _chainProjs) if (p) p.Stop();
            foreach (var a in _auraPool) if (a && a.IsPlaying) a.StopGraceful();
            if (_aurasPlaying > 0) EnterPhase(Phase.AuraWait);
            else Finish();
        }

        private void StopInternal()
        {
            KillChargeOrbs();
            if (_mainProj) _mainProj.Stop();
            foreach (var p in _chainProjs) if (p) p.Stop();
            foreach (var a in _auraPool) if (a) a.Stop();
            if (flash) flash.StopAll();
            _aurasPlaying = 0;
            _phase = Phase.Idle;
            IsPlaying = false;
        }

        private void Finish()
        {
            _phase = Phase.Idle;
            IsPlaying = false;
            RaiseFinished();
        }

        private void EnterPhase(Phase p)
        {
            _phase = p;
            _phaseT = 0f;
        }

        private void Update()
        {
            if (!IsPlaying) return;
            if (livePreview && masterPreset) PullFromMaster();
            _phaseT += Time.deltaTime;

            switch (_phase)
            {
                case Phase.HandCharge:
                    if (_phaseT >= handChargeTime)
                    {
                        // 수렴 시작: 소켓에서 분리해 월드 이동
                        BeginConverge(_orbL, out _convStartL);
                        BeginConverge(_orbR, out _convStartR);
                        EnterPhase(Phase.Converge);
                    }
                    break;

                case Phase.Converge:
                {
                    float u = Mathf.Clamp01(_phaseT / Mathf.Max(convergeTime, 1e-4f));
                    float e = u * u;   // easeIn: 빨려들 듯 가속 수렴
                    Vector3 mp = MergePoint();
                    if (_orbL) _orbL.transform.position = Vector3.Lerp(_convStartL, mp, e);
                    if (_orbR) _orbR.transform.position = Vector3.Lerp(_convStartR, mp, e);
                    if (u >= 1f)
                    {
                        KillChargeOrbs();   // 오브 즉시 소멸(트레일/반짝임은 자연 소멸)
                        if (flash) flash.Flash(mp, mergeFlashColor, flashSizeMul);
                        EnsureMainProjectile();
                        _mainProj.Show(mp);
                        _launched = false;
                        _mainLanded = false;
                        EnterPhase(Phase.Flight);
                    }
                    break;
                }

                case Phase.Flight:
                    if (!_launched && _phaseT >= launchDelay)
                    {
                        _launched = true;
                        SubscribeOnce(_mainProj, () => _mainLanded = true);
                        _mainProj.Launch(_target.position + Vector3.up * hitYOffset);
                    }
                    if (_mainLanded)
                    {
                        SpawnAura(_target.position);
                        ResolveChainTargets();
                        if (_chainTargets.Count > 0) EnterPhase(Phase.ChainDelay);
                        else EnterPhase(Phase.AuraWait);
                    }
                    break;

                case Phase.ChainDelay:
                    if (_phaseT >= chainDelay)
                    {
                        _chainLaunchedCount = 0;
                        _chainPending = _chainTargets.Count;
                        EnterPhase(Phase.ChainFlight);
                    }
                    break;

                case Phase.ChainFlight:
                    // 시차 발사
                    while (_chainLaunchedCount < _chainTargets.Count &&
                           _phaseT >= _chainLaunchedCount * chainStagger)
                    {
                        LaunchChain(_chainLaunchedCount);
                        _chainLaunchedCount++;
                    }
                    if (_chainLaunchedCount >= _chainTargets.Count && _chainPending <= 0)
                        EnterPhase(Phase.AuraWait);
                    break;

                case Phase.AuraWait:
                    if (_aurasPlaying <= 0) Finish();
                    break;
            }
        }

        // ── 차징 오브 ──

        private ChargeOrbVfx SpawnChargeOrb(Transform socket)
        {
            if (chargeOrbPrefab == null) return null;
            Transform parent = socket != null ? socket : _caster;
            var orb = Instantiate(chargeOrbPrefab.gameObject, parent).GetComponent<ChargeOrbVfx>();
            orb.transform.localRotation = Quaternion.identity;
            float ps = parent.lossyScale.x;
            if (ps < 1e-6f) ps = 1f;
            orb.transform.localPosition = socketLocalOffsetWorld / ps;   // 월드 오프셋을 소켓 스케일로 보정
            return orb;
        }

        private static void BeginConverge(ChargeOrbVfx orb, out Vector3 startPos)
        {
            startPos = Vector3.zero;
            if (orb == null) return;
            startPos = orb.transform.position;
            orb.transform.SetParent(null, true);   // 소켓 추적 해제 → 월드 수렴 이동
        }

        private void KillChargeOrbs()
        {
            if (_orbL) { _orbL.Stop(); Destroy(_orbL.gameObject, 2f); _orbL = null; }
            if (_orbR) { _orbR.Stop(); Destroy(_orbR.gameObject, 2f); _orbR = null; }
        }

        private static Transform FindSocket(Transform root, string socketName)
        {
            if (string.IsNullOrEmpty(socketName)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == socketName) return t;
            return null;
        }

        private Vector3 MergePoint() => _caster ? _caster.TransformPoint(mergeLocalOffset) : transform.position;

        // ── 투사체 / 힐 오라 ──

        private void EnsureMainProjectile()
        {
            if (_mainProj == null && projectilePrefab != null)
                _mainProj = Instantiate(projectilePrefab);   // 씬 루트(lossyScale 1)
        }

        private void LaunchChain(int index)
        {
            var tgt = _chainTargets[index];
            // 발사 시점의 좌표를 굳혀 둔다 — 비행 도중 대상이 죽어도 착탄점이 사라지지 않는다.
            Vector3 landPos = tgt.Position;

            while (_chainProjs.Count <= index)
                _chainProjs.Add(chainProjectilePrefab != null ? Instantiate(chainProjectilePrefab) : null);
            var proj = _chainProjs[index];
            if (proj == null) { _chainPending--; return; }

            proj.Show(_target.position + Vector3.up * chainSpawnYOffset);
            SubscribeOnce(proj, () =>
            {
                _chainPending--;
                // 착탄 시점에 다시 읽는다 — 대상이 살아 있으면 그 사이 움직인 위치를 따라간다.
                SpawnAura(tgt.HasAnchor ? tgt.Position : landPos);
            });
            proj.Launch(landPos + Vector3.up * hitYOffset);
        }

        private void SpawnAura(Vector3 groundPos)
        {
            HealOrbitVfx aura = null;
            foreach (var a in _auraPool)
                if (a && !a.IsPlaying) { aura = a; break; }
            if (aura == null)
            {
                if (healAuraPrefab == null) return;
                aura = Instantiate(healAuraPrefab);   // 씬 루트(lossyScale 1)
                _auraPool.Add(aura);
            }
            _aurasPlaying++;
            SubscribeOnce(aura, () => _aurasPlaying--);
            aura.Play(groundPos);
        }

        /// <summary>OnFinished 1회성 구독(자기 해제 클로저).</summary>
        private static void SubscribeOnce(VfxEffect fx, System.Action onDone)
        {
            System.Action<VfxEffect> h = null;
            h = _ => { fx.OnFinished -= h; onDone(); };
            fx.OnFinished += h;
        }

        // ── 연쇄 대상 결정 ──

        /// <summary>주입이 있으면 그대로 쓰고, 없을 때만 폴백 판정을 돌린다.</summary>
        private void ResolveChainTargets()
        {
            _chainTargets.Clear();
            if (_hasInjected)
            {
                foreach (var t in _injected)
                    if (t.HasAnchor || t.fallbackPos != Vector3.zero) _chainTargets.Add(t);
                return;
            }
            ResolveChainTargetsFallback();
        }

        /// <summary>
        /// 폴백 — 좌표 거리로 십자 인접을 추정한다. <b>정확한 판정이 아니다.</b>
        /// 캐릭터가 칸 중앙에서 벗어나면 오판하므로 실전에서는 <see cref="SetTargets"/> 를 써야 한다.
        /// (실제로 cellSize 가 실측 격자와 어긋나 판정이 전부 탈락한 적이 있다 — 260804)
        /// </summary>
        private void ResolveChainTargetsFallback()
        {
            if (_candidates == null) return;

            _fallbackExclude.Clear();
            _fallbackExclude.Add(_caster);
            _fallbackExclude.Add(_target);

            // 규칙은 VfxGridAdjacency 한 곳에만 둔다 — 여기와 스테퍼가 같은 답을 내야 한다.
            VfxGridAdjacency.ResolveCross(
                _target.position, _candidates, _fallbackPicked,
                _fallbackExclude, cellSize, axisTol, maxChainTargets);

            foreach (var t in _fallbackPicked) _chainTargets.Add(VfxTarget.Of(t));
        }

        private void PullFromMaster()
        {
            handChargeTime = masterPreset.handChargeTime;
            convergeTime = masterPreset.convergeTime;
            mergeLocalOffset = masterPreset.mergeLocalOffset;
            mergeFlashColor = masterPreset.mergeFlashColor;
            flashSizeMul = masterPreset.flashSizeMul;
            launchDelay = masterPreset.launchDelay;
            hitYOffset = masterPreset.hitYOffset;
            chainDelay = masterPreset.chainDelay;
            chainSpawnYOffset = masterPreset.chainSpawnYOffset;
            chainStagger = masterPreset.chainStagger;
            cellSize = masterPreset.cellSize;
            axisTol = masterPreset.axisTol;
            maxChainTargets = masterPreset.maxChainTargets;
        }
    }
}
