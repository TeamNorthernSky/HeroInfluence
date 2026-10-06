using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using ASB.Work.Battle.Core;
using ASB.Work.Battle.Sequence;
using ASB.Work.Battle.SkillExecution;

public sealed partial class SkillPresentationDirector
{
    private sealed class PlanMoveSpan
    {
        public PresentationMoveMarker Start;
        public PresentationMoveMarker End;
        public int Slot;
        public Vector3 From;
        public Vector3 To;
        public Quaternion FromRotation;
        public Quaternion ToRotation;
        public bool Started;
        public bool Completed;
    }

    public bool CanRunMultiTargetPlan(BattleCharactor actor, SkillData skill,
        IReadOnlyList<DamageContext> slotContexts)
    {
        if (actor == null || skill == null || slotContexts == null || slotContexts.Count == 0 ||
            ChainState != null || _battle.PresentationCatalog == null ||
            _battle.PresentationCatalog.ForceAnimatorRail ||
            SkillPresentationSequenceRegistry.TryGet(skill.skillKey, out _))
            return false;

        SkillPresentationData presentation = _battle.PresentationCatalog.Get(skill.skillIndex);
        if (presentation == null || !presentation.IsTimelineRail ||
            (presentation.PresentationArchetype != PresentationArchetype.Stationary &&
             presentation.PresentationArchetype != PresentationArchetype.Melee) ||
            !presentation.HasMultiTargetPlan(actor.TemplateIndex, actor.UnitName))
            return false;

        for (int i = 0; i < slotContexts.Count; i++)
        {
            DamageContext context = slotContexts[i];
            if (context == null || context.Caster != actor || context.SkillIndex != skill.skillIndex)
                return false;
        }
        return true;
    }

    public IEnumerator RunMultiTargetSkillSequence(BattleCharactor actor, SkillData skill,
        IReadOnlyList<DamageContext> slotContexts,
        IReadOnlyList<Func<BattleHitResult>> slotCallbacks)
    {
        yield return TrackSequence(RunMultiTargetSkillSequenceInternal(actor, skill, slotContexts, slotCallbacks));
    }

