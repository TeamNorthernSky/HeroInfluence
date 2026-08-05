using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>트랙 위의 한 페이즈 구간. 클립이 없는 구간(Move/Return)은 <see cref="Clip"/>이 null이다.</summary>
    public sealed class JigPhaseInterval
    {
        public string Label;                 // "Attack.Beats[0]" 등 표시용
        public double Start;                 // 트랙에서 이 구간이 시작하는 시각
        public double Duration;              // 트랙에서 차지하는 길이
        public double EffectiveLength;       // Cue 시각 해석의 분모. 빈 구간은 0
        public AnimationClip Clip;           // null이면 빈 구간(로코모션)
        public List<CueBinding> Cues;        // null 가능
        public bool Resolved = true;         // false면 state 해석 실패
        public string FailureReason;

        /// <summary>
        /// 이 페이즈가 Cue를 가질 수 있는지. Move/Return은 false —
        /// <c>MovePhase</c>/<c>ReturnPhase</c>에 <c>Cues</c> 필드가 아예 없다.
        /// (클립은 프리뷰용으로 표시하되 마커는 놓지 않는다.)
        /// </summary>
        public bool SupportsCues = true;

        /// <summary>로코모션 구간. 클립 길이가 아니라 이동 지속시간이 구간 길이를 정한다.</summary>
        public bool IsLocomotion;

        /// <summary>
        /// Cue를 놓을 수 있는 상한(구간 시작 기준 초). Post의 ExtraDelay 구간은 제외된다 —
        /// 클립이 끝난 뒤라 재생 중인 state가 없어 드라이버가 시각을 해석할 근거가 없다.
        /// </summary>
        public double CueLimit;

        public bool CanHostCues => SupportsCues && Resolved && Clip != null && EffectiveLength > 0d;
    }

    /// <summary>
    /// 연출 데이터를 트랙 구간 목록으로 펼치고, Cue 시각 ↔ 트랙 시각을 변환한다.
    ///
    /// 구간 순서는 런타임 시퀀서(<c>SkillPresentationDirector.RunSkillSequenceCore</c>)의
    /// 페이즈 순서와 같아야 한다: MovePrepare → Move → AttackPrepare → Attack.Beats → Return → Post.
    /// MovingAttack이 켜져 있으면 Move/AttackPrepare/Attack을 대체한다.
    /// </summary>
    public static class JigPhaseLayout
    {
        /// <summary>발화가 보장되는 정규화 시각의 상한. CharactorAnimationController의 클립 종료 임계값과 같다.</summary>
        public const float CueFireGuaranteedLimit = 0.95f;

        /// <summary>역기입이 쓸 수 있는 정규화 최댓값. 상한 바로 아래로 클램프해 검증 경고를 유발하지 않는다.</summary>
        public const float MaxWritableNormalizedTime = 0.949f;

        // ──────────────────────────────────────────────────────────────
        // 변환 함수 2개 — 순수. 램프(SpeedRamps) 도입 시 이 둘만 교체한다.
        // ──────────────────────────────────────────────────────────────

        /// <summary>Cue 시각 → 트랙 시각.</summary>
        public static double ToTimelineTime(CueTimingSource timing, float time, double phaseStart, double effectiveLength)
        {
            double local = timing == CueTimingSource.NormalizedTime
                ? Mathf.Max(0f, time) * effectiveLength
                : Mathf.Max(0f, time);
            return phaseStart + local;
        }

        /// <summary>
        /// 트랙 시각 → Cue 시각. NormalizedTime은 [0, <see cref="MaxWritableNormalizedTime"/>]으로 클램프한다.
        /// </summary>
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
                data.MovePrepare, data.MovePrepare?.Cues, 0d);

            if (useMovingAttack)
            {
                // MovingAttack이 Move/AttackPrepare/Attack을 대체한다.
                AddResolved(intervals, ref cursor, ref anyFailure, controller, "MovingAttack",
                    data.MovingAttack.AnimationStateName, data.MovingAttack.Cues, 0d);
            }
            else
            {
                // 2. Move — 로코모션. 지속시간은 거리와 무관한 고정값이고, 클립은 프리뷰용으로만 표시한다.
                AddLocomotion(intervals, ref cursor, controller, "Move (접근)",
                    data.Move != null ? data.Move.AnimationStateName : null,
                    movement != null ? movement.MoveDuration : 0.25f,
                    data.Move != null && data.Move.Enabled);

                // 3. AttackPrepare
                AddCuePhase(intervals, ref cursor, ref anyFailure, controller, "AttackPrepare",
                    data.AttackPrepare, data.AttackPrepare?.Cues, 0d);

                // 4. Attack.Beats
                if (data.Attack != null && data.Attack.Enabled && data.Attack.Beats != null)
                {
                    for (int i = 0; i < data.Attack.Beats.Count; i++)
                    {
                        AttackBeat beat = data.Attack.Beats[i];
                        if (beat == null) continue;
                        AddResolved(intervals, ref cursor, ref anyFailure, controller,
                            $"Attack.Beats[{i}]", beat.AnimationStateName, beat.Cues, 0d);
                    }
                }
            }

            // 5. Return — 로코모션
            AddLocomotion(intervals, ref cursor, controller, "Return (복귀)",
                data.Return != null ? data.Return.AnimationStateName : null,
                movement != null ? movement.ReturnDuration : 0.3f,
                data.Return != null && data.Return.Enabled);

            // 6. Post — 클립 + ExtraDelay. ExtraDelay 구간에는 Cue를 놓을 수 없다.
            double extraDelay = data.Post != null ? Mathf.Max(0f, data.Post.ExtraDelay) : 0f;
            AddCuePhase(intervals, ref cursor, ref anyFailure, controller, "Post",
                data.Post, data.Post?.Cues, extraDelay);

            return intervals;
        }

        private static void AddCuePhase(List<JigPhaseInterval> list, ref double cursor, ref bool anyFailure,
            RuntimeAnimatorController controller, string label, CuePhase phase, List<CueBinding> cues,
            double trailingPadding)
        {
            if (phase == null || !phase.Enabled)
            {
                return;
            }
            AddResolved(list, ref cursor, ref anyFailure, controller, label, phase.AnimationStateName, cues, trailingPadding);
        }

        private static void AddResolved(List<JigPhaseInterval> list, ref double cursor, ref bool anyFailure,
            RuntimeAnimatorController controller, string label, string stateName, List<CueBinding> cues,
            double trailingPadding)
        {
            // state 이름이 비면 이 페이즈는 애니를 재생하지 않는다. Cue가 있어도 데이터 시각을 해석할 수 없다.
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
                    FailureReason = $"'{stateName}' — {r.FailureReason}",
                });
                // ★길이 0으로 이어 붙이지 않는다 — cursor를 진행시키지 않으면 뒤 구간의 Start도
                //   신뢰할 수 없게 되지만, 그 사실을 anyFailure로 알리고 역기입 자체를 막는다(§2.5.1).
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
                CueLimit = r.EffectiveLength * CueFireGuaranteedLimit,
            });
            cursor += r.EffectiveLength + trailingPadding;
        }

        /// <summary>
        /// 로코모션 구간(Move/Return). 구간 길이는 <b>이동 지속시간</b>이 정하고(클립 길이가 아니다 —
        /// MoveToTargetAction이 속도가 아니라 지속시간을 받는다), 클립은 포즈 프리뷰용으로만 표시한다.
        ///
        /// state 해석이 실패해도 <b>anyFailure를 세우지 않는다</b> — 이 구간에는 Cue가 없어
        /// 역기입 정확도에 영향이 없고, 구간 길이도 클립과 무관하므로 뒤 구간이 밀리지 않는다.
        /// 클립만 비어 프리뷰가 덜 보일 뿐이다.
        /// </summary>
        private static void AddLocomotion(List<JigPhaseInterval> list, ref double cursor,
            RuntimeAnimatorController controller, string label, string stateName, double duration, bool enabled)
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
                Resolved = true,          // 로코모션 해석 실패는 치명적이지 않다
                SupportsCues = false,     // MovePhase/ReturnPhase에 Cues 필드가 없다
                IsLocomotion = true,
                CueLimit = 0d,
                FailureReason = note,
            });
            cursor += duration;
        }
    }
}
