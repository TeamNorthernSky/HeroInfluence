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

    private static TimelineAsset ResolveTimeline(ScriptableObject data, string key)
    {
        return (TimelineAsset)DataType.GetMethod("ResolveTimeline").Invoke(data, new object[] { key });
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
