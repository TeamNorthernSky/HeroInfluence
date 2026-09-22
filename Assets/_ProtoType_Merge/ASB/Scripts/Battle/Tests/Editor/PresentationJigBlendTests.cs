using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Timeline 지그의 전 구간 블렌드 표시 — 경계 분류·클램프·겹침 계산의 순수 로직 계약을 고정한다.
///
/// 계약(Docs/SkillPresentation_Jig_전구간블렌드표시_구현지시서.md):
///   ① 경계 모드는 "이동 포함"이 아니라 "이전 액션이 조기 종료하는가"로 정한다(§2).
///   ② MovingAttack은 조기 종료 → 나가는 경계가 EarlyOverlap(이동 뒤여도).
///   ③ cursor = start + duration — EarlyOverlap 이후 다음 구간이 밀리지 않는다(§4.1).
///   ④ PostBoundary 클램프는 이전 클립 길이로 제한하지 않는다(§4.3).
///   ⑤ EarlyOverlap 연속은 3중 겹침을 막고, 요청/적용을 둘 다 보존한다(§4.3).
///
/// 지그 코드는 Assembly-CSharp-Editor에 있어 리플렉션으로 접근한다.
/// </summary>
public sealed class PresentationJigBlendTests
{
    private static Type LayoutType => FindRuntimeType("ASB.Work.EditorTools.Jig.JigPhaseLayout");
    private static Type IntervalType => FindRuntimeType("ASB.Work.EditorTools.Jig.JigPhaseInterval");
    private static Type BoundaryType => FindRuntimeType("ASB.Work.EditorTools.Jig.JigBlendBoundary");

    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _created.Count; i++)
        {
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        }
        _created.Clear();
    }

    // ── ① 경계 분류 ──

    [Test]
    public void Classify_AnimToAnim_IsEarlyOverlap()
    {
        object a = NewInterval("AttackPrepare", 1.0, 0.0f, hasClip: true);
        object b = NewInterval("Beat0", 1.0, 0.2f, hasClip: true);
        Assert.That(Classify(a, b), Is.EqualTo("EarlyOverlap"),
            "애니↔애니는 이전이 조기 종료 → EarlyOverlap.");
    }

    [Test]
    public void Classify_LocomotionFrom_IsPostBoundary()
    {
        object move = NewInterval("Move", 0.25, 0.1f, hasClip: true, isLoco: true);
        object prep = NewInterval("AttackPrepare", 1.0, 0.2f, hasClip: true);
        Assert.That(Classify(move, prep), Is.EqualTo("PostBoundaryBlend"),
            "이동이 이전이면 완료 대기 → PostBoundary.");
    }

    [Test]
    public void Classify_AnimToLocomotion_IsPostBoundary()
    {
        object prep = NewInterval("MovePrepare", 1.0, 0.0f, hasClip: true);
        object move = NewInterval("Move", 0.25, 0.1f, hasClip: true, isLoco: true);
        Assert.That(Classify(prep, move), Is.EqualTo("PostBoundaryBlend"),
            "다음이 이동이면 이전 애니가 조기 종료하지 않음(nextBlend 없음) → PostBoundary.");
    }

    [Test]
    public void Classify_MovingAttackFrom_IsEarlyOverlap_EvenIntoLocomotion()
    {
        object ma = NewInterval("MovingAttack", 1.0, 0.1f, hasClip: true, isMoving: true);
        object ret = NewInterval("Return", 0.3, 0.15f, hasClip: true, isLoco: true);
        Assert.That(Classify(ma, ret), Is.EqualTo("EarlyOverlap"),
            "MovingAttack은 CalculateExitNormalized로 조기 종료 → 이동 뒤여도 EarlyOverlap.");
    }

    [Test]
    public void Classify_NoClip_IsAnnotationOnly()
    {
        object a = NewInterval("AttackPrepare", 1.0, 0.0f, hasClip: true);
        object fail = NewInterval("Beat0", 0.0, 0.2f, hasClip: false);
        Assert.That(Classify(a, fail), Is.EqualTo("AnnotationOnly"));
        Assert.That(Classify(fail, a), Is.EqualTo("AnnotationOnly"),
            "겹칠 클립이 없으면 어느 방향이든 주석 전용.");
    }

    // ── ③ cursor 누적 ──

    [Test]
    public void Cursor_EarlyOverlap_DoesNotPushNextSegment()
    {
        // A(1.0) → B(1.0, blend 0.2) → C(1.0, blend 0.2), 전부 애니
        var list = NewList(
            NewInterval("A", 1.0, 0.0f, hasClip: true),
            NewInterval("B", 1.0, 0.2f, hasClip: true),
            NewInterval("C", 1.0, 0.2f, hasClip: true));

        Compute(list);

        Assert.That(Start(list, 0), Is.EqualTo(0.0).Within(1e-4), "A는 0에서 시작.");
        Assert.That(Start(list, 1), Is.EqualTo(0.8).Within(1e-4), "B는 A끝(1.0) - 0.2 = 0.8.");
        // cursor = start + baseDur = 0.8 + 1.0 = 1.8. C = 1.8 - 0.2 = 1.6 (요청 0.2 그대로 잘못 밀리지 않음)
        Assert.That(Start(list, 2), Is.EqualTo(1.6).Within(1e-4),
            "cursor=start+duration이어야 C가 blend만큼 밀리지 않는다.");
    }

    // ── ④ PostBoundary 클램프는 이전 길이 미제한 ──

    [Test]
    public void PostBoundary_NotShrunkByShortPreviousClip()
    {
        // 짧은 로코모션(0.05) 다음 긴 blend(0.2)로 들어오는 애니
        var list = NewList(
            NewInterval("Move", 0.05, 0.0f, hasClip: true, isLoco: true),
            NewInterval("AttackPrepare", 1.0, 0.2f, hasClip: true));

        IList boundaries = Compute(list);
        object b = boundaries[0];

        Assert.That(BMode(b), Is.EqualTo("PostBoundaryBlend"));
        Assert.That(BApplied(b), Is.EqualTo(0.2f).Within(1e-4f),
            "PostBoundary는 이전 클립 길이(0.05)로 줄이지 않는다 — 런타임이 끝 포즈를 유지하며 blend한다.");
    }

    // ── ⑤ EarlyOverlap 3중 겹침 방지 + 요청/적용 보존 ──

    [Test]
    public void EarlyOverlap_PreventsTripleOverlap_AndPreservesRequestedVsApplied()
    {
        // A(1.0) → B(1.0, 0.6) → C(1.0, 0.6). B는 자기 incoming 0.6을 이미 씀 → C는 남은 ~0.4로 제한.
        var list = NewList(
            NewInterval("A", 1.0, 0.0f, hasClip: true),
            NewInterval("B", 1.0, 0.6f, hasClip: true),
            NewInterval("C", 1.0, 0.6f, hasClip: true));

        IList boundaries = Compute(list);
        object bc = boundaries[1];   // B→C

        Assert.That(BRequested(bc), Is.EqualTo(0.6f).Within(1e-4f), "요청값은 원래대로 보존.");
        Assert.That(BApplied(bc), Is.LessThan(0.6f), "3중 겹침 방지로 적용값이 줄어야 한다.");
        Assert.That(BApplied(bc), Is.EqualTo(0.4f).Within(1e-3f),
            "B(1.0)가 incoming 0.6을 이미 썼으므로 C는 ~0.4만 허용.");
        Assert.That(BWarning(bc), Is.Not.Null.And.Not.Empty, "클램프 시 요청/적용 경고가 붙어야 한다.");
    }

    [Test]
    public void EventGated_BoundaryFlaggedWhenPreviousBeatAdvanceOnEvent()
    {
        var list = NewList(
            NewInterval("Beat0", 1.0, 0.0f, hasClip: true, advanceOnEvent: true),
            NewInterval("Beat1", 1.0, 0.2f, hasClip: true));

        IList boundaries = Compute(list);
        Assert.That(BEventGated(boundaries[0]), Is.True,
            "이전 Beat가 AdvanceOnEvent면 그 경계는 이벤트 게이트(실제 겹침이 요청과 다를 수 있음).");
    }

    // ──────────────────────────────────────────────────────────────
    // 리플렉션 도우미
    // ──────────────────────────────────────────────────────────────

    private object NewInterval(string label, double effLen, float blend,
        bool hasClip, bool isLoco = false, bool isMoving = false, bool advanceOnEvent = false)
    {
        object iv = Activator.CreateInstance(IntervalType);
        SetF(iv, "Label", label);
        SetF(iv, "Duration", effLen);
        SetF(iv, "EffectiveLength", effLen);
        SetF(iv, "RequestedBlendSeconds", blend);
        SetF(iv, "IsLocomotion", isLoco);
        SetF(iv, "IsMovingAttack", isMoving);
        SetF(iv, "AdvanceOnEvent", advanceOnEvent);
        SetF(iv, "Resolved", true);
        if (hasClip)
        {
            var clip = new AnimationClip();
            _created.Add(clip);
            SetF(iv, "Clip", clip);
        }
        return iv;
    }

    private IList NewList(params object[] intervals)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(IntervalType));
        foreach (object iv in intervals) list.Add(iv);
        return list;
    }

    private static string Classify(object from, object to)
    {
        object mode = LayoutType.GetMethod("ClassifyMode", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new[] { from, to });
        return mode.ToString();
    }

    private static IList Compute(IList intervals)
    {
        return (IList)LayoutType.GetMethod("ComputeBlendBoundaries", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { intervals });
    }

    private static double Start(IList intervals, int i) => (double)IntervalType.GetField("Start").GetValue(intervals[i]);

    private static string BMode(object b) => BoundaryType.GetField("Mode").GetValue(b).ToString();
    private static float BApplied(object b) => (float)BoundaryType.GetField("AppliedSeconds").GetValue(b);
    private static float BRequested(object b) => (float)BoundaryType.GetField("RequestedSeconds").GetValue(b);
    private static bool BEventGated(object b) => (bool)BoundaryType.GetField("IsEventGated").GetValue(b);
    private static string BWarning(object b) => (string)BoundaryType.GetField("Warning").GetValue(b);

    private static void SetF(object target, string field, object value)
    {
        FieldInfo f = IntervalType.GetField(field);
        Assert.That(f, Is.Not.Null, $"JigPhaseInterval.{field}를 찾지 못했습니다.");
        f.SetValue(target, value);
    }

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
}
