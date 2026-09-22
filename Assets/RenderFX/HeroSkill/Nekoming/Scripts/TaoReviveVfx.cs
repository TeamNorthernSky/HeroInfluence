using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「쓰러지면 안돼」(Taosenaiyo)의 부활 오라 <b>부품</b> (큐 이름 <c>tao_revive</c>, 260807).
    /// 통짜 TaosenaiyoVfx 에서 오라 구간만 분리 — 유성·손 플래시 없음, 착지 플래시 + 복합 오라
    /// (마법진+발산 오라+스커트+깃털+소형 십자+스파클) → 페이드인/유지/페이드아웃.
    ///
    /// 위치 = 이 부품이 스폰된 자기 위치(스테퍼가 T8.landOffset 으로 결정). Play 의 앵커는 쓰지 않는다.
    /// 실전에서는 유성(tao_fire)의 착탄 시그널 뒤에 이 부품이 불린다(스테퍼 waitForImpact 와 동일).
    /// 타이밍 정본 = T8_TaoReviveMaster(B/A 공용). 플래시 색은 변종별 프리팹 직렬화.
    /// </summary>
    public class TaoReviveVfx : VfxEffect
    {
        [Header("References")]
        [Tooltip("착지 플래시(자식, PawFlash 재사용)")]
        [SerializeField] private PawFlash flash;
        [Tooltip("마법진 장판(자식)")]
        [SerializeField] private TaoMagicCircle magicCircle;
        [Tooltip("발산 오라(자식)")]
        [SerializeField] private TaoAuraFlare auraFlare;
        [Tooltip("기초 방사 스프레이 스커트(자식)")]
        [SerializeField] private TaoBaseSpray baseSpray;
        [Tooltip("깃털 버스트(자식)")]
        [SerializeField] private TaoFeatherBurst featherBurst;
        [Tooltip("소형 스타버스트 십자(자식, HealCrossBurst+T5 프리셋 재사용)")]
        [SerializeField] private HealCrossBurst crossBurst;
        [Tooltip("빛 입자 스파클(자식 ParticleSystem)")]
        [SerializeField] private ParticleSystem sparkles;

        [Header("런타임 프리뷰")]
        [Tooltip("타이밍/착지점 마스터(T8, B/A 공용). 없으면 아래 필드값 사용.")]
        [SerializeField] private TaoReviveMasterPreset masterPreset;
        [Tooltip("켜면 변경한 설정을 실행 중인 이 효과에 갱신합니다. 파일 저장과는 별개이며 이미 시작된 시간표는 재시전하여 확인합니다.")]
        [SerializeField] private bool livePreview = true;

        [Header("착지 플래시 (변종별 직렬화 — Basic 은백 / Alter 금)")]
        [Tooltip("착지·부활 시 순간적으로 번쩍이는 섬광의 색입니다.")]
        [ColorUsage(true, true)] [SerializeField] private Color impactFlashColor = new Color(1f, 0.9f, 0.45f);
        [Tooltip("종료 또는 착지 섬광 크기에 곱하는 배수입니다. 1이면 원래 크기를 유지합니다.")]
        [Range(0.2f, 3f)] [SerializeField] private float flashSizeMul = 0.9f;

        [Header("오라 타이밍 (masterPreset 없을 때 폴백)")]
        [Tooltip("시작 페이드인 시간(초). 총 재생시간 기준")]
        [Range(0.05f, 1.5f)] [SerializeField] private float fadeInTime = 0.35f;
        [Tooltip("유지 시간(초).")]
        [Range(0.2f, 8f)] [SerializeField] private float sustainTime = 2.6f;
        [Tooltip("종료 페이드아웃 시간(초). 총 재생시간 끝에서 이 시간만큼 페이드")]
        [Range(0.05f, 2f)] [SerializeField] private float fadeOutTime = 0.5f;

        private enum Phase { Idle, In, Sustain, Out }
        private Phase _phase = Phase.Idle;
        private float _phaseT;
        private float _fade;

        /// <summary>호출자(스테퍼)가 착지 오프셋을 읽어 가는 창구.</summary>
        public TaoReviveMasterPreset Master => masterPreset;

        public override void Play(Transform origin, Transform target)
        {
            StopInternal();
            if (livePreview && masterPreset) PullFromMaster();

            Vector3 g = transform.position;
            if (flash) flash.Flash(g + Vector3.up * 0.5f, impactFlashColor, flashSizeMul);
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
            IsPlaying = true;
        }

        public override void Stop() => StopInternal();

        /// <summary>어느 페이즈에서든 페이드아웃으로 점프해 부드럽게 종료.</summary>
        public void StopGraceful()
        {
            if (!IsPlaying || _phase == Phase.Out) return;
            BeginOut();
        }

        private void StopInternal()
        {
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

        private void Update()
        {
            if (!IsPlaying) return;
            if (livePreview && masterPreset) PullFromMaster();
            _phaseT += Time.deltaTime;

            switch (_phase)
            {
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

        private void PullFromMaster()
        {
            fadeInTime = masterPreset.fadeInTime;
            sustainTime = masterPreset.sustainTime;
            fadeOutTime = masterPreset.fadeOutTime;
        }
    }
}
