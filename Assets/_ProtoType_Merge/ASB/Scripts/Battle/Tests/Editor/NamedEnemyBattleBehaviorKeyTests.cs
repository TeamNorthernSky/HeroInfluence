using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 네임드 변형 적(아놀드 200051·로니 200052·은행 강도 두목 200053)의 전투 동작 키(20005) 전달 검증.
/// 데이터 키·이름은 유지되고 프리팹·AI·스킬·Timeline 연결만 20005를 쓰는지, 스폰 이후 연결까지 확인한다.
/// 런타임 타입은 Assembly-CSharp에 있어 asmdef가 직접 참조할 수 없으므로 reflection으로 접근한다.
/// </summary>
public sealed class NamedEnemyBattleBehaviorKeyTests
{
    private const string BehaviorKey = "20005";
    private const string PrefabCatalogPath = "Assets/ASB_Work/Prefab/BattleUnitPrefabCatalog.asset";
    // TmpBattleScene BattleVisualDirector._catalog(guid 80b063d4)가 가리키는 실제 전투씬 연출 카탈로그.
    private const string PresentationCatalogPath =
        "Assets/RenderFX/_Seam/Data/Merged/JC_Merged_SkillPresentationCatalog.asset";

    private static readonly (string Key, string Name)[] NamedVariants =
    {
        ("FV200051", "아놀드"),
        ("FV200052", "로니"),
        ("FV200053", "은행 강도 두목"),
    };

    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        created.Clear();
    }

    // ── 매핑 표 ────────────────────────────────────────────────

    [TestCase("FV200051")]
    [TestCase("200052")]
    [TestCase("FV200053")]
    public void Rules_NamedVariant_MapsToBehaviorKey(string unitKey)
    {
        Assert.That(TryGetBaseUnitKey(unitKey, out string baseKey), Is.True);
        Assert.That(baseKey, Is.EqualTo(BehaviorKey));
    }

    [TestCase("20005")]
    [TestCase("20006")]
    [TestCase("20007")]
    [TestCase("10002")]
    [TestCase(null)]
    public void Rules_OtherKeys_AreNotMapped(string unitKey)
    {
        Assert.That(TryGetBaseUnitKey(unitKey, out _), Is.False, $"'{unitKey}'는 매핑되면 안 됩니다.");
    }

    // ── 전투씬 빌더 ────────────────────────────────────────────

    [Test]
    public void Builder_NamedVariants_KeepDataKeyAndName_PassBehaviorKey()
    {
        foreach ((string key, string name) in NamedVariants)
        {
            object entry = BuildSingleEntry(key, name, slot: 2);
            string numericKey = key.Substring(2);
            object data = Get(entry, "Data");

            Assert.That(GetField(data, "Index"), Is.EqualTo(numericKey), $"{key}: 데이터 키는 유지해야 합니다.");
            Assert.That(GetField(data, "Name"), Is.EqualTo(name), $"{key}: 이름은 자기 행을 써야 합니다.");
            Assert.That(GetField(data, "UnitAI"), Is.EqualTo(BehaviorKey), $"{key}: AI 오버라이드");
            Assert.That(Get(entry, "PrefabKey"), Is.EqualTo(BehaviorKey), $"{key}: 프리팹 키");
            Assert.That(Get(entry, "BattleBehaviorKey"), Is.EqualTo(BehaviorKey), $"{key}: 전투 동작 키");
            Assert.That(Get(entry, "SourceUnitKey"), Is.EqualTo(key), $"{key}: 원래 키");
            Assert.That(Get(entry, "CombatSlot"), Is.EqualTo(2));

            IList skills = (IList)Get(entry, "ExplicitSkills");
            Assert.That(skills.Count, Is.EqualTo(2));
            AssertSkill(skills[0], 200051, "20005_1", "파동탄", key);
            AssertSkill(skills[1], 200052, "20005_2", "연격", key);
        }
    }

    [TestCase("FV20002", "20002")]
    [TestCase("FV20006", "20006")]
    public void Builder_UnmappedUnits_AreUnchanged(string key, string numericKey)
    {
        object entry = BuildSingleEntry(key, "대조군", slot: 1);
        object data = Get(entry, "Data");

        Assert.That(GetField(data, "Index"), Is.EqualTo(numericKey));
        Assert.That(GetField(data, "UnitAI"), Is.EqualTo("설명 텍스트"));
        Assert.That(Get(entry, "PrefabKey"), Is.EqualTo(numericKey));
        Assert.That(Get(entry, "BattleBehaviorKey"), Is.EqualTo(string.Empty));

        IList skills = (IList)Get(entry, "ExplicitSkills");
        AssertSkill(skills[0], int.Parse(numericKey) * 10 + 1, numericKey + "_1", "파동탄", key);
        AssertSkill(skills[1], int.Parse(numericKey) * 10 + 2, numericKey + "_2", "연격", key);
    }

    // ── 프리팹 선택(스폰 이후 연결) ─────────────────────────────

    [Test]
    public void Prefab_NamedVariants_AreUnregistered_AndSpawnerResolvesBehaviorPrefab()
    {
        ScriptableObject catalog = LoadAsset(PrefabCatalogPath);
        GameObject expected = TryGetEnemyPrefab(catalog, BehaviorKey);
        Assert.That(expected, Is.Not.Null, "카탈로그에 20005 프리팹이 있어야 합니다.");

        Component spawner = NewComponent("EnemySpawner");
        spawner.GetType().GetField("prefabCatalog", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(spawner, catalog);
        MethodInfo resolve = spawner.GetType().GetMethod("ResolvePlanPrefab", BindingFlags.Instance | BindingFlags.NonPublic);

        foreach ((string key, string name) in NamedVariants)
        {
            string numericKey = key.Substring(2);
            Assert.That(TryGetEnemyPrefab(catalog, numericKey), Is.Null,
                $"{numericKey}가 카탈로그에 등록되면 스포너 1순위(Data.Index)가 빌더 결정을 무시합니다.");

            object entry = BuildSingleEntry(key, name, slot: 1);
            var prefab = (GameObject)resolve.Invoke(spawner, new[] { entry });
            Assert.That(prefab, Is.SameAs(expected), $"{key}: 20005 프리팹으로 스폰돼야 합니다.");
        }
    }

    // ── 유닛에 전투 동작 키 전달 ───────────────────────────────

    [Test]
    public void ApplyBattleBehaviorKey_SetsPresentationIndex_OnlyWhenKeyPresent()
    {
        MethodInfo apply = FindType("EnemySpawner")
            .GetMethod("ApplyBattleBehaviorKey", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(apply, Is.Not.Null);

        Component named = NewBattleCharactor("200051");
        apply.Invoke(null, new[] { named, BuildSingleEntry("FV200051", "아놀드", slot: 1) });
        Assert.That(Get(named, "PresentationIndex"), Is.EqualTo(BehaviorKey));
        Assert.That(Get(named, "TemplateIndex"), Is.EqualTo("200051"), "데이터 키는 바뀌면 안 됩니다.");

        Component plain = NewBattleCharactor("20002");
        apply.Invoke(null, new[] { plain, BuildSingleEntry("FV20002", "소총수", slot: 1) });
        Assert.That(Get(plain, "PresentationIndex"), Is.EqualTo("20002"), "빈 키면 TemplateIndex를 써야 합니다.");
    }

    [Test]
    public void PresentationIndex_DefaultsToTemplateIndex_AndClearsOnBlank()
    {
        Component unit = NewBattleCharactor("200052");
        Assert.That(Get(unit, "PresentationIndex"), Is.EqualTo("200052"));

        Invoke(unit, "SetPresentationIndex", " 20005 ");
        Assert.That(Get(unit, "PresentationIndex"), Is.EqualTo(BehaviorKey));

        Invoke(unit, "SetPresentationIndex", " ");
        Assert.That(Get(unit, "PresentationIndex"), Is.EqualTo("200052"));
    }

    // ── Timeline 레일(실제 전투씬 카탈로그) ─────────────────────

    [TestCase(200051)]
    [TestCase(200052)]
    public void Timeline_BehaviorKeyResolvesRail_DataKeyDoesNot(int skillIndex)
    {
        object presentation = GetPresentation(skillIndex);
        MethodInfo resolve = presentation.GetType()
            .GetMethod("ResolveTimeline", new[] { typeof(string), typeof(string) });

        foreach ((string key, string name) in NamedVariants)
        {
            Assert.That(resolve.Invoke(presentation, new object[] { BehaviorKey, name }), Is.Not.Null,
                $"{skillIndex}: PresentationIndex=20005로 레일을 찾아야 합니다.");
            Assert.That(resolve.Invoke(presentation, new object[] { key.Substring(2), name }), Is.Null,
                $"{skillIndex}: 데이터 키({key})로는 레일이 없어야 PresentationIndex 전달이 필요함이 증명됩니다.");
        }
    }

    [Test]
    public void Timeline_ComboMultiTargetPlan_ResolvesForBehaviorKey()
    {
        object presentation = GetPresentation(200052);
        Assert.That((bool)presentation.GetType().GetMethod("HasMultiTargetPlan")
            .Invoke(presentation, new object[] { BehaviorKey, "아놀드" }), Is.True);

        Type segmentType = FindType("SkillTimelineSegment");
        var segments = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(segmentType));
        bool found = (bool)presentation.GetType().GetMethod("TryResolvePlan")
            .Invoke(presentation, new object[] { BehaviorKey, "아놀드", 2, segments });
        Assert.That(found, Is.True);
        Assert.That(segments.Count, Is.EqualTo(2), "연격 2대상 플랜은 세그먼트 2개여야 합니다.");
    }

    // ── AI ─────────────────────────────────────────────────────

    [Test]
    public void Ai_NumericUnitAiOverride_SelectsBehaviorAi_TextFallsBackTo20001()
    {
        MethodInfo resolve = FindType("EnemyScript")
            .GetMethod("ResolveAiIndex", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(resolve, Is.Not.Null);

        object overridden = NewEnemyData("200053", BehaviorKey);
        Assert.That(resolve.Invoke(null, new[] { overridden }), Is.EqualTo(20005));

        object textOnly = NewEnemyData("200053", "설명 텍스트");
        Assert.That(resolve.Invoke(null, new[] { textOnly }), Is.EqualTo(20001),
            "오버라이드가 없으면 200053은 전용 AI 대상이 아닙니다(빌더가 UnitAI를 넣어야 하는 이유).");
    }

    // ── 스킬 핸들러 ────────────────────────────────────────────

    [TestCase("20005_1", "ASB.Work.Battle.SkillExecution.DamageSkillHandler")]
    [TestCase("20005_2", "ASB.Work.Battle.SkillExecution.HitTargetAroundRandomHandler")]
    public void Handler_BehaviorSkillKeys_ResolveStrongSoldierHandlers(string skillKey, string handlerType)
    {
        MethodInfo tryGet = FindType("ASB.Work.Battle.SkillExecution.SkillExecutionRegistry")
            .GetMethod("TryGetHandler", BindingFlags.Static | BindingFlags.Public);
        object[] args = { skillKey, null };
        Assert.That((bool)tryGet.Invoke(null, args), Is.True, $"{skillKey} 핸들러가 등록돼 있어야 합니다.");
        Assert.That(args[1].GetType().FullName, Is.EqualTo(handlerType));
    }

    // ── helpers ────────────────────────────────────────────────

    private static void AssertSkill(object skill, int index, string key, string name, string unitKey)
    {
        Assert.That(GetField(skill, "skillIndex"), Is.EqualTo(index), $"{unitKey}: skillIndex");
        Assert.That(GetField(skill, "skillKey"), Is.EqualTo(key), $"{unitKey}: skillKey");
        Assert.That(GetField(skill, "skillName"), Is.EqualTo(name), $"{unitKey}: 스킬 이름은 자기 행을 유지");
    }

    private static object BuildSingleEntry(string unitKey, string name, int slot)
    {
        Type unitType = FindType("CombatEventBattleUnitData");
        object unit = Activator.CreateInstance(unitType);
        SetField(unit, "UnitKey", unitKey);
        SetField(unit, "Slot", slot);
        SetField(unit, "Level", 1);
        SetField(unit, "EnemyName", name);
        SetField(unit, "EnemyConcept", "테스트");
        SetField(unit, "MaxHp", 100);
        SetField(unit, "Atk", 6);
        SetField(unit, "Def", 6);
        SetField(unit, "Speed", 1);
        SetField(unit, "UnitAI", "설명 텍스트");
        SetField(unit, "ExperiencePoint", 80);

        // EventBattlePlanSource.TryAddEventBattleSkill과 같은 형식(자기 키 기준 번호).
        string numericKey = BattleKeyDigits(unitKey);
        var skills = (IList)GetField(unit, "Skills");
        skills.Add(NewSkill(int.Parse(numericKey) * 10 + 1, numericKey + "_1", 1, "파동탄"));
        skills.Add(NewSkill(int.Parse(numericKey) * 10 + 2, numericKey + "_2", 2, "연격"));

        var units = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(unitType));
        units.Add(unit);

        MethodInfo build = FindType("EnemySpawnPlanBuilder")
            .GetMethod("TryBuildEntriesFromUnits", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(build, Is.Not.Null, "EnemySpawnPlanBuilder.TryBuildEntriesFromUnits를 찾지 못했습니다.");
        object[] args = { units, null, null };
        Assert.That((bool)build.Invoke(null, args), Is.True, $"빌드 실패: {args[2]}");
        var entries = (IList)args[1];
        Assert.That(entries.Count, Is.EqualTo(1));
        return entries[0];
    }

    private static object NewSkill(int index, string key, int slot, string name)
    {
        object skill = Activator.CreateInstance(FindType("SkillData"));
        SetField(skill, "skillIndex", index);
        SetField(skill, "skillKey", key);
        SetField(skill, "slot", slot);
        SetField(skill, "skillName", name);
        return skill;
    }

    private static object NewEnemyData(string index, string unitAi)
    {
        object data = Activator.CreateInstance(FindType("EnemyData"));
        SetField(data, "Index", index);
        SetField(data, "UnitAI", unitAi);
        return data;
    }

    private Component NewBattleCharactor(string templateIndex)
    {
        Component unit = NewComponent("BattleCharactor");
        Invoke(unit, "SetTemplateIndex", templateIndex);
        return unit;
    }

    private Component NewComponent(string typeName)
    {
        var go = new GameObject(typeName + "_Test");
        created.Add(go);
        return go.AddComponent(FindType(typeName));
    }

    private static object GetPresentation(int skillIndex)
    {
        ScriptableObject catalog = LoadAsset(PresentationCatalogPath);
        object presentation = catalog.GetType().GetMethod("Get", new[] { typeof(int) })
            .Invoke(catalog, new object[] { skillIndex });
        Assert.That(presentation, Is.Not.Null, $"전투씬 연출 카탈로그에 {skillIndex}가 있어야 합니다.");
        return presentation;
    }

    private static GameObject TryGetEnemyPrefab(ScriptableObject catalog, string key)
    {
        object[] args = { key, null };
        bool found = (bool)catalog.GetType().GetMethod("TryGetEnemyPrefab").Invoke(catalog, args);
        return found ? (GameObject)args[1] : null;
    }

    private static bool TryGetBaseUnitKey(string unitKey, out string baseKey)
    {
        object[] args = { unitKey, null };
        bool found = (bool)FindType("EnemyBaseUnitRules")
            .GetMethod("TryGetBaseUnitKey", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, args);
        baseKey = (string)args[1];
        return found;
    }

    private static ScriptableObject LoadAsset(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        Assert.That(asset, Is.Not.Null, $"에셋을 찾지 못했습니다: {path}");
        return asset;
    }

    private static string BattleKeyDigits(string unitKey) => new string(unitKey.Where(char.IsDigit).ToArray());

    private static object Get(object target, string property) =>
        target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public).GetValue(target);

    private static object GetField(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public).GetValue(target);

    private static void SetField(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public).SetValue(target, value);

    private static void Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public).Invoke(target, args);

    private static Type FindType(string fullName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(found => found != null);
        Assert.That(type, Is.Not.Null, $"타입을 찾지 못했습니다: {fullName}");
        return type;
    }
}