    private IEnumerator RunMultiTargetSkillSequenceInternal(BattleCharactor actor, SkillData skill,
        IReadOnlyList<DamageContext> slotContexts,
        IReadOnlyList<Func<BattleHitResult>> slotCallbacks)
    {
        int count = slotContexts != null ? slotContexts.Count : 0;
        if (count == 0 || slotCallbacks == null || slotCallbacks.Count != count) yield break;

        if (actor == null)
        {
            for (int i = 0; i < count; i++) slotCallbacks[i]?.Invoke();
            yield break;
        }

        skill = ResolveSkillAnimationData(skill);
        ApplyPresentationOverride(skill);
        actor.EnsureAnimationController();
        actor.Anim?.SetAnimationSpeed(_battle.CurrentBattleSpeed);
        SkillPresentationData presentation = skill != null ? _battle.PresentationCatalog?.Get(skill.skillIndex) : null;
        var segments = new List<SkillTimelineSegment>();
        bool validPlan = presentation != null &&
            presentation.TryResolvePlan(actor.TemplateIndex, actor.UnitName, count, segments);
        if (validPlan)
        {
            segments.RemoveAll(segment => segment.TargetSlot > count);
            validPlan = segments.Count > 0;
        }

        var moveSpansBySegment = new List<List<PlanMoveSpan>>(segments.Count);
        if (validPlan)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                TimelineAsset timeline = segments[i].Timeline;
                List<PlanMoveSpan> spans = CollectPlanMoveSpans(timeline, segments[i].TargetSlot);
                if (timeline == null || timeline.duration <= 0d || spans == null)
                {
                    validPlan = false;
                    break;
                }
                for (int m = 0; m < spans.Count; m++)
                {
                    if (spans[m].Slot < 1 || spans[m].Slot > count ||
                        presentation.PresentationArchetype != PresentationArchetype.Melee)
                    {
                        validPlan = false;
                        break;
                    }
                }
                if (!validPlan) break;
                moveSpansBySegment.Add(spans);
            }
        }

        string targetTrigger = skill?.ResolvedTargetAnimationTrigger ?? "Hit";
        if (!validPlan)
        {
            Debug.LogError($"[PathA] Multi-target plan missing or invalid. skill={skill?.skillIndex}, " +
                $"actor={actor.TemplateIndex}/{actor.UnitName}, targets={count}", actor);
            for (int i = 0; i < count; i++)
            {
                var hit = new ResolveHitAction(actor, slotContexts[i]?.Target, slotCallbacks[i],
                    targetTrigger, _battle.CurrentBattleSpeed, _battle.VisualDirector);
                if (presentation?.ImpactTiming == TimelineImpactTiming.MarkerFrame) hit.ExecuteImmediate();
                else yield return _battle.StartCoroutine(hit.ExecuteRoutine(_battle));
            }
            yield break;
        }

        BattleCharactor primary = slotContexts[0].Target;
        bool melee = presentation.PresentationArchetype == PresentationArchetype.Melee;
        ResolveSkillMovement(actor, primary, skill, out UnitMovementProfile movement,
            out bool shouldMove, out bool shouldRotate, out Vector3 originPosition,
            out float originRotationY, primary != null ? primary.transform : null);
        bool moveEnabled = presentation.Move == null || presentation.Move.Enabled;
        if (melee && moveEnabled)
        {
            var approach = new ActionSequenceRunner();
            EnqueueSkillApproach(approach, actor, primary, actor.Anim, movement, shouldMove,
                shouldRotate, presentation.Move);
            yield return _battle.StartCoroutine(approach.RunAll(_battle));
        }

        // 비-Melee는 Animator 레일과 같은 회전 규칙: Move가 켜진 회전형 유닛만 대상 쪽으로 돌고 끝나면 복귀한다.
        // Move가 꺼진 스킬(예: 3030 더블 공격)은 대상 쪽으로 돌지 않는다.
        bool rotateForPlan = !melee && moveEnabled && shouldRotate;
        bool faceSegmentTargets = melee || rotateForPlan;
        if (rotateForPlan)
        {
            var approach = new ActionSequenceRunner();
            EnqueueSkillApproach(approach, actor, primary, actor.Anim, movement, false, true, presentation.Move);
            yield return _battle.StartCoroutine(approach.RunAll(_battle));
        }

        Animator animator = actor.Anim != null ? actor.Anim.Animator : null;
        if (animator != null)
        {
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
        }

        int actionId = NextActionInstanceId();
        RegisterTimelineRailCues(actor, primary, skill, presentation, actionId);
        PlayableDirector director = EnsureTimelineDirector(actor);
        director.extrapolationMode = DirectorWrapMode.Hold;
        PresentationSignalReceiver receiver = EnsureSignalReceiver(actor);
        UnitAnimationEventRouter router = actor.GetComponent<UnitAnimationEventRouter>();
        if (router != null) router.SuppressClipCues = true;

        var resolved = new bool[count];
        var hitRoutines = new List<Coroutine>();
        int segmentSlot = 1;
        List<PlanMoveSpan> activeSpans = null;
        PresentationInterruptReason interrupt = PresentationInterruptReason.None;

        void ResolveSlot(int markerSlot)
        {
            int slot = markerSlot > 0 ? markerSlot : segmentSlot;
            if (slot < 1 || slot > count || resolved[slot - 1]) return;
            resolved[slot - 1] = true;
            var hit = new ResolveHitAction(actor, slotContexts[slot - 1]?.Target,
                slotCallbacks[slot - 1], targetTrigger, _battle.CurrentBattleSpeed,
                _battle.VisualDirector);
            if (presentation.ImpactTiming == TimelineImpactTiming.MarkerFrame)
                hit.ExecuteImmediate();
            else
                hitRoutines.Add(_battle.StartCoroutine(hit.ExecuteRoutine(_battle)));
        }

        void OnMove(PresentationMoveMarker marker)
        {
            if (activeSpans == null || marker == null) return;
            for (int i = 0; i < activeSpans.Count; i++)
            {
                PlanMoveSpan span = activeSpans[i];
                if (marker == span.Start && !span.Started)
                {
                    BattleCharactor target = slotContexts[span.Slot - 1]?.Target;
                    if (target == null) return;
                    span.Started = true;
                    span.From = actor.transform.position;
                    span.FromRotation = actor.transform.rotation;
                    Vector3 away = span.From - target.transform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.0001f) away = -actor.transform.forward;
                    away.Normalize();
                    span.To = target.transform.position + away * (movement != null ? movement.ApproachDistance : 1.2f);
                    span.To.y = span.From.y;
                    span.ToRotation = Quaternion.LookRotation(-away, Vector3.up);
                    RetargetPlanContext(actor, target);
                    return;
                }
                if (marker == span.End && span.Started && !span.Completed)
                {
                    actor.transform.SetPositionAndRotation(span.To, span.ToRotation);
                    span.Completed = true;
                    return;
                }
            }
        }

        void OnInterrupt(PresentationInterruptReason reason)
        {
            if (reason == PresentationInterruptReason.None) return;
            interrupt = reason;
            if (director != null) director.Stop();
        }

        actor.PresentationInterruptRequested += OnInterrupt;
        try
        {
            for (int s = 0; s < segments.Count && interrupt == PresentationInterruptReason.None &&
                    actor != null && !actor.IsDead; s++)
            {
                SkillTimelineSegment segment = segments[s];
                TimelineAsset timeline = segment.Timeline;
                segmentSlot = segment.TargetSlot;
                activeSpans = moveSpansBySegment[s];
                BattleCharactor target = slotContexts[segmentSlot - 1]?.Target;
                RetargetPlanContext(actor, target);
                // 이 세그먼트의 Move가 같은 대상으로 가면 회전은 Move 보간이 맡는다(시작 프레임 스냅 방지).
                if (faceSegmentTargets && !MovesToSlot(activeSpans, segmentSlot)) FacePlanTarget(actor, target);
                // Rebuild the graph on this same frame. Play() on an already Playing director
                // can otherwise keep the previous segment's graph on some Unity versions.
                if (s > 0) director.Stop();
                receiver.ConfigureForPlan(router, ResolveSlot, OnMove, 0d, timeline.duration);
                BindTimelineToActor(director, timeline, actor);
                director.Play();
                director.time = 0d;
                director.Evaluate();

                List<KeyValuePair<double, float>> holds = CollectHoldMarks(timeline);
                var holdDone = new bool[holds.Count];
                double previousTime = 0d;
                while (actor != null && !actor.IsDead && interrupt == PresentationInterruptReason.None)
                {
                    double now = director.time;
                    UpdatePlanMove(actor, activeSpans, now);
                    for (int h = 0; h < holds.Count; h++)
                    {
                        if (holdDone[h] || holds[h].Key <= previousTime + 0.0001d ||
                            holds[h].Key > now + 0.0001d) continue;
                        holdDone[h] = true;
                        yield return FreezeForHoldRoutine(director, holds[h].Value,
                            () => actor == null || actor.IsDead || interrupt != PresentationInterruptReason.None);
                    }
                    ApplyBattleSpeedToDirector(director,
                        _battle.CurrentBattleSpeed * PresentationTimelineSpeed.SpeedAt(timeline, now));
                    previousTime = now;
                    if (now >= timeline.duration - 0.0001d) break;
                    yield return null;
                }

                SnapUnfinishedPlanMoves(actor, activeSpans);
                receiver.ClearConfig();
                activeSpans = null;
            }

            if (actor != null && !actor.IsDead && interrupt == PresentationInterruptReason.None)
            {
                yield return HandoffMultiTargetPlan(actor, director, presentation, melee,
                    movement, shouldMove, shouldRotate, originPosition, originRotationY);

                // 비-Melee 회전 복귀: 핸드오프로 Idle이 된 상태를 유지한 채 원래 방향으로 돌아온다(빈 MoveReturn 미사용).
                if (rotateForPlan && actor != null && !actor.IsDead)
                {
                    var tail = new ActionSequenceRunner();
                    EnqueueSkillReturn(tail, actor.Anim, movement, false, true, originPosition, originRotationY,
                        presentation.Return, keepCurrentAnimation: true);
                    yield return _battle.StartCoroutine(tail.RunAll(_battle));
                }
            }
        }
        finally
        {
            if (actor != null) actor.PresentationInterruptRequested -= OnInterrupt;
            director.Stop();
            director.playableAsset = null;
            director.extrapolationMode = DirectorWrapMode.None;
            receiver.ClearConfig();
            if (router != null) router.SuppressClipCues = false;
            ClearPresentationContext(actor);
        }

        for (int i = 0; i < count; i++)
            if (!resolved[i]) ResolveSlot(i + 1);
        for (int i = 0; i < hitRoutines.Count; i++)
            if (hitRoutines[i] != null) yield return hitRoutines[i];
    }

    private IEnumerator HandoffMultiTargetPlan(BattleCharactor actor, PlayableDirector director,
        SkillPresentationData presentation, bool melee, UnitMovementProfile movement,
        bool shouldMove, bool shouldRotate, Vector3 originPosition, float originRotationY)
    {
        bool returnMovement = melee && (shouldMove || shouldRotate) &&
            (presentation.Return == null || presentation.Return.Enabled);
        string state = returnMovement && !string.IsNullOrWhiteSpace(presentation.Return?.AnimationStateName)
            ? presentation.Return.AnimationStateName.Trim()
            : returnMovement ? "MoveReturn" : "Idle";
        Animator animator = actor.Anim != null ? actor.Anim.Animator : null;
        if (animator != null)
        {
            animator.Play(state, 0, 0f);
            animator.Update(0f);
        }

        // Unity 2022.3 project probe: lowering AnimationPlayableOutput weight to zero
        // retained the Timeline pose. Use the documented same-frame handoff fallback.
        director.Stop();
        director.playableAsset = null;
        director.extrapolationMode = DirectorWrapMode.None;

        Coroutine returnRoutine = null;
        if (returnMovement)
        {
            var tail = new ActionSequenceRunner();
            EnqueueSkillReturn(tail, actor.Anim, movement, shouldMove, shouldRotate,
                originPosition, originRotationY, presentation.Return, keepCurrentAnimation: true);
            returnRoutine = _battle.StartCoroutine(tail.RunAll(_battle));
        }

        if (returnRoutine != null) yield return returnRoutine;
        if (actor != null && !actor.IsDead && melee) actor.Anim?.PlayIdleAnimation();
    }

    private static void FacePlanTarget(BattleCharactor actor, BattleCharactor target)
    {
        if (actor == null || target == null) return;
        Vector3 direction = target.transform.position - actor.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            actor.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static void RetargetPlanContext(BattleCharactor actor, BattleCharactor target)
    {
        PresentationRuntimeContext runtime = actor != null ? actor.GetComponent<PresentationRuntimeContext>() : null;
        SkillEffectContext context = runtime != null ? runtime.Current : null;
        if (context == null) return;
        context.PrimaryTarget = target;
        context.Targets = target != null ? new List<BattleCharactor> { target } : null;
        context.TargetPosition = target != null ? target.transform.position : actor.transform.position;
    }

    private static List<PlanMoveSpan> CollectPlanMoveSpans(TimelineAsset timeline, int segmentSlot)
    {
        var spans = new List<PlanMoveSpan>();
        if (timeline == null || timeline.markerTrack == null) return spans;
        var markers = new List<PresentationMoveMarker>();
        foreach (IMarker marker in timeline.markerTrack.GetMarkers())
            if (marker is PresentationMoveMarker move) markers.Add(move);
        markers.Sort((a, b) => a.time.CompareTo(b.time));
        PresentationMoveMarker start = null;
        foreach (PresentationMoveMarker marker in markers)
        {
            if (marker.Boundary == PresentationSectionBoundary.Start)
            {
                if (start != null) return null;
                start = marker;
                continue;
            }
            if (start == null || marker.time <= start.time) return null;
            int startSlot = start.TargetSlot > 0 ? start.TargetSlot : segmentSlot;
            int endSlot = marker.TargetSlot > 0 ? marker.TargetSlot : segmentSlot;
            if (startSlot != endSlot) return null;
            spans.Add(new PlanMoveSpan { Start = start, End = marker, Slot = startSlot });
            start = null;
        }
        return start == null ? spans : null;
    }

    private static void UpdatePlanMove(BattleCharactor actor, List<PlanMoveSpan> spans, double time)
    {
        if (actor == null || spans == null) return;
        for (int i = 0; i < spans.Count; i++)
        {
            PlanMoveSpan span = spans[i];
            if (!span.Started || span.Completed) continue;
            float t = Mathf.Clamp01((float)((time - span.Start.time) / (span.End.time - span.Start.time)));
            actor.transform.SetPositionAndRotation(
                Vector3.Lerp(span.From, span.To, span.Start.EvaluatePosition(t)),
                Quaternion.Slerp(span.FromRotation, span.ToRotation, span.Start.EvaluateRotation(t)));
        }
    }

    private static bool MovesToSlot(List<PlanMoveSpan> spans, int slot)
    {
        if (spans == null) return false;
        for (int i = 0; i < spans.Count; i++)
            if (spans[i].Slot == slot) return true;
        return false;
    }

    private static void SnapUnfinishedPlanMoves(BattleCharactor actor, List<PlanMoveSpan> spans)
    {
        if (actor == null || spans == null) return;
        for (int i = 0; i < spans.Count; i++)
        {
            PlanMoveSpan span = spans[i];
            if (!span.Started || span.Completed) continue;
            actor.transform.SetPositionAndRotation(span.To, span.ToRotation);
            span.Completed = true;
        }
    }
}
