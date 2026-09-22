using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「아픈 거 다 날아가라」(HealSkill)의 차징 파트. LetsFightingLove(양손 차징)도 재사용.
    /// 차징 구체 VFX. Play() 시 소켓 위치에서 startWorldSize→endWorldSize로 growDuration에 걸쳐 커지며,
    /// 씬-공간 트레일 팔로워가 손 궤적을 따라 파티클 트레일/반짝임을 남긴다.
    /// Stop() 시 구체는 즉시 사라지고 트레일/반짝임은 수명대로 자연 소멸.
    /// ★크기는 "월드 지름"을 목표로 부모 lossyScale를 보정(믹사모 본 lossyScale 100 등에도 견고).
    /// 트리거는 외부(하네스/스킬 로직)가 담당 — 이 컴포넌트는 룩과 거동만 책임진다.
    /// </summary>
    public class ChargeOrbVfx : VfxEffect
    {
        [Header("References")]
        [Tooltip("코어 렌더러(CoreInner). 페이드/프리뷰 MPB 적용 대상.")]
        [SerializeField] private Renderer coreRenderer;
        [Tooltip("차징 구체 주변의 스파크 파티클 시스템입니다. 입자 조절값을 적용할 대상을 연결합니다.")]
        [SerializeField] private ParticleSystem sparks;
        [Tooltip("씬-공간 파티클 트레일 팔로워 프리팹(선택). Play 시 씬 루트에 1개 생성해 재사용.")]
        [SerializeField] private ChargeTrailFollower trailFollowerPrefab;

        [Header("Scale-In (월드 지름 기준)")]
        [Tooltip("차징 구체가 나타날 때의 월드 지름(m)입니다. 종료 크기까지 성장합니다.")]
        [SerializeField] private float startWorldSize = 0.015f;
        [Tooltip("최종 월드 지름(m).")]
        [SerializeField] private float endWorldSize = 0.11f;
        [Tooltip("시작 크기에서 종료 크기까지 커지는 시간(초)입니다. 짧을수록 빠르게 커집니다.")]
        [SerializeField] private float growDuration = 0.5f;

        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 재생(Play/ShowCharged)마다 프리셋 전 항목을 읽고, 색은 매 프레임 반영(비파괴 MPB).\n" +
                 "개발 루프: 시전 → 프리셋 조절 → 재시전 → … → 플레이 종료 후 「프리팹에 적용」으로 저장.\n" +
                 "★변종은 제 프리셋을 물 것. LFL 소형 배리언트는 크기 고정이 필요하므로 비워 둔다.")]
        [SerializeField] private ChargeOrbPreset preset;
        [Tooltip("켜면 변경한 설정을 실행 중인 이 효과에 갱신합니다. 파일 저장과는 별개이며 이미 시작된 시간표는 재시전하여 확인합니다.")]
        [SerializeField] private bool livePreview = true;

        private float _t;
        private bool _growing;
        private ChargeTrailFollower _follower;
        private MaterialPropertyBlock _mpb;

        private bool Live => livePreview && preset != null;

        /// <summary>호출자(스테퍼/큐 드라이버)가 발사 시작점 등을 읽어 가는 창구.</summary>
        public ChargeOrbPreset Preset => preset;

        private void Awake() => ApplyHidden();

        /// <summary>
        /// ★팔로워는 씬 루트에 있어 자식이 아니다 — 이 오브가 파괴될 때 같이 지워 주지 않으면
        /// 공중에 반짝임이 켜진 채 고아로 남는다(호출자가 Stop 없이 Destroy 하는 경우 포함).
        /// </summary>
        private void OnDestroy()
        {
            if (_follower) Destroy(_follower.gameObject);
        }

        public override void Play()
        {
            if (Live) PullFromPreset();   // ★재생마다 최신 프리셋 값으로 — 「조절 → 재시전」 개발 루프
            IsPlaying = true;
            _t = 0f;
            _growing = true;
            SetWorldSize(startWorldSize);
            if (coreRenderer) coreRenderer.enabled = true;
            if (sparks) { sparks.Clear(); sparks.Play(); }

            if (trailFollowerPrefab != null)
            {
                if (_follower == null)
                    _follower = Instantiate(trailFollowerPrefab);   // 씬 루트(부모 없음) = lossyScale 1
                if (Live) _follower.ApplyPresetConfig(preset);      // 생성 직후라 PullFromPreset 시점엔 없었다
                _follower.Begin(transform.position);
            }
        }

        /// <summary>
        /// ★이미 차오른 상태로 즉시 표시 — 성장 생략. 합체(LflMergeVfx)처럼
        /// 「차징이 끝난 오브」를 넘겨받은 것처럼 보여야 하는 연출이 쓴다.
        /// </summary>
        public void ShowCharged()
        {
            if (Live) PullFromPreset();
            IsPlaying = true;
            _growing = false;
            SetWorldSize(endWorldSize);
            if (coreRenderer) coreRenderer.enabled = true;
            if (sparks) { sparks.Clear(); sparks.Play(); }

            if (trailFollowerPrefab != null)
            {
                if (_follower == null)
                    _follower = Instantiate(trailFollowerPrefab);   // 씬 루트(부모 없음) = lossyScale 1
                if (Live) _follower.ApplyPresetConfig(preset);      // 생성 직후라 PullFromPreset 시점엔 없었다
                _follower.Begin(transform.position);
            }
        }

        public override void Stop()
        {
            IsPlaying = false;
            _growing = false;
            if (coreRenderer) coreRenderer.enabled = false;                                   // 구체 즉시 사라짐
            if (sparks) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_follower) _follower.EndEmit();                                                // 트레일/반짝임 자연 소멸
            RaiseFinished();
        }

        private void ApplyHidden()
        {
            IsPlaying = false;
            _growing = false;
            if (coreRenderer) coreRenderer.enabled = false;
            if (sparks) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetWorldSize(startWorldSize);
        }

        private void Update()
        {
            if (_growing)
            {
                _t += Time.deltaTime;
                float k = growDuration > 0f ? Mathf.Clamp01(_t / growDuration) : 1f;
                SetWorldSize(Mathf.Lerp(startWorldSize, endWorldSize, k));
                if (k >= 1f) _growing = false;
            }
            if (IsPlaying && _follower) _follower.Follow(transform.position);

            // 색·밝기는 진행 중에도 즉시 반영 — 저스티스 프리뷰와 같은 감각
            if (Live && IsPlaying) ApplyLookMpb();
        }

        // ── 런타임 프리뷰 — 에디터 「프리팹에 적용」과 같은 매핑, 대상만 런타임 인스턴스 ──

        private void PullFromPreset()
        {
            var p = preset;
            var t = p.TransformSource;   // ★트랜스폼은 따름 규칙 적용(Alter → Basic)
            startWorldSize = t.startWorldSize;
            endWorldSize = t.endWorldSize;
            growDuration = t.growDuration;

            if (sparks)
            {
                var m = sparks.main;
                m.startSize = new ParticleSystem.MinMaxCurve(p.sparkSizeMin, p.sparkSizeMax);
                m.startSpeed = p.sparkSpeed;
                var e = sparks.emission; e.rateOverTime = p.sparkRate;
            }
            if (_follower) _follower.ApplyPresetConfig(p);   // 없으면 Play 의 Begin 직후 다시 시도된다
        }

        private void ApplyLookMpb()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            var p = preset;
            if (coreRenderer)
            {
                coreRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor("_CoreColor", p.coreColor);
                _mpb.SetColor("_RimColor", p.rimColor);
                _mpb.SetFloat("_CoreIntensity", p.coreIntensity);
                _mpb.SetFloat("_RimIntensity", p.rimIntensity);
                _mpb.SetFloat("_CorePower", p.corePower);
                _mpb.SetFloat("_RimPower", p.rimPower);
                _mpb.SetFloat("_GapStrength", p.gapStrength);
                _mpb.SetFloat("_CoreSpikeAmount", p.coreSpikeAmount);
                _mpb.SetFloat("_RimSpikeAmount", p.rimSpikeAmount);
                _mpb.SetFloat("_SpikeFreq", p.spikeFreq);
                _mpb.SetFloat("_SpikeSpeed", p.spikeSpeed);
                _mpb.SetFloat("_SpikeSharp", p.spikeSharp);
                coreRenderer.SetPropertyBlock(_mpb);
            }
            if (sparks)
            {
                var r = sparks.GetComponent<ParticleSystemRenderer>();
                if (r)
                {
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_BaseColor", p.sparkColor);
                    r.SetPropertyBlock(_mpb);
                }
            }
            if (_follower) _follower.ApplyPresetColors(p, _mpb);
        }

        /// <summary>부모 lossyScale를 보정해 구체가 지정 월드 지름이 되도록 localScale을 설정.</summary>
        private void SetWorldSize(float worldSize)
        {
            float parentScale = transform.parent ? Mathf.Max(1e-6f, transform.parent.lossyScale.x) : 1f;
            transform.localScale = Vector3.one * (worldSize / parentScale);
        }
    }
}
