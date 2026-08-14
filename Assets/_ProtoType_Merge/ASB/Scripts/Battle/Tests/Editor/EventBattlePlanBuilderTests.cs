using System;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// 이벤트 전투 키-빌드(EnemySpawnPlanBuilder.TryBuildFromEventBattleKey) 검증.
///
/// 게임 코드는 Assembly-CSharp에 있어 테스트 asmdef가 직접 참조할 수 없으므로 리플렉션으로 호출한다
/// (BattleLogicalSlotMapTests와 동일 패턴).
///
/// 여기서는 카탈로그 데이터가 필요 없는 fail-fast 가드(§9-2)만 결정적으로 검증한다.
/// 스탯/스킬/인질 스냅샷 동등성(§9-1, BE002~BE005)은 EventScriptCatalog(DHScene_3 로드 SO)와
/// BattleScenarioCatalog 데이터가 있어야 하며, EditMode 주입 수단이 없어 별도 픽스처/런타임 검증이 필요하다
/// (아래 SnapshotEquivalence_RequiresCatalogFixture 참조).
/// </summary>
public sealed class EventBattlePlanBuilderTests
{
    private static Type BuilderType => FindRuntimeType("EnemySpawnPlanBuilder");
    private static Type SourceType => FindRuntimeType("EventBattlePlanSource");
    private static Type CatalogType => FindRuntimeType("EventScriptCatalog");

    [TestCase("")]
    [TestCase("   ")]
    public void TryBuildFromEventBattleKey_EmptyBattleKey_FailsFast(string battleKey)
    {
        bool success = InvokeTryBuildFromEventBattleKey(1, battleKey, 1, out object plan, out string error);

        Assert.That(success, Is.False, "빈 BattleKey는 즉시 실패해야 한다(§4).");
        Assert.That(plan, Is.Null, "실패 시 부분 결과(plan)를 반환하면 안 된다.");
        Assert.That(string.IsNullOrEmpty(error), Is.False, "실패 사유(error)가 채워져야 한다.");
    }

    [Test]
    public void TryBuildUnits_EmptyBattleKey_FailsFast()
    {
        bool success = InvokeTryBuildUnits(1, string.Empty, 1, failFast: true, out object units, out string error);

        Assert.That(success, Is.False);
        Assert.That(string.IsNullOrEmpty(error), Is.False);
        // units는 빈 리스트(부분 결과 없음).
        Assert.That(units, Is.Not.Null);
    }

    [Test]
    public void TryBuildFromEventBattleKey_CatalogMissing_FailsFast()
    {
        if (GetCatalogInstance() != null)
        {
            Assert.Ignore("EventScriptCatalog.Instance가 이미 존재(DDOL 잔존) — 카탈로그 미로드 경로를 격리 검증할 수 없어 스킵.");
            return;
        }

        // 비어있지 않은 임의 키 → 카탈로그가 없으면 진입 실패해야 한다(§9-2: 직접 실행 = 정상 중단).
        bool success = InvokeTryBuildFromEventBattleKey(1, "Start_BE999", 1, out object plan, out string error);

        Assert.That(success, Is.False);
        Assert.That(plan, Is.Null);
        Assert.That(error, Does.Contain("EventScriptCatalog"),
            "카탈로그 미로드 시 사유에 카탈로그 부재가 드러나야 한다.");
    }

    /// <summary>
    /// §9-1 동등성 스냅샷(UnitKey/슬롯/레벨, HP/ATK/DEF/경험치+전투스탯, PrefabKey/SourceUnitKey,
    /// 스킬 개수·순서·전체 필드, 인질 슬롯·HP·초기HP·위협 설정 / BE002~BE004 + 시나리오 없는 BE005)은
    /// EventScriptCatalog(DHScene_3 로드) + BattleScenarioCatalog 데이터가 필요하다.
    /// EditMode에서 카탈로그 주입 수단이 없어 현재는 런타임(DHScene_3 경유) 또는 전용 픽스처가 있어야 한다.
    /// 픽스처(카탈로그 SO 주입 하네스) 마련 후 활성화할 것.
    /// </summary>
    [Test]
    [Ignore("카탈로그 픽스처 필요: EventScriptCatalog/BattleScenarioCatalog 주입 하네스 마련 후 활성화. 그전엔 DHScene_3 런타임 검증(§9-2)으로 대체.")]
    public void SnapshotEquivalence_RequiresCatalogFixture()
    {
    }

    // ----- reflection helpers -----

    private static bool InvokeTryBuildFromEventBattleKey(
        int zoneId, string battleKey, int enemyLevel, out object plan, out string error)
    {
        MethodInfo method = BuilderType.GetMethod(
            "TryBuildFromEventBattleKey", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "EnemySpawnPlanBuilder.TryBuildFromEventBattleKey 를 찾지 못했습니다.");

        object[] args = { zoneId, battleKey, enemyLevel, null, null };
        bool success = (bool)method.Invoke(null, args);
        plan = args[3];
        error = args[4] as string ?? string.Empty;
        return success;
    }

    private static bool InvokeTryBuildUnits(
        int zoneId, string battleKey, int enemyLevel, bool failFast, out object units, out string error)
    {
        MethodInfo method = SourceType.GetMethod(
            "TryBuildUnits", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "EventBattlePlanSource.TryBuildUnits 를 찾지 못했습니다.");

        object[] args = { zoneId, battleKey, enemyLevel, failFast, null, null };
        bool success = (bool)method.Invoke(null, args);
        units = args[4];
        error = args[5] as string ?? string.Empty;
        return success;
    }

    private static object GetCatalogInstance()
    {
        PropertyInfo instanceProperty = CatalogType.GetProperty(
            "Instance", BindingFlags.Public | BindingFlags.Static);
        return instanceProperty != null ? instanceProperty.GetValue(null) : null;
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
