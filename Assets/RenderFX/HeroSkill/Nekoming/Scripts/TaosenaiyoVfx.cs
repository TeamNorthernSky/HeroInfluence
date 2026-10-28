using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「쓰러지면 안돼」(Taosenaiyo, 부활)의 오케스트레이터.
    /// Taosenaiyo(부활) 스킬 VFX 오케스트레이터.
    /// 시퀀스: 시전자 손 플래시 → 유성 투사체(ProjectileVfx 복제, 트레일 강화·arcHeight 곡선) →
    ///         착지 플래시 + 복합 오라 개시(마법진+발산 오라+깃털+소형 십자+스파클) →
    ///         페이드인 → 유지 → 페이드아웃 → 종료.
    /// ★Play(caster, target) 2점 주입. 사망 판정 없음 — 순수 연출(대상=Fighter 표준).
    /// 마법진/발산 오라 = SetEnvelope 주입식, 깃털/십자 = 스포너(페이드아웃 시 StopSpawning), 스파클 = ParticleSystem.
    /// </summary>
    public class TaosenaiyoVfx : VfxEffect
    {
        [Header("References")]
        [Tooltip("유성 투사체 프리팹(런타임 인스턴스화)")]
        [SerializeField] private ProjectileVfx projectilePrefab;
        [Tooltip("손/착지 플래시(자식, PawFlash 재사용)")]
        [SerializeField] private PawFlash flash;
        [Tooltip("마법진 장판(자식)")]
        [SerializeField] private TaoMagicCircle magicCircle;
        [Tooltip("발산 오라(자식)")]
        [SerializeField] private TaoAuraFlare auraFlare;
        [Tooltip("기초 방사 스프레이 스커트(자식, 전용 컴포넌트)")]
        [SerializeField] private TaoBaseSpray baseSpray;
        [Tooltip("깃털 버스트(자식)")]
        [SerializeField] private TaoFeatherBurst featherBurst;
        [Tooltip("소형 스타버스트 십자(자식, HealCrossBurst 재사용+T4 프리셋)")]
        [SerializeField] private HealCrossBurst crossBurst;
        [Tooltip("빛 입자 스파클(자식 ParticleSystem)")]
        [SerializeField] private ParticleSystem sparkles;

        [Header("런타임 프리뷰")]
        [Tooltip("전체 타이밍/페이드 마스터 프리셋. 없으면 아래 필드값 사용.")]
        [SerializeField] private TaoMasterPreset masterPreset;
        [SerializeField] private bool livePreview = true;

        [Header("발사 (livePreview 시 preset 사용)")]
        [Tooltip("시전자 손 소켓 이름(계층 검색). 없으면 시전자 가슴 높이 폴백")]
        [SerializeField] private string socketName = "Socket_R_VFX";
        [Range(0f, 0.5f)] [SerializeField] private float launchDelay = 0.12f;
        [SerializeField] private float hitYOffset = 0.68f;
        [ColorUsage(true, true)] [SerializeField] private Color handFlashColor = new Color(1f, 0.92f, 0.55f);
        [ColorUsage(true, true)] [SerializeField] private Color impactFlashColor = new Color(1f, 0.9f, 0.45f);
        [Range(0.2f, 3f)] [SerializeField] private float flashSizeMul = 0.9f;

        [Header("오라 타이밍")]
        [Range(0.05f, 1.5f)] [SerializeField] private float fadeInTime = 0.35f;
        [Range(0.2f, 8f)] [SerializeField] private float sustainTime = 2.6f;
        [Range(0.05f, 2f)] [SerializeField] private float fadeOutTime = 0.5f;

        private enum Phase { Idle, Launch, FlightWait, In, Sustain, Out }
        private Phase _phase = Phase.Idle;
        private float _phaseT;
        private float _fade;

        private Transform _caster, _target;
        private ProjectileVfx _proj;
        private bool _launched, _landed;

        public override void Play(Transform origin, Transform target)
        {
            if (origin == null || target == null)
            {
                Debug.LogWarning("[TaosenaiyoVfx] caster/target 없이 Play 호출 — 무시");
                return;
            }
            StopInternal();
            _caster = origin;
            _target = target;
            if (livePreview && masterPreset) PullFromMaster();

            Vector3 hand = HandPos();
            if (flash) flash.Flash(hand, handFlashColor, flashSizeMul);
            EnsureProjectile();
            _proj.Show(hand);
            _launched = false;
            _landed = false;
            EnterPhase(Phase.Launch);
            IsPlaying = true;
        }

        public override void Stop() => StopInternal();

        /// <summary>어느 페이즈에서든 페이드아웃으로 점프해 부드럽게 종료.</summary>
        public void StopGraceful()
        {
            if (!IsPlaying || _phase == Phase.Out) return;
            if (_phase == Phase.Launch || _phase == Phase.FlightWait)
            {
                StopInternal();
                RaiseFinished();
                return;
            }
            BeginOut();
        }

        private void StopInternal()
        {
            if (_proj) _proj.Stop();
            if (flash) flash.StopAll();
            if (magicCircle) magicCircle.Stop();
            if (auraFlare) auraFlare.Stop();
            if (baseSpray) baseSpray.Stop();
            if (featherBurst) featherBurst.StopAll();
            if (crossBurst) crossBurst.StopAll();
            if (sparkles) sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _fade = 0f;
            _phase = Phase.Idle;
            IsPlaying = false;
        }

        private void EnterPhase(Phase p)
        {
            _phase = p;
            _phaseT = 0f;
        }

        private void BeginOut()
        {
            if (featherBurst) featherBurst.StopSpawning();
            if (crossBurst) crossBurst.StopSpawning();
            if (sparkles) sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            EnterPhase(Phase.Out);
        }

        private Vector3 HandPos()
        {
            if (_caster != null && !string.IsNullOrEmpty(socketName))
                foreach (var t in _caster.GetComponentsInChildren<Transform>(true))
                    if (t.name == socketName) return t.position;
            return _caster != null ? _caster.position + Vector3.up * 1.0f + _caster.forward * 0.4f : transform.position;
        }

        private Vector3 TargetGround() => _target ? _target.position : transform.position;

        private void Update()
        {
            if (!IsPlaying) return;
            if (livePreview && masterPreset) PullFromMaster();
            _phaseT += Time.deltaTime;

            switch (_phase)
            {
                case Phase.Launch:
                    if (!_launched && _phaseT >= launchDelay)
                    {
                        _launched = true;
                        System.Action<VfxEffect> h = null;
                        h = _ => { _proj.OnFinished -= h; _landed = true; };
                        _proj.OnFinished += h;
                        _proj.Launch(_target.position + Vector3.up * hitYOffset);
                        EnterPhase(Phase.FlightWait);
                    }
                    break;

                case Phase.FlightWait:
                    if (_landed)
                    {
                        Vector3 g = TargetGround();
                        if (flash) flash.Flash(g + Vector3.up * hitYOffset, impactFlashColor, flashSizeMul);
                        if (magicCircle) magicCircle.Play(g);
                        if (auraFlare) auraFlare.Play(g);
                        if (baseSpray) baseSpray.Play(g);
                        if (featherBurst) featherBurst.Play(g);
                        if (crossBurst) crossBurst.Play(g);
                        if (sparkles)
                        {
                            sparkles.transform.position = g + Vector3.up * 0.3f;
                            sparkles.Clear();
                            sparkles.Play();
                        }
                        _fade = 0f;
                        EnterPhase(Phase.In);
                    }
                    break;

                case Phase.In:
                    _fade = Mathf.Clamp01(_phaseT / Mathf.Max(fadeInTime, 1e-4f));
                    if (_phaseT >= fadeInTime) { _fade = 1f; EnterPhase(Phase.Sustain); }
                    break;

                case Phase.Sustain:
                    _fade = 1f;
                    if (_phaseT >= sustainTime) BeginOut();
                    break;

                case Phase.Out:
                    _fade = 1f - Mathf.Clamp01(_phaseT / Mathf.Max(fadeOutTime, 1e-4f));
                    if (_phaseT >= fadeOutTime)
                    {
                        StopInternal();
                        RaiseFinished();
                        return;
                    }
                    break;
            }

            if (magicCircle) magicCircle.SetEnvelope(_fade);
            if (auraFlare) auraFlare.SetEnvelope(_fade);
            if (baseSpray) baseSpray.SetEnvelope(_fade);
        }

        private void EnsureProjectile()
        {
            if (_proj == null && projectilePrefab != null)
                _proj = Instantiate(projectilePrefab);   // 씬 루트(lossyScale 1)
        }

        private void PullFromMaster()
        {
            launchDelay = masterPreset.launchDelay;
            hitYOffset = masterPreset.hitYOffset;
            handFlashColor = masterPreset.handFlashColor;
            impactFlashColor = masterPreset.impactFlashColor;
            flashSizeMul = masterPreset.flashSizeMul;
            fadeInTime = masterPreset.fadeInTime;
            sustainTime = masterPreset.sustainTime;
            fadeOutTime = masterPreset.fadeOutTime;
        }
    }
}
