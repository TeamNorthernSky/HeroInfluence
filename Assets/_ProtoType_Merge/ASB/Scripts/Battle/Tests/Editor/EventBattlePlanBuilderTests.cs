using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 이벤트 전투 키-빌드(EnemySpawnPlanBuilder.TryBuildFromEventBattleKey) 검증.
///
/// 게임 코드는 Assembly-CSharp에 있어 테스트 asmdef가 직접 참조할 수 없으므로 리플렉션으로 호출한다
/// (BattleLogicalSlotMapTests와 동일 패턴).
///
/// 여기서는 카탈로그 데이터가 필요 없는 fail-fast 가드(§9-2)만 결정적으로 검증한다.
/// 스탯/스킬 스냅샷 동등성(§9-1, BE002~BE005)은 EventScriptCatalog(DHScene_3 로드 SO)가
/// 있어야 하며, EditMode 주입 수단이 없어 별도 픽스처/런타임 검증이 필요하다
/// (아래 SnapshotEquivalence_RequiresCatalogFixture 참조).
/// </summary>
public sealed class EventBattlePlanBuilderTests
{
    private static Type BuilderType => FindRuntimeType("EnemySpawnPlanBuilder");
    private static Type SourceType => FindRuntimeType("EventBattlePlanSource");
    private static Type CatalogType => FindRuntimeType("EventScriptCatalog");

    // DHScene_3 DataStorage의 2구역 이벤트 전투 유닛 테이블(굴렁쇠 소환 데이터 출처).
    private const string BetaZone2UnitTablePath =
        "Assets/_ProtoType_Merge/ASB/Data/Tables/H.I 베타 2구역 이벤트 전투 테이블/EnemyUnit1SectorDataTable.asset";
    // 같은 구역의 이벤트 전투 그룹 테이블(율리아 보스전 BE490 출처).
    private const string BetaZone2GroupTablePath =
        "Assets/_ProtoType_Merge/ASB/Data/Tables/H.I 베타 2구역 이벤트 전투 테이블/EnemyGroupDataTable.asset";

    [Test]
    public void GuardSkill_EventAndCatalogKeysResolveToSameHandler()
    {
        AssertSameHandler("FV20003_2", "20003_2", "GuardSkillHandler");
    }

    // 율리아 전투(BE490)는 2구역 이벤트 전투라 숫자 키로 들어온다. 카탈로그 키와 같은 핸들러여야 한다.
    [TestCase("FV40001_1", "40001_1", "AoEDamageSkillHandler")]
    [TestCase("FV40001_2", "40001_2", "AoEDamageSkillHandler")]
    [TestCase("FV40001_3", "40001_3", "SummonSkillHandler")]
    [TestCase("FV40002_3", "40002_3", "AoEDamageSkillHandler")]
    [TestCase("FV40003_3", "40003_3", "AoEDamageSkillHandler")]
    [TestCase("FV40004_1", "40004_1", "SummonSkillHandler")]
    [TestCase("FV40005_1", "40005_1", "SelfDestructRowAoEHandler")]
    public void Sector4Skill_EventAndCatalogKeysResolveToSameHandler(string catalogKey, string eventKey, string handlerName)
    {
        AssertSameHandler(catalogKey, eventKey, handlerName);
    }

