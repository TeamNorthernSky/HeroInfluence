using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 절망의 굴렁쇠(FV40005) 돌진 연출 계약: 광역 커스텀 연출 등록, 피해 확정 1회 가드, 돌진 위치 유지·복귀.
/// 게임 코드는 Assembly-CSharp에 있어 리플렉션으로 호출한다(HoopSameRowTargetingTests와 동일 패턴).
/// </summary>
public sealed class HoopPresentationTests
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < created.Count; i++)
        {
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    [Test]
    public void Registry_ResolvesCatalogAndEventKeys_AndKeepsThemAfterClear()
    {
        Type registry = FindRuntimeType("SkillPresentationSequenceRegistry");

        Assert.That(TryGetAoE(registry, "FV40005_1"), Is.True);
        Assert.That(TryGetAoE(registry, "40005_1"), Is.True);
        Assert.That(TryGetAoE(registry, "FV40005_2"), Is.False);

        registry.GetMethod("Clear", AnyStatic).Invoke(null, null);

        Assert.That(TryGetAoE(registry, "FV40005_1"), Is.True, "Clear()는 기본 등록 상태로 리셋해야 한다.");
        Assert.That(TryGetAoE(registry, "40005_1"), Is.True);
    }

    [Test]
    public void AoEHitApplier_RunsEachCallbackOnce_EvenWhenInvokedTwice()
    {
        Type contextType = FindRuntimeType("ASB.Work.Battle.Core.DamageContext");
        Type hitResultType = FindRuntimeType("ASB.Work.Battle.Core.BattleHitResult");
        Type funcType = typeof(Func<>).MakeGenericType(hitResultType);

        // 대상 없는 컨텍스트 2개 → 콜백만 실행되고 피격 연출은 건너뛴다.
        IList contexts = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(contextType));
        contexts.Add(null);
        contexts.Add(null);

        var counter = new CallCounter();
        IList callbacks = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(funcType));
        for (int i = 0; i < 2; i++)
        {
            Expression body = Expression.Block(
                Expression.Call(Expression.Constant(counter), typeof(CallCounter).GetMethod(nameof(CallCounter.Hit))),
                Expression.Constant(null, hitResultType));
            callbacks.Add(Expression.Lambda(funcType, body).Compile());
        }

        Type director = FindRuntimeType("SkillPresentationDirector");
        MethodInfo create = director.GetMethod("CreateAoEHitApplier", AnyStatic);
        Assert.That(create, Is.Not.Null);
        var applyHits = (Action)create.Invoke(null, new object[] { null, contexts, callbacks, 2, 1f, null });

        applyHits();
        applyHits();

        Assert.That(counter.Count, Is.EqualTo(2), "대상 2명 × 1회. 두 번째 호출은 무시해야 한다.");
    }

    [Test]
    public void DashEnd_StopsShortOfTarget_AtCasterHeight()
    {
        Component target = CreateUnit("Target");
        target.transform.position = new Vector3(-3f, 2f, 0f);

        Type sequence = FindRuntimeType("HoopDashPresentationSequence");
        MethodInfo resolve = sequence.GetMethod("ResolveDashEnd", AnyStatic);
        var end = (Vector3)resolve.Invoke(null, new object[] { new Vector3(1f, 0f, 0f), target, .5f });

        Assert.That(end.x, Is.EqualTo(-2.5f).Within(1e-4f));
        Assert.That(end.y, Is.EqualTo(0f).Within(1e-4f));
        Assert.That(end.z, Is.EqualTo(0f).Within(1e-4f));
    }

    [Test]
    public void WheelVisual_RestoresHome_WhenOwnerSurvivesSkill()
    {
        (Component owner, Component visual) = CreateHoop();
        Vector3 home = new Vector3(2f, 0f, 1f);
        BeginDashHold(visual, home);
        owner.transform.position = new Vector3(-1f, 0f, 1f);

        Resolve(visual, owner);

        Assert.That(owner.transform.position, Is.EqualTo(home));
        Assert.That(IsDashHoldActive(visual), Is.False);
    }

    [Test]
    public void WheelVisual_IgnoresOtherActorsSkill()
    {
        (Component owner, Component visual) = CreateHoop();
        Component other = CreateUnit("OtherActor");
        Vector3 dashed = new Vector3(-1f, 0f, 1f);
        BeginDashHold(visual, new Vector3(2f, 0f, 1f));
        owner.transform.position = dashed;

        Resolve(visual, other);

        Assert.That(owner.transform.position, Is.EqualTo(dashed));
        Assert.That(IsDashHoldActive(visual), Is.True);
    }

    [Test]
    public void WheelVisual_KeepsDashPosition_WhenOwnerDied()
    {
        (Component owner, Component visual) = CreateHoop();
        Vector3 dashed = new Vector3(-1f, 0f, 1f);
        BeginDashHold(visual, new Vector3(2f, 0f, 1f));
        owner.transform.position = dashed;
        owner.GetType().GetMethod("TakeDamage", AnyInstance).Invoke(owner, new object[] { 1_000_000f });
        Assert.That((bool)owner.GetType().GetProperty("IsDead").GetValue(owner), Is.True);

        Resolve(visual, owner);

        Assert.That(owner.transform.position, Is.EqualTo(dashed), "사망 시 파괴 이펙트가 도착 위치에 나오도록 유지한다.");
        Assert.That(IsDashHoldActive(visual), Is.True, "부활하면 되돌릴 수 있게 원위치 기억은 유지한다.");
    }

    // ----- helpers -----

    public sealed class CallCounter
    {
        public int Count;
        public void Hit() => Count++;
    }

    private static bool TryGetAoE(Type registry, string key)
    {
        MethodInfo tryGet = registry.GetMethod("TryGetAoE", AnyStatic);
        Assert.That(tryGet, Is.Not.Null);
        object[] args = { key, null };
        bool found = (bool)tryGet.Invoke(null, args);
        return found && args[1] != null;
    }

    private (Component owner, Component visual) CreateHoop()
    {
        Component owner = CreateUnit("Hoop");
        // EditMode AddComponent는 Awake/OnEnable/Start를 부르지 않는다 → 외형 생성 없이 상태 로직만 검증.
        Component visual = owner.gameObject.AddComponent(FindRuntimeType("HoopWheelVisual"));
        FieldInfo ownerField = visual.GetType().GetField("owner", AnyInstance);
        Assert.That(ownerField, Is.Not.Null);
        ownerField.SetValue(visual, owner);
        return (owner, visual);
    }

    private Component CreateUnit(string name)
    {
        var go = new GameObject(name);
        created.Add(go);
        Type unitType = FindRuntimeType("BattleCharactor");
        Component unit = go.AddComponent(unitType);
        FieldInfo hp = unitType.GetField("currentHp", AnyInstance);
        Assert.That(hp, Is.Not.Null);
        hp.SetValue(unit, 30f);
        return unit;
    }

    private static void BeginDashHold(Component visual, Vector3 home)
    {
        visual.GetType().GetMethod("BeginDashHold", AnyInstance).Invoke(visual, new object[] { home });
    }

    private static bool IsDashHoldActive(Component visual)
    {
        return (bool)visual.GetType().GetProperty("IsDashHoldActive", AnyInstance).GetValue(visual);
    }

    private static void Resolve(Component visual, Component actor)
    {
        Type contextType = FindRuntimeType("SkillResolutionContext");
        object context = Activator.CreateInstance(contextType, actor, null, null, true, null);
        MethodInfo handle = visual.GetType().GetMethod("HandleSkillResolved", AnyInstance);
        Assert.That(handle, Is.Not.Null);
        handle.Invoke(visual, new[] { context });
    }

    private static Type FindRuntimeType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }

        throw new InvalidOperationException($"Runtime type '{typeName}' was not found in loaded assemblies.");
    }
}
