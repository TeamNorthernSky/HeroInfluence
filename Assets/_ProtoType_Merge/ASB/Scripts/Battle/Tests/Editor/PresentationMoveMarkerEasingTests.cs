using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 프로젝트 런타임은 Assembly-CSharp에 있어 asmdef 테스트가 직접 참조할 수 없다.
/// PresentationTimelineSpeedTests와 동일하게 reflection으로 접근한다.
/// 대상 간 이동(PresentationMoveMarker)의 위치 이징·회전 비율 검증.
/// </summary>
public class PresentationMoveMarkerEasingTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    private static Type MarkerType => FindRuntimeType("PresentationMoveMarker");

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void Defaults_EaseInOutPosition_KeepsEndpoints()
    {
        ScriptableObject marker = NewMarker();
        Assert.That(Position(marker, 0f), Is.EqualTo(0f).Within(1e-4f));
        Assert.That(Position(marker, 1f), Is.EqualTo(1f).Within(1e-4f));
        Assert.That(Position(marker, 0.5f), Is.EqualTo(0.5f).Within(1e-3f));
        Assert.That(Position(marker, 0.2f), Is.LessThan(0.2f), "기본 곡선은 출발이 선형보다 느려야 합니다.");
        Assert.That(Position(marker, 0.8f), Is.GreaterThan(0.8f), "기본 곡선은 도착이 선형보다 느려야 합니다.");
    }

    [Test]
    public void Defaults_RotationFinishesAtPortion()
    {
        ScriptableObject marker = NewMarker();
        float portion = (float)MarkerType.GetProperty("RotationPortion").GetValue(marker);
        Assert.That(portion, Is.EqualTo(0.35f).Within(1e-5f));
        Assert.That(Rotation(marker, 0f), Is.EqualTo(0f).Within(1e-5f));
        Assert.That(Rotation(marker, portion * 0.5f), Is.EqualTo(0.5f).Within(1e-4f));
        Assert.That(Rotation(marker, portion), Is.EqualTo(1f).Within(1e-5f));
        Assert.That(Rotation(marker, 0.9f), Is.EqualTo(1f).Within(1e-5f));
    }

    [Test]
    public void NullCurve_FallsBackToLinear()
    {
        ScriptableObject marker = NewMarker();
        ConfigureEasing(marker, null, 1f);
        Assert.That(Position(marker, 0.3f), Is.EqualTo(0.3f).Within(1e-5f));
        Assert.That(Rotation(marker, 0.5f), Is.EqualTo(0.5f).Within(1e-5f));
    }

    [Test]
    public void OutOfRangeInputAndOvershootingCurve_AreClamped()
    {
        ScriptableObject marker = NewMarker();
        ConfigureEasing(marker, AnimationCurve.Linear(0f, 0f, 1f, 2f), 1f);
        Assert.That(Position(marker, -1f), Is.EqualTo(0f).Within(1e-5f));
        Assert.That(Position(marker, 2f), Is.EqualTo(1f).Within(1e-5f));
        Assert.That(Position(marker, 0.75f), Is.EqualTo(1f).Within(1e-5f), "위치 비율은 목적지를 넘지 않아야 합니다.");
    }

    [Test]
    public void ConfigureEasing_ClampsRotationPortion()
    {
        ScriptableObject marker = NewMarker();
        ConfigureEasing(marker, null, 0f);
        Assert.That((float)MarkerType.GetProperty("RotationPortion").GetValue(marker),
            Is.EqualTo((float)MarkerType.GetField("MinRotationPortion").GetValue(null)).Within(1e-6f));
        ConfigureEasing(marker, null, 3f);
        Assert.That((float)MarkerType.GetProperty("RotationPortion").GetValue(marker), Is.EqualTo(1f).Within(1e-6f));
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────

    private ScriptableObject NewMarker()
    {
        var marker = ScriptableObject.CreateInstance(MarkerType);
        _created.Add(marker);
        return marker;
    }

    private static float Position(ScriptableObject marker, float t) =>
        (float)MarkerType.GetMethod("EvaluatePosition").Invoke(marker, new object[] { t });

    private static float Rotation(ScriptableObject marker, float t) =>
        (float)MarkerType.GetMethod("EvaluateRotation").Invoke(marker, new object[] { t });

    private static void ConfigureEasing(ScriptableObject marker, AnimationCurve curve, float portion) =>
        MarkerType.GetMethod("ConfigureEasing").Invoke(marker, new object[] { curve, portion });

    private static Type FindRuntimeType(string fullName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName, false))
            .FirstOrDefault(t => t != null);
        Assert.That(type, Is.Not.Null, $"타입 '{fullName}'을 찾지 못했습니다.");
        return type;
    }
}