    // BossController 소환 매칭: FV 접두어 유무는 무시하고 적 번호·슬롯이 같아야 같은 스킬.
    [TestCase("FV40001_3", "40001_3", true)]
    [TestCase("40001_3", "FV40001_3", true)]
    [TestCase("FV40001_3", " FV40001_3 ", true)]
    [TestCase("FV40001_3", "40001_1", false)]
    [TestCase("FV40001_3", "40004_1", false)]
    [TestCase("FV40001_3", "", false)]
    [TestCase(null, "40001_3", false)]
    public void IsSameSkill_IgnoresCatalogPrefix(string a, string b, bool expected)
    {
        MethodInfo method = FindRuntimeType("EnemySkillKeyRules")
            .GetMethod("IsSameSkill", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        Assert.That((bool)method.Invoke(null, new object[] { a, b }), Is.EqualTo(expected));
    }

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

    [TestCase("")]
    [TestCase("   ")]
    public void TryBuildUnit_EmptyUnitKey_Fails(string unitKey)
    {
        bool success = InvokeTryBuildUnit(2, unitKey, 1, out object unit, out string error);

        Assert.That(success, Is.False, "빈 UnitKey는 즉시 실패해야 한다.");
        Assert.That(unit, Is.Null, "실패 시 유닛을 반환하면 안 된다.");
        Assert.That(string.IsNullOrEmpty(error), Is.False, "실패 사유(error)가 채워져야 한다.");
    }

    [Test]
    public void TryBuildEventUnitEntry_CatalogMissing_Fails()
    {
        if (GetCatalogInstance() != null)
        {
            Assert.Ignore("EventScriptCatalog.Instance가 이미 존재(DDOL 잔존) — 카탈로그 미로드 경로를 격리 검증할 수 없어 스킵.");
            return;
        }

        bool success = InvokeTryBuildEventUnitEntry(2, "FV40005", 1, out object entry, out string error);

        Assert.That(success, Is.False);
        Assert.That(entry, Is.Null);
        Assert.That(error, Does.Contain("EventScriptCatalog"));
    }

    // 굴렁쇠 소환은 이벤트 전투와 같은 구역 테이블(베타 2구역)에서 데이터·스킬을 만든다(4구역 카탈로그 미사용).
    // 소환 엔트리는 숫자 키(40005)로, 테이블은 FV 키로 적혀 있어 둘 다 찾혀야 한다.
    [TestCase("FV40005")]
    [TestCase("40005")]
    public void TryBuildEventUnitEntry_HoopFromBetaZone2Table(string minionKey)
    {
        object previous = GetCatalogInstance();
        var host = new GameObject("EventScriptCatalog_Test");
        try
        {
            CreateCatalogFixture(host, 2, BetaZone2UnitTablePath);

            bool success = InvokeTryBuildEventUnitEntry(2, minionKey, 1, out object entry, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(GetMember(GetMember(entry, "Data"), "Index"), Is.EqualTo("40005"));
            Assert.That(GetMember(entry, "PrefabKey"), Is.EqualTo("40005"), "외형은 데이터 인덱스로 카탈로그에서 찾는다.");
            Assert.That(GetMember(entry, "CombatSlot"), Is.EqualTo(0), "칸은 소환 호출자가 정한다.");

            object dash = FindSkillBySlot((IEnumerable)GetMember(entry, "ExplicitSkills"), 1);
            Assert.That(dash, Is.Not.Null, "돌진(슬롯 1) 스킬이 명시 스킬로 들어가야 한다.");
            Assert.That(GetMember(dash, "skillIndex"), Is.EqualTo(400051), "AI(EAI_40005)가 400051로 찾는다.");
            Assert.That(GetMember(dash, "skillKey"), Is.EqualTo("40005_1"), "실행·연출 레지스트리의 숫자 키.");
            Assert.That(GetMember(dash, "description"), Is.Not.Empty, "툴팁이 읽는 설명이 비면 안 된다.");
            Assert.That(GetMember(dash, "boundary"), Is.EquivalentTo(new[] { 3, 7 }), "행 전체 피해 범위는 [3,7]이다.");
        }
        finally
        {
            SetCatalogInstance(previous);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    // 모의전투 율리아전은 4구역 카탈로그 대신 실게임과 같은 베타 2구역 이벤트 전투(BE490)를 키-빌드한다.
    [Test]
    public void SimulationValidateEnemy_Be490FromBetaZone2EventTable()
    {
        object previous = GetCatalogInstance();
        var host = new GameObject("EventScriptCatalog_Test");
        try
        {
            CreateCatalogFixture(host, 2, BetaZone2UnitTablePath, BetaZone2GroupTablePath);

            bool success = InvokeSimulationValidateEnemy("BE490", 2, 2, out string error);

            Assert.That(success, Is.True, error);
        }
        finally
        {
            SetCatalogInstance(previous);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void SimulationValidateEnemy_EventZoneWithEmptyKey_Fails()
    {
        bool success = InvokeSimulationValidateEnemy("  ", 2, 1, out string error);

        Assert.That(success, Is.False);
        Assert.That(string.IsNullOrEmpty(error), Is.False);
    }

    // 이벤트 구역이 있으면 이벤트 전투로 등록하되, 복귀·저장 생략을 위해 모의전투 표시는 유지해야 한다.
    [Test]
    public void BeginSimulation_WithEventZone_RegistersEventBattleAndKeepsSimulation()
    {
        var host = new GameObject("CombatContext_Test");
        try
        {
            Component context = host.AddComponent(FindRuntimeType("CombatContext"));

            bool begun = InvokeBeginSimulation(context, "BE490", 2, "BattleSimulationScene", 2);

            Assert.That(begun, Is.True);
            Assert.That(GetMember(context, "IsSimulation"), Is.EqualTo(true));
            Assert.That(GetMember(context, "HasEventBattle"), Is.EqualTo(true));
            Assert.That(GetMember(context, "CombatEnemy"), Is.Null, "이벤트 전투로 등록하면 일반 적 데이터는 비어야 한다.");
            object eventBattle = GetMember(context, "EventBattle");
            Assert.That(GetMember(eventBattle, "ZoneId"), Is.EqualTo(2));
            Assert.That(GetMember(eventBattle, "BattleKey"), Is.EqualTo("BE490"));
            Assert.That(GetMember(eventBattle, "EnemyLevel"), Is.EqualTo(2));
            Assert.That(GetMember(context, "ReturnSceneName"), Is.EqualTo("BattleSimulationScene"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BeginSimulation_WithoutEventZone_RegistersEnemyGroup()
    {
        var host = new GameObject("CombatContext_Test");
        try
        {
            Component context = host.AddComponent(FindRuntimeType("CombatContext"));

            bool begun = InvokeBeginSimulation(context, "A2FEP001", 1, "BattleSimulationScene", 0);

            Assert.That(begun, Is.True);
            Assert.That(GetMember(context, "IsSimulation"), Is.EqualTo(true));
            Assert.That(GetMember(context, "HasEventBattle"), Is.EqualTo(false));
            Assert.That(GetMember(GetMember(context, "CombatEnemy"), "EnemyGroupKey"), Is.EqualTo("A2FEP001"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    /// <summary>
    /// §9-1 동등성 스냅샷(UnitKey/슬롯/레벨, HP/ATK/DEF/경험치+전투스탯, PrefabKey/SourceUnitKey,
    /// 스킬 개수·순서·전체 필드 / BE002~BE005)는
    /// EventScriptCatalog(DHScene_3 로드) 데이터가 필요하다.
    /// EditMode에서 카탈로그 주입 수단이 없어 현재는 런타임(DHScene_3 경유) 또는 전용 픽스처가 있어야 한다.
    /// 픽스처(카탈로그 SO 주입 하네스) 마련 후 활성화할 것.
    /// </summary>
    [Test]
    [Ignore("카탈로그 픽스처 필요: EventScriptCatalog/BattleScenarioCatalog 주입 하네스 마련 후 활성화. 그전엔 DHScene_3 런타임 검증(§9-2)으로 대체.")]
    public void SnapshotEquivalence_RequiresCatalogFixture()
    {
    }

    // ----- reflection helpers -----

    private static void AssertSameHandler(string catalogKey, string eventKey, string handlerName)
    {
        Type registryType = FindRuntimeType("ASB.Work.Battle.SkillExecution.SkillExecutionRegistry");
        MethodInfo lookup = registryType.GetMethod("TryGetHandler", BindingFlags.Public | BindingFlags.Static);
        Assert.That(lookup, Is.Not.Null);

        object[] catalogArgs = { catalogKey, null };
        object[] eventArgs = { eventKey, null };
        Assert.That((bool)lookup.Invoke(null, catalogArgs), Is.True, $"{catalogKey} 핸들러 미등록");
        Assert.That((bool)lookup.Invoke(null, eventArgs), Is.True, $"{eventKey} 핸들러 미등록");
        Assert.That(catalogArgs[1].GetType().Name, Is.EqualTo(handlerName));
        Assert.That(eventArgs[1], Is.SameAs(catalogArgs[1]));
    }

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

    private static bool InvokeTryBuildUnit(
        int zoneId, string unitKey, int enemyLevel, out object unit, out string error)
    {
        MethodInfo method = SourceType.GetMethod(
            "TryBuildUnit", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "EventBattlePlanSource.TryBuildUnit 를 찾지 못했습니다.");

        object[] args = { zoneId, unitKey, enemyLevel, 0, null, null };
        bool success = (bool)method.Invoke(null, args);
        unit = args[4];
        error = args[5] as string ?? string.Empty;
        return success;
    }

    private static bool InvokeTryBuildEventUnitEntry(
        int zoneId, string unitKey, int enemyLevel, out object entry, out string error)
    {
        MethodInfo method = BuilderType.GetMethod(
            "TryBuildEventUnitEntry", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "EnemySpawnPlanBuilder.TryBuildEventUnitEntry 를 찾지 못했습니다.");

        object[] args = { zoneId, unitKey, enemyLevel, null, null };
        bool success = (bool)method.Invoke(null, args);
        entry = args[3];
        error = args[4] as string ?? string.Empty;
        return success;
    }

    private static bool InvokeSimulationValidateEnemy(
        string enemyGroupKey, int eventZoneId, int enemyLevel, out string error)
    {
        MethodInfo method = FindRuntimeType("SimulationUnitFactory").GetMethod(
            "ValidateEnemy", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "SimulationUnitFactory.ValidateEnemy 를 찾지 못했습니다.");

        object[] args = { enemyGroupKey, eventZoneId, enemyLevel, null };
        bool success = (bool)method.Invoke(null, args);
        error = args[3] as string ?? string.Empty;
        return success;
    }

    // 아군 한 명(내용 없는 UnitPersistentData)으로 CombatContext.BeginSimulation을 호출한다.
    private static bool InvokeBeginSimulation(
        Component context, string enemyGroupKey, int enemyLevel, string returnScene, int eventZoneId)
    {
        Type allyType = FindRuntimeType("SimulationAllyRuntimeData");
        object unitData = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(
            FindRuntimeType("UnitPersistentData"));
        object ally = Activator.CreateInstance(allyType, 900000001, unitData);
        var allies = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(allyType));
        allies.Add(ally);

        MethodInfo method = context.GetType().GetMethod("BeginSimulation", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "CombatContext.BeginSimulation 을 찾지 못했습니다.");
        return (bool)method.Invoke(context, new object[] { "SIM_TEST", allies, enemyGroupKey, enemyLevel, returnScene, eventZoneId });
    }

    // EditMode에서는 Awake가 돌지 않으므로 구역 하나만 채운 카탈로그를 만들고 Instance를 직접 지정한다.
    private static void CreateCatalogFixture(GameObject host, int zoneId, string unitTablePath, string groupTablePath = null)
    {
        var unitTable = AssetDatabase.LoadAssetAtPath<ScriptableObject>(unitTablePath);
        Assert.That(unitTable, Is.Not.Null, $"테이블 에셋을 찾지 못했습니다: {unitTablePath}");

        Component catalog = host.AddComponent(CatalogType);
        Type zoneType = FindRuntimeType("EventScriptZone");
        object zone = Activator.CreateInstance(zoneType);
        zoneType.GetField("zoneId").SetValue(zone, zoneId);
        zoneType.GetField("enemyBattleUnitTable").SetValue(zone, unitTable);
        if (!string.IsNullOrEmpty(groupTablePath))
        {
            var groupTable = AssetDatabase.LoadAssetAtPath<ScriptableObject>(groupTablePath);
            Assert.That(groupTable, Is.Not.Null, $"테이블 에셋을 찾지 못했습니다: {groupTablePath}");
            zoneType.GetField("enemyBattleGroupTable").SetValue(zone, groupTable);
        }
        var zones = (IList)CatalogType.GetField("zones", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(catalog);
        zones.Add(zone);

        SetCatalogInstance(catalog);
        CatalogType.GetMethod("Reload", BindingFlags.Public | BindingFlags.Instance).Invoke(catalog, null);
    }

    private static object FindSkillBySlot(IEnumerable skills, int slot)
    {
        if (skills == null)
            return null;

        foreach (object skill in skills)
        {
            if (skill != null && (int)GetMember(skill, "slot") == slot)
                return skill;
        }

        return null;
    }

    private static object GetMember(object target, string name)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property != null)
            return property.GetValue(target);

        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"{type.Name}.{name} 멤버를 찾지 못했습니다.");
        return field.GetValue(target);
    }

    private static object GetCatalogInstance()
    {
        PropertyInfo instanceProperty = CatalogType.GetProperty(
            "Instance", BindingFlags.Public | BindingFlags.Static);
        return instanceProperty != null ? instanceProperty.GetValue(null) : null;
    }

    private static void SetCatalogInstance(object value)
    {
        CatalogType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, value);
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
