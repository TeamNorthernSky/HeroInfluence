using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// §3(빈 애니 커버리지) + §4(SkillTimelines 구조 검증 / ResolveTimeline 정책) 검증.
/// 런타임/에디터 타입은 Assembly-CSharp(-Editor)에 있어 asmdef가 직접 참조할 수 없으므로 reflection으로 접근한다.
/// </summary>
public class SkillTimelineValidatorTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    private static Type ValidatorType => FindRuntimeType("ASB.Work.EditorTools.Jig.SkillTimelineValidator");
    private static Type MessageType => FindRuntimeType("ASB.Work.EditorTools.Jig.SkillTimelineValidationMessage");
    private static Type DataType => FindRuntimeType("SkillPresentationData");
    private static Type RailType => FindRuntimeType("AnimationRail");
    private static Type BindingType => FindRuntimeType("SkillTimelineBinding");
    private static Type SegmentType => FindRuntimeType("SkillTimelineSegment");

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    // ── §3 커버리지 ──────────────────────────────────────────────

    [Test]
    public void Coverage_GapBeforeFirstClip_ReportsError()
    {
        TimelineAsset timeline = NewTimeline(1d);
        TimelineClip clip = AddClip(timeline, 0.3d, 0.7d); // [0.3 ~ 1.0] → [0 ~ 0.3] 시작 공백
        SetExtrapolation(clip, "None", "None");

        Assert.That(RunCoverage(timeline), Is.GreaterThan(0),
            "구간 시작~첫 클립 사이 공백(extrapolation 없음)은 빈 애니 Error여야 합니다.");
    }

    [Test]
    public void Coverage_GapCoveredByHoldExtrapolation_NoError()
    {
        TimelineAsset timeline = NewTimeline(1d);
        TimelineClip first = AddClip(timeline, 0d, 0.4d);
        SetExtrapolation(first, "None", "Hold"); // 0.4 이후를 Hold로 덮음
        TimelineClip second = AddClip(timeline, 0.6d, 0.4d);
        SetExtrapolation(second, "None", "None");

        Assert.That(RunCoverage(timeline), Is.EqualTo(0),
            "post-extrapolation Hold가 공백을 덮으면 오탐하면 안 됩니다.");
    }

    [Test]
    public void Coverage_FullClip_NoError()
    {
        TimelineAsset timeline = NewTimeline(1d);
        AddClip(timeline, 0d, 1d);
        Assert.That(RunCoverage(timeline), Is.EqualTo(0), "구간 전체를 덮는 단일 클립은 정상입니다.");
    }

    [Test]
    public void Coverage_EmptyAnimationTrack_ReportsError()
    {
        TimelineAsset timeline = NewTimeline(1d);
        GetOrCreateAnimationTrack(timeline); // 클립 없는 트랙
        Assert.That(RunCoverage(timeline), Is.GreaterThan(0), "클립 없는 트랙은 전 구간 공백입니다.");
    }

    [Test]
    public void Coverage_MaskedOverrideCannotHideBaseGap()
    {
        TimelineAsset timeline = NewTimeline(1d);
        TimelineClip baseClip = AddClip(timeline, 0d, 0.3d);
        SetExtrapolation(baseClip, "None", "None");
        AnimationTrack masked = timeline.CreateTrack<AnimationTrack>(GetOrCreateAnimationTrack(timeline), "Masked");
        masked.applyAvatarMask = true;
        masked.avatarMask = Track(new AvatarMask());
        var clip = Track(new AnimationClip { name = "mask" });
        TimelineClip filler = masked.CreateClip(clip);
        filler.start = 0d;
        filler.duration = 1d;
        Assert.That(RunCoverage(timeline), Is.GreaterThan(0));
    }

    // ── §4 SkillTimelines 구조 검증 ───────────────────────────────

    [Test]
    public void AnimatorRail_DanglingBinding_ReportsError()
    {
        ScriptableObject data = NewData(rail: "Animator");
        SetSkillTimelines(data, new (string, TimelineAsset)[] { ("블래스터", null) }); // null/dangling

        Assert.That(HasErrorContaining(Validate(data, "블래스터", null), "Timeline 참조가 비어"),
            "Animator Rail이라도 dangling 바인딩은 Error로 보고해야 합니다.");
    }

    [Test]
    public void AnimatorRail_EmptyBindings_NoError()
    {
        ScriptableObject data = NewData(rail: "Animator");
        SetSkillTimelines(data, Array.Empty<(string, TimelineAsset)>());
        Assert.That(CountErrors(Validate(data, "블래스터", null)), Is.EqualTo(0),
            "Animator Rail + 빈 SkillTimelines는 정상입니다.");
    }

    [Test]
    public void TimelineRail_EmptyBindings_ReportsError()
    {
        ScriptableObject data = NewData(rail: "Timeline");
        SetSkillTimelines(data, Array.Empty<(string, TimelineAsset)>());
        Assert.That(HasErrorContaining(Validate(data, "블래스터", null), "비어"),
            "Timeline Rail + 빈 SkillTimelines는 Error여야 합니다.");
    }

    // ── §4 ResolveTimeline 정책 ───────────────────────────────────

    [Test]
    public void ResolveTimeline_ExactKeyWinsOverWildcard()
    {
        TimelineAsset wild = NewTimeline(1d);
        TimelineAsset exact = NewTimeline(1d);
        ScriptableObject data = NewData(rail: "Timeline");
        SetSkillTimelines(data, new (string, TimelineAsset)[] { ("", wild), ("블래스터", exact) });

        Assert.That(ResolveTimeline(data, "블래스터"), Is.SameAs(exact), "정확 키가 wildcard보다 우선해야 합니다.");
        Assert.That(ResolveTimeline(data, "블래스터".ToUpperInvariant()), Is.SameAs(exact),
            "대소문자 무시 매칭이어야 합니다.");
        Assert.That(ResolveTimeline(data, "없는키"), Is.SameAs(wild), "없는 키는 wildcard로 폴백해야 합니다.");
    }

    [Test]
    public void ResolveTimeline_NullWildcard_DoesNotShadowExactKey()
    {
        TimelineAsset exact = NewTimeline(1d);
        ScriptableObject data = NewData(rail: "Timeline");
        SetSkillTimelines(data, new (string, TimelineAsset)[] { ("", null), ("블래스터", exact) });

        Assert.That(ResolveTimeline(data, "블래스터"), Is.SameAs(exact),
            "null wildcard가 유효한 정확 키를 가리면 안 됩니다.");
    }

    // ── Index 우선 조회(Index → unitName → wildcard) ───────────────

    [Test]
    public void ResolveTimelineByIndex_IndexWinsOverNameAndWildcard()
    {
        TimelineAsset wild = NewTimeline(1d);
        TimelineAsset byName = NewTimeline(1d);
        TimelineAsset byIndex = NewTimeline(1d);
        ScriptableObject data = NewData(rail: "Timeline");
        // 이름 항목을 앞에 둬도 Index 항목이 이겨야 한다(순서 무관).
        SetSkillTimelines(data, new (string, TimelineAsset)[] { ("", wild), ("블래스터", byName), ("10002", byIndex) });

        Assert.That(ResolveTimeline(data, "10002", "블래스터"), Is.SameAs(byIndex),
            "Index 일치가 이름 일치보다 우선해야 합니다.");
    }

    [Test]
    public void ResolveTimelineByIndex_FallsBackToName_ThenWildcard_ThenNull()
    {
        TimelineAsset wild = NewTimeline(1d);
        TimelineAsset byName = NewTimeline(1d);
        ScriptableObject data = NewData(rail: "Timeline");
        SetSkillTimelines(data, new (string, TimelineAsset)[] { ("블래스터", byName), ("", wild) });

        Assert.That(ResolveTimeline(data, "10002", "블래스터"), Is.SameAs(byName),
            "Index가 없으면 이름 일치로 폴백해야 합니다(과도기 호환).");
        Assert.That(ResolveTimeline(data, "20002", "빌런연합 소총수"), Is.SameAs(wild),
            "Index·이름 모두 없으면 wildcard로 폴백해야 합니다.");

        ScriptableObject noWild = NewData(rail: "Timeline");
        SetSkillTimelines(noWild, new (string, TimelineAsset)[] { ("블래스터", byName) });
        Assert.That(ResolveTimeline(noWild, "20002", "빌런연합 소총수"), Is.Null,
            "일치 항목도 wildcard도 없으면 null이어야 합니다.");
    }

    [Test]
    public void ResolveTimelineByIndex_TrimsAndIgnoresCase_EmptyIndexUsesName()
    {
        TimelineAsset byIndex = NewTimeline(1d);
        TimelineAsset byName = NewTimeline(1d);
        ScriptableObject data = NewData(rail: "Timeline");
        SetSkillTimelines(data, new (string, TimelineAsset)[] { (" 20002 ", byIndex), (" Villan_Gun ", byName) });

        Assert.That(ResolveTimeline(data, "20002  ", null), Is.SameAs(byIndex), "trim 후 Index가 일치해야 합니다.");
        Assert.That(ResolveTimeline(data, "", "villan_gun"), Is.SameAs(byName),
            "Index가 비어 있으면 대소문자 무시 이름 매칭을 해야 합니다.");
        Assert.That(ResolveTimeline(data, null, null), Is.Null, "키가 모두 없으면 wildcard가 없을 때 null이어야 합니다.");
    }

    [Test]
    public void Plan_ExactTargetCountThenDefaultThenNearestSmaller()
    {
        TimelineAsset one = NewTimeline(1d);
        TimelineAsset two = NewTimeline(1d);
        TimelineAsset fallback = NewTimeline(1d);
        ScriptableObject data = NewData("Timeline");
        SetPlanBindings(data, ("20005", 0, fallback), ("20005", 1, one), ("20005", 2, two));
        Assert.That(ResolvePlanFirst(data, "20005", 2), Is.SameAs(two));
        Assert.That(ResolvePlanFirst(data, "20005", 3), Is.SameAs(fallback));
        Assert.That(ResolveTimeline(data, "20005"), Is.SameAs(fallback));

        SetPlanBindings(data, ("20005", 1, one), ("20005", 2, two));
        Assert.That(ResolvePlanFirst(data, "20005", 3), Is.SameAs(two));
    }

    [Test]
    public void Plan_SegmentsAndKeyTierArePreserved()
    {
        TimelineAsset indexTimeline = NewTimeline(1d);
        TimelineAsset nameTimeline = NewTimeline(1d);
        ScriptableObject data = NewData("Timeline");
        SetPlanBindings(data, ("unitName", 2, nameTimeline), ("20005", 1, indexTimeline));
        Assert.That(ResolvePlanFirst(data, "20005", 2, "unitName"), Is.SameAs(indexTimeline));
        Assert.That((bool)DataType.GetMethod("HasMultiTargetPlan").Invoke(data,
            new object[] { "20005", "unitName" }), Is.True);
    }

    [Test]
    public void Bindings_OnlySameKeyAndTargetCountDuplicateIsError()
    {
        TimelineAsset timeline = NewTimeline(1d);
        AddClip(timeline, 0d, 1d);
        timeline.CreateMarkerTrack();
        ScriptableObject data = NewData("Timeline");
        SetPlanBindings(data, ("20005", 1, timeline), ("20005", 2, timeline));
        Assert.That(HasErrorContaining(Validate(data, "20005", timeline), "중복"), Is.False);
        SetPlanBindings(data, ("20005", 1, timeline), ("20005", 1, timeline));
        Assert.That(HasErrorContaining(Validate(data, "20005", timeline), "중복"), Is.True);
    }

    [Test]
    public void Plan_MoveSlotMismatch_ReportsError()
    {
        TimelineAsset timeline = NewTimeline(1d);
        AddClip(timeline, 0d, 1d);
        timeline.CreateMarkerTrack();
        AddMoveMarker(timeline, 0.1d, "Start", 2);
        AddMoveMarker(timeline, 0.5d, "End", 1);
        ScriptableObject data = NewData("Timeline");
        DataType.GetField("PresentationArchetype").SetValue(data,
            Enum.Parse(DataType.GetField("PresentationArchetype").FieldType, "Melee"));
        SetPlanBindings(data, ("20005", 2, timeline));
        Assert.That(HasErrorContaining(ValidatePlan(data, "20005", 2), "Start/End"), Is.True);
    }

    [Test]
    public void Plan_ProjectileMarker_ReportsError()
    {
        TimelineAsset timeline = NewTimeline(1d);
        AddClip(timeline, 0d, 1d);
        timeline.CreateMarkerTrack();
        AddSignalMarker(timeline, 0.2d, "Projectile", 0);
        ScriptableObject data = NewData("Timeline");
        SetPlanBindings(data, ("20005", 2, timeline));
        Assert.That(HasErrorContaining(ValidatePlan(data, "20005", 2), "Projectile"), Is.True);
    }

    [Test]
    public void BindingUsage_ValidatesRequestedBindingWithoutResolvingAnotherOne()
    {
        TimelineAsset valid = NewTimeline(1d);
        AddClip(valid, 0d, 1d);
        valid.CreateMarkerTrack();
        TimelineAsset invalid = NewTimeline(1d);
        invalid.CreateMarkerTrack();
        ScriptableObject data = NewData("Timeline");
        SetPlanBindings(data, ("20005", 1, valid), ("20005", 1, invalid));

        Assert.That(HasErrorContaining(ValidateBindingUsage(data, 0, 0), "Animation Track"), Is.False);
        Assert.That(HasErrorContaining(ValidateBindingUsage(data, 1, 0), "Animation Track"), Is.True);
    }

    [Test]
    public void ProvisionalPlan_AllowsHeldEffectToStopInLaterSegment()
    {
        ScriptableObject data = NewData("Timeline");
        AddCue(data, "spawn", "spawn-id", "Spawn", "trail");
        AddCue(data, "stop", "stop-id", "Stop", "trail");

        TimelineAsset first = NewTimeline(1d);
        AddClip(first, 0d, 1d);
        first.CreateMarkerTrack();
        AddCueMarker(first, 0.2d, "spawn", "spawn-id");
        TimelineAsset second = NewTimeline(1d);
        AddClip(second, 0d, 1d);
        second.CreateMarkerTrack();
        AddCueMarker(second, 0.2d, "stop", "stop-id");

        object oneSegment = NewSegmentList((first, 1));
        object twoSegments = NewSegmentList((first, 1), (second, 2));
        Assert.That(HasErrorContaining(ValidateProvisionalPlan(data, "20005", 2, oneSegment),
            "플랜 종료 시 held"), Is.True);
        Assert.That(HasErrorContaining(ValidateProvisionalPlan(data, "20005", 2, twoSegments),
            "플랜 종료 시 held"), Is.False);
    }

    [Test]
    public void BindingUsage_UsesWholePlanForHeldEffectFlow()
    {
        ScriptableObject data = NewData("Timeline");
        AddCue(data, "spawn", "spawn-id", "Spawn", "trail");
        AddCue(data, "stop", "stop-id", "Stop", "trail");

        TimelineAsset first = NewTimeline(1d);
        AddClip(first, 0d, 1d);
        first.CreateMarkerTrack();
        AddCueMarker(first, 0.2d, "spawn", "spawn-id");
        TimelineAsset second = NewTimeline(1d);
        AddClip(second, 0d, 1d);
        second.CreateMarkerTrack();
        AddCueMarker(second, 0.2d, "stop", "stop-id");
        SetSegmentPlanBinding(data, "20005", 2, (first, 1), (second, 2));

        Assert.That(HasErrorContaining(ValidateBindingUsage(data, 0, 1), "플랜 종료 시 held"),
            Is.False, "세그먼트 사용처 검증은 같은 바인딩의 다음 세그먼트 Stop까지 포함해야 합니다.");
    }

    [Test]
    public void Marker_Defaults_AreInheritedSlotAndMoveStart()
    {
        Type signalType = FindRuntimeType("PresentationSignalMarker");
        Type moveType = FindRuntimeType("PresentationMoveMarker");
        var signal = (ScriptableObject)ScriptableObject.CreateInstance(signalType);
        var move = (ScriptableObject)ScriptableObject.CreateInstance(moveType);
        _created.Add(signal);
        _created.Add(move);
        Assert.That((int)signalType.GetProperty("TargetSlot").GetValue(signal), Is.EqualTo(0));
        Assert.That(moveType.GetProperty("Boundary").GetValue(move).ToString(), Is.EqualTo("Start"));
        Assert.That((int)moveType.GetProperty("TargetSlot").GetValue(move), Is.EqualTo(0));
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────

    private int RunCoverage(TimelineAsset timeline)
    {
        MethodInfo cov = ValidatorType.GetMethod("ValidateAnimationCoverage",
            BindingFlags.NonPublic | BindingFlags.Static);
        object list = NewMessageList();
        cov.Invoke(null, new object[] { timeline, list });
        return CountErrors(list);
    }

    private object Validate(ScriptableObject data, string key, TimelineAsset timeline)
    {
        MethodInfo validate = ValidatorType.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static);
        return validate.Invoke(null, new object[] { data, key, timeline });
    }

    private static object NewMessageList()
    {
        Type listType = typeof(List<>).MakeGenericType(MessageType);
        return Activator.CreateInstance(listType);
    }

    private static int CountErrors(object messageList)
    {
        FieldInfo sevField = MessageType.GetField("Severity");
        int errors = 0;
        foreach (object message in (IEnumerable)messageList)
            if (sevField.GetValue(message).ToString() == "Error") errors++;
        return errors;
    }

    private static bool HasErrorContaining(object messageList, string fragment)
    {
        FieldInfo sevField = MessageType.GetField("Severity");
        FieldInfo msgField = MessageType.GetField("Message");
        foreach (object message in (IEnumerable)messageList)
            if (sevField.GetValue(message).ToString() == "Error"
                && ((string)msgField.GetValue(message)).Contains(fragment))
                return true;
        return false;
    }

    private ScriptableObject NewData(string rail)
    {
        ScriptableObject data = Track(ScriptableObject.CreateInstance(DataType));
        DataType.GetField("AnimationRail").SetValue(data, Enum.Parse(RailType, rail));
        return data;
    }

    private void SetSkillTimelines(ScriptableObject data, (string key, TimelineAsset timeline)[] entries)
    {
        Type listType = typeof(List<>).MakeGenericType(BindingType);
        var list = (IList)Activator.CreateInstance(listType);
        foreach ((string key, TimelineAsset timeline) in entries)
        {
            object binding = Activator.CreateInstance(BindingType);
            BindingType.GetField("CharacterKey").SetValue(binding, key);
            BindingType.GetField("Timeline").SetValue(binding, timeline);
            list.Add(binding);
        }
        DataType.GetField("SkillTimelines").SetValue(data, list);
    }

    private object ValidatePlan(ScriptableObject data, string key, int targetCount)
    {
        MethodInfo validate = ValidatorType.GetMethod("ValidatePlan", BindingFlags.Public | BindingFlags.Static);
        return validate.Invoke(null, new object[] { data, key, targetCount });
    }

    private object ValidateBindingUsage(ScriptableObject data, int bindingIndex, int segmentIndex)
    {
        MethodInfo validate = ValidatorType.GetMethod("ValidateBindingUsage",
            BindingFlags.Public | BindingFlags.Static);
        return validate.Invoke(null, new object[] { data, bindingIndex, segmentIndex });
    }

    private object ValidateProvisionalPlan(ScriptableObject data, string key, int targetCount,
        object segments)
    {
        MethodInfo validate = ValidatorType.GetMethod("ValidateProvisionalPlan",
            BindingFlags.Public | BindingFlags.Static);
        return validate.Invoke(null, new[] { (object)data, key, targetCount, segments });
    }

    private static object NewSegmentList(params (TimelineAsset timeline, int slot)[] entries)
    {
        Type listType = typeof(List<>).MakeGenericType(SegmentType);
        var list = (IList)Activator.CreateInstance(listType);
        foreach (var entry in entries)
        {
            object segment = Activator.CreateInstance(SegmentType);
            SegmentType.GetField("Timeline").SetValue(segment, entry.timeline);
            SegmentType.GetField("TargetSlot").SetValue(segment, entry.slot);
            list.Add(segment);
        }
        return list;
    }

    private static void AddCue(ScriptableObject data, string name, string id,
        string operation, string instanceKey)
    {
        Type cueType = FindRuntimeType("CueBinding");
        Type operationType = FindRuntimeType("CueOperation");
        object cue = Activator.CreateInstance(cueType);
        cueType.GetField("CueName").SetValue(cue, name);
        cueType.GetField("CueId").SetValue(cue, id);
        cueType.GetField("Operation").SetValue(cue, Enum.Parse(operationType, operation));
        cueType.GetField("InstanceKey").SetValue(cue, instanceKey);
        object phase = DataType.GetField("AttackPrepare").GetValue(data);
        var cues = (IList)phase.GetType().GetField("Cues").GetValue(phase);
        cues.Add(cue);
    }

    private static void AddCueMarker(TimelineAsset timeline, double time, string name, string id)
    {
        Type type = FindRuntimeType("PresentationSignalMarker");
        Type kindType = FindRuntimeType("PresentationSignalKind");
        object marker = AddMarker(timeline, type, time);
        type.GetMethod("Configure").Invoke(marker,
            new[] { Enum.Parse(kindType, "Cue"), (object)name, id, 0 });
    }

    private void SetPlanBindings(ScriptableObject data,
        params (string key, int count, TimelineAsset timeline)[] entries)
    {
        Type listType = typeof(List<>).MakeGenericType(BindingType);
        var list = (IList)Activator.CreateInstance(listType);
        foreach (var entry in entries)
        {
            object binding = Activator.CreateInstance(BindingType);
            BindingType.GetField("CharacterKey").SetValue(binding, entry.key);
            BindingType.GetField("TargetCount").SetValue(binding, entry.count);
            BindingType.GetField("Timeline").SetValue(binding, entry.timeline);
            list.Add(binding);
        }
        DataType.GetField("SkillTimelines").SetValue(data, list);
    }

    private static void SetSegmentPlanBinding(ScriptableObject data, string key, int count,
        params (TimelineAsset timeline, int slot)[] segments)
    {
        Type listType = typeof(List<>).MakeGenericType(BindingType);
        var bindings = (IList)Activator.CreateInstance(listType);
        object binding = Activator.CreateInstance(BindingType);
        BindingType.GetField("CharacterKey").SetValue(binding, key);
        BindingType.GetField("TargetCount").SetValue(binding, count);
        BindingType.GetField("Segments").SetValue(binding, NewSegmentList(segments));
        bindings.Add(binding);
        DataType.GetField("SkillTimelines").SetValue(data, bindings);
    }

    private static TimelineAsset ResolvePlanFirst(ScriptableObject data, string index, int count,
        string unitName = null)
    {
        Type listType = typeof(List<>).MakeGenericType(SegmentType);
        var destination = (IList)Activator.CreateInstance(listType);
        bool found = (bool)DataType.GetMethod("TryResolvePlan").Invoke(data,
            new object[] { index, unitName, count, destination });
        Assert.That(found, Is.True);
        return (TimelineAsset)SegmentType.GetField("Timeline").GetValue(destination[0]);
    }

    private static object AddMarker(TimelineAsset timeline, Type markerType, double time)
    {
        MethodInfo create = typeof(TrackAsset).GetMethods()
            .Single(method => method.Name == "CreateMarker" && method.IsGenericMethodDefinition);
        return create.MakeGenericMethod(markerType).Invoke(timeline.markerTrack, new object[] { time });
    }

    private static void AddMoveMarker(TimelineAsset timeline, double time, string boundary, int slot)
    {
        Type type = FindRuntimeType("PresentationMoveMarker");
        Type boundaryType = FindRuntimeType("PresentationSectionBoundary");
        object marker = AddMarker(timeline, type, time);
        type.GetMethod("Configure").Invoke(marker,
            new[] { Enum.Parse(boundaryType, boundary), (object)slot });
    }

    private static void AddSignalMarker(TimelineAsset timeline, double time, string kind, int slot)
    {
        Type type = FindRuntimeType("PresentationSignalMarker");
        Type kindType = FindRuntimeType("PresentationSignalKind");
        object marker = AddMarker(timeline, type, time);
        type.GetMethod("Configure").Invoke(marker,
            new[] { Enum.Parse(kindType, kind), null, null, (object)slot });
    }

    private static TimelineAsset ResolveTimeline(ScriptableObject data, string key)
    {
        // 오버로드(1인자/2인자)가 있으므로 시그니처를 명시한다.
        MethodInfo method = DataType.GetMethod("ResolveTimeline", new[] { typeof(string) });
        return (TimelineAsset)method.Invoke(data, new object[] { key });
    }

    private static TimelineAsset ResolveTimeline(ScriptableObject data, string templateIndex, string unitName)
    {
        MethodInfo method = DataType.GetMethod("ResolveTimeline", new[] { typeof(string), typeof(string) });
        return (TimelineAsset)method.Invoke(data, new object[] { templateIndex, unitName });
    }

    private TimelineAsset NewTimeline(double duration)
    {
        TimelineAsset timeline = Track(ScriptableObject.CreateInstance<TimelineAsset>());
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = duration;
        return timeline;
    }

    private AnimationTrack GetOrCreateAnimationTrack(TimelineAsset timeline)
    {
        foreach (TrackAsset track in timeline.GetOutputTracks())
            if (track is AnimationTrack existing) return existing;
        return timeline.CreateTrack<AnimationTrack>(null, "Anim");
    }

    private TimelineClip AddClip(TimelineAsset timeline, double start, double duration)
    {
        AnimationTrack track = GetOrCreateAnimationTrack(timeline);
        var animClip = Track(new AnimationClip { name = "cov", frameRate = 30f });
        animClip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x",
            AnimationCurve.Linear(0f, 0f, (float)duration, 0f));
        TimelineClip clip = track.CreateClip(animClip);
        clip.start = start;
        clip.duration = duration;
        return clip;
    }

    /// <summary>pre/post extrapolation은 public setter가 없어 리플렉션으로 설정한다.</summary>
    private static void SetExtrapolation(TimelineClip clip, string pre, string post)
    {
        SetExtrapProp(clip, "preExtrapolationMode", "m_PreExtrapolationMode", pre);
        SetExtrapProp(clip, "postExtrapolationMode", "m_PostExtrapolationMode", post);
    }

    private static void SetExtrapProp(TimelineClip clip, string propName, string fieldName, string mode)
    {
        object value = Enum.Parse(typeof(TimelineClip.ClipExtrapolation), mode);
        MethodInfo setter = typeof(TimelineClip).GetProperty(propName)?.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(clip, new object[] { value });
            return;
        }
        FieldInfo field = typeof(TimelineClip).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"{propName}를 설정할 방법을 찾지 못했습니다.");
        field.SetValue(clip, value);
    }

    private static Type FindRuntimeType(string fullName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName, false))
            .FirstOrDefault(t => t != null);
        Assert.That(type, Is.Not.Null, $"타입 '{fullName}'을 찾지 못했습니다.");
        return type;
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        _created.Add(value);
        return value;
    }
}
