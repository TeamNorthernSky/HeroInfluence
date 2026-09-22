using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Timeline 편집 지그의 순수 로직 계약을 고정한다.
///
/// 계약:
///   ① Cue 시각 ↔ 트랙 시각 왕복 변환은 항등이다 (역기입이 값을 훼손하지 않는다).
///   ② 역기입은 발화 보장 상한(0.95) 아래로 클램프한다.
///   ③ state 경로는 전체 세그먼트를 따라간다 — 짧은 이름 재귀 검색을 하지 않는다.
///   ④ 실효 길이는 state.speed를 반영한다.
///   ⑤ 해석 실패는 조용히 넘어가지 않는다(anyFailure).
///
/// 실제 Timeline 에셋 생성·마커 드래그·프레임 스냅은 여기서 재현할 수 없다 → 수동 확인 항목이다.
/// (지시서 Docs/SkillPresentation_TimelineJig_구현지시서.md §7.3)
///
/// 지그 코드는 Assembly-CSharp-Editor에 있고 asmdef 테스트 어셈블리는 그것을 참조할 수 없으므로
/// 기존 테스트들과 같은 리플렉션 방식을 쓴다.
/// </summary>
public sealed class PresentationTimelineJigTests
{
    private static Type LayoutType => FindRuntimeType("ASB.Work.EditorTools.Jig.JigPhaseLayout");
    private static Type ResolverType => FindRuntimeType("ASB.Work.EditorTools.Jig.JigStateResolver");
    private static Type IntervalType => FindRuntimeType("ASB.Work.EditorTools.Jig.JigPhaseInterval");
    private static Type MarkerType => FindRuntimeType("ASB.Work.EditorTools.Jig.CueMarker");
    private static Type TimingEnumType => FindRuntimeType("CueTimingSource");

    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _created.Count; i++)
        {
            if (_created[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_created[i]);
            }
        }
        _created.Clear();
    }

    // ──────────────────────────────────────────────────────────────
    // ① 왕복 변환 항등
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void RoundTrip_NormalizedTime_IsIdentity()
    {
        const double phaseStart = 0.5d;
        const double effLen = 1.2d;

        foreach (float original in new[] { 0f, 0.1f, 0.35f, 0.5f, 0.9f })
        {
            double markerTime = ToTimelineTime("NormalizedTime", original, phaseStart, effLen);
            float back = ToCueTime("NormalizedTime", markerTime, phaseStart, effLen, out bool clamped);

            Assert.That(clamped, Is.False, $"{original}는 클램프 대상이 아닙니다.");
            Assert.That(back, Is.EqualTo(original).Within(1e-5f),
                $"왕복 변환이 항등이어야 합니다. original={original} marker={markerTime} back={back}");
        }
    }

    [Test]
    public void RoundTrip_Seconds_IsIdentity()
    {
        const double phaseStart = 0.75d;
        const double effLen = 2.0d;

        foreach (float original in new[] { 0f, 0.2f, 0.4f, 1.9f })
        {
            double markerTime = ToTimelineTime("Seconds", original, phaseStart, effLen);
            float back = ToCueTime("Seconds", markerTime, phaseStart, effLen, out bool _);

            Assert.That(back, Is.EqualTo(original).Within(1e-5f),
                "Seconds는 클립 길이와 무관하게 항등이어야 합니다.");
        }
    }

    [Test]
    public void ToTimelineTime_NormalizedScalesWithEffectiveLength()
    {
        double a = ToTimelineTime("NormalizedTime", 0.25f, 0d, 2d);
        double b = ToTimelineTime("NormalizedTime", 0.25f, 0d, 4d);

        Assert.That(a, Is.EqualTo(0.5d).Within(1e-9));
        Assert.That(b, Is.EqualTo(1.0d).Within(1e-9),
            "같은 정규화 값이 실효 길이에 비례해야 합니다 — 배속·길이 불변성의 근거입니다.");
    }

    [Test]
    public void ToTimelineTime_AddsPhaseStart()
    {
        double t = ToTimelineTime("NormalizedTime", 0.5f, 3d, 2d);
        Assert.That(t, Is.EqualTo(4.0d).Within(1e-9),
            "기준점은 페이즈 진입 시각이므로 phaseStart가 더해져야 합니다.");
    }

    // ──────────────────────────────────────────────────────────────
    // ② 클램프
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void ToCueTime_ClampsAboveFireGuaranteedLimit()
    {
        const double effLen = 1.0d;
        // 0.98 지점으로 드래그 — 발화 보장 상한(0.95) 초과
        float result = ToCueTime("NormalizedTime", 0.98d, 0d, effLen, out bool clamped);

        Assert.That(clamped, Is.True, "상한 초과는 클램프 사실을 알려야 합니다.");
        Assert.That(result, Is.LessThan(GetConst<float>(LayoutType, "CueFireGuaranteedLimit")),
            "클램프 결과가 발화 보장 상한 미만이어야 합니다.");
        Assert.That(result, Is.EqualTo(GetConst<float>(LayoutType, "MaxWritableNormalizedTime")).Within(1e-6f));
    }

    [Test]
    public void MaxWritable_IsBelowGuaranteedLimit()
    {
        float writable = GetConst<float>(LayoutType, "MaxWritableNormalizedTime");
        float limit = GetConst<float>(LayoutType, "CueFireGuaranteedLimit");

        Assert.That(writable, Is.LessThan(limit),
            "역기입 최댓값이 상한 미만이어야 SkillPresentationData의 검증 경고를 유발하지 않습니다.");
    }

    [Test]
    public void ToCueTime_ClampsNegativeLocalTime()
    {
        // 마커를 페이즈 시작보다 앞으로 끌었을 때
        float result = ToCueTime("NormalizedTime", 0.2d, 1.0d, 1.0d, out bool clamped);

        Assert.That(clamped, Is.True);
        Assert.That(result, Is.EqualTo(0f).Within(1e-6f), "음수 시각은 0으로 클램프해야 합니다.");
    }

    [Test]
    public void ToCueTime_UnknownEffectiveLength_IsClampedToZero()
    {
        float result = ToCueTime("NormalizedTime", 0.5d, 0d, 0d, out bool clamped);

        Assert.That(clamped, Is.True, "길이를 모르면 변환이 불가하므로 클램프 사실을 알려야 합니다.");
        Assert.That(result, Is.EqualTo(0f).Within(1e-6f));
    }

    // ──────────────────────────────────────────────────────────────
    // ③ state 경로 파싱
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void SplitStatePath_SplitsEverySegment()
    {
        Assert.That(SplitStatePath("Base Layer.Combat.ClassSkill_1"),
            Is.EqualTo(new[] { "Base Layer", "Combat", "ClassSkill_1" }),
            "서브 스테이트머신을 따라가려면 모든 세그먼트가 필요합니다.");
    }

    [Test]
    public void SplitStatePath_TrimsSegments()
    {
        Assert.That(SplitStatePath("  Base Layer . ClassSkill_1  "),
            Is.EqualTo(new[] { "Base Layer", "ClassSkill_1" }));
    }

    [Test]
    public void SplitStatePath_ShortNameYieldsSingleSegment()
    {
        Assert.That(SplitStatePath("ClassSkill_1"), Is.EqualTo(new[] { "ClassSkill_1" }),
            "점이 없으면 레이어를 특정할 수 없으므로 한 세그먼트로 취급합니다.");
    }

    [Test]
    public void SplitStatePath_EmptyOrNull_YieldsNothing()
    {
        Assert.That(SplitStatePath(null), Is.Empty);
        Assert.That(SplitStatePath("   "), Is.Empty);
        Assert.That(SplitStatePath("..."), Is.Empty, "빈 세그먼트는 제거되어야 합니다.");
    }

    // ──────────────────────────────────────────────────────────────
    // ④ 실효 길이
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void ComputeEffectiveLength_DividesByStateSpeed()
    {
        Assert.That(ComputeEffectiveLength(1.2d, 1f), Is.EqualTo(1.2d).Within(1e-9));
        Assert.That(ComputeEffectiveLength(1.2d, 2f), Is.EqualTo(0.6d).Within(1e-9),
            "state.speed가 2면 실효 길이가 절반이어야 합니다 — 런타임 length와 어긋나면 마커가 전부 틀어집니다.");
    }

    [Test]
    public void ComputeEffectiveLength_ZeroSpeed_IsZero()
    {
        Assert.That(ComputeEffectiveLength(1.2d, 0f), Is.EqualTo(0d).Within(1e-9),
            "0으로 나누지 않고 0을 반환해야 합니다(호출자가 해석 불가로 처리).");
    }

    // ──────────────────────────────────────────────────────────────
    // ⑤ 해석 실패는 조용히 넘어가지 않는다
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void Build_CuePhaseWithoutStateName_ReportsFailure()
    {
        object data = NewPresentation();

        // AttackPrepare에 Cue는 있지만 AnimationStateName이 비어 있다 → 시각 해석 불가
        SetPhaseEnabled(data, "AttackPrepare", true);
        SetPhaseMember(data, "AttackPrepare", "AnimationStateName", string.Empty);
        SetPhaseCues(data, "AttackPrepare", NewCueBinding("cast", "NormalizedTime", 0.3f));

        List<object> intervals = Build(data, null, null, out bool anyFailure);

        Assert.That(anyFailure, Is.True,
            "Cue가 있는데 state를 해석할 수 없으면 실패로 보고해야 합니다 — 조용히 넘기면 역기입이 틀린 값을 씁니다.");
        Assert.That(intervals.Count, Is.EqualTo(1));
        Assert.That(GetField<bool>(intervals[0], "Resolved"), Is.False);
        Assert.That(GetField<string>(intervals[0], "FailureReason"), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Build_DisabledPhases_AreSkipped()
    {
        object data = NewPresentation();

        List<object> intervals = Build(data, null, null, out bool anyFailure);

        Assert.That(intervals, Is.Empty, "비활성 페이즈는 구간을 만들지 않아야 합니다.");
        Assert.That(anyFailure, Is.False);
    }

    [Test]
    public void Build_EmptyIntervals_AccumulateCursor()
    {
        object data = NewPresentation();

        // 애니가 필요한 페이즈는 모두 끈 상태(NewPresentation) + 로코모션 빈 구간만 켠다.
        SetPhaseEnabled(data, "Move", true);
        SetPhaseEnabled(data, "Return", true);

        var host = new GameObject("MovementProfileHost");
        _created.Add(host);
        Component movement = host.AddComponent(MovementType);
        MovementType.GetField("MoveDuration").SetValue(movement, 0.25f);
        MovementType.GetField("ReturnDuration").SetValue(movement, 0.5f);

        List<object> intervals = Build(data, null, movement, out bool anyFailure);

        Assert.That(anyFailure, Is.False);
        Assert.That(intervals.Count, Is.EqualTo(2));

        Assert.That(GetField<double>(intervals[0], "Start"), Is.EqualTo(0d).Within(1e-9));
        Assert.That(GetField<double>(intervals[0], "Duration"), Is.EqualTo(0.25d).Within(1e-6));

        Assert.That(GetField<double>(intervals[1], "Start"), Is.EqualTo(0.25d).Within(1e-6),
            "뒤 구간의 Start는 앞 구간 길이만큼 밀려야 합니다.");
        Assert.That(GetField<double>(intervals[1], "Duration"), Is.EqualTo(0.5d).Within(1e-6));

        Assert.That(GetField<bool>(intervals[0], "CanHostCues"), Is.False,
            "로코모션 빈 구간에는 Cue를 붙일 수 없습니다(MovePhase에 Cues 필드가 없음).");
    }

    // ──────────────────────────────────────────────────────────────
    // 마커 계약
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void CueMarker_CarriesGenerationSnapshot()
    {
        // 역기입이 phaseStart/길이를 재계산하지 않도록 생성 시점 값을 마커가 들고 있어야 한다.
        foreach (string field in new[] { "CueId", "PhaseStart", "EffectiveLength", "TimingSource", "FromClipEvent" })
        {
            Assert.That(MarkerType.GetField(field), Is.Not.Null,
                $"CueMarker.{field}가 있어야 역기입이 안전합니다.");
        }
    }

    [Test]
    public void CueMarker_DoesNotImplementINotification()
    {
        bool isNotification = MarkerType.GetInterfaces()
            .Any(i => i.FullName == "UnityEngine.Playables.INotification");

        Assert.That(isNotification, Is.False,
            "지그의 마커는 발화하지 않는다 — INotification을 구현하면 런타임 발화 경로가 열립니다.");
    }

    // ──────────────────────────────────────────────────────────────
    // 리플렉션 도우미
    // ──────────────────────────────────────────────────────────────

    private static Type FindRuntimeType(string fullName)
    {
        Type found = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.FullName == fullName);

        Assert.That(found, Is.Not.Null, $"타입을 찾지 못했습니다: {fullName}");
        return found;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
    }

    private static Type DataType => FindRuntimeType("SkillPresentationData");
    private static Type CueBindingType => FindRuntimeType("CueBinding");
    private static Type MovementType => FindRuntimeType("UnitMovementProfile");

    private static object Timing(string name) => Enum.Parse(TimingEnumType, name);

    /// <summary>모든 페이즈를 끈 Schema=1 연출 자산. 테스트가 필요한 페이즈만 다시 켠다.</summary>
    private object NewPresentation()
    {
        var data = ScriptableObject.CreateInstance(DataType);
        _created.Add(data);
        DataType.GetField("PresentationSchemaVersion").SetValue(data, 1);

        foreach (string phase in new[] { "MovePrepare", "Move", "AttackPrepare", "Attack", "Return", "Post" })
        {
            SetPhaseEnabled(data, phase, false);
        }
        return data;
    }

    private static object GetPhase(object data, string phaseField)
    {
        FieldInfo f = DataType.GetField(phaseField);
        Assert.That(f, Is.Not.Null, $"SkillPresentationData.{phaseField}를 찾지 못했습니다.");
        object phase = f.GetValue(data);
        Assert.That(phase, Is.Not.Null, $"{phaseField}가 null입니다.");
        return phase;
    }

    private static void SetPhaseEnabled(object data, string phaseField, bool enabled)
    {
        object phase = GetPhase(data, phaseField);
        FieldInfo f = phase.GetType().GetField("Enabled");
        Assert.That(f, Is.Not.Null, $"{phaseField}.Enabled를 찾지 못했습니다.");
        f.SetValue(phase, enabled);
    }

    private static void SetPhaseMember(object data, string phaseField, string member, object value)
    {
        object phase = GetPhase(data, phaseField);
        FieldInfo f = phase.GetType().GetField(member);
        Assert.That(f, Is.Not.Null, $"{phaseField}.{member}를 찾지 못했습니다.");
        f.SetValue(phase, value);
    }

    private static void SetPhaseCues(object data, string phaseField, params object[] cues)
    {
        object phase = GetPhase(data, phaseField);
        FieldInfo f = phase.GetType().GetField("Cues");
        Assert.That(f, Is.Not.Null, $"{phaseField}.Cues를 찾지 못했습니다.");

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(CueBindingType));
        foreach (object cue in cues) list.Add(cue);
        f.SetValue(phase, list);
    }

    private static object NewCueBinding(string cueName, string timing, float time)
    {
        object cue = Activator.CreateInstance(CueBindingType);
        CueBindingType.GetField("CueName").SetValue(cue, cueName);
        CueBindingType.GetField("Timing").SetValue(cue, Timing(timing));
        CueBindingType.GetField("Time").SetValue(cue, time);
        return cue;
    }

    private static double ToTimelineTime(string timing, float time, double phaseStart, double effLen)
    {
        return (double)LayoutType.GetMethod("ToTimelineTime", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { Timing(timing), time, phaseStart, effLen });
    }

    private static float ToCueTime(string timing, double markerTime, double phaseStart, double effLen, out bool clamped)
    {
        var args = new object[] { Timing(timing), markerTime, phaseStart, effLen, false };
        float result = (float)LayoutType.GetMethod("ToCueTime", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, args);
        clamped = (bool)args[4];
        return result;
    }

    private static string[] SplitStatePath(string path)
    {
        return (string[])ResolverType.GetMethod("SplitStatePath", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { path });
    }

    private static double ComputeEffectiveLength(double clipLength, float speed)
    {
        return (double)ResolverType.GetMethod("ComputeEffectiveLength", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { clipLength, speed });
    }

    private static List<object> Build(object data, object controller, object movement, out bool anyFailure)
    {
        MethodInfo build = LayoutType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        Assert.That(build, Is.Not.Null, "JigPhaseLayout.Build를 찾지 못했습니다.");

        var args = new object[] { data, controller, movement, false };
        var list = (IEnumerable)build.Invoke(null, args);
        anyFailure = (bool)args[3];

        var result = new List<object>();
        foreach (object item in list) result.Add(item);
        return result;
    }

    private static T GetField<T>(object target, string name)
    {
        FieldInfo f = IntervalType.GetField(name);
        if (f != null) return (T)f.GetValue(target);

        PropertyInfo p = IntervalType.GetProperty(name);
        Assert.That(p, Is.Not.Null, $"JigPhaseInterval.{name}을 찾지 못했습니다.");
        return (T)p.GetValue(target);
    }

    private static T GetConst<T>(Type type, string name)
    {
        FieldInfo f = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
        Assert.That(f, Is.Not.Null, $"{type.Name}.{name}을 찾지 못했습니다.");
        return (T)f.GetValue(null);
    }
}
