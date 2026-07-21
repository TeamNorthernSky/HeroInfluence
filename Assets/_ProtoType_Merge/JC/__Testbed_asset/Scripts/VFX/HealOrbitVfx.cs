using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 힐 오라 E-1: 대상(착지점) 주위를 궤도 운동하는 구체.
    /// 발사체 오브와 동일한 룩(HealOrbitCoreInner/RimShell 2오브젝트, 전용 재질)을 재사용.
    /// ★닫힌 해 파라메트릭: angle += ω·dt, 되먹임 없음 → Update+deltaTime. 충돌/착지 없음.
    /// 좌표(궤도 중심)는 호출자가 주입(Play(center)), 거동(반경/높이/각속도)은 이 오브젝트가 소유.
    /// 실 게임에선 투사체 ProjectileVfx.OnFinished(착지) → Play(착지점) 으로 연결.
    ///
    /// ★알파 페이드: 생성 시 _FadeMul 0→1(fadeInTime), 종료 시 1→0(fadeOutTime). 가산 재질 페이드.
    ///   MPB로 항상 구동하므로 livePreview 여부·실 게임 무관하게 작동.
    /// ★런타임 프리뷰: preset 지정 + livePreview 켜면, 플레이 중 프리셋 값을 매 프레임 반영(재질은 비파괴 MPB).
    ///   튜닝이 끝나면 프리셋 인스펙터의 "▶ 프리팹에 적용"으로 베이크.
    /// </summary>
    public class HealOrbitVfx : VfxEffect
    {
        [Header("References")]
        [Tooltip("궤도를 도는 구체 비주얼(Orb 컨테이너). 크기 = orbWorldSize.")]
        [SerializeField] private Transform orb;
        [Tooltip("코어 렌더러(CoreInner). 페이드/프리뷰 MPB 적용 대상.")]
        [SerializeField] private Renderer coreRenderer;
        [Tooltip("외곽선 렌더러(RimShell). 페이드/프리뷰 MPB 적용 대상.")]
        [SerializeField] private Renderer rimRenderer;
        [Tooltip("함께 재생/정지할 파티클들. 비어도 됨.")]
        [SerializeField] private ParticleSystem[] particleSystems;
        [Tooltip("E-2 원호 스윕 링들(구체와 독립, 겹치기 가능). 중심은 궤도 중심과 동일.")]
        [SerializeField] private HealArcRing[] arcRings;

        [Header("런타임 프리뷰")]
        [Tooltip("이펙트 전체 타이밍(총 재생시간+전후 페이드) 마스터 프리셋. 없으면 아래 필드값 사용.")]
        [SerializeField] private HealAuraMasterPreset masterPreset;
        [Tooltip("지정하면 livePreview에서 이 프리셋 값(궤도 오브 룩)을 매 프레임 반영.")]
        [SerializeField] private HealOrbitPreset preset;
        [Tooltip("플레이 중 preset/master 값을 매 프레임 반영(비파괴, MPB). 끄면 아래 프리팹 값 사용.")]
        [SerializeField] private bool livePreview = true;

        [Header("Orbit (파라메트릭)")]
        [Tooltip("궤도 반경(m)")]
        [SerializeField] private float orbitRadius = 1.0f;
        [Tooltip("궤도면 높이(중심 기준, m). 이미지상 무릎~허리")]
        [SerializeField] private float orbitHeight = 0.35f;
        [Tooltip("각속도(도/초). +면 시계 반대방향")]
        [SerializeField] private float angularSpeed = 220f;
        [Tooltip("시작 각도(도)")]
        [SerializeField] private float startAngle = 0f;
        [Tooltip("궤도면 기울기(도, X축 회전). 0=완전 수평 링")]
        [SerializeField] private float tiltDeg = 0f;

        [Header("Orb 크기")]
        [Tooltip("궤도 구체 월드 지름(m). 발사체와 동일하게 0.11 권장")]
        [SerializeField] private float orbWorldSize = 0.11f;
        [Tooltip("CoreInner 상대 크기(갭). 라이브 프리뷰 시 preset.coreSize 사용")]
        [SerializeField] private float coreSize = 0.72f;
        [Tooltip("RimShell 상대 크기. 라이브 프리뷰 시 preset.rimSize 사용")]
        [SerializeField] private float rimSize = 1.0f;

        [Header("수명 / 알파 페이드")]
        [Tooltip("재생 지속(초). 0=Stop 호출 전까지 무한")]
        [SerializeField] private float duration = 3.5f;
        [Tooltip("생성 시 페이드인 시간(초). 0~1. 0=즉시 등장")]
        [Range(0f, 1f)] [SerializeField] private float fadeInTime = 0.2f;
        [Tooltip("종료 시 페이드아웃 시간(초). 0~1. 0=즉시 소멸")]
        [Range(0f, 1f)] [SerializeField] private float fadeOutTime = 0.2f;

        private enum Phase { Idle, In, Sustain, Out }
        private Phase _phase = Phase.Idle;
        private Vector3 _center;
        private float _angle, _elapsed, _phaseT, _fade;
        private MaterialPropertyBlock _mpb;

        private static readonly int CoreColorID = Shader.PropertyToID("_CoreColor");
        private static readonly int CoreIntensityID = Shader.PropertyToID("_CoreIntensity");
        private static readonly int CorePowerID = Shader.PropertyToID("_CorePower");
        private static readonly int RimColorID = Shader.PropertyToID("_RimColor");
        private static readonly int RimIntensityID = Shader.PropertyToID("_RimIntensity");
        private static readonly int RimPowerID = Shader.PropertyToID("_RimPower");
        private static readonly int CoreSpikeID = Shader.PropertyToID("_CoreSpikeAmount");
        private static readonly int RimSpikeID = Shader.PropertyToID("_RimSpikeAmount");
        private static readonly int SpikeFreqID = Shader.PropertyToID("_SpikeFreq");
        private static readonly int SpikeSpeedID = Shader.PropertyToID("_SpikeSpeed");
        private static readonly int SpikeSharpID = Shader.PropertyToID("_SpikeSharp");
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            HideImmediate();
        }

        /// <summary>궤도 중심(착지점)을 주입하고 재생 시작.</summary>
        public void Play(Vector3 center)
        {
            _center = center;
            _angle = startAngle;
            _elapsed = 0f;
            _phaseT = 0f;
            if (livePreview && preset) PullFromPreset();
            if (livePreview && masterPreset) PullFromMaster();
            _phase = Phase.In;
            _fade = fadeInTime > 0f ? 0f : 1f;
            if (orb)
            {
                orb.gameObject.SetActive(true);
                orb.localScale = Vector3.one * orbWorldSize;
                orb.position = OrbitPos(_angle);
            }
            ApplyVisual();
            if (particleSystems != null)
                foreach (var ps in particleSystems) if (ps) { ps.Clear(); ps.Play(); }
            if (arcRings != null) foreach (var ar in arcRings) if (ar) ar.Play(_center);
            IsPlaying = true;
        }

        /// <summary>파라미터 없는 재생 = 현재 위치를 중심으로.</summary>
        public override void Play() => Play(transform.position);

        /// <summary>즉시 정지(페이드 없음).</summary>
        public override void Stop() => HideImmediate();

        /// <summary>페이드아웃을 거쳐 정지. (토글 종료 등 부드러운 종료용)</summary>
        public void StopGraceful()
        {
            if (!IsPlaying) return;
            if (_phase == Phase.Out) return;
            _phase = Phase.Out;
            _phaseT = 0f;
            if (arcRings != null) foreach (var ar in arcRings) if (ar) ar.StopSpawning();
        }

        private void HideImmediate()
        {
            if (particleSystems != null)
                foreach (var ps in particleSystems) if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (arcRings != null) foreach (var ar in arcRings) if (ar) ar.StopAll();
            if (orb) orb.gameObject.SetActive(false);
            _phase = Phase.Idle;
            _fade = 0f;
            IsPlaying = false;
        }

        private Vector3 OrbitPos(float angleDeg)
        {
            float r = Mathf.Deg2Rad * angleDeg;
            Vector3 offset = new Vector3(Mathf.Cos(r) * orbitRadius, orbitHeight, Mathf.Sin(r) * orbitRadius);
            if (Mathf.Abs(tiltDeg) > 0.01f)
                offset = Quaternion.AngleAxis(tiltDeg, Vector3.right) * offset;
            return _center + offset;
        }

        private void Update()
        {
            if (!IsPlaying) return;

            if (livePreview && preset) PullFromPreset();
            if (livePreview && masterPreset) PullFromMaster();

            float dt = Time.deltaTime;
            _elapsed += dt;
            _phaseT += dt;

            switch (_phase)
            {
                case Phase.In:
                    _fade = fadeInTime > 0f ? Mathf.Clamp01(_phaseT / fadeInTime) : 1f;
                    if (_phaseT >= fadeInTime) { _phase = Phase.Sustain; _fade = 1f; }
                    break;
                case Phase.Sustain:
                    _fade = 1f;
                    if (duration > 0f && _elapsed >= duration - fadeOutTime)
                    {
                        _phase = Phase.Out;
                        _phaseT = 0f;
                        if (arcRings != null) foreach (var ar in arcRings) if (ar) ar.StopSpawning();   // 페이드아웃 시작 → 새 원호 중단, 기존은 자연 소멸
                    }
                    break;
                case Phase.Out:
                    _fade = fadeOutTime > 0f ? Mathf.Clamp01(1f - _phaseT / fadeOutTime) : 0f;
                    if (_phaseT >= fadeOutTime)
                    {
                        HideImmediate();
                        RaiseFinished();
                        return;
                    }
                    break;
            }

            _angle += angularSpeed * dt;
            if (orb) orb.position = OrbitPos(_angle);
            ApplyVisual();
        }

        /// <summary>프리셋 값을 런타임 필드로 복사(거동+크기+페이드).</summary>
        private void PullFromPreset()
        {
            orbitRadius = preset.orbitRadius;
            orbitHeight = preset.orbitHeight;
            angularSpeed = preset.angularSpeed;
            startAngle = preset.startAngle;
            tiltDeg = preset.tiltDeg;
            orbWorldSize = preset.orbWorldSize;
            coreSize = preset.coreSize;
            rimSize = preset.rimSize;
        }

        /// <summary>마스터 프리셋 값을 런타임 필드로 복사(이펙트 전체 타이밍).</summary>
        private void PullFromMaster()
        {
            duration = masterPreset.duration;
            fadeInTime = masterPreset.fadeInTime;
            fadeOutTime = masterPreset.fadeOutTime;
        }

        /// <summary>크기 + 재질 반영. _FadeMul은 항상 _fade로 구동(페이드), 프리뷰 켜지면 색/밝기도 preset로.</summary>
        private void ApplyVisual()
        {
            if (orb) orb.localScale = Vector3.one * orbWorldSize;
            if (coreRenderer) coreRenderer.transform.localScale = Vector3.one * coreSize;
            if (rimRenderer) rimRenderer.transform.localScale = Vector3.one * rimSize;

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            bool live = livePreview && preset;

            if (coreRenderer)
            {
                coreRenderer.GetPropertyBlock(_mpb);
                if (live)
                {
                    _mpb.SetColor(CoreColorID, preset.coreColor);
                    _mpb.SetFloat(CoreIntensityID, preset.coreIntensity);
                    _mpb.SetFloat(CorePowerID, preset.corePower);
                    _mpb.SetFloat(CoreSpikeID, preset.coreSpikeAmount);
                    _mpb.SetFloat(RimSpikeID, preset.rimSpikeAmount);
                    _mpb.SetFloat(SpikeFreqID, preset.spikeFreq);
                    _mpb.SetFloat(SpikeSpeedID, preset.spikeSpeed);
                    _mpb.SetFloat(SpikeSharpID, preset.spikeSharp);
                    _mpb.SetFloat(RimIntensityID, 0f);
                }
                _mpb.SetFloat(FadeMulID, _fade);
                coreRenderer.SetPropertyBlock(_mpb);
            }
            if (rimRenderer)
            {
                rimRenderer.GetPropertyBlock(_mpb);
                if (live)
                {
                    _mpb.SetColor(RimColorID, preset.rimColor);
                    _mpb.SetFloat(RimIntensityID, preset.rimIntensity);
                    _mpb.SetFloat(RimPowerID, preset.rimPower);
                    _mpb.SetFloat(CoreIntensityID, 0f);
                    _mpb.SetFloat(CoreSpikeID, 0f);
                    _mpb.SetFloat(RimSpikeID, 0f);
                }
                _mpb.SetFloat(FadeMulID, _fade);
                rimRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
