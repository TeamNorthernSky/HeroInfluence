using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 시간 기반 Cue 발화와 State 게이트의 순수 로직 계약을 고정한다.
///
/// 계약:
///   ① Cue는 등록되어 있다는 이유만으로 발화하지 않는다 — 기대 Animator state에 진입해야 한다.
///   ② 한 상태 진입당 최대 한 번 발화한다(시간 기반 경로).
///   ③ 정규화 시각은 클립 길이에 비례하므로 배속·클립 길이 변화에 불변이다.
///   ④ 같은 프레임에 여러 Cue가 걸리면 선언 순서로 발화한다.
///
/// 실제 Animator 전환·배속·홀드·CrossFade는 여기서 재현할 수 없다 → PlayMode/PreviewScene 검증이 별도로 필요하다.
/// (지시서 Docs/SkillPresentation_CueTiming_구현지시서.md §9)
///
/// 런타임 코드는 Assembly-CSharp에 있고 asmdef 테스트 어셈블리는 그것을 참조할 수 없으므로
/// HitDeliveryGateContractTests와 같은 리플렉션 방식을 쓴다.
/// </summary>
public sealed class PresentationCueTimingTests
{
    private static Type DriverType => FindRuntimeType("PresentationCueDriver");
    private static Type RuntimeCueType => FindRuntimeType("RuntimeCue");
    private static Type ContextType => FindRuntimeType("PresentationRuntimeContext");
    private static Type TimingEnumType => FindRuntimeType("CueTimingSource");

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_spawned[i]);
            }
        }
        _spawned.Clear();
    }

    // ──────────────────────────────────────────────────────────────
    // ③ 발화 시각 해석
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void NormalizedTime_ScalesWithClipLength()
    {
        object cue = NewCue("impact", "NormalizedTime", 0.25f);

        Assert.That(ResolveFireSeconds(cue, 2f), Is.EqualTo(0.5f).Within(1e-5f),
            "정규화 0.25는 2초 클립에서 0.5초여야 합니다.");
        Assert.That(ResolveFireSeconds(cue, 4f), Is.EqualTo(1f).Within(1e-5f),
            "같은 정규화 값이 클립 길이에 비례해야 합니다 — 이것이 배속·길이 불변성의 근거입니다.");
    }

    [Test]
    public void Seconds_IgnoresClipLength()
    {
        object cue = NewCue("impact", "Seconds", 0.4f);

        Assert.That(ResolveFireSeconds(cue, 2f), Is.EqualTo(0.4f).Within(1e-5f));
        Assert.That(ResolveFireSeconds(cue, 9f), Is.EqualTo(0.4f).Within(1e-5f),
            "Seconds는 절대 시각이므로 클립 길이와 무관해야 합니다.");
    }

    [Test]
    public void NormalizedTime_WithUnknownClipLength_IsNotFireable()
    {
        object cue = NewCue("impact", "NormalizedTime", 0.5f);

        Assert.That(ResolveFireSeconds(cue, 0f), Is.LessThan(0f),
            "클립 길이를 모르면 정규화 시각을 해석할 수 없으므로 발화 불가(음수)여야 합니다.");
    }

    [Test]
    public void ClipEventTiming_IsNotDataTimed()
    {
        object clipEventCue = NewCue("impact", "ClipEvent", 0.5f);
        object dataCue = NewCue("impact", "NormalizedTime", 0.5f);

        Assert.That(IsDataTimed(clipEventCue), Is.False,
            "ClipEvent는 클립 이벤트가 발화하므로 드라이버가 건드리면 이중 발화가 됩니다.");
        Assert.That(IsDataTimed(dataCue), Is.True);
    }

    // ──────────────────────────────────────────────────────────────
    // ② 프레임 교차 판정 — 한 진입당 한 번
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void CrossedFireTime_FiresExactlyOnceAcrossFrames()
    {
        // 0.5초 지점을 0.4 → 0.6 프레임 사이에 지난다.
        Assert.That(Crossed(0.4f, 0.6f, 0.5f), Is.True, "구간을 지나는 프레임에 발화해야 합니다.");
        Assert.That(Crossed(0.6f, 0.8f, 0.5f), Is.False, "이미 지난 시각은 다시 발화하지 않아야 합니다.");
        Assert.That(Crossed(0.2f, 0.4f, 0.5f), Is.False, "아직 도달하지 않았으면 발화하지 않아야 합니다.");
    }

    [Test]
    public void CrossedFireTime_IsInclusiveAtUpperBoundOnly()
    {
        Assert.That(Crossed(0.4f, 0.5f, 0.5f), Is.True, "구간 끝(seconds == target)은 포함해야 합니다.");
        Assert.That(Crossed(0.5f, 0.7f, 0.5f), Is.False,
            "구간 시작(prevSeconds == target)은 제외해야 합니다 — 포함하면 두 프레임 연속 발화합니다.");
    }

    [Test]
    public void CrossedFireTime_AfterReentry_CatchesPastDueCue()
    {
        // 재진입 직후 prevSeconds = -1. 첫 관측이 이미 지난 시각이어도 놓치지 않아야 한다.
        Assert.That(Crossed(-1f, 0.03f, 0f), Is.True, "시각 0의 Cue는 첫 프레임에 발화해야 합니다.");
        Assert.That(Crossed(-1f, 0.30f, 0.10f), Is.True,
            "첫 관측이 이미 지난 시각이어도(프레임 드랍 등) 발화해야 합니다.");
    }

    [Test]
    public void CrossedFireTime_NegativeTarget_NeverFires()
    {
        Assert.That(Crossed(-1f, 99f, -1f), Is.False,
            "해석 불가(음수) 시각은 어떤 구간에서도 발화하지 않아야 합니다.");
    }

    // ──────────────────────────────────────────────────────────────
    // ① State 게이트
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void StateHashMatches_AcceptsFullPathOrShortName()
    {
        int shortHash = Animator.StringToHash("ClassSkill_1");
        int fullHash = Animator.StringToHash("Base Layer.ClassSkill_1");

        Assert.That(StateHashMatches(fullHash, shortHash, shortHash), Is.True,
            "AnimationStateName이 짧은 이름으로 저장된 자산을 받아야 합니다.");
        Assert.That(StateHashMatches(fullHash, shortHash, fullHash), Is.True,
            "AnimationStateName이 전체 경로로 저장된 자산도 받아야 합니다.");
        Assert.That(StateHashMatches(fullHash, shortHash, Animator.StringToHash("Idle")), Is.False,
            "무관한 state는 거부해야 합니다.");
    }

    [Test]
    public void GateDefaultsToDiagnostic()
    {
        FieldInfo mode = DriverType.GetField("GateMode", BindingFlags.Public | BindingFlags.Static);
        Assert.That(mode, Is.Not.Null, "GateMode 정적 필드가 있어야 합니다.");
        Assert.That(mode.GetValue(null).ToString(), Is.EqualTo("Diagnostic"),
            "도입 단계 기본값은 Diagnostic이어야 합니다 — 바로 차단하면 지금 동작하는 Cue가 침묵할 수 있습니다.");
    }

    // ──────────────────────────────────────────────────────────────
    // ④ 등록 순서 보존 / 중복 처리 / 게이트 상태 저장
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void SetActive_PreservesDeclarationOrder()
    {
        object ctx = NewContext(out GameObject _);
        var cues = NewCueList(
            NewCue("cast", "NormalizedTime", 0.1f),
            NewCue("fire", "NormalizedTime", 0.2f),
            NewCue("impact", "NormalizedTime", 0.3f));

        InvokeSetActive(ctx, 7, cues, 1234, "test");

        Assert.That(OrderedCueNames(ctx), Is.EqualTo(new[] { "cast", "fire", "impact" }),
            "같은 프레임에 여러 Cue가 걸릴 때 선언 순서로 발화하려면 순서가 보존되어야 합니다.");
    }

    [Test]
    public void SetActive_DropsDuplicateCueNames()
    {
        object ctx = NewContext(out GameObject _);
        var cues = NewCueList(
            NewCue("impact", "NormalizedTime", 0.1f),
            NewCue("impact", "NormalizedTime", 0.9f));

        InvokeSetActive(ctx, 7, cues, 1234, "test");

        Assert.That(OrderedCueNames(ctx), Is.EqualTo(new[] { "impact" }),
            "이름이 조회 키이므로 중복은 첫 항목만 남아야 합니다(뒤 항목이 조용히 무시되는 것을 고정).");
    }

    [Test]
    public void SetActive_StoresExpectedStateHash()
    {
        object ctx = NewContext(out GameObject _);
        int expected = Animator.StringToHash("ClassSkill_2");

        InvokeSetActive(ctx, 7, NewCueList(NewCue("impact", "NormalizedTime", 0.5f)), expected, "asset/state");

        Assert.That(GetProperty<int>(ctx, "ExpectedStateHash"), Is.EqualTo(expected),
            "게이트가 소비할 기대 해시가 보존되어야 합니다.");
        Assert.That(GetProperty<string>(ctx, "DebugLabel"), Is.EqualTo("asset/state"));
    }

    [Test]
    public void Clear_ResetsGateStateAndCues()
    {
        object ctx = NewContext(out GameObject _);
        InvokeSetActive(ctx, 7, NewCueList(NewCue("impact", "NormalizedTime", 0.5f)), 1234, "test");

        ContextType.GetMethod("Clear").Invoke(ctx, null);

        Assert.That(GetProperty<int>(ctx, "ExpectedStateHash"), Is.EqualTo(0));
        Assert.That(GetProperty<int>(ctx, "CurrentActionInstanceId"), Is.EqualTo(0));
        Assert.That(OrderedCueNames(ctx), Is.Empty,
            "컨텍스트를 비우면 남은 Cue가 다음 연출로 새지 않아야 합니다.");
    }

    [Test]
    public void SetActive_WithZeroActionId_ClearsInsteadOfRegistering()
    {
        object ctx = NewContext(out GameObject _);

        InvokeSetActive(ctx, 0, NewCueList(NewCue("impact", "NormalizedTime", 0.5f)), 1234, "test");

        Assert.That(GetProperty<bool>(ctx, "HasValidContext"), Is.False,
            "actionId 0은 '연출 없음'을 뜻하므로 등록되지 않아야 합니다.");
    }

    // ──────────────────────────────────────────────────────────────
    // 리플렉션 도우미
    // ──────────────────────────────────────────────────────────────

    private static Type FindRuntimeType(string fullName)
    {
        Type found = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.FullName == fullName);

        Assert.That(found, Is.Not.Null, $"런타임 타입을 찾지 못했습니다: {fullName}");
        return found;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null);
        }
    }

    private static object NewCue(string normalizedName, string timing, float time)
    {
        object cue = Activator.CreateInstance(RuntimeCueType);
        RuntimeCueType.GetField("NormalizedCueName").SetValue(cue, normalizedName);
        RuntimeCueType.GetField("Timing").SetValue(cue, Enum.Parse(TimingEnumType, timing));
        RuntimeCueType.GetField("Time").SetValue(cue, time);
        return cue;
    }

    private static IList NewCueList(params object[] cues)
    {
        Type listType = typeof(List<>).MakeGenericType(RuntimeCueType);
        var list = (IList)Activator.CreateInstance(listType);
        foreach (object cue in cues)
        {
            list.Add(cue);
        }
        return list;
    }

    private object NewContext(out GameObject host)
    {
        host = new GameObject("CueContextHost");
        _spawned.Add(host);
        return host.AddComponent(ContextType);
    }

    private static void InvokeSetActive(object ctx, int actionId, IList cues, int expectedHash, string label)
    {
        MethodInfo setActive = ContextType.GetMethod("SetActive");
        Assert.That(setActive, Is.Not.Null, "SetActive를 찾지 못했습니다.");
        // (actionInstanceId, SkillEffectContext, List<RuntimeCue>, expectedStateHash, debugLabel)
        object effectContext = Activator.CreateInstance(FindRuntimeType("SkillEffectContext"));
        setActive.Invoke(ctx, new object[] { actionId, effectContext, cues, expectedHash, label });
    }

    private static string[] OrderedCueNames(object ctx)
    {
        var ordered = (IEnumerable)ContextType.GetProperty("OrderedCues").GetValue(ctx);
        var names = new List<string>();
        foreach (object cue in ordered)
        {
            names.Add((string)RuntimeCueType.GetField("NormalizedCueName").GetValue(cue));
        }
        return names.ToArray();
    }

    private static float ResolveFireSeconds(object cue, float stateLength)
    {
        return (float)RuntimeCueType.GetMethod("ResolveFireSeconds").Invoke(cue, new object[] { stateLength });
    }

    private static bool IsDataTimed(object cue)
    {
        return (bool)RuntimeCueType.GetProperty("IsDataTimed").GetValue(cue);
    }

    private static bool Crossed(float prevSeconds, float seconds, float target)
    {
        return (bool)DriverType.GetMethod("CrossedFireTime", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { prevSeconds, seconds, target });
    }

    private static bool StateHashMatches(int fullPathHash, int shortNameHash, int expected)
    {
        return (bool)DriverType.GetMethod("StateHashMatches", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { fullPathHash, shortNameHash, expected });
    }

    private static T GetProperty<T>(object target, string name)
    {
        return (T)target.GetType().GetProperty(name).GetValue(target);
    }
}
