using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AmplifierDeathPolicyContractTests
{
    private const string BossPrefabPath =
        "Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanMadonna_40001.prefab";

    private static readonly string[] AmplifierPrefabPaths =
    {
        "Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanAmplifier_40002.prefab",
        "Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanAmplifier_40003.prefab"
    };

    [TestCaseSource(nameof(AmplifierPrefabPaths))]
    public void AmplifierPrefab_UsesPermanentDeathPolicy(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, $"증폭기 프리팹을 불러오지 못했습니다: {prefabPath}");

        Type participantType = FindRuntimeType("EncounterParticipant");
        Component participant = prefab.GetComponent(participantType);
        Assert.That(participant, Is.Not.Null, $"EncounterParticipant가 없습니다: {prefabPath}");

        Assert.That(ReadBoolProperty(participant, "UseIncapacitation"), Is.False,
            "증폭기는 무력화/부활 흐름에 들어가면 안 됩니다.");
        Assert.That(ReadBoolProperty(participant, "KeepCorpseAfterDeath"), Is.True,
            "증폭기는 사망 후 시체 오브젝트를 유지해야 합니다.");

        var serializedParticipant = new SerializedObject(participant);
        Assert.That(serializedParticipant.FindProperty("_reviveStartPhase").intValue, Is.EqualTo(1),
            "율리아전 페이즈가 없어(항상 1) 첫 사망부터 시체 부활을 걸어야 합니다.");
        Assert.That(serializedParticipant.FindProperty("_corpseReviveDelayRounds").intValue, Is.EqualTo(2));
        Assert.That(serializedParticipant.FindProperty("_reviveHpRatio").floatValue, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(serializedParticipant.FindProperty("_reviveGroupId").stringValue, Is.EqualTo("YuliaAmplifier"),
            "두 증폭기가 모두 쓰러지면 함께 부활하도록 같은 부활 묶음이어야 합니다.");
    }

    [Test]
    public void ComputeGroupReviveRounds_UsesEarliestOnlyWhenWholeGroupIsDown()
    {
        Assert.That(ComputeGroupReviveRounds(new[] { true, true }, new[] { 1, 2 }), Is.EqualTo(1),
            "시차를 두고 모두 쓰러지면 먼저 쓰러진 쪽의 남은 라운드에 맞춰야 합니다.");
        Assert.That(ComputeGroupReviveRounds(new[] { true, true }, new[] { 2, 2 }), Is.EqualTo(2));
        Assert.That(ComputeGroupReviveRounds(new[] { true, false }, new[] { 2, -1 }), Is.EqualTo(-1),
            "하나라도 살아 있으면 각자 부활해야 합니다.");
        Assert.That(ComputeGroupReviveRounds(new[] { true, true }, new[] { 0, 2 }), Is.EqualTo(0),
            "부활이 거절돼 0에 머문 쪽이 있으면 그 시점에 맞춰야 합니다.");
        Assert.That(ComputeGroupReviveRounds(new[] { true, true }, new[] { -1, 2 }), Is.EqualTo(2),
            "카운트다운이 없는(사망 셀을 모르는) 쪽은 맞출 기준에서 빠져야 합니다.");
    }

    [Test]
    public void SyncGroupRevival_AlignsCountdownsOnlyWhenWholeGroupIsDown()
    {
        string groupId = "AmplifierGroup_Test_" + Guid.NewGuid().ToString("N");
        var firstObject = new GameObject("AmplifierGroup_First_Test");
        var secondObject = new GameObject("AmplifierGroup_Second_Test");

        try
        {
            Component first = AddGroupMember(firstObject, groupId, roundsLeft: 1, isDead: true);
            Component second = AddGroupMember(secondObject, groupId, roundsLeft: 2, isDead: true);

            InvokePrivate(second, "SyncGroupRevival");
            Assert.That(ReadIntProperty(first, "CorpseReviveRoundsLeft"), Is.EqualTo(1));
            Assert.That(ReadIntProperty(second, "CorpseReviveRoundsLeft"), Is.EqualTo(1),
                "늦게 쓰러진 증폭기가 먼저 쓰러진 쪽과 같은 라운드에 부활해야 합니다.");

            SetProperty(firstObject.GetComponent(FindRuntimeType("BattleCharactor")), "IsDead", false);
            SetPrivateField(first, "_corpseReviveRoundsLeft", -1);
            SetPrivateField(second, "_corpseReviveRoundsLeft", 2);

            InvokePrivate(second, "SyncGroupRevival");
            Assert.That(ReadIntProperty(second, "CorpseReviveRoundsLeft"), Is.EqualTo(2),
                "다른 증폭기가 살아 있으면 쓰러진 쪽은 자기 카운트다운으로 부활해야 합니다.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(secondObject);
            UnityEngine.Object.DestroyImmediate(firstObject);
        }
    }

    [Test]
    public void DisableCorpseInteraction_Disables3DAnd2DColliders()
    {
        Type spawnerType = FindRuntimeType("EnemySpawner");
        MethodInfo method = spawnerType.GetMethod(
            "DisableCorpseInteraction",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "시체 상호작용 차단 메서드를 찾지 못했습니다.");

        var corpse = new GameObject("AmplifierCorpse_Test");
        try
        {
            BoxCollider collider3D = corpse.AddComponent<BoxCollider>();

            var child = new GameObject("Child");
            child.transform.SetParent(corpse.transform);
            BoxCollider2D collider2D = child.AddComponent<BoxCollider2D>();

            method.Invoke(null, new object[] { corpse });

            Assert.That(collider3D.enabled, Is.False);
            Assert.That(collider2D.enabled, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(corpse);
        }
    }

    [Test]
    public void BossPrefab_DoesNotResummonAmplifiersAtPhase2()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        Assert.That(prefab, Is.Not.Null, $"보스 프리팹을 불러오지 못했습니다: {BossPrefabPath}");

        Type bossType = FindRuntimeType("BossController");
        Component boss = prefab.GetComponent(bossType);
        Assert.That(boss, Is.Not.Null, "40001 프리팹에 BossController가 없습니다.");

        var serializedBoss = new SerializedObject(boss);
        SerializedProperty entries = serializedBoss.FindProperty("_summonEntries");
        Assert.That(entries, Is.Not.Null, "BossController._summonEntries를 찾지 못했습니다.");

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            string enemyId = entry.FindPropertyRelative("MinionEnemyId").stringValue;
            int phase = entry.FindPropertyRelative("Phase").intValue;

            bool duplicateAmplifier =
                phase == 2 && (enemyId == "40002" || enemyId == "40003");
            Assert.That(duplicateAmplifier, Is.False,
                $"동일 시체 부활과 충돌하는 phase2 신규 소환이 남아 있습니다: {enemyId}");
        }
    }

    [Test]
    public void CorpseReviveCountdown_DoesNotResetAndTicksOncePerRound()
    {
        var blackboardObject = new GameObject("EncounterBlackboard_Test");
        var participantObject = new GameObject("AmplifierParticipant_Test");

        try
        {
            Type blackboardType = FindRuntimeType("EncounterBlackboard");
            Component blackboard = blackboardObject.AddComponent(blackboardType);
            Type battleType = FindRuntimeType("BattleCharactor");
            Component battle = participantObject.AddComponent(battleType);
            Type participantType = FindRuntimeType("EncounterParticipant");
            Component participant = participantObject.AddComponent(participantType);

            SetPrivateField(participant, "_blackboard", blackboard);
            SetPrivateField(participant, "_useIncapacitation", false);
            SetPrivateField(participant, "_keepCorpseAfterDeath", true);
            SetPrivateField(participant, "_deathGridNumber", 300);
            SetPrivateField(participant, "_corpseReviveRoundsLeft", -1);
            SetProperty(battle, "IsDead", true);

            InvokePrivate(participant, "HandlePhaseAdvanced", 2);
            Assert.That(ReadIntProperty(participant, "CorpseReviveRoundsLeft"), Is.EqualTo(2));

            InvokePrivate(participant, "HandlePhaseAdvanced", 3);
            Assert.That(ReadIntProperty(participant, "CorpseReviveRoundsLeft"), Is.EqualTo(2),
                "후속 페이즈 이벤트가 진행 중인 부활 카운트다운을 리셋했습니다.");

            InvokePrivate(participant, "HandleTurnStarted", 10, null);
            Assert.That(ReadIntProperty(participant, "CorpseReviveRoundsLeft"), Is.EqualTo(1));

            InvokePrivate(participant, "HandleTurnStarted", 10, null);
            Assert.That(ReadIntProperty(participant, "CorpseReviveRoundsLeft"), Is.EqualTo(1),
                "동일 roundIndex에서 카운트다운이 두 번 감소했습니다.");

            InvokePrivate(participant, "HandleTurnStarted", 11, null);
            Assert.That(ReadIntProperty(participant, "CorpseReviveRoundsLeft"), Is.EqualTo(0),
                "두 번째 고유 라운드 경계에서 부활 대기 상태가 되지 않았습니다.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(participantObject);
            UnityEngine.Object.DestroyImmediate(blackboardObject);
        }
    }

    [Test]
    public void ClearYuliaReservationFrom_RemovesOnlyReservationAndKeepsRoundGate()
    {
        var blackboardObject = new GameObject("EncounterBlackboard_Reservation_Test");
        var sourceObject = new GameObject("AmplifierSource_Test");

        try
        {
            Type blackboardType = FindRuntimeType("EncounterBlackboard");
            Component blackboard = blackboardObject.AddComponent(blackboardType);
            Type battleType = FindRuntimeType("BattleCharactor");
            Component source = sourceObject.AddComponent(battleType);

            MethodInfo reserve = blackboardType.GetMethod("TryReserveYulia");
            MethodInfo clear = blackboardType.GetMethod("ClearYuliaReservationFrom");
            Assert.That(reserve, Is.Not.Null);
            Assert.That(clear, Is.Not.Null);
            Assert.That((bool)reserve.Invoke(blackboard, new object[] { 1, source }), Is.True);

            clear.Invoke(blackboard, new object[] { source });

            Assert.That(ReadPrivateField<int>(blackboard, "_reservedSocket"), Is.Zero);
            Assert.That(ReadPrivateField<object>(blackboard, "_reservedSource"), Is.Null);
            Assert.That(ReadPrivateField<bool>(blackboard, "_releaseUsedThisRound"), Is.True,
                "사망으로 예약만 무효화해야 하며 같은 라운드의 사용 게이트를 되돌리면 안 됩니다.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceObject);
            UnityEngine.Object.DestroyImmediate(blackboardObject);
        }
    }

    private static int ComputeGroupReviveRounds(bool[] isDead, int[] roundsLeft)
    {
        MethodInfo method = FindRuntimeType("EncounterParticipant").GetMethod(
            "ComputeGroupReviveRounds",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "묶음 부활 계산 메서드를 찾지 못했습니다.");
        return (int)method.Invoke(null, new object[] { isDead, roundsLeft });
    }

    // EditMode AddComponent는 Awake를 부르지 않으므로 _self를 직접 넣는다.
    private static Component AddGroupMember(GameObject owner, string groupId, int roundsLeft, bool isDead)
    {
        Component battle = owner.AddComponent(FindRuntimeType("BattleCharactor"));
        Component participant = owner.AddComponent(FindRuntimeType("EncounterParticipant"));
        SetPrivateField(participant, "_self", battle);
        SetPrivateField(participant, "_useIncapacitation", false);
        SetPrivateField(participant, "_keepCorpseAfterDeath", true);
        SetPrivateField(participant, "_reviveGroupId", groupId);
        SetPrivateField(participant, "_corpseReviveRoundsLeft", roundsLeft);
        SetProperty(battle, "IsDead", isDead);
        return participant;
    }

    private static bool ReadBoolProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, $"프로퍼티를 찾지 못했습니다: {propertyName}");
        return (bool)property.GetValue(instance);
    }

    private static int ReadIntProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, $"프로퍼티를 찾지 못했습니다: {propertyName}");
        return (int)property.GetValue(instance);
    }

    private static T ReadPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"필드를 찾지 못했습니다: {fieldName}");
        return (T)field.GetValue(instance);
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"필드를 찾지 못했습니다: {fieldName}");
        field.SetValue(instance, value);
    }

    private static void SetProperty(object instance, string propertyName, object value)
    {
        PropertyInfo property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, $"프로퍼티를 찾지 못했습니다: {propertyName}");
        property.SetValue(instance, value);
    }

    private static void InvokePrivate(object instance, string methodName, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, $"메서드를 찾지 못했습니다: {methodName}");
        method.Invoke(instance, args);
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
