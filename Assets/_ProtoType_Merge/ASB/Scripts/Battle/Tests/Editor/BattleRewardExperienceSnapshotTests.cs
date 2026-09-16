using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleRewardExperienceSnapshotTests
{
    [Test]
    public void Victory_UsesSpawnPlanExperienceSnapshotWithoutEnemyObjects()
    {
        Type battleCharacterType = FindRuntimeType("BattleCharactor");
        Type battleResultType = FindRuntimeType("BattleResult");
        Type handlerType = FindRuntimeType("BattleResultPersistenceHandler");

        GameObject first = null;
        GameObject second = null;
        try
        {
            Component firstPlayer = CreatePlayer(battleCharacterType, 1, out first);
            Component secondPlayer = CreatePlayer(battleCharacterType, 2, out second);
            object players = CreateRuntimeList(battleCharacterType, firstPlayer, secondPlayer);
            object victory = Enum.Parse(battleResultType, "Victory");

            object plan = InvokeSnapshotBuild(handlerType, battleResultType, players, victory, 75f);
            IList previews = ReadPreviews(plan);

            Assert.That(previews.Count, Is.EqualTo(2));
            Assert.That(ReadGainedExp(previews[0]), Is.EqualTo(38));
            Assert.That(ReadGainedExp(previews[1]), Is.EqualTo(38));
        }
        finally
        {
            if (first != null) UnityEngine.Object.DestroyImmediate(first);
            if (second != null) UnityEngine.Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void Defeat_IgnoresSpawnPlanExperienceSnapshot()
    {
        Type battleCharacterType = FindRuntimeType("BattleCharactor");
        Type battleResultType = FindRuntimeType("BattleResult");
        Type handlerType = FindRuntimeType("BattleResultPersistenceHandler");

        GameObject playerObject = null;
        try
        {
            Component player = CreatePlayer(battleCharacterType, 1, out playerObject);
            object players = CreateRuntimeList(battleCharacterType, player);
            object defeat = Enum.Parse(battleResultType, "Defeat");

            object plan = InvokeSnapshotBuild(handlerType, battleResultType, players, defeat, 75f);
            IList previews = ReadPreviews(plan);

            Assert.That(previews.Count, Is.EqualTo(1));
            Assert.That(ReadGainedExp(previews[0]), Is.Zero);
        }
        finally
        {
            if (playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    private static Component CreatePlayer(Type battleCharacterType, int unitIndex, out GameObject gameObject)
    {
        gameObject = new GameObject($"RewardSnapshotPlayer_{unitIndex}");
        Component player = gameObject.AddComponent(battleCharacterType);

        PropertyInfo isPlayer = battleCharacterType.GetProperty("IsPlayer", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(isPlayer, Is.Not.Null);
        isPlayer.SetValue(player, true);

        Type persistentDataType = FindRuntimeType("UnitPersistentData");
        object persistentData = Activator.CreateInstance(persistentDataType, new object[] { unitIndex });
        MethodInfo bind = battleCharacterType.GetMethod(
            "BindPersistentSourceData",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(bind, Is.Not.Null);
        bind.Invoke(player, new[] { persistentData });

        return player;
    }

    private static object InvokeSnapshotBuild(
        Type handlerType,
        Type battleResultType,
        object players,
        object result,
        float totalExperience)
    {
        MethodInfo method = handlerType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate =>
            {
                if (candidate.Name != "BuildBattleRewardPlan") return false;
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Length == 3
                    && parameters[1].ParameterType == battleResultType
                    && parameters[2].ParameterType == typeof(float);
            });

        return method.Invoke(null, new[] { players, result, (object)totalExperience });
    }

    private static IList ReadPreviews(object plan)
    {
        Assert.That(plan, Is.Not.Null);
        FieldInfo previewsField = plan.GetType().GetField("UnitPreviews", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(previewsField, Is.Not.Null);
        return (IList)previewsField.GetValue(plan);
    }

    private static int ReadGainedExp(object preview)
    {
        FieldInfo field = preview.GetType().GetField("GainedExp", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        return (int)field.GetValue(preview);
    }

    private static object CreateRuntimeList(Type elementType, params object[] entries)
    {
        Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(elementType);
        IList list = (IList)Activator.CreateInstance(listType);
        for (int i = 0; i < entries.Length; i++)
            list.Add(entries[i]);
        return list;
    }

    private static Type FindRuntimeType(string typeName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"Runtime type '{typeName}' was not found.");
        return type;
    }
}
