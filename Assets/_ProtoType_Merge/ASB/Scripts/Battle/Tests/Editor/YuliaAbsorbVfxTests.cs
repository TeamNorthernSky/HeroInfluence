using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class YuliaAbsorbVfxTests
{
    private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const string MadonnaUnitPath = "Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanMadonna_40001.prefab";
    private const string ArcPrefabPath = "Assets/ASB_Work/Effect/Yulia/Boss/FX_YuliaAbsorbArc.prefab";

    [Test]
    public void ComputeControlPoints_LiftsThirdsByArchHeight()
    {
        MethodInfo compute = FindRuntimeType("ElectricArcEndpoints").GetMethod("ComputeControlPoints", AnyStatic);
        var args = new object[] { Vector3.zero, new Vector3(3f, 0f, 0f), 0.6f, null, null };
        compute.Invoke(null, args);

        Assert.That(Vector3.Distance((Vector3)args[3], new Vector3(1f, 0.6f, 0f)), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance((Vector3)args[4], new Vector3(2f, 0.6f, 0f)), Is.LessThan(0.0001f));
    }

    [Test]
    public void AbsorbSteps_ResolveFromCueNames()
    {
        MethodInfo resolve = FindRuntimeType("YuliaAbsorbVfx").GetMethod("ResolveStep", AnyStatic);
        object Step(string cue) => resolve.Invoke(null, new object[] { cue, "absorb_glow", "absorb_arc", "absorb_complete" });

        Assert.That(Step("absorb_glow").ToString(), Is.EqualTo("Glow"));
        Assert.That(Step("absorb_arc").ToString(), Is.EqualTo("Arc"));
        Assert.That(Step("absorb_complete").ToString(), Is.EqualTo("Complete"));
        Assert.That(Step("").ToString(), Is.EqualTo("None"), "이름 없는 마커는 무시한다.");
        Assert.That(Step("yulia_cast_charge").ToString(), Is.EqualTo("None"), "다른 Cue는 흡수 단계가 아니다.");
    }

    [Test]
    public void MadonnaUnit_HasAbsorbVfxWithAllPrefabs()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MadonnaUnitPath);
        Assert.That(prefab, Is.Not.Null);
        Component absorb = prefab.GetComponent(FindRuntimeType("YuliaAbsorbVfx"));
        Assert.That(absorb, Is.Not.Null, "율리아 유닛에 흡수 연출이 없다.");
        Assert.That(prefab.GetComponent(FindRuntimeType("MadonnaSkillReceiveTimeline")), Is.Not.Null);

        var so = new SerializedObject(absorb);
        foreach (string field in new[] { "amplifierGlowPrefab", "arcPrefab", "handFlashPrefab" })
        {
            var linked = so.FindProperty(field).objectReferenceValue as GameObject;
            Assert.That(linked, Is.Not.Null, $"{field}가 비어 있다.");
            Assert.That(linked.GetComponent(FindRuntimeType("VisualEffectGraphEffect")), Is.Not.Null,
                $"{field}에 VisualEffectGraphEffect가 없으면 수명이 관리되지 않는다.");
        }

        foreach (string field in new[] { "glowCue", "arcCue", "completeCue" })
            Assert.That(so.FindProperty(field).stringValue, Is.Not.Empty, $"{field}가 비면 그 단계가 나오지 않는다.");
    }

    [Test]
    public void ReceiveTimeline_AbsorbCuesFollowHandMotion()
    {
        var timeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>(
            "Assets/ASB_Work/Skills/Timelines/Skill400014_SkillReceive.playable");
        Assert.That(timeline, Is.Not.Null);
        var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(MadonnaUnitPath).GetComponent(FindRuntimeType("YuliaAbsorbVfx")));

        // 흡수 Timeline 알림은 Director 오브젝트의 수신기로 가므로 기본 마커 트랙(markerTrack)에 둔다.
        Assert.That(timeline.markerTrack, Is.Not.Null);
        var times = new Dictionary<string, float>();
        foreach (UnityEngine.Timeline.IMarker marker in timeline.markerTrack.GetMarkers())
        {
            if (marker.GetType().Name != "PresentationSignalMarker") continue;
            var kind = marker.GetType().GetProperty("Kind").GetValue(marker).ToString();
            var cue = (string)marker.GetType().GetProperty("CueName").GetValue(marker);
            if (kind == "Cue" && !string.IsNullOrEmpty(cue)) times[cue] = (float)marker.time;
        }

        string glowCue = so.FindProperty("glowCue").stringValue;
        string arcCue = so.FindProperty("arcCue").stringValue;
        string completeCue = so.FindProperty("completeCue").stringValue;
        Assert.That(times.ContainsKey(glowCue) && times.ContainsKey(arcCue) && times.ContainsKey(completeCue), Is.True,
            "흡수 Timeline에 증광·아크·완료 Cue 마커가 모두 있어야 한다.");

        // 손 동작(Victory_NoWeapon 오른손: 0.42~0.67 올림, 0.67~1.33 유지, 1.33~ 내림)에 맞춘다:
        // 손을 들기 시작할 때 증광 → 다 든 뒤 아크 → 내리기 전에 완료(아크 소거).
        float glow = times[glowCue], arc = times[arcCue], complete = times[completeCue];
        Assert.That(glow, Is.GreaterThanOrEqualTo(0.42f), "손을 들기 전(준비 동작)에 증광하지 않는다.");
        Assert.That(arc, Is.GreaterThan(glow), "증광이 먼저 보이고 아크는 뒤에 이어진다.");
        Assert.That(arc, Is.GreaterThanOrEqualTo(0.58f), "손이 올라간 뒤 아크.");
        Assert.That(complete, Is.GreaterThan(arc));
        Assert.That(complete, Is.LessThan(1.33f), "손을 내리기 전에 아크가 사라진다.");
    }

    [Test]
    public void ArcPrefab_HasAllFourEndpointsAndLoopMode()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArcPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        Component endpoints = prefab.GetComponent(FindRuntimeType("ElectricArcEndpoints"));
        Assert.That(endpoints, Is.Not.Null);
        var so = new SerializedObject(endpoints);
        for (int i = 1; i <= 4; i++)
            Assert.That(so.FindProperty("pos" + i).objectReferenceValue, Is.Not.Null, $"Pos{i}가 연결되지 않았다.");

        Component effect = prefab.GetComponent(FindRuntimeType("VisualEffectGraphEffect"));
        SerializedProperty mode = new SerializedObject(effect).FindProperty("mode");
        Assert.That(mode.enumNames[mode.enumValueIndex], Is.EqualTo("Loop"), "흡수 완료 신호까지 유지되어야 한다.");
    }

    private static Type FindRuntimeType(string fullName)
    {
        Type found = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(type => type.FullName == fullName);

        Assert.That(found, Is.Not.Null, $"런타임 타입을 찾지 못했습니다: {fullName}");
        return found;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null);
        }
    }
}
