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
        /// <summary>빔이 대상에게 닿은 시점. 전투 판정은 구독자가 처리합니다.</summary>
        public event System.Action OnTargetImpacted;
        public enum WarpStyle { PillarBlink, TravelStreak }

        /// <summary>
        /// 이 프리팹이 담당할 구간. 부품을 독립적으로 큐에서 부르기 위한 분할 축이다.
        ///   All   = 전 구간(기존 동작). 프리뷰·레거시 프리팹이 쓴다.
        ///   Spawn = 시전자 위 발의 일생  (등장 → 유지 → 소멸)          → paw_spawn
        ///   Warp  = 워프 기둥·섬광 한 번  (스폰된 자기 위치에서)         → paw_warp  ※출발·도착 각 1회 호출
        ///   Beam  = 대상 위 발 재등장 + 광선 + 착탄                     → paw_beam
        /// ★구간을 나눠도 연출이 같은 이유: 발은 이동하지 않고 양쪽에서 껐다 켜지는 블링크다.
        /// </summary>
        public enum Segment { All, Spawn, Warp, Beam }

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
        [Tooltip("발 등장 팝 시간(초)입니다. 길수록 해당 단계가 오래 지속됩니다.")]
        [Range(0.05f, 1f)] [SerializeField] private float appearTime = 0.25f;
        [Tooltip("효과가 나타난 후 현재 상태를 유지하는 시간(초)입니다. 길수록 다음 단계가 늦게 시작됩니다.")]
        [Range(0f, 3f)] [SerializeField] private float holdTime = 0.5f;
        [Tooltip("워프 소멸(스쿼시 축소) 시간(초)입니다. 길수록 해당 단계가 오래 지속됩니다.")]
        [Range(0.03f, 0.5f)] [SerializeField] private float warpOutTime = 0.12f;
        [Tooltip("워프 공백(소멸→재등장 사이) 시간. 블링크의 사라져 있는 시간")]
        [Range(0.03f, 0.5f)] [SerializeField] private float warpTravelTime = 0.1f;
        [Tooltip("대상 머리 위 재등장 팝 시간(초)입니다. 길수록 해당 단계가 오래 지속됩니다.")]
        [Range(0.05f, 0.6f)] [SerializeField] private float warpInTime = 0.15f;
        [Tooltip("광선 신장(발→대상) 시간(초)입니다. 길수록 해당 단계가 오래 지속됩니다.")]
        [Range(0.03f, 0.6f)] [SerializeField] private float beamExtendTime = 0.12f;
        [Tooltip("광선 유지 시간(초)입니다. 길수록 해당 단계가 오래 지속됩니다.")]
        [Range(0.1f, 5f)] [SerializeField] private float beamSustainTime = 1.2f;
        [Tooltip("종료 페이드아웃 시간(초). 총 재생시간 끝에서 이 시간만큼 페이드")]
        [Range(0.05f, 1.5f)] [SerializeField] private float fadeOutTime = 0.35f;

        [Header("팝 / 배치")]
        [Tooltip("스케일 팝 오버슛(스폰 시 튐). 0=팝 없음")]
        [Range(0f, 4f)] [SerializeField] private float popOvershoot = 1.7f;
        // ★위치는 부품 프리셋이 정본(260805) — 발 = P1(spawnOffset/headOffset), 광선 시작 = P2(startOffset, 끝은 수직 낙하 고정).
        [Tooltip("발 아래에서 광선이 시작되는 간격(m). ★음수 = 겹침 — 광선 머리를 글리프 뒤로 밀어넣어 소프트캡 이음새를 숨긴다(표준 기법).")]
        [Range(-0.6f, 0.6f)] [SerializeField] private float pawBeamGap = 0.12f;
        [Tooltip("종료 또는 착지 섬광 크기에 곱하는 배수입니다. 1이면 원래 크기를 유지합니다.")]
        [Range(0.2f, 3f)] [SerializeField] private float flashSizeMul = 1f;

        [Header("구간 분할")]
        [Tooltip("이 프리팹이 담당할 구간. All = 기존 전 구간 동작.")]
        [SerializeField] private Segment segment = Segment.All;
        // ★warpBurstTime 폐지(260805) — Warp 구간 정리 대기 = max(기둥 P5 duration, 섬광 duration).
        //   P5 duration 이 유일 조절축이 되어 「duration 을 늘리면 기둥이 잘리는」 간섭이 소멸한다.

        private enum Phase { Idle, Appear, Hold, WarpOut, Travel, WarpIn, BeamExtend, Sustain, FadeOut, WarpBurst }
        private Phase _phase = Phase.Idle;
        private float _phaseT;
        private Transform _caster, _target;

        private void Awake() => HideImmediate();

        /// <summary>시전자/대상 주입 재생 — 실 게임 결선용 시그니처.</summary>
        public override void Play(Transform origin, Transform target)
        {
            // 필요한 인자는 구간마다 다르다. Spawn은 시전자만, Beam은 대상만 있으면 된다.
            bool needCaster = segment == Segment.All || segment == Segment.Spawn;
            bool needTarget = segment == Segment.All || segment == Segment.Beam;
            if (needCaster && origin == null)
            {
                Debug.LogWarning($"[PawForYouVfx:{segment}] caster 없이 Play 호출 — 무시", this);
                return;
            }
            if (needTarget && target == null)
            {
                Debug.LogWarning($"[PawForYouVfx:{segment}] target 없이 Play 호출 — 무시", this);
                return;
            }

            _caster = origin;
            _target = target;
            if (livePreview && masterPreset) PullFromMaster();
            HideImmediate();

            switch (segment)
            {
                case Segment.Warp:
                    // 일회성. 스폰된 자기 위치에서 터뜨린다 — 위치는 큐의 Anchor 가 정한다.
                    if (flash && paw) flash.Flash(SelfAnchor(), paw.WarpFlashColor, flashSizeMul);
                    if (warpPillar) warpPillar.Burst(SelfAnchor());
                    EnterPhase(Phase.WarpBurst);
                    break;

                case Segment.Beam:
                    // 대상 위에서 발이 다시 나타나는 지점부터 시작한다.
                    EnterPhase(Phase.WarpIn);
                    if (paw)
                    {
                        paw.Show();
                        paw.SetBasePosition(TargetHead());
                        paw.SetScaleMul(0f);
                        paw.SetEnvelope(1f);
                    }
                    break;

                default:   // All · Spawn
                    EnterPhase(Phase.Appear);
                    if (paw)
                    {
                        paw.Show();
                        paw.SetBasePosition(CasterAnchor());
                        paw.SetScaleMul(0f);
                        paw.SetEnvelope(1f);
                    }
                    break;
            }
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

        // ── 앵커 산출 ── ★발·광선 끝 좌표의 정본은 부품 프리셋(P1/P2). 프리셋 없으면 앵커 루트로 폴백.
        //   소켓+오프셋 해석 규칙은 JcVfxPlacementPreset.Resolve 한 곳(회전만 적용·스케일 무시).
        /// <summary>이 프리팹의 발 프리셋 — 워프 기둥 위치를 발 좌표에 맞추려는 호출자(스테퍼·큐 드라이버)가 읽는다.</summary>
        public PawSpritePreset PawPreset => paw ? paw.Preset : null;

        private Vector3 CasterAnchor()
        {
            if (_caster == null) return transform.position;
            var pp = PawPreset != null ? PawPreset.TransformSource : null;
            return pp != null ? JcVfxPlacementPreset.Resolve(_caster, pp.spawnSocketName, pp.spawnOffset) : _caster.position;
        }

        private Vector3 TargetHead()
        {
            if (_target == null) return transform.position;
            var pp = PawPreset != null ? PawPreset.TransformSource : null;
            return pp != null ? JcVfxPlacementPreset.ResolveWorld(_target, pp.headSocketName, pp.headOffset) : _target.position;
        }

        /// <summary>
        /// ★광선 시작점(260806 개편) — 정본 = P2 프리셋(startSocketName/startOffset).
        /// 소켓이 비면 발바닥(발 현재 위치 − pawBeamGap) 기준, 지정 시 대상 소켓 기준. 오프셋은 월드 축.
        /// </summary>
        private Vector3 BeamStart()
        {
            // ★접점 핀(260806) — 발의 「시각 접점」(패드 하단)이 정본. 핀이 꺼진 프리셋에선 쿼드 중심(종전과 동일).
            Vector3 basePos = paw != null ? paw.AnchorPosition + Vector3.down * pawBeamGap
                                          : transform.position;
            var bp = beam && beam.Preset != null ? beam.Preset.TransformSource : null;
            if (bp == null) return basePos;
            if (!string.IsNullOrWhiteSpace(bp.startSocketName) && _target != null)
                return JcVfxPlacementPreset.ResolveWorld(_target, bp.startSocketName, bp.startOffset);
            return basePos + bp.startOffset;
        }

        /// <summary>광선 끝 = 시작의 XZ 그대로, y만 바닥(대상 루트) — 광선은 항상 XZ평면에 수직.</summary>
        private Vector3 BeamEnd(Vector3 start) => new Vector3(start.x, TargetGround().y, start.z);

        private Vector3 TargetGround() => _target ? _target.position : transform.position;
        /// <summary>Warp 구간 전용 — 스폰된 자기 위치. 출발·도착 어느 쪽인지는 큐의 Anchor 가 정한다.</summary>
        private Vector3 SelfAnchor() => transform.position;

        /// <summary>Warp 구간 정리 대기 시간 — 기둥(P5 duration)·섬광 중 긴 쪽. 어느 쪽도 잘리지 않는다.</summary>
        private float WarpBurstDuration()
        {
            float wait = 0.05f;   // 요소가 하나도 없어도 즉시 종료는 피한다
            if (warpPillar) wait = Mathf.Max(wait, warpPillar.CurrentDuration);
            if (flash) wait = Mathf.Max(wait, flash.Duration);
            return wait;
        }

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

            _phaseT += EffectDeltaTime;
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
                        // ★분할 시에는 이 연출을 paw_warp 부품이 대신 낸다 → All 에서만 낸다.
                        if (segment == Segment.All)
                        {
                            if (flash) flash.Flash(CasterAnchor(), paw.WarpFlashColor, flashSizeMul);
                            if (warpStyle == WarpStyle.PillarBlink && warpPillar) warpPillar.Burst(CasterAnchor());
                        }
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
                        // Spawn 구간은 여기서 제 할 일이 끝난다. 이후는 paw_warp · paw_beam 몫.
                        if (segment == Segment.Spawn)
                        {
                            HideImmediate();
                            RaiseFinished();
                            break;
                        }
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

                case Phase.WarpBurst:
                    // Warp 구간 — 기둥·섬광은 Play 에서 이미 터뜨렸다. 둘 다 제 수명을 다하면 정리한다.
                    if (_phaseT >= WarpBurstDuration())
                    {
                        HideImmediate();
                        RaiseFinished();
                    }
                    break;

                case Phase.Travel:
                    // PillarBlink: 공백(소멸 지점 기둥만 위로 빠져나감) / TravelStreak: 스트릭 신장 이동
                    if (warpStyle == WarpStyle.TravelStreak && warpStreak)
                        warpStreak.SetExtend(Mathf.Clamp01(_phaseT / Mathf.Max(warpTravelTime, 1e-4f)));
                    if (_phaseT >= warpTravelTime)
                    {
                        // 재등장: 대상 머리 위 플래시 + 팝 (+빛기둥 블링크)
                        // ★분할 시 이 연출도 paw_warp 몫 → All 에서만. (Beam 구간은 WarpIn 부터 시작한다)
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
                            Vector3 bs = BeamStart();
                            beam.SetLine(bs, BeamEnd(bs));
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
                        Vector3 bs = BeamStart();
                        beam.SetLine(bs, BeamEnd(bs));
                        beam.SetExtend(u);
                    }
                    if (_phaseT >= beamExtendTime)
                    {
                        // 착탄: 플래시 + 타격 이펙트 시작
                        if (flash && beam) flash.Flash(BeamEnd(BeamStart()), beam.HitFlashColor, flashSizeMul);
                        if (impactCross) impactCross.Play(TargetGround());
                        if (impactGround)
                        {
                            impactGround.Play(TargetGround());
                            impactGround.SetEnvelope(1f);
                        }
                        EnterPhase(Phase.Sustain);
                        OnTargetImpacted?.Invoke();
                    }
                    break;

                case Phase.Sustain:
                    paw.SetBasePosition(TargetHead());
                    if (beam)
                    {
                        Vector3 bs = BeamStart();
                        beam.SetLine(bs, BeamEnd(bs));
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
                        Vector3 bs = BeamStart();
                        beam.SetLine(bs, BeamEnd(bs));
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
