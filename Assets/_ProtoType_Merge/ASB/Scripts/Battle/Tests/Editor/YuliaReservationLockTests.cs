using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// [2026-10-02 변경] 율리아 예약 고정 규칙: 대기 예약이 있으면 능력개방 불가(덮어쓰기 금지),
/// 예약한 증폭기가 죽어도 예약은 유지, 소비는 소켓만 비교.
/// 런타임이 Assembly-CSharp라 reflection으로 접근한다(AmplifierDeathPolicyContractTests와 같은 방식).
/// </summary>
public sealed class YuliaReservationLockTests
{
    private GameObject _blackboardObject;
    private Component _blackboard;
    private Type _blackboardType;

    [SetUp]
    public void SetUp()
    {
        _blackboardObject = new GameObject("EncounterBlackboard_Lock_Test");
        _blackboardType = FindRuntimeType("EncounterBlackboard");
        _blackboard = _blackboardObject.AddComponent(_blackboardType);
    }

    [TearDown]
    public void TearDown()
    {
        if (_blackboardObject != null) UnityEngine.Object.DestroyImmediate(_blackboardObject);
    }

    [Test]
    public void PendingReservation_BlocksAnyNewRelease_EvenNextRound()
    {
        var sourceObject = new GameObject("Amplifier_Socket2");
        var otherObject = new GameObject("Amplifier_Socket1");
        try
        {
            Component socket2Source = AddBattleCharactor(sourceObject);
            Component socket1Source = AddBattleCharactor(otherObject);

            Assert.That(Reserve(2, socket2Source), Is.True);
            Invoke("NotifyRound", 5);   // 다음 라운드: 라운드 게이트는 풀리지만 대기 예약이 막아야 한다.

            Assert.That(CanReserve(1), Is.False, "대기 예약이 있으면 소켓1도 능력개방할 수 없어야 합니다.");
            Assert.That(CanReserve(2), Is.False);
            Assert.That(Reserve(1, socket1Source), Is.False, "소켓1 우선 덮어쓰기는 제거됐습니다.");
            Assert.That(ReadPrivateField<int>("_reservedSocket"), Is.EqualTo(2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceObject);
            UnityEngine.Object.DestroyImmediate(otherObject);
        }
    }

    [Test]
    public void ReservationSurvivesSourceDestruction_AndConsumesBySocket()
    {
        var sourceObject = new GameObject("Amplifier_Dies");
        Component source = AddBattleCharactor(sourceObject);
        Assert.That(Reserve(1, source), Is.True);

        UnityEngine.Object.DestroyImmediate(sourceObject);   // 예약한 증폭기 파괴

        object[] peekArgs = { null, 0, null };
        bool found = (bool)_blackboardType.GetMethod("TryPeekYuliaReservation").Invoke(_blackboard, peekArgs);
        Assert.That(found, Is.True, "예약한 증폭기가 죽어도 예약은 남아야 합니다.");
        Assert.That((int)peekArgs[1], Is.EqualTo(1));

        _blackboardType.GetMethod("ConsumeYuliaReservationIfMatch").Invoke(_blackboard, new object[] { 1, null });
        Assert.That(ReadPrivateField<int>("_reservedSocket"), Is.Zero, "소비는 소켓만 비교해야 합니다.");
    }

    [Test]
    public void AfterConsume_NextRoundCanReserveAgain()
    {
        var sourceObject = new GameObject("Amplifier_Reuse");
        try
        {
            Component source = AddBattleCharactor(sourceObject);
            Assert.That(Reserve(2, source), Is.True);
            Assert.That(CanReserve(1), Is.False, "같은 라운드에는 1회 게이트로 막혀야 합니다.");

            _blackboardType.GetMethod("ConsumeYuliaReservationIfMatch").Invoke(_blackboard, new object[] { 2, source });
            Invoke("NotifyRound", 7);

            Assert.That(CanReserve(1), Is.True);
            Assert.That(Reserve(1, source), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceObject);
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────

    private bool Reserve(int socket, Component source) =>
        (bool)_blackboardType.GetMethod("TryReserveYulia").Invoke(_blackboard, new object[] { socket, source });

    private bool CanReserve(int socket) =>
        (bool)_blackboardType.GetMethod("CanReserveYulia").Invoke(_blackboard, new object[] { socket });

    private void Invoke(string method, int arg) =>
        _blackboardType.GetMethod(method).Invoke(_blackboard, new object[] { arg });

    private static Component AddBattleCharactor(GameObject go) =>
        go.AddComponent(FindRuntimeType("BattleCharactor"));

    private T ReadPrivateField<T>(string fieldName)
    {
        FieldInfo field = _blackboardType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"필드를 찾지 못했습니다: {fieldName}");
        return (T)field.GetValue(_blackboard);
    }

    private static Type FindRuntimeType(string fullName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName, false))
            .FirstOrDefault(t => t != null);
        Assert.That(type, Is.Not.Null, $"타입 '{fullName}'을 찾지 못했습니다.");
        return type;
    }
}
