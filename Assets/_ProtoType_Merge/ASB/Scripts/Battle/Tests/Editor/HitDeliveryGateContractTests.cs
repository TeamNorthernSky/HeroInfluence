using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// 리팩터링 1단계가 세운 계약을 지키는지 검증한다.
///
/// 계약: <b>연출은 피해 확정의 '시점'만 정하고 '여부'는 정하지 못한다.</b>
/// 투사체가 취소·타임아웃돼도 피해·상태이상·반격은 규칙 계층이 확정한다.
///
/// 이 계약은 "게이트를 규칙 경로에서 아무도 읽지 않는다"는 구조로 보장되므로,
/// 여기서는 (a) 게이트 자체의 의미와 (b) 규칙↔연출 결합이 되살아나지 않았는지를 검사한다.
/// 전투 전체를 도는 종단 검증은 PlayMode가 필요하다(별도).
/// </summary>
public sealed class HitDeliveryGateContractTests
{
    private static Type GateType => FindRuntimeType("ASB.Work.Battle.Core.HitDeliveryGate");
    private static Type ResultEnumType => FindRuntimeType("ASB.Work.Battle.Core.ProjectileDeliveryResult");
    private static Type BattleManagerType => FindRuntimeType("BattleManager");

    private static Type FindRuntimeType(string fullName)
    {
        Type found = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.FullName == fullName);

        Assert.That(found, Is.Not.Null, $"런타임 타입을 찾지 못했습니다: {fullName}");
        return found;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null);
        }
    }

    private static object NewGate() => Activator.CreateInstance(GateType);

    private static void SetResult(object gate, string enumMemberName)
    {
        object value = Enum.Parse(ResultEnumType, enumMemberName);
        GateType.GetMethod("SetResult").Invoke(gate, new[] { value });
    }

    private static bool ShouldPlayImpactPresentation(object gate)
        => (bool)GateType.GetProperty("ShouldPlayImpactPresentation").GetValue(gate);

    // ── 게이트 의미 ────────────────────────────────────────────────

    [Test]
    public void Gate_DefaultsToArrived_SoNonProjectilePathsPresentNormally()
    {
        object gate = NewGate();
        Assert.That(ShouldPlayImpactPresentation(gate), Is.True,
            "투사체가 없는 전투 경로는 기본값으로 임팩트 연출을 재생해야 합니다.");
    }

    [Test]
    public void Gate_FallbackStillPresents()
    {
        object gate = NewGate();
        SetResult(gate, "Fallback");
        Assert.That(ShouldPlayImpactPresentation(gate), Is.True,
            "폴백은 도착으로 간주해 임팩트 연출을 재생해야 합니다.");
    }

    [Test]
    public void Gate_CancelledSuppressesPresentationOnly()
    {
        object gate = NewGate();
        SetResult(gate, "Cancelled");
        Assert.That(ShouldPlayImpactPresentation(gate), Is.False);
    }

    [Test]
    public void Gate_CancelledLatches_LaterBeatCannotRevive()
    {
        object gate = NewGate();
        SetResult(gate, "Cancelled");
        SetResult(gate, "Arrived");

        Assert.That(ShouldPlayImpactPresentation(gate), Is.False,
            "취소된 전달은 뒤따르는 콤보 비트가 되살릴 수 없어야 합니다.");
    }

    // ── 규칙↔연출 결합이 되살아나지 않았는지 ──────────────────────

    [Test]
    public void Gate_HasNoRuleFacingMember()
    {
        Assert.That(GateType.GetMember("CanApplyEffects"), Is.Empty,
            "CanApplyEffects는 '규칙 효과를 적용해도 되는가'라는 뜻이라 연출이 피해를 취소할 수 있게 만듭니다. " +
            "ShouldPlayImpactPresentation(연출 전용)으로 대체됐습니다.");
    }

    [Test]
    public void BattleManager_HasNoGateDependentRuleGuards()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.Instance | BindingFlags.Static;

        Assert.That(BattleManagerType.GetMethod("CanApplyStatusEffect", all), Is.Null,
            "상태이상 적용이 투사체 전달 결과에 좌우되면 안 됩니다.");
        Assert.That(BattleManagerType.GetMethod("CanApplyDeliveryEffects", all), Is.Null,
            "반격 성립이 투사체 전달 결과에 좌우되면 안 됩니다.");
    }

    [Test]
    public void CollectCounterAttackRequests_DoesNotTakeDeliveryGates()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.Instance | BindingFlags.Static;

        MethodInfo method = BattleManagerType.GetMethod("CollectCounterAttackRequests", all);
        Assert.That(method, Is.Not.Null, "CollectCounterAttackRequests를 찾지 못했습니다.");

        ParameterInfo[] parameters = method.GetParameters();
        Assert.That(parameters.Length, Is.EqualTo(1),
            "반격 수집은 SkillExecutionResult만 받아야 합니다. " +
            "전달 게이트를 다시 인자로 받으면 연출이 반격을 취소할 수 있게 됩니다.");
    }

    // ── 확정 스윕 ─────────────────────────────────────────────────

    [Test]
    public void FlushPendingCommits_InvokesEveryPendingCommit()
    {
        const BindingFlags all = BindingFlags.NonPublic | BindingFlags.Static;
        MethodInfo flush = BattleManagerType.GetMethod("FlushPendingCommits", all);
        Assert.That(flush, Is.Not.Null,
            "FlushPendingCommits가 없습니다. 연출이 확정하지 못한 히트를 규칙 계층이 쓸어담는 장치입니다.");

        Type hitResultType = FindRuntimeType("ASB.Work.Battle.Core.BattleHitResult");
        Type funcType = typeof(Func<>).MakeGenericType(hitResultType);
        Type listType = typeof(List<>).MakeGenericType(funcType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType);

        int invoked = 0;
        // 실제 커밋 델리게이트와 같은 모양(인자 없음 → BattleHitResult 반환)으로 대역을 만든다.
        MethodInfo maker = typeof(HitDeliveryGateContractTests)
            .GetMethod(nameof(MakeCounter), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(hitResultType);

        Action bump = () => invoked++;
        list.Add(maker.Invoke(null, new object[] { bump }));
        list.Add(null); // null 항목이 섞여도 죽지 않아야 한다
        list.Add(maker.Invoke(null, new object[] { bump }));

        flush.Invoke(null, new object[] { list });

        Assert.That(invoked, Is.EqualTo(2), "대기 중인 확정이 전부 실행돼야 합니다.");
    }

    [Test]
    public void FlushPendingCommits_ToleratesNullList()
    {
        const BindingFlags all = BindingFlags.NonPublic | BindingFlags.Static;
        MethodInfo flush = BattleManagerType.GetMethod("FlushPendingCommits", all);
        Assert.That(flush, Is.Not.Null);

        Assert.DoesNotThrow(() => flush.Invoke(null, new object[] { null }));
    }

    private static Func<T> MakeCounter<T>(Action onInvoke) where T : class
    {
        return () =>
        {
            onInvoke();
            return null;
        };
    }
}
