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
    private const string ArcPrefabPath = "Assets/ASB_Work/Effect/Yulia/FX_YuliaAbsorbArc.prefab";

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
    public void AbsorbSteps_SpawnArcThenCompleteOnceEach()
    {
        Type type = FindRuntimeType("YuliaAbsorbVfx");
        MethodInfo arc = type.GetMethod("ShouldSpawnArc", AnyStatic);
        MethodInfo complete = type.GetMethod("ShouldComplete", AnyStatic);

        Assert.That((bool)arc.Invoke(null, new object[] { 0.05d, 0.1f, false }), Is.False, "증광이 먼저 보이고 아크는 뒤에 이어진다.");
        Assert.That((bool)arc.Invoke(null, new object[] { 0.1d, 0.1f, false }), Is.True);
        Assert.That((bool)arc.Invoke(null, new object[] { 0.5d, 0.1f, true }), Is.False, "아크는 한 번만 만든다.");

        Assert.That((bool)complete.Invoke(null, new object[] { 0.6d, 0.667f, false }), Is.False);
        Assert.That((bool)complete.Invoke(null, new object[] { 0.667d, 0.667f, false }), Is.True);
        Assert.That((bool)complete.Invoke(null, new object[] { 1.0d, 0.667f, true }), Is.False, "손빛은 한 번만 낸다.");
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

        // 흡수 Timeline의 흡수 완료 Cue 마커 시각과 맞아야 한다.
        Assert.That(so.FindProperty("completeSeconds").floatValue, Is.EqualTo(0.667f).Within(0.01f));
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
