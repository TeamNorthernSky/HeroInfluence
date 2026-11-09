using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CounterattackRulesTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created)
            if (go != null) Object.DestroyImmediate(go);
        created.Clear();
    }

    [TestCase("FV40002_1", 400021)]
    [TestCase("FV40002_2", 400022)]
    [TestCase("FV40003_1", 400031)]
    [TestCase("FV40003_2", 400032)]
    public void AmplifierSelfAction_CannotBeCounterSkill(string key, int index)
    {
        Component defender = Unit("Amplifier", false, 2, 0);
        foreach (float value in new[] { 0f, 1f })
        {
            object skill = Skill(key, index, 0, value);
            Assert.That(CanDefenderCounter(defender, skill), Is.False, $"{key}, value={value}");
        }
    }

    [Test]
    public void CounterSkill_RequiresFrontRowAndActualDamage_ButNotMeleeRange()
    {
        Component front = Unit("Front", false, 2, 0);
        Component back = Unit("Back", false, 3, 0);
        Component allyFront = Unit("AllyFront", true, 1, 0);
        Component allyBack = Unit("AllyBack", true, 0, 0);
        object melee = Skill("FV20003_1", 200031, 0, 0.8f);
        object ranged = Skill("FV20002_1", 200021, 1, 1f);
        object heal = Skill("HS4010", 4010, 1, 1f, 1);

        Assert.That(CanDefenderCounter(front, melee), Is.True);
        Assert.That(CanDefenderCounter(front, ranged), Is.True);
        Assert.That(CanDefenderCounter(back, melee), Is.False);
        Assert.That(CanDefenderCounter(allyFront, melee), Is.True);
        Assert.That(CanDefenderCounter(allyBack, melee), Is.False);
        Assert.That(CanDefenderCounter(front, heal), Is.False);
        Assert.That(CounterRate(front) - CounterRate(back), Is.EqualTo(0.3f).Within(0.001f));
        Assert.That(CounterRate(allyFront) - CounterRate(allyBack), Is.EqualTo(0.3f).Within(0.001f));
    }

    [Test]
    public void EmptyFixedFrontRow_DoesNotPromoteBackRowIntoCounterPosition()
    {
        Component enemyBack = Unit("EnemyBackOnly", false, 3, 0);
        Component allyBack = Unit("AllyBackOnly", true, 0, 0);
        object attack = Skill("FV20002_1", 200021, 1, 1f);

        Assert.That(CanDefenderCounter(enemyBack, attack), Is.False);
        Assert.That(CanDefenderCounter(allyBack, attack), Is.False);

        foreach (string managerName in new[] { "BattleManager", "JcBattleManager" })
        {
            MethodInfo canTrigger = FindType(managerName).GetMethod(
                "CanTriggerCounterattack", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(canTrigger, Is.Not.Null);
            Assert.That(canTrigger.Invoke(null, new[] { Hit(allyBack, enemyBack, -1) }), Is.False);
            Assert.That(canTrigger.Invoke(null, new[] { Hit(enemyBack, allyBack, -1) }), Is.False);
        }
    }

    [Test]
    public void CounterDamage_IgnoresDefenseSkillAndOtherDamageModifiers()
    {
        Component counterAttacker = Unit("CounterAttacker", false, 2, 0);
        Component target = Unit("Target", true, 1, 0);
        SetFinalStat(counterAttacker, "Atk", 9f);
        SetFinalStat(target, "DEF", 100f);
        SetFinalStat(target, "AvoidRate", 1f);
        counterAttacker.GetType().GetField("currentInfluence", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(counterAttacker, 100f);

        object context = Hit(counterAttacker, target, 1010, isRanged: true);
        Type contextType = context.GetType();
        contextType.GetField("IsCounterAttack").SetValue(context, true);
        contextType.GetField("IsCritical").SetValue(context, true);
        contextType.GetField("IsDamageReduced").SetValue(context, (bool?)true);
        contextType.GetField("SkillValue").SetValue(context, 3f);

        Type calculator = FindType("ASB.Work.Battle.Core.CombatCalculator");
        MethodInfo calculate = calculator.GetMethod("CalculateDamage", new[] { contextType });
        MethodInfo jcCalculate = FindType("ASB.Work.Battle.Core.JcCombatCalculator")
            .GetMethod("CalculateDamage", new[] { contextType });
        Assert.That(calculate, Is.Not.Null);
        Assert.That(jcCalculate, Is.Not.Null);
        Assert.That((float)calculate.Invoke(null, new[] { context }), Is.EqualTo(3f),
            "공격력 9의 반격은 기본 배율 0.35로 3.15를 반올림해 3이며 다른 피해 계수는 무시한다.");
        Assert.That((float)jcCalculate.Invoke(null, new[] { context }), Is.EqualTo(5f),
            "JC 복사본은 기본 배율 0.5 그대로(4.5 → 5).");

        MethodInfo calculateWithMultiplier = calculator.GetMethod("CalculateDamage", new[] { contextType, typeof(float) });
        MethodInfo jcCalculateWithMultiplier = FindType("ASB.Work.Battle.Core.JcCombatCalculator")
            .GetMethod("CalculateDamage", new[] { contextType, typeof(float) });
        Assert.That(calculateWithMultiplier, Is.Not.Null);
        Assert.That(jcCalculateWithMultiplier, Is.Not.Null);
        Assert.That((float)calculateWithMultiplier.Invoke(null, new object[] { context, 0.75f }), Is.EqualTo(7f));
        Assert.That((float)jcCalculateWithMultiplier.Invoke(null, new object[] { context, 0.75f }), Is.EqualTo(7f));

        SetFinalStat(counterAttacker, "Atk", 1f);
        Assert.That((float)calculate.Invoke(null, new[] { context }), Is.EqualTo(2f),
            "반격에도 최소 피해 2를 적용한다.");
        Assert.That((float)jcCalculate.Invoke(null, new[] { context }), Is.EqualTo(2f));
    }

    [TestCase("BattleManager")]
    [TestCase("JcBattleManager")]
    public void OnlyMeleeHitsOnFixedFrontRow_CanTriggerCounter(string managerName)
    {
        Component heroFront = Unit("HeroFront", true, 1, 0);
        Component heroBack = Unit("HeroBack", true, 0, 0);
        Component enemyFront = Unit("EnemyFront", false, 2, 0);
        Component enemyBack = Unit("EnemyBack", false, 3, 0);
        Type manager = FindType(managerName);
        MethodInfo canTrigger = manager.GetMethod("CanTriggerCounterattack", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(canTrigger, Is.Not.Null);

        AddSkill(heroFront, Skill("HS1010", 1010, 0, 1f));
        AddSkill(heroFront, Skill("HS1020", 1020, 1, 1f));
        AddSkill(enemyFront, Skill("FV20003_1", 200031, 0, 1f));
        AddSkill(enemyFront, Skill("FV20002_1", 200021, 1, 1f));

        Assert.That(canTrigger.Invoke(null, new[] { Hit(heroFront, enemyFront, 1010) }), Is.True);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(heroFront, enemyBack, 1010) }), Is.False);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(heroFront, enemyFront, 1020, true) }), Is.False);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(enemyFront, heroFront, 200031) }), Is.True);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(enemyFront, heroBack, 200031) }), Is.False);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(enemyFront, heroFront, 200021, true) }), Is.False);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(heroFront, enemyFront, -1) }), Is.True);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(heroFront, enemyFront, -1, true) }), Is.False);
    }

    [Test]
    public void OnlySingleTargetSkills_CountAsSingleTargetAttack()
    {
        Type rules = FindType("ASB.Work.Battle.Core.CounterattackRules");
        MethodInfo isSingle = rules.GetMethod("IsSingleTargetSkill");
        Assert.That(isSingle, Is.Not.Null);

        Assert.That(isSingle.Invoke(null, new[] { Skill("HS1010", 1010, 0, 1f) }), Is.True, "대상 단일·범위 없음");
        Assert.That(isSingle.Invoke(null, new[] { Skill("HS1020", 1020, 0, 1f, boundary: new[] { 0 }) }), Is.True, "중심 칸만");
        Assert.That(isSingle.Invoke(null, new[] { Skill("HS1030", 1030, 0, 1f, boundary: new[] { 3 }) }), Is.False, "관통 패턴");
        Assert.That(isSingle.Invoke(null, new[] { Skill("HS1040", 1040, 0, 1f, target: 2) }), Is.False, "전체");
        Assert.That(isSingle.Invoke(null, new[] { Skill("FV20005_2", 200052, 0, 1f, target: 1,
            boundary: new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }) }), Is.False, "연격(복수)");
    }

    [Test]
    public void MultiTargetAttack_QueuesNoCounter_EvenWhenHitsAreMarkedCounterable()
    {
        Component hero = Unit("Hero", true, 1, 0);
        Component enemyA = Unit("EnemyA", false, 2, 0);
        Component enemyB = Unit("EnemyB", false, 2, 1);
        object shieldBash = Skill("FV20003_1", 200031, 0, 1f);
        foreach (Component enemy in new[] { enemyA, enemyB })
        {
            enemy.GetType().GetProperty("SelectedSkillData").SetValue(enemy, shieldBash);
            SetFinalStat(enemy, "CounterRate", 1f);
        }

        GameObject managerObject = new GameObject("CounterManager");
        created.Add(managerObject);
        Component manager = managerObject.AddComponent(FindType("BattleManager"));
        MethodInfo collect = manager.GetType().GetMethod("CollectCounterAttackRequests", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(collect, Is.Not.Null);

        int Requests(object skill, params Component[] targets)
        {
            Type resultType = FindType("ASB.Work.Battle.SkillExecution.SkillExecutionResult");
            object result = resultType.GetMethods().First(m => m.Name == "SuccessResult" && m.GetParameters().Length == 4)
                .Invoke(null, new object[] { hero, skill, null, null });
            IList hits = (IList)resultType.GetProperty("DamageContexts").GetValue(result);
            foreach (Component target in targets)
            {
                object hit = Hit(hero, target, (int)skill.GetType().GetField("skillIndex").GetValue(skill));
                hit.GetType().GetField("CanTriggerCounter").SetValue(hit, true);
                hits.Add(hit);
            }
            return ((IList)collect.Invoke(manager, new[] { result })).Count;
        }

        Assert.That(Requests(Skill("HS1010", 1010, 0, 1f), enemyA), Is.EqualTo(1), "단일 근접 공격은 반격");
        Assert.That(Requests(Skill("HS1010", 1010, 0, 1f), enemyA, enemyA), Is.EqualTo(1), "같은 대상 2회 타격도 단일");
        Assert.That(Requests(Skill("FV20005_2", 200052, 0, 1f, target: 1, boundary: new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }),
            enemyA, enemyB), Is.EqualTo(0), "연격: 주 대상·추가 대상 모두 반격 없음");
        Assert.That(Requests(Skill("HS1040", 1040, 0, 1f, target: 2), enemyA, enemyB), Is.EqualTo(0), "전체 공격");
        Assert.That(Requests(Skill("HS1030", 1030, 0, 1f, boundary: new[] { 3 }), enemyA), Is.EqualTo(0),
            "관통 스킬은 1명만 맞아도 반격 없음");
        Assert.That(Requests(Skill("HS1010", 1010, 0, 1f), enemyA, enemyB), Is.EqualTo(0),
            "단일 스킬이라도 실제로 2명을 맞혔으면 반격 없음");
    }

    private static void AddSkill(Component unit, object skill)
    {
        ((IList)unit.GetType().GetField("availableSkills").GetValue(unit)).Add(skill);
    }

    private Component Unit(string name, bool isPlayer, int x, int y)
    {
        GameObject cellObject = new GameObject($"Grid_{x}_{y}_{name}");
        created.Add(cellObject);
        Component cell = cellObject.AddComponent(FindType("ASB.Work.BattleGrid.GridCell"));
        cell.GetType().GetMethod("SetCoords").Invoke(cell, new object[] { new Vector2Int(x, y) });

        GameObject unitObject = new GameObject(name);
        created.Add(unitObject);
        Component unit = unitObject.AddComponent(FindType("BattleCharactor"));
        unit.GetType().GetProperty("IsPlayer").SetValue(unit, isPlayer);
        unit.GetType().GetMethod("AssignToCell").Invoke(unit, new object[] { cell });
        return unit;
    }

    private static object Skill(string key, int index, int range, float value, int effect = 0,
        int target = 0, int[] boundary = null)
    {
        Type type = FindType("SkillData");
        object skill = Activator.CreateInstance(type);
        type.GetField("skillKey").SetValue(skill, key);
        type.GetField("skillIndex").SetValue(skill, index);
        type.GetField("classSkillRange").SetValue(skill, range);
        type.GetField("classSkillEffect").SetValue(skill, effect);
        type.GetField("skillValue").SetValue(skill, value);
        type.GetField("classSkillTarget").SetValue(skill, target);
        if (boundary != null)
            type.GetField("boundary").SetValue(skill, new List<int>(boundary));
        return skill;
    }

    private static object Hit(Component caster, Component target, int skillIndex, bool isRanged = false)
    {
        Type type = FindType("ASB.Work.Battle.Core.DamageContext");
        object context = Activator.CreateInstance(type);
        type.GetField("Caster").SetValue(context, caster);
        type.GetField("Target").SetValue(context, target);
        type.GetField("SkillIndex").SetValue(context, skillIndex);
        type.GetField("IsRangedAttack").SetValue(context, isRanged);
        return context;
    }

    private static bool CanDefenderCounter(Component defender, object skill)
    {
        Type rules = FindType("ASB.Work.Battle.Core.CounterattackRules");
        return (bool)rules.GetMethod("CanDefenderCounter").Invoke(null, new[] { defender, skill });
    }

    private static float CounterRate(Component unit)
    {
        object stats = unit.GetType().GetProperty("FinalStats").GetValue(unit);
        return (float)stats.GetType().GetField("CounterRate").GetValue(stats);
    }

    private static void SetFinalStat(Component unit, string statName, float value)
    {
        FieldInfo finalStats = unit.GetType().GetField("finalStats", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(finalStats, Is.Not.Null);
        object stats = finalStats.GetValue(unit);
        stats.GetType().GetField(statName).SetValue(stats, value);
        finalStats.SetValue(unit, stats);
    }

    private static Type FindType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).FirstOrDefault(found => found != null);
        Assert.That(type, Is.Not.Null, name);
        return type;
    }
}
