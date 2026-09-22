using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// 지그 Cue 미리보기의 순수 발화 교차 판정을 고정한다(§13.1).
/// 이번 tick 구간 (prev, now] 에 발화 시각이 걸리면 1회 발화한다.
/// (스폰·오디오는 에디터 상호작용이라 자동 테스트 불가 → 수동.)
///
/// 지그 코드는 Assembly-CSharp-Editor에 있어 리플렉션으로 접근한다.
/// </summary>
public sealed class PresentationJigCuePreviewTests
{
    private static Type PreviewType => FindRuntimeType("ASB.Work.EditorTools.Jig.JigCuePreview");

    [Test]
    public void Crossed_FiresWhenFireTimeInWindow()
    {
        Assert.That(Crossed(0.4, 0.6, 0.5), Is.True, "구간을 지나는 tick에 발화해야 합니다.");
        Assert.That(Crossed(0.5, 0.7, 0.5), Is.False, "구간 시작(prev==fire)은 제외 — 두 tick 연속 발화 방지.");
        Assert.That(Crossed(0.4, 0.5, 0.5), Is.True, "구간 끝(now==fire)은 포함해야 합니다.");
        Assert.That(Crossed(0.6, 0.8, 0.5), Is.False, "이미 지난 시각은 이 tick에서 발화하지 않습니다.");
        Assert.That(Crossed(0.2, 0.4, 0.5), Is.False, "아직 도달하지 않았으면 발화하지 않습니다.");
    }

    private static bool Crossed(double prev, double now, double fire)
    {
        return (bool)PreviewType.GetMethod("Crossed", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { prev, now, fire });
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
