using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>트랙 위의 한 페이즈 구간. 클립이 없는 구간(Move/Return)은 <see cref="Clip"/>이 null이다.</summary>
    public sealed class JigPhaseInterval
    {
        public string Label;                 // "Attack.Beats[0]" 등 표시용
        public double Start;                 // 트랙에서 이 구간이 시작하는 시각 (블렌드 겹침 반영 후 최종값)
        public double Duration;              // 논리 길이(LogicalDuration). Post는 ExtraDelay 포함
        public double EffectiveLength;       // Cue 시각 해석의 분모. 빈 구간은 0
        public AnimationClip Clip;           // null이면 빈 구간(로코모션) 또는 해석 실패
        public List<CueBinding> Cues;        // null 가능
        public bool Resolved = true;         // false면 state 해석 실패
        public string FailureReason;

        /// <summary>
        /// 이 페이즈가 Cue를 가질 수 있는지. Move/Return은 false —
        /// <c>MovePhase</c>/<c>ReturnPhase</c>에 <c>Cues</c> 필드가 아예 없다.
        /// </summary>
        public bool SupportsCues = true;

        /// <summary>로코모션 구간(Move/Return). 클립 길이가 아니라 이동 지속시간이 구간 길이를 정한다.</summary>
        public bool IsLocomotion;

        /// <summary>MovingAttack 구간. 이동을 포함하지만 조기 종료(CalculateExitNormalized)하므로 나가는 경계는 EarlyOverlap이다.</summary>
        public bool IsMovingAttack;

        /// <summary>이 구간(Beat)이 AdvanceOnEvent인지. 나가는 경계의 실제 겹침이 이벤트 타이밍에 좌우된다(§2.2).</summary>
        public bool AdvanceOnEvent;

        /// <summary>이 구간으로 <b>들어오는</b> 블렌드(초). 데이터의 BlendInSeconds(또는 MovingAttack.AnimationBlendInSeconds).</summary>
        public float RequestedBlendSeconds;

        /// <summary>
        /// Cue를 놓을 수 있는 상한(구간 시작 기준 초). Post의 ExtraDelay 구간은 제외된다.
        /// </summary>
        public double CueLimit;

        public bool CanHostCues => SupportsCues && Resolved && Clip != null && EffectiveLength > 0d;

        /// <summary>클립으로 표시되는 기본 길이. Post의 ExtraDelay는 제외한다(§3.2).</summary>
        public double BaseClipDuration => IsLocomotion ? Duration : EffectiveLength;

        /// <summary>트랙에 그릴 클립 길이 = BaseClipDuration + PostBoundary 연장분. ComputeBlendBoundaries가 채운다.</summary>
        public double DisplayDuration;

        /// <summary>겹침·연장을 표현할 클립이 있는지(해석 실패/로코모션 해석 실패면 없다).</summary>
        public bool HasClip => Resolved && Clip != null;
    }

    /// <summary>경계 블렌드 표현 방식.</summary>
    public enum JigBlendMode
    {
        /// <summary>이전 애니가 다음 블렌드만큼 조기 종료 → 다음 클립을 당겨 겹친다.</summary>
        EarlyOverlap,

        /// <summary>이전 액션이 끝까지 재생/도착 → 이전 클립을 연장하고 다음은 이전 끝에서 시작.</summary>
        PostBoundaryBlend,

        /// <summary>겹칠 클립이 없어(시작·종료·해석실패) 주석으로만 표시.</summary>
        AnnotationOnly
    }

    /// <summary>두 구간 사이의 블렌드 경계. 진실은 데이터, 이것은 표시용 계산 결과다.</summary>
    public sealed class JigBlendBoundary
    {
        public int FromIndex;
        public int ToIndex;
        public float RequestedSeconds;     // 데이터 원래 blend
        public float AppliedSeconds;       // 클램프된 실제 표시값
        public JigBlendMode Mode;
        public double Start;               // 겹침/주석 구간 트랙 시작
        public double End;                 // 트랙 끝
        public bool IsEventGated;          // AdvanceOnEvent — 실제 겹침이 요청과 다를 수 있음(§2.2)
        public string ApproximationReason; // 이벤트게이트/해석실패 등
        public string Warning;             // 클램프 등. JigBuildResult.Warnings로도 복사(§6)

        public string FromLabel;
        public string ToLabel;
    }

    /// <summary>
    /// 연출 데이터를 트랙 구간 목록으로 펼치고, 경계 블렌드를 계산하고, Cue 시각 ↔ 트랙 시각을 변환한다.
    ///
    /// 구간 순서는 런타임 시퀀서와 같다: MovePrepare → Move → AttackPrepare → Attack.Beats → Return → Post.
    /// MovingAttack이 켜지면 Move/AttackPrepare/Attack을 대체한다(1차 레이아웃은 Move 접근·AttackPrepare 생략 = 근사, §2.3).
    /// </summary>
    public static class JigPhaseLayout
    {
        /// <summary>발화가 보장되는 정규화 시각의 상한.</summary>
        public const float CueFireGuaranteedLimit = 0.95f;

        /// <summary>역기입이 쓸 수 있는 정규화 최댓값.</summary>
        public const float MaxWritableNormalizedTime = 0.949f;

        private const double BlendEpsilon = 1e-4d;

        // ──────────────────────────────────────────────────────────────
        // 변환 함수 2개 — 순수. 램프(SpeedRamps) 도입 시 이 둘만 교체한다.
        // ──────────────────────────────────────────────────────────────

        public static double ToTimelineTime(CueTimingSource timing, float time, double phaseStart, double effectiveLength)
        {
            double local = timing == CueTimingSource.NormalizedTime
                ? Mathf.Max(0f, time) * effectiveLength
                : Mathf.Max(0f, time);
            return phaseStart + local;
        }

        public static float ToCueTime(CueTimingSource timing, double markerTime, double phaseStart,
            double effectiveLength, out bool clamped)
        {
            clamped = false;
            double local = markerTime - phaseStart;
            if (local < 0d)
            {
                local = 0d;
                clamped = true;
            }

            if (timing != CueTimingSource.NormalizedTime)
            {
                return (float)local;
            }

            if (effectiveLength <= 0d)
            {
                clamped = true;
                return 0f;
            }

            double normalized = local / effectiveLength;
            if (normalized > MaxWritableNormalizedTime)
            {
                normalized = MaxWritableNormalizedTime;
                clamped = true;
            }
            return (float)normalized;
        }

        // ──────────────────────────────────────────────────────────────
        // 구간 배치
        // ──────────────────────────────────────────────────────────────

        public static List<JigPhaseInterval> Build(
            SkillPresentationData data,
            RuntimeAnimatorController controller,
            UnitMovementProfile movement,
            out bool anyFailure)
        {
            anyFailure = false;
            var intervals = new List<JigPhaseInterval>();
            if (data == null)
            {
                return intervals;
            }

            double cursor = 0d;
            bool useMovingAttack = data.MovingAttack != null && data.MovingAttack.Enabled;

            // 1. MovePrepare
            AddCuePhase(intervals, ref cursor, ref anyFailure, controller, "MovePrepare",
                data.MovePrepare, data.MovePrepare?.Cues, 0d, Blend(data.MovePrepare));

            if (useMovingAttack)
            {
                AddResolved(intervals, ref cursor, ref anyFailure, controller, "MovingAttack",
                    data.MovingAttack.AnimationStateName, data.MovingAttack.Cues, 0d,
                    Mathf.Max(0f, data.MovingAttack.AnimationBlendInSeconds), isMovingAttack: true);
            }
            else
            {
                // 2. Move
                AddLocomotion(intervals, ref cursor, controller, "Move (접근)",
                    data.Move != null ? data.Move.AnimationStateName : null,
                    movement != null ? movement.MoveDuration : 0.25f,
                    data.Move != null && data.Move.Enabled, Blend(data.Move));

                // 3. AttackPrepare
                AddCuePhase(intervals, ref cursor, ref anyFailure, controller, "AttackPrepare",
                    data.AttackPrepare, data.AttackPrepare?.Cues, 0d, Blend(data.AttackPrepare));

                // 4. Attack.Beats
                if (data.Attack != null && data.Attack.Enabled && data.Attack.Beats != null)
                {
                    for (int i = 0; i < data.Attack.Beats.Count; i++)
                    {
                        AttackBeat beat = data.Attack.Beats[i];
                        if (beat == null) continue;
                        AddResolved(intervals, ref cursor, ref anyFailure, controller,
                            $"Attack.Beats[{i}]", beat.AnimationStateName, beat.Cues, 0d,
                            Mathf.Max(0f, beat.BlendInSeconds), advanceOnEvent: beat.AdvanceOnEvent);
                    }
                }
            }

            // 5. Return
            AddLocomotion(intervals, ref cursor, controller, "Return (복귀)",
                data.Return != null ? data.Return.AnimationStateName : null,
                movement != null ? movement.ReturnDuration : 0.3f,
                data.Return != null && data.Return.Enabled, Blend(data.Return));

            // 6. Post
            double extraDelay = data.Post != null ? Mathf.Max(0f, data.Post.ExtraDelay) : 0f;
            AddCuePhase(intervals, ref cursor, ref anyFailure, controller, "Post",
                data.Post, data.Post?.Cues, extraDelay, Blend(data.Post));

            return intervals;
        }

        private static float Blend(CuePhase phase) => phase != null ? Mathf.Max(0f, phase.BlendInSeconds) : 0f;
        private static float Blend(MovePhase phase) => phase != null ? Mathf.Max(0f, phase.BlendInSeconds) : 0f;
        private static float Blend(ReturnPhase phase) => phase != null ? Mathf.Max(0f, phase.BlendInSeconds) : 0f;

        private static void AddCuePhase(List<JigPhaseInterval> list, ref double cursor, ref bool anyFailure,
            RuntimeAnimatorController controller, string label, CuePhase phase, List<CueBinding> cues,
            double trailingPadding, float blendSeconds)
        {
            if (phase == null || !phase.Enabled)
            {
                return;
            }
            AddResolved(list, ref cursor, ref anyFailure, controller, label, phase.AnimationStateName, cues,
                trailingPadding, blendSeconds);
        }

        private static void AddResolved(List<JigPhaseInterval> list, ref double cursor, ref bool anyFailure,
            RuntimeAnimatorController controller, string label, string stateName, List<CueBinding> cues,
            double trailingPadding, float blendSeconds, bool isMovingAttack = false, bool advanceOnEvent = false)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                bool hasCue = cues != null && cues.Count > 0;
                if (hasCue)
                {
                    anyFailure = true;
                    list.Add(new JigPhaseInterval
                    {
                        Label = label,
                        Start = cursor,
                        Duration = 0d,
                        Cues = cues,
                        Resolved = false,
                        RequestedBlendSeconds = blendSeconds,
                        IsMovingAttack = isMovingAttack,
                        AdvanceOnEvent = advanceOnEvent,
                        FailureReason = "AnimationStateName이 비어 있어 Cue 시각을 해석할 수 없습니다.",
                    });
                }
                return;
            }

            JigStateResolution r = JigStateResolver.Resolve(controller, stateName);
            if (!r.Success)
            {
                anyFailure = true;
                list.Add(new JigPhaseInterval
                {
                    Label = label,
                    Start = cursor,
                    Duration = 0d,
                    Cues = cues,
                    Resolved = false,
                    RequestedBlendSeconds = blendSeconds,
                    IsMovingAttack = isMovingAttack,
                    AdvanceOnEvent = advanceOnEvent,
                    FailureReason = $"'{stateName}' — {r.FailureReason}",
                });
                return;
            }

            list.Add(new JigPhaseInterval
            {
                Label = label,
                Start = cursor,
                Duration = r.EffectiveLength + trailingPadding,
                EffectiveLength = r.EffectiveLength,
                Clip = r.Clip,
                Cues = cues,
                Resolved = true,
                RequestedBlendSeconds = blendSeconds,
                IsMovingAttack = isMovingAttack,
                AdvanceOnEvent = advanceOnEvent,
                CueLimit = r.EffectiveLength * CueFireGuaranteedLimit,
            });
            cursor += r.EffectiveLength + trailingPadding;
        }

        private static void AddLocomotion(List<JigPhaseInterval> list, ref double cursor,
            RuntimeAnimatorController controller, string label, string stateName, double duration, bool enabled,
            float blendSeconds)
        {
            if (!enabled || duration <= 0d)
            {
                return;
            }

            AnimationClip clip = null;
            double effectiveLength = 0d;
            string note = null;

            if (!string.IsNullOrWhiteSpace(stateName))
            {
                JigStateResolution r = JigStateResolver.Resolve(controller, stateName);
                if (r.Success)
                {
                    clip = r.Clip;
                    effectiveLength = r.EffectiveLength;
                }
                else
                {
                    note = $"'{stateName}' — {r.FailureReason} (프리뷰만 비어 있고 편집에는 영향 없음)";
                }
            }

            list.Add(new JigPhaseInterval
            {
                Label = label,
                Start = cursor,
                Duration = duration,
                EffectiveLength = effectiveLength,
                Clip = clip,
                Cues = null,
                Resolved = true,
                SupportsCues = false,
                IsLocomotion = true,
                RequestedBlendSeconds = blendSeconds,
                CueLimit = 0d,
                FailureReason = note,
            });
            cursor += duration;
        }

        // ──────────────────────────────────────────────────────────────
        // 경계 블렌드 계산 (겹침/연장) — 순수, 테스트 대상
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 경계 A→B의 모드. "이동 포함 여부"가 아니라 <b>이전 액션 A가 어떻게 끝나는가</b>로 정한다(§2).
        /// - A가 로코모션(MoveToTarget, 완료 대기) → PostBoundaryBlend
        /// - A가 MovingAttack(CalculateExitNormalized 조기 종료) → EarlyOverlap
        /// - A가 애니 페이즈 → 다음 B가 애니면 EarlyOverlap(조기 종료), B가 로코모션이면 PostBoundary(nextBlend 없음)
        /// - 어느 쪽이든 클립이 없으면(해석 실패 등) AnnotationOnly
        /// </summary>
        public static JigBlendMode ClassifyMode(JigPhaseInterval from, JigPhaseInterval to)
        {
            if (from == null || to == null || !from.HasClip || !to.HasClip)
            {
                return JigBlendMode.AnnotationOnly;
            }
            if (from.IsLocomotion)
            {
                return JigBlendMode.PostBoundaryBlend;
            }
            if (from.IsMovingAttack)
            {
                return JigBlendMode.EarlyOverlap;
            }
            // from은 애니 페이즈
            return to.IsLocomotion ? JigBlendMode.PostBoundaryBlend : JigBlendMode.EarlyOverlap;
        }

        /// <summary>
        /// 인터벌들의 <see cref="JigPhaseInterval.Start"/>/<see cref="JigPhaseInterval.DisplayDuration"/>을
        /// 경계 블렌드가 반영되도록 <b>다시 계산</b>하고, 경계 목록을 돌려준다.
        /// - cursor는 <c>start + BaseClipDuration</c>로 진행한다(겹침 후에도 다음 구간이 밀리지 않게).
        /// - 클램프는 모드별로 다르다(§4.3).
        /// - Cue 마커의 PhaseStart는 이 Start를 쓴다 → 왕복 보존.
        /// </summary>
        public static List<JigBlendBoundary> ComputeBlendBoundaries(List<JigPhaseInterval> intervals)
        {
            var boundaries = new List<JigBlendBoundary>();
            if (intervals == null || intervals.Count == 0)
            {
                return boundaries;
            }

            double cursor = 0d;
            double prevIncomingApplied = 0d;

            for (int i = 0; i < intervals.Count; i++)
            {
                JigPhaseInterval iv = intervals[i];
                iv.DisplayDuration = iv.BaseClipDuration;
                double baseDur = iv.BaseClipDuration;

                if (i == 0)
                {
                    iv.Start = 0d;
                    cursor = baseDur;
                    prevIncomingApplied = 0d;
                    continue;
                }

                JigPhaseInterval prev = intervals[i - 1];
                JigBlendMode mode = ClassifyMode(prev, iv);

                float requested = Mathf.Max(0f, iv.RequestedBlendSeconds);
                double incomingDur = baseDur;
                double previousDur = prev.BaseClipDuration;

                double applied;
                switch (mode)
                {
                    case JigBlendMode.EarlyOverlap:
                        // 3중 겹침 방지: 이전 클립이 앞 경계에서 이미 쓴 몫을 남긴다.
                        applied = Min3(requested, incomingDur - BlendEpsilon,
                            previousDur - prevIncomingApplied - BlendEpsilon);
                        break;
                    case JigBlendMode.PostBoundaryBlend:
                        // 이전 길이로 제한하지 않는다 — 런타임은 이전 끝 포즈를 유지하며 blend한다.
                        applied = System.Math.Min(requested, incomingDur - BlendEpsilon);
                        break;
                    default:
                        applied = 0d;
                        break;
                }
                if (applied < 0d) applied = 0d;

                double start = mode == JigBlendMode.EarlyOverlap ? cursor - applied : cursor;
                iv.Start = start;

                if (mode == JigBlendMode.PostBoundaryBlend && applied > 0d)
                {
                    // 이전 클립을 연장(표시용). 로코모션은 루프, 비루프 애니는 끝 포즈 Hold(빌더에서 처리).
                    prev.DisplayDuration = prev.BaseClipDuration + applied;
                }

                var boundary = new JigBlendBoundary
                {
                    FromIndex = i - 1,
                    ToIndex = i,
                    FromLabel = prev.Label,
                    ToLabel = iv.Label,
                    RequestedSeconds = requested,
                    AppliedSeconds = (float)applied,
                    Mode = mode,
                    Start = mode == JigBlendMode.EarlyOverlap ? start : cursor,
                    End = (mode == JigBlendMode.EarlyOverlap ? start : cursor) + applied,
                    IsEventGated = prev.AdvanceOnEvent,
                };

                if (mode == JigBlendMode.AnnotationOnly)
                {
                    boundary.ApproximationReason = "겹칠 클립이 없어 주석으로만 표시(해석 실패/빈 구간).";
                }
                else if (boundary.IsEventGated)
                {
                    boundary.ApproximationReason = "이벤트 게이트(AdvanceOnEvent) — 실제 겹침은 요청값과 다를 수 있습니다.";
                }

                if (mode != JigBlendMode.AnnotationOnly && applied + BlendEpsilon < requested)
                {
                    boundary.Warning =
                        $"{prev.Label}→{iv.Label} 블렌드 클램프: 요청 {requested:F3}s / 적용 {applied:F3}s";
                }

                boundaries.Add(boundary);

                cursor = start + baseDur;
                prevIncomingApplied = applied;
            }

            return boundaries;
        }

        private static double Min3(double a, double b, double c)
        {
            return System.Math.Min(a, System.Math.Min(b, c));
        }
    }
}
