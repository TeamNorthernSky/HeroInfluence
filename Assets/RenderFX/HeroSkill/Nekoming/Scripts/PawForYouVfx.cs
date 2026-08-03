using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「널 위해 준비했어」(PawForYou)와 파생 PawForYouMistake의 공용 오케스트레이터.
    /// PawForYou 계열 스킬 VFX 오케스트레이터(PawForYou / PawForYouMistake 공용 — warpStyle로 분기).
    /// 시퀀스: 발 등장(시전자 우상단) → 유지 → 워프 소멸(플래시+스쿼시) → 공백/이동 →
    ///         재등장(대상 머리 위, 플래시+팝) → 광선 수직 신장 → 유지(+타격 이펙트) → 페이드아웃.
    /// ★워프 스타일 2종:
    ///   PillarBlink  = 이동 궤적 없는 블링크(록맨풍) — 소멸/재등장 지점에서 한줄기 빛기둥이 반짝→위로 슈릭. (PawForYou)
    ///   TravelStreak = 시전자→대상 잔상 스트릭 이동(PawBeam 재사용). (PawForYouMistake)
    /// ★Play(caster, target) 2점 주입 — ASB seam 트랙에서 실증한 확장 규약과 동일 방향.
    /// 요소들은 전부 수동적(SetLine/SetExtend/SetEnvelope 주입식). 타임라인은 여기가 소유.
    /// 타격 이펙트는 힐 트랙의 HealCrossBurst/HealGroundShine을 색 변형 프리셋으로 재사용.
    /// </summary>
    public class PawForYouVfx : VfxEffect
    {
        public enum WarpStyle { PillarBlink, TravelStreak }

        [Header("References")]
        [Tooltip("고양이 발 빌보드")]
        [SerializeField] private PawSprite paw;
        [Tooltip("워프 연출 방식: PillarBlink=빛기둥 블링크(PawForYou) / TravelStreak=잔상 스트릭 이동(PawForYouMistake)")]
        [SerializeField] private WarpStyle warpStyle = WarpStyle.PillarBlink;
        [Tooltip("빛기둥 블링크(PillarBlink용). 소멸/재등장 지점에서 재생")]
        [SerializeField] private PawWarpPillar warpPillar;
        [Tooltip("워프 잔상 스트릭(TravelStreak용, PawBeam 공용)")]
        [SerializeField] private PawBeam warpStreak;
        [Tooltip("수직 광선(PawBeam, 빔 프리셋)")]
        [SerializeField] private PawBeam beam;
        [Tooltip("원샷 플래시 풀(워프/착탄)")]
        [SerializeField] private PawFlash flash;
        [Tooltip("타격: 십자 버스트(힐 E-4 재사용, 색 변형 프리셋)")]
        [SerializeField] private HealCrossBurst impactCross;
        [Tooltip("타격: 바닥 샤인(힐 E-5 재사용, 색 변형 프리셋)")]
        [SerializeField] private HealGroundShine impactGround;

        [Header("런타임 프리뷰")]
        [Tooltip("전체 타이밍/배치 마스터 프리셋. 없으면 아래 필드값 사용.")]
        [SerializeField] private PawMasterPreset masterPreset;
        [Tooltip("플레이 중 masterPreset 값을 매 프레임 반영.")]
        [SerializeField] private bool livePreview = true;

        [Header("페이즈 타이밍 (초)")]
        [Range(0.05f, 1f)] [SerializeField] private float appearTime = 0.25f;
        [Range(0f, 3f)] [SerializeField] private float holdTime = 0.5f;
        [Range(0.03f, 0.5f)] [SerializeField] private float warpOutTime = 0.12f;
        [Tooltip("워프 공백(소멸→재등장 사이) 시간. 블링크의 사라져 있는 시간")]
        [Range(0.03f, 0.5f)] [SerializeField] private float warpTravelTime = 0.1f;
        [Range(0.05f, 0.6f)] [SerializeField] private float warpInTime = 0.15f;
        [Range(0.03f, 0.6f)] [SerializeField] private float beamExtendTime = 0.12f;
        [Range(0.1f, 5f)] [SerializeField] private float beamSustainTime = 1.2f;
        [Range(0.05f, 1.5f)] [SerializeField] private float fadeOutTime = 0.35f;

        [Header("팝 / 배치")]
        [Range(0f, 4f)] [SerializeField] private float popOvershoot = 1.7f;
        [SerializeField] private Vector3 casterOffset = new Vector3(0.55f, 1.7f, 0.15f);
        [SerializeField] private float targetHeadOffset = 1.9f;
        [SerializeField] private float beamEndOffsetY = 0.35f;
        [Range(0f, 0.6f)] [SerializeField] private float pawBeamGap = 0.12f;
        [Range(0.2f, 3f)] [SerializeField] private float flashSizeMul = 1f;

        private enum Phase { Idle, Appear, Hold, WarpOut, Travel, WarpIn, BeamExtend, Sustain, FadeOut }
        private Phase _phase = Phase.Idle;
        private float _phaseT;
        private Transform _caster, _target;

        private void Awake() => HideImmediate();

        /// <summary>시전자/대상 주입 재생 — 실 게임 결선용 시그니처.</summary>
        public override void Play(Transform origin, Transform target)
        {
            if (origin == null || target == null)
            {
                Debug.LogWarning("[PawForYouVfx] caster/target 없이 Play 호출 — 무시");
                return;
            }
            _caster = origin;
            _target = target;
            if (livePreview && masterPreset) PullFromMaster();
            HideImmediate();
            EnterPhase(Phase.Appear);
            paw.Show();
            paw.SetBasePosition(CasterAnchor());
            paw.SetScaleMul(0f);
            paw.SetEnvelope(1f);
            IsPlaying = true;
        }

        /// <summary>즉시 정지(페이드 없음).</summary>
        public override void Stop() => HideImmediate();

        /// <summary>어느 페이즈에서든 페이드아웃으로 점프해 부드럽게 종료.</summary>
        public void StopGraceful()
        {
            if (!IsPlaying || _phase == Phase.FadeOut) return;
            EnterFadeOut();
        }

        /// <summary>변형 프리셋 일괄 교체(발 은백/금 × 광선 노랑/녹색 자유 조합).</summary>
        public void ApplyVariant(PawSpritePreset pawP, PawBeamPreset beamP, HealCrossPreset crossP, HealGroundShinePreset groundP)
        {
            if (paw && pawP) paw.SetPreset(pawP);
            if (beam && beamP) beam.SetPreset(beamP);
            if (impactCross && crossP) impactCross.SetPreset(crossP);
            if (impactGround && groundP) impactGround.SetPreset(groundP);
        }

        private void HideImmediate()
        {
            if (paw) paw.Hide();
            if (warpPillar) warpPillar.StopAll();
            if (warpStreak) warpStreak.Hide();
            if (beam) beam.Hide();
            if (flash) flash.StopAll();
            if (impactCross) impactCross.StopAll();
            if (impactGround) impactGround.Stop();
            _phase = Phase.Idle;
            IsPlaying = false;
        }

        // ── 앵커 산출 ──
        private Vector3 CasterAnchor() => _caster ? _caster.TransformPoint(casterOffset) : transform.position;
        private Vector3 TargetHead() => _target ? _target.position + Vector3.up * targetHeadOffset : transform.position;
        private Vector3 BeamEnd() => _target ? _target.position + Vector3.up * beamEndOffsetY : transform.position;
        private Vector3 TargetGround() => _target ? _target.position : transform.position;

        private void EnterPhase(Phase p)
        {
            _phase = p;
            _phaseT = 0f;
        }

        private void EnterFadeOut()
        {
            EnterPhase(Phase.FadeOut);
            if (impactCross) impactCross.StopSpawning();
        }

        private void Update()
        {
            if (!IsPlaying) return;
            if (livePreview && masterPreset) PullFromMaster();

            _phaseT += Time.deltaTime;
            float u;

            switch (_phase)
            {
                case Phase.Appear:
                    u = Mathf.Clamp01(_phaseT / Mathf.Max(appearTime, 1e-4f));
                    paw.SetBasePosition(CasterAnchor());
                    paw.SetScaleMul(EaseOutBack01(u, popOvershoot));
                    paw.SetEnvelope(Mathf.Clamp01(u * 2f));
                    if (_phaseT >= appearTime) EnterPhase(Phase.Hold);
                    break;

                case Phase.Hold:
                    paw.SetBasePosition(CasterAnchor());
                    paw.SetScaleMul(1f);
                    paw.SetEnvelope(1f);
                    if (_phaseT >= holdTime)
                    {
                        // 워프 소멸: 원위치 플래시 (+빛기둥 블링크)
                        if (flash) flash.Flash(CasterAnchor(), paw.WarpFlashColor, flashSizeMul);
                        if (warpStyle == WarpStyle.PillarBlink && warpPillar) warpPillar.Burst(CasterAnchor());
                        EnterPhase(Phase.WarpOut);
                    }
                    break;

                case Phase.WarpOut:
                    u = Mathf.Clamp01(_phaseT / Mathf.Max(warpOutTime, 1e-4f));
                    paw.SetBasePosition(CasterAnchor());
                    paw.SetScaleMul(1f - u * u);   // easeIn 축소 → 순간 소멸감
                    if (_phaseT >= warpOutTime)
                    {
                        paw.SetScaleMul(0f);
                        if (warpStyle == WarpStyle.TravelStreak && warpStreak)
                        {
                            warpStreak.Show();
                            warpStreak.SetLine(CasterAnchor(), TargetHead());
                            warpStreak.SetExtend(0f);
                            warpStreak.SetEnvelope(1f);
                        }
                        EnterPhase(Phase.Travel);
                    }
                    break;

                case Phase.Travel:
                    // PillarBlink: 공백(소멸 지점 기둥만 위로 빠져나감) / TravelStreak: 스트릭 신장 이동
                    if (warpStyle == WarpStyle.TravelStreak && warpStreak)
                        warpStreak.SetExtend(Mathf.Clamp01(_phaseT / Mathf.Max(warpTravelTime, 1e-4f)));
                    if (_phaseT >= warpTravelTime)
                    {
                        // 재등장: 대상 머리 위 플래시 + 팝 (+빛기둥 블링크)
                        if (flash) flash.Flash(TargetHead(), paw.WarpFlashColor, flashSizeMul);
                        if (warpStyle == WarpStyle.PillarBlink && warpPillar) warpPillar.Burst(TargetHead());
                        paw.SetBasePosition(TargetHead());
                        EnterPhase(Phase.WarpIn);
                    }
                    break;

                case Phase.WarpIn:
                    u = Mathf.Clamp01(_phaseT / Mathf.Max(warpInTime, 1e-4f));
                    paw.SetBasePosition(TargetHead());
                    paw.SetScaleMul(EaseOutBack01(u, popOvershoot));
                    if (warpStyle == WarpStyle.TravelStreak && warpStreak)
                    {
                        warpStreak.SetExtend(1f);
                        warpStreak.SetEnvelope(1f - u);   // 잔상 자연 소멸
                    }
                    if (_phaseT >= warpInTime)
                    {
                        if (warpStyle == WarpStyle.TravelStreak && warpStreak) warpStreak.Hide();
                        if (beam)
                        {
                            beam.Show();
                            beam.SetLine(paw.CurrentPosition + Vector3.down * pawBeamGap, BeamEnd());
                            beam.SetExtend(0f);
                            beam.SetEnvelope(1f);
                        }
                        EnterPhase(Phase.BeamExtend);
                    }
                    break;

                case Phase.BeamExtend:
                    u = Mathf.Clamp01(_phaseT / Mathf.Max(beamExtendTime, 1e-4f));
                    paw.SetBasePosition(TargetHead());
                    paw.SetScaleMul(1f);
                    if (beam)
                    {
                        beam.SetLine(paw.CurrentPosition + Vector3.down * pawBeamGap, BeamEnd());
                        beam.SetExtend(u);
                    }
                    if (_phaseT >= beamExtendTime)
                    {
                        // 착탄: 플래시 + 타격 이펙트 시작
                        if (flash && beam) flash.Flash(BeamEnd(), beam.HitFlashColor, flashSizeMul);
                        if (impactCross) impactCross.Play(TargetGround());
                        if (impactGround)
                        {
                            impactGround.Play(TargetGround());
                            impactGround.SetEnvelope(1f);
                        }
                        EnterPhase(Phase.Sustain);
                    }
                    break;

                case Phase.Sustain:
                    paw.SetBasePosition(TargetHead());
                    if (beam)
                    {
                        beam.SetLine(paw.CurrentPosition + Vector3.down * pawBeamGap, BeamEnd());
                        beam.SetExtend(1f);
                    }
                    if (impactGround) impactGround.SetEnvelope(1f);
                    if (_phaseT >= beamSustainTime) EnterFadeOut();
                    break;

                case Phase.FadeOut:
                    u = Mathf.Clamp01(_phaseT / Mathf.Max(fadeOutTime, 1e-4f));
                    float fade = 1f - u;
                    paw.SetEnvelope(fade);
                    if (beam)
                    {
                        beam.SetLine(paw.CurrentPosition + Vector3.down * pawBeamGap, BeamEnd());
                        beam.SetEnvelope(fade);
                    }
                    if (impactGround) impactGround.SetEnvelope(fade);
                    if (_phaseT >= fadeOutTime)
                    {
                        HideImmediate();
                        RaiseFinished();
                    }
                    break;
            }
        }

        private void PullFromMaster()
        {
            appearTime = masterPreset.appearTime;
            holdTime = masterPreset.holdTime;
            warpOutTime = masterPreset.warpOutTime;
            warpTravelTime = masterPreset.warpTravelTime;
            warpInTime = masterPreset.warpInTime;
            beamExtendTime = masterPreset.beamExtendTime;
            beamSustainTime = masterPreset.beamSustainTime;
            fadeOutTime = masterPreset.fadeOutTime;
            popOvershoot = masterPreset.popOvershoot;
            casterOffset = masterPreset.casterOffset;
            targetHeadOffset = masterPreset.targetHeadOffset;
            beamEndOffsetY = masterPreset.beamEndOffsetY;
            pawBeamGap = masterPreset.pawBeamGap;
            flashSizeMul = masterPreset.flashSizeMul;
        }

        /// <summary>easeOutBack: 0→(오버슛 &gt;1)→1. s=오버슛 강도(0=오버슛 없음).</summary>
        private static float EaseOutBack01(float x, float s)
        {
            float c1 = s;
            float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }
    }
}
