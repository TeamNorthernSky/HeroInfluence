using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 데이터 드리븐 튜토리얼(인터프리터)의 순수 판정·조율·정리 테스트(지시서 §10).
/// EditMode 어셈블리는 Assembly-CSharp를 직접 참조하지 못하므로 리플렉션(FindRuntimeType) 사용.
/// host 목을 만들 수 없어(인터페이스 타입 참조 불가) 오케스트레이션은 실제 bare Director를 host로 쓴다
/// — SetFlag/SetStep 액션은 host 내부를 건드리지 않으므로 초기화 없이도 검증 가능하다.
///
/// PlayMode 유보(실인스턴스 필요): Spawn rollback, RequestId 잠금 해제 라우팅,
/// BattleResultShown/Accepted 라우팅, SetHp→HpChanged 재진입 FIFO, Shutdown 전체 정리,
/// 광역 스킬 지정대상 사망(해석된 SkillExecutionResult 필요).
/// </summary>
public sealed class TutorialInterpreterTests
{
    private const BindingFlags AllInstance =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // ─────────────────────────── 공용 리플렉션 헬퍼 ───────────────────────────

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
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
    }

    private static void SetPublic(object obj, string field, object value)
        => obj.GetType().GetField(field).SetValue(obj, value);

    private static void SetPrivate(object obj, string field, object value)
        => obj.GetType().GetField(field, AllInstance).SetValue(obj, value);

    private static object GetPrivate(object obj, string field)
        => obj.GetType().GetField(field, AllInstance).GetValue(obj);

    // ─────────────────────────── 타입 캐시 ───────────────────────────

    private static Type TriggerType => FindRuntimeType("TutorialTrigger");
    private static Type ActionType => FindRuntimeType("TutorialAction");
    private static Type RuleType => FindRuntimeType("TutorialRuleEntry");
    private static Type SheetType => FindRuntimeType("TutorialScheduleSheet");
    private static Type CatalogType => FindRuntimeType("TutorialScheduleCatalog");
    private static Type EventCtxType => FindRuntimeType("TutorialEventContext");
    private static Type EvaluatorType => FindRuntimeType("TutorialTriggerEvaluator");
    private static Type FlowType => FindRuntimeType("DataDrivenTutorialFlow");
    private static Type DirectorType => FindRuntimeType("TutorialBattleDirector");
    private static Type LifetimeType => FindRuntimeType("TutorialEffectLifetime");

    private static object EnumVal(Type type, string name) => Enum.Parse(type, name);
    private static object TriggerKind(string name) => EnumVal(FindRuntimeType("TutorialTriggerType"), name);
    private static object ActionKind(string name) => EnumVal(FindRuntimeType("TutorialActionType"), name);
    private static object Side(string name) => EnumVal(FindRuntimeType("TutorialUnitSide"), name);

    // ─────────────────────────── 팩토리 ───────────────────────────

    private static object NewTrigger(string kind)
    {
        object t = Activator.CreateInstance(TriggerType);
        SetPublic(t, "type", TriggerKind(kind));
        return t;
    }

    private static object NewEventCtx(string kind)
    {
        object c = Activator.CreateInstance(EventCtxType);
        SetPublic(c, "eventType", TriggerKind(kind));
        return c;
    }

    private static bool Matches(object trigger, object ctx)
    {
        MethodInfo m = EvaluatorType.GetMethod("Matches", new[] { TriggerType, EventCtxType });
        return (bool)m.Invoke(null, new[] { trigger, ctx });
    }

    private static object NewAction(string kind)
    {
        object a = Activator.CreateInstance(ActionType);
        SetPublic(a, "type", ActionKind(kind));
        return a;
    }

    private static object NewSetFlagAction(string flag)
    {
        object a = NewAction("SetFlag");
        SetPublic(a, "stringValue", flag);
        return a;
    }

    private static object NewRule(string ruleId, object trigger, bool once, params object[] actions)
    {
        object r = Activator.CreateInstance(RuleType);
        SetPublic(r, "ruleId", ruleId);
        SetPublic(r, "trigger", trigger);
        SetPublic(r, "once", once);
        var list = (IList)GetPrivate(r, "actions") ?? MakeActionList();
        list.Clear();
        foreach (object a in actions) list.Add(a);
        SetPublicOrPrivate(r, "actions", list);
        return r;
    }

    private static IList MakeActionList()
        => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ActionType));

    private static void SetPublicOrPrivate(object obj, string field, object value)
    {
        FieldInfo f = obj.GetType().GetField(field) ?? obj.GetType().GetField(field, AllInstance);
        f.SetValue(obj, value);
    }

    private static ScriptableObject NewSheet(int zoneId, string battleKey, params object[] rules)
    {
        ScriptableObject sheet = ScriptableObject.CreateInstance(SheetType);
        SetPrivate(sheet, "zoneId", zoneId);
        SetPrivate(sheet, "battleKey", battleKey);
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(RuleType));
        foreach (object r in rules) list.Add(r);
        SheetType.GetField("rules", AllInstance).SetValue(sheet, list);
        return sheet;
    }

    // flow 생성 + bare Director를 host로 Attach. director는 caller가 파괴한다.
    private static object NewAttachedFlow(ScriptableObject sheet, out GameObject hostGo, out Component director)
    {
        object flow = Activator.CreateInstance(FlowType, new object[] { sheet, null });
        hostGo = new GameObject("TutorialInterpreterTestHost");
        director = hostGo.AddComponent(DirectorType);
        FlowType.GetMethod("Attach").Invoke(flow, new object[] { director });
        return flow;
    }

    private static void Callback(object flow, string name, params object[] args)
    {
        Type[] sig = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();
        MethodInfo m = FlowType.GetMethod(name) ?? FlowType.GetMethod(name, sig);
        m.Invoke(flow, args);
    }

    private static HashSet<string> Flags(object flow) => (HashSet<string>)GetPrivate(flow, "flags");
    private static HashSet<string> Fired(object flow) => (HashSet<string>)GetPrivate(flow, "firedRuleIds");
    private static int CurrentStep(object flow) => (int)FlowType.GetProperty("CurrentStep").GetValue(flow);

    private sealed class Probe : IDisposable
    {
        public bool Disposed;
        public void Dispose() => Disposed = true;
    }

    // ═══════════════════════════ 1. 순수 Evaluator ═══════════════════════════

    [Test]
    public void Evaluator_Step_ExactGate()
    {
        object t = NewTrigger("UIAction");
        SetPublic(t, "requiredStep", 1);

        object c0 = NewEventCtx("UIAction");
        SetPublic(c0, "currentStep", 0);
        Assert.That(Matches(t, c0), Is.False, "정확 Step 불일치 → 미실행");

        object c1 = NewEventCtx("UIAction");
        SetPublic(c1, "currentStep", 1);
        Assert.That(Matches(t, c1), Is.True, "정확 Step 일치 → 실행");
    }

    [Test]
    public void Evaluator_Step_Range()
    {
        object t = NewTrigger("UIAction");
        SetPublic(t, "minStep", 1);
        SetPublic(t, "maxStep", 3);

        Assert.That(Matches(t, StepCtx(0)), Is.False, "범위 하한 미만");
        Assert.That(Matches(t, StepCtx(2)), Is.True, "범위 내");
        Assert.That(Matches(t, StepCtx(4)), Is.False, "범위 상한 초과");
    }

    private static object StepCtx(int step)
    {
        object c = NewEventCtx("UIAction");
        SetPublic(c, "currentStep", step);
        return c;
    }

    [Test]
    public void Evaluator_Flag_Required()
    {
        object t = NewTrigger("UIAction");
        SetPublic(t, "requireFlag", "opened");

        object noFlag = NewEventCtx("UIAction");
        Assert.That(Matches(t, noFlag), Is.False, "flag 미설정 → 미실행");

        object withFlag = NewEventCtx("UIAction");
        SetPublic(withFlag, "flags", new HashSet<string>(StringComparer.Ordinal) { "opened" });
        Assert.That(Matches(t, withFlag), Is.True, "flag 설정 → 실행");
    }

    [Test]
    public void Evaluator_Round_Range()
    {
        object t = NewTrigger("RoundStarted");
        SetPublic(t, "minRound", 2);
        SetPublic(t, "maxRound", 3);

        Assert.That(Matches(t, RoundCtx(1)), Is.False);
        Assert.That(Matches(t, RoundCtx(2)), Is.True);
        Assert.That(Matches(t, RoundCtx(3)), Is.True);
        Assert.That(Matches(t, RoundCtx(4)), Is.False);
    }

    private static object RoundCtx(int round)
    {
        object c = NewEventCtx("RoundStarted");
        SetPublic(c, "round", round);
        return c;
    }

    [Test]
    public void Evaluator_UIAction_ActionIdMatch()
    {
        object specific = NewTrigger("UIAction");
        SetPublic(specific, "uiActionId", "confirm");

        object cancel = NewEventCtx("UIAction");
        SetPublic(cancel, "actionId", "cancel");
        Assert.That(Matches(specific, cancel), Is.False, "다른 입력 무시");

        object confirm = NewEventCtx("UIAction");
        SetPublic(confirm, "actionId", "confirm");
        Assert.That(Matches(specific, confirm), Is.True, "일치 입력만");

        object anyTrigger = NewTrigger("UIAction");   // uiActionId 빈값 = 아무 입력
        Assert.That(Matches(anyTrigger, confirm), Is.True, "빈 uiActionId → 모두 허용");
    }

    [Test]
    public void Evaluator_AliveCount_SideAndThreshold()
    {
        object t = NewTrigger("AliveCountAtOrBelow");
        SetPublic(t, "side", Side("Enemy"));
        SetPublic(t, "aliveCountAtOrBelow", 1);

        Assert.That(Matches(t, AliveCtx(enemy: 2)), Is.False, "임계 초과");
        Assert.That(Matches(t, AliveCtx(enemy: 1)), Is.True, "임계 이하");

        object disabled = NewTrigger("AliveCountAtOrBelow");   // -1 = 무시
        SetPublic(disabled, "side", Side("Enemy"));
        Assert.That(Matches(disabled, AliveCtx(enemy: 0)), Is.False, "aliveCountAtOrBelow=-1 → 무시");
    }

    private static object AliveCtx(int enemy)
    {
        object c = NewEventCtx("AliveCountAtOrBelow");
        SetPublic(c, "aliveEnemyCount", enemy);
        return c;
    }

    [Test]
    public void Evaluator_HpRatio_AtOrBelow()
    {
        Type characterType = FindRuntimeType("BattleCharactor");
        var go = new GameObject("TutorialHpRatioUnit");
        try
        {
            Component unit = go.AddComponent(characterType);   // side Any + templateId 빈값 → 매칭

            object t = NewTrigger("UnitHpAtOrBelow");
            SetPublic(t, "side", Side("Any"));
            SetPublic(t, "hpRatioAtOrBelow", 0.3f);

            Assert.That(Matches(t, HpCtx(unit, 30f, 100f)), Is.True, "0.30 ≤ 0.30");
            Assert.That(Matches(t, HpCtx(unit, 40f, 100f)), Is.False, "0.40 > 0.30");
            Assert.That(Matches(t, HpCtx(unit, 10f, 0f)), Is.False, "maxHp 0 방어");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static object HpCtx(object unit, float cur, float max)
    {
        object c = NewEventCtx("UnitHpAtOrBelow");
        SetPublic(c, "unit", unit);
        SetPublic(c, "currentHp", cur);
        SetPublic(c, "maxHp", max);
        return c;
    }

    // ═══════════════════════════ 2. 카탈로그 / 검증 ═══════════════════════════

    [Test]
    public void ScheduleCatalog_Find_ExactThenWildcard_Ordinal()
    {
        ScriptableObject exact = NewSheet(5, "TUT_A");
        ScriptableObject wild = NewSheet(-1, "TUT_B");
        ScriptableObject catalog = ScriptableObject.CreateInstance(CatalogType);
        try
        {
            var sheets = (IList)CatalogType.GetField("sheets", AllInstance).GetValue(catalog);
            sheets.Add(exact);
            sheets.Add(wild);

            MethodInfo find = CatalogType.GetMethod("Find");
            Assert.That(find.Invoke(catalog, new object[] { 5, "TUT_A" }), Is.SameAs(exact), "정확 (zone,key)");
            Assert.That(find.Invoke(catalog, new object[] { 9, "TUT_A" }), Is.Null, "다른 zone·와일드카드 없음");
            Assert.That(find.Invoke(catalog, new object[] { 9, "TUT_B" }), Is.SameAs(wild), "zone 와일드카드(-1)");
            Assert.That(find.Invoke(catalog, new object[] { 5, "tut_a" }), Is.Null, "Ordinal 대소문자 구분");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(exact);
            UnityEngine.Object.DestroyImmediate(wild);
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void ScheduleSheet_Validation_DetectsDupEmptyAndMissingUiKey()
    {
        object showUiNoKey = NewAction("ShowUi");   // uiKey 비어있음
        object dupA = NewRule("dup", NewTrigger("BattleEntered"), true, NewSetFlagAction("x"));
        object dupB = NewRule("dup", NewTrigger("BattleEntered"), true, showUiNoKey);   // 중복 ruleId + 빈 uiKey
        object empty = NewRule("", NewTrigger("BattleEntered"), true);

        ScriptableObject sheet = NewSheet(1, "TUT_V", dupA, dupB, empty);
        try
        {
            var issues = new List<string>();
            SheetType.GetMethod("CollectValidationIssues").Invoke(sheet, new object[] { issues });

            Assert.That(issues.Exists(s => s.Contains("중복")), Is.True, "중복 ruleId 검출");
            Assert.That(issues.Exists(s => s.Contains("빈 ruleId")), Is.True, "빈 ruleId 검출");
            Assert.That(issues.Exists(s => s.Contains("uiKey")), Is.True, "빈 uiKey 검출");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    // ═══════════════════════════ 3. 조율(flow, bare Director host) ═══════════════════════════

    [Test]
    public void Flow_MultipleRules_AllFireOnSameEvent()
    {
        object r1 = NewRule("r1", NewTrigger("BattleEntered"), true, NewSetFlagAction("a"));
        object r2 = NewRule("r2", NewTrigger("BattleEntered"), true, NewSetFlagAction("b"));
        ScriptableObject sheet = NewSheet(-1, "TUT_M", r1, r2);

        object flow = NewAttachedFlow(sheet, out GameObject hostGo, out _);
        try
        {
            Callback(flow, "OnBattleEntered");

            HashSet<string> flags = Flags(flow);
            Assert.That(flags.Contains("a") && flags.Contains("b"), Is.True,
                "한 이벤트에 매칭된 모든 규칙이 실행되어야 한다");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hostGo);
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    [Test]
    public void Flow_OnceRule_FiresExactlyOnce()
    {
        object rule = NewRule("only-once", UiActionTrigger("go"), true, NewSetFlagAction("F"));
        ScriptableObject sheet = NewSheet(-1, "TUT_O", rule);

        object flow = NewAttachedFlow(sheet, out GameObject hostGo, out _);
        try
        {
            Callback(flow, "OnUIAction", "go");
            Assert.That(Flags(flow).Contains("F"), Is.True, "1회차 실행");
            Assert.That(Fired(flow).Contains("only-once"), Is.True, "firedRuleIds 표기");

            Flags(flow).Remove("F");                 // 재실행되면 다시 채워질 것
            Callback(flow, "OnUIAction", "go");
            Assert.That(Flags(flow).Contains("F"), Is.False, "once=true → 두 번째는 실행 안 됨");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hostGo);
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    [Test]
    public void Flow_StepGate_OnlyFiresAtRequiredStep()
    {
        object trigger = UiActionTrigger("go");
        SetPublic(trigger, "requiredStep", 1);
        object rule = NewRule("gated", trigger, true, NewSetFlagAction("F"));
        ScriptableObject sheet = NewSheet(-1, "TUT_S", rule);

        object flow = NewAttachedFlow(sheet, out GameObject hostGo, out _);
        try
        {
            Callback(flow, "OnUIAction", "go");
            Assert.That(Flags(flow).Contains("F"), Is.False, "step 0 → 미실행");

            FlowType.GetMethod("SetStep").Invoke(flow, new object[] { 1 });
            Callback(flow, "OnUIAction", "go");
            Assert.That(Flags(flow).Contains("F"), Is.True, "step 1 → 실행");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hostGo);
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    private static object UiActionTrigger(string actionId)
    {
        object t = NewTrigger("UIAction");
        SetPublic(t, "uiActionId", actionId);
        return t;
    }

    [Test]
    public void Flow_ImmediateChain_HitsCapAndLogsError()
    {
        // once=false + 액션 없음 = 매 순회마다 재매칭 → 상한(64)에서 중단.
        object loop = NewRule("loop", NewTrigger("Immediate"), false);
        ScriptableObject sheet = NewSheet(-1, "TUT_I", loop);

        object flow = NewAttachedFlow(sheet, out GameObject hostGo, out _);
        try
        {
            LogAssert.Expect(LogType.Error, new Regex("Immediate 연쇄 상한"));
            Callback(flow, "OnUIAction", "noop");   // UIAction 매칭 규칙 없음 → Immediate 체인만 돈다
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hostGo);
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    // ═══════════════════════════ 4. Director scopeId 격리 ═══════════════════════════

    [Test]
    public void Director_ReleaseEffects_IsScopeIsolated()
    {
        var go = new GameObject("TutorialScopeIsolationHost");
        try
        {
            Component director = go.AddComponent(DirectorType);
            // Track는 IsRunning일 때만 등록한다(초기화 대체).
            DirectorType.GetField("<IsRunning>k__BackingField", AllInstance).SetValue(director, true);

            object step = EnumVal(LifetimeType, "Step");
            MethodInfo track = DirectorType.GetMethod("Track",
                new[] { typeof(IDisposable), LifetimeType, typeof(string) });
            MethodInfo release = DirectorType.GetMethod("ReleaseEffects", new[] { typeof(string) });

            var a = new Probe();
            var b = new Probe();
            track.Invoke(director, new object[] { a, step, "ruleA" });
            track.Invoke(director, new object[] { b, step, "ruleB" });

            release.Invoke(director, new object[] { "ruleA" });
            Assert.That(a.Disposed, Is.True, "해당 scope 효과만 해제");
            Assert.That(b.Disposed, Is.False, "다른 scope 잠금은 유지");

            release.Invoke(director, new object[] { "ruleB" });
            Assert.That(b.Disposed, Is.True, "그 scope 해제 시 정리");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
