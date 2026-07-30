using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleLogicalSlotMapTests
{
    private readonly List<GameObject> roots = new List<GameObject>();

    private static Type MapType => FindRuntimeType("BattleLogicalSlotMap");
    private static Type GridCellType => FindRuntimeType("ASB.Work.BattleGrid.GridCell");

    [TearDown]
    public void TearDown()
    {
        for (int i = roots.Count - 1; i >= 0; i--)
        {
            if (roots[i] != null)
                UnityEngine.Object.DestroyImmediate(roots[i]);
        }

        roots.Clear();
    }

    [Test]
    public void TryCreate_MapsSixEnemyCellsInPersistentGridKeyOrder()
    {
        Transform root = CreateRoot("EnemyPlace");
        AddCell(root, "Grid_3_2");
        AddCell(root, "Grid_2_1");
        AddCell(root, "Grid_3_0");
        AddCell(root, "Grid_2_0");
        AddCell(root, "Grid_3_1");
        AddCell(root, "Grid_2_2");

        Assert.That(TryCreateMap(root, out object map, out string error), Is.True, error);

        AssertSlot(map, 1, 200, "Grid_2_0");
        AssertSlot(map, 2, 201, "Grid_2_1");
        AssertSlot(map, 3, 202, "Grid_2_2");
        AssertSlot(map, 4, 300, "Grid_3_0");
        AssertSlot(map, 5, 301, "Grid_3_1");
        AssertSlot(map, 6, 302, "Grid_3_2");
    }

    [Test]
    public void TryCreate_OnlyCollectsCellsBelowTheProvidedEnemyRoot()
    {
        Transform sceneRoot = CreateRoot("SceneRoot");
        Transform playerRoot = CreateChild(sceneRoot, "PlayerPlace");
        Transform enemyRoot = CreateChild(sceneRoot, "EnemyPlace");

        AddCell(playerRoot, "Grid_0_0");
        AddCell(playerRoot, "Grid_1_0");
        AddCell(enemyRoot, "Grid_2_0");
        AddCell(enemyRoot, "Grid_2_1");

        Assert.That(TryCreateMap(enemyRoot, out object map, out string error), Is.True, error);
        Assert.That(GetProperty<int>(map, "Count"), Is.EqualTo(2));
        AssertSlot(map, 1, 200, "Grid_2_0");
        AssertSlot(map, 2, 201, "Grid_2_1");
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(7)]
    public void TryResolve_RejectsLogicalSlotsOutsideCreatedRange(int logicalSlot)
    {
        Transform root = CreateRoot("EnemyPlace");
        for (int i = 0; i < 6; i++)
            AddCell(root, i < 3 ? $"Grid_2_{i}" : $"Grid_3_{i - 3}");

        Assert.That(TryCreateMap(root, out object map, out string error), Is.True, error);
        Assert.That(TryResolve(map, logicalSlot, out _), Is.False);
    }

    [Test]
    public void TryCreate_RejectsDuplicateResolvedGridNumbers()
    {
        Transform root = CreateRoot("EnemyPlace");
        AddCell(root, "Grid_2_0");
        AddCell(root, "Grid_200");

        Assert.That(TryCreateMap(root, out _, out string error), Is.False);
        StringAssert.Contains("Duplicate enemy grid number 200", error);
    }

    [TestCase("Grid_2_0", 200)]
    [TestCase("Grid_3_2", 302)]
    [TestCase("Grid_201", 201)]
    public void TryParseGridNumber_SupportsCoordinateAndLegacyNames(string name, int expected)
    {
        Assert.That(TryParseGridNumber(name, out int actual), Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("Grid")]
    [TestCase("Grid_X_Y")]
    [TestCase("Player_2_0")]
    public void TryParseGridNumber_RejectsInvalidNames(string name)
    {
        Assert.That(TryParseGridNumber(name, out _), Is.False);
    }

    private Transform CreateRoot(string name)
    {
        var root = new GameObject(name);
        roots.Add(root);
        return root.transform;
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static void AddCell(Transform root, string name)
    {
        var cellObject = new GameObject(name);
        cellObject.transform.SetParent(root, false);
        cellObject.AddComponent(GridCellType);
    }

    private static void AssertSlot(object map, int logicalSlot, int expectedGridNumber, string expectedGridName)
    {
        Assert.That(TryResolve(map, logicalSlot, out object slot), Is.True);
        Assert.That(GetProperty<int>(slot, "GridNumber"), Is.EqualTo(expectedGridNumber));
        Assert.That(GetProperty<string>(slot, "GridName"), Is.EqualTo(expectedGridName));
    }

    private static bool TryCreateMap(Transform root, out object map, out string error)
    {
        MethodInfo method = MapType.GetMethod("TryCreate", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        object[] args = { root, null, null };
        bool success = (bool)method.Invoke(null, args);
        map = args[1];
        error = args[2] as string ?? string.Empty;
        return success;
    }

    private static bool TryResolve(object map, int logicalSlot, out object slot)
    {
        MethodInfo method = MapType.GetMethod("TryResolve", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);
        object[] args = { logicalSlot, null };
        bool success = (bool)method.Invoke(map, args);
        slot = args[1];
        return success;
    }

    private static bool TryParseGridNumber(string name, out int gridNumber)
    {
        MethodInfo method = MapType.GetMethod("TryParseGridNumber", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        object[] args = { name, 0 };
        bool success = (bool)method.Invoke(null, args);
        gridNumber = (int)args[1];
        return success;
    }

    private static T GetProperty<T>(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(instance);
    }

    private static Type FindRuntimeType(string fullName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"Runtime type '{fullName}' was not found.");
        return type;
    }
}
