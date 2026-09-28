using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Jig "Runtime Timeline Variant로 마이그레이션"의 굽기 계획(JigPathABaker.Plan)을 고정한다.
/// - Attack-only 지원 범위, Beat 상태명 규칙(런타임 ResolveAttackBeatState와 동일)
/// - Beat 겹침 배치(ComputeBlendBoundaries 재사용)
/// - Cue Timing별 배치, Delivery 마커의 Animator 레일 일치 규칙
/// - 실패 시 파일·SO가 바뀌지 않음
///
/// 연출 데이터·지그 코드는 Assembly-CSharp(-Editor)에 있어 리플렉션/SerializedObject로 접근한다.
/// AnimatorController·클립·SO는 전부 메모리에만 만든다(Bake 실패 테스트도 에셋을 만들지 않는다).
/// </summary>
public sealed class PresentationJigBakePlanTests
{
    private const int NormalizedTime = 1;   // CueTimingSource: ClipEvent=0, NormalizedTime=1, Seconds=2
    private const int Seconds = 2;
    private const int ClipEvent = 0;

    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    private static Type BakerType => FindType("ASB.Work.EditorTools.Jig.JigPathABaker");
    private static Type DataType => FindType("SkillPresentationData");

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    // ── Beat 상태명 규칙 ─────────────────────────────────────────

    [Test]
    public void FirstBeatEmptyState_FallsBackToTopLevelState()
    {
        AnimatorController ctrl = Controller(("ClassSkill_2", Clip(0.5f)));
        ScriptableObject data = Data(topState: "Base Layer.ClassSkill_2", beats: new[] { Beat("") });

        object plan = Plan(data, ctrl);

        Assert.That(Errors(plan), Is.Empty);
        Assert.That(ClipStates(plan), Is.EqualTo(new[] { "Base Layer.ClassSkill_2" }));
    }

    [Test]
    public void LaterBeatEmptyState_IsError()
    {
        AnimatorController ctrl = Controller(("ClassSkill_2", Clip(0.5f)));
        ScriptableObject data = Data(beats: new[] { Beat("Base Layer.ClassSkill_2"), Beat("") });

        object plan = Plan(data, ctrl);

        Assert.That(Errors(plan).Any(e => e.Contains("Attack.Beats[1]")), Is.True, string.Join("\n", Errors(plan)));
        Assert.That(Clips(plan).Count, Is.EqualTo(0), "실패 시 클립 계획이 남으면 안 됩니다.");
    }

    // ── 배치 ─────────────────────────────────────────────────────

    [Test]
    public void TwoBeats_OverlapByBlendIn_FirstBlendIgnored()
    {
        AnimatorController ctrl = Controller(("A", Clip(0.5f)), ("B", Clip(0.4f)));
        ScriptableObject data = Data(beats: new[] { Beat("Base Layer.A", blend: 0.3f), Beat("Base Layer.B", blend: 0.1f) });

        object plan = Plan(data, ctrl);

        Assert.That(Errors(plan), Is.Empty);
        Assert.That(ClipStart(plan, 0), Is.EqualTo(0d).Within(1e-6), "첫 Beat의 BlendIn은 배치에 영향이 없어야 합니다.");
        Assert.That(ClipStart(plan, 1), Is.EqualTo(0.4d).Within(1e-6), "두 번째 Beat는 BlendIn만큼 앞당겨 겹쳐야 합니다.");
        Assert.That((double)Get(plan, "SectionEnd"), Is.EqualTo(0.8d).Within(1e-6));
        Assert.That(Strings(plan, "Infos").Any(s => s.Contains("BlendIn")), Is.True, "첫 Beat BlendIn 미반영 안내가 있어야 합니다.");
    }

    [Test]
    public void BlendLongerThanClip_IsClamped_WithWarning()
    {
        AnimatorController ctrl = Controller(("A", Clip(0.5f)), ("B", Clip(0.4f)));
        ScriptableObject data = Data(beats: new[] { Beat("Base Layer.A"), Beat("Base Layer.B", blend: 5f) });

        object plan = Plan(data, ctrl);

        Assert.That(ClipStart(plan, 1), Is.GreaterThan(0.1d - 1e-6), "블렌드는 클립 길이 안으로 클램프되어야 합니다.");
        Assert.That(Strings(plan, "Warnings").Any(s => s.Contains("클램프")), Is.True);
    }

    // ── Cue Timing ───────────────────────────────────────────────

    [Test]
    public void Cues_PlacedPerTimingRelativeToOwnBeat()
    {
        AnimationClip b = Clip(0.4f, cueEvents: new[] { ("hit", 0.2f) });
        AnimatorController ctrl = Controller(("A", Clip(0.5f)), ("B", b));
        ScriptableObject data = Data(beats: new[]
        {
            Beat("Base Layer.A", cues: new[] { Cue("a0", NormalizedTime, 0f), Cue("an", NormalizedTime, 0.5f) }),
            Beat("Base Layer.B", blend: 0.1f, cues: new[]
            {
                Cue("bs", Seconds, 0.1f), Cue("bn", NormalizedTime, 0.1f), Cue("hit", ClipEvent, 0f), Cue("none", ClipEvent, 0f),
            }),
        });

        object plan = Plan(data, ctrl);
        Dictionary<string, double> t = CueTimes(plan);

        Assert.That(t["a0"], Is.EqualTo(0d).Within(1e-6), "Time=0은 유효값 — BeatStart에 놓여야 합니다.");
        Assert.That(t["an"], Is.EqualTo(0.25d).Within(1e-6));
        Assert.That(t["bs"], Is.EqualTo(0.5d).Within(1e-6), "Seconds = BeatStart(0.4) + 0.1");
        Assert.That(t["bn"], Is.EqualTo(0.44d).Within(1e-6), "NormalizedTime = BeatStart(0.4) + 0.1 × 0.4");
        Assert.That(t["hit"], Is.EqualTo(0.6d).Within(1e-6), "ClipEvent = BeatStart(0.4) + 실제 이벤트 0.2");
        Assert.That(t["none"], Is.EqualTo(0.5d).Within(1e-6), "이벤트 없는 ClipEvent는 Beat 안 25%에 임시 배치");
        Assert.That(Strings(plan, "Warnings").Any(s => s.Contains("'none'")), Is.True, "임시 배치 경고가 있어야 합니다.");
    }

    // ── Delivery ─────────────────────────────────────────────────

    [Test]
    public void Delivery_AllBeatsNoWaitForHit_AtFirstBeatStart()
    {
        AnimatorController ctrl = Controller(("A", Clip(0.5f)), ("B", Clip(0.4f)));
        ScriptableObject data = Data(beats: new[] { Beat("Base Layer.A", wait: false), Beat("Base Layer.B", wait: false) });

        object plan = Plan(data, ctrl);

        Assert.That((double)Get(plan, "DeliveryTime"), Is.EqualTo(0d).Within(1e-6),
            "Animator 레일(ComboSkillAction)은 이 경우 첫 Beat 시작에 즉시 처리합니다.");
    }

    [Test]
    public void Delivery_WaitForHitBeat_UsesFirstOnHitEvent()
    {
        AnimationClip b = Clip(0.4f, onHit: 0.2f);
        AnimatorController ctrl = Controller(("A", Clip(0.5f)), ("B", b));
        ScriptableObject data = Data(beats: new[]
        {
            Beat("Base Layer.A", wait: false), Beat("Base Layer.B", blend: 0.1f, wait: true),
        });

        object plan = Plan(data, ctrl);

        Assert.That((double)Get(plan, "DeliveryTime"), Is.EqualTo(0.6d).Within(1e-6), "BeatStart(0.4) + OnHit(0.2)");
    }

    [Test]
    public void Delivery_WaitForHitWithoutOnHit_Falls60Percent_WithWarning()
    {
        AnimatorController ctrl = Controller(("A", Clip(0.5f)));
        ScriptableObject data = Data(beats: new[] { Beat("Base Layer.A", wait: true) });

        object plan = Plan(data, ctrl);

        Assert.That((double)Get(plan, "DeliveryTime"), Is.EqualTo(0.3d).Within(1e-6));
        Assert.That(Strings(plan, "Warnings").Any(s => s.Contains("AniEvent_OnHit")), Is.True);
    }

    // ── 지원 범위 ─────────────────────────────────────────────────

    [Test]
    public void NonAttackPhaseContent_IsError()
    {
        AnimatorController ctrl = Controller(("A", Clip(0.5f)));

        ScriptableObject prep = Data(beats: new[] { Beat("Base Layer.A") });
        SetString(prep, "AttackPrepare.AnimationStateName", "Base Layer.A");
        Assert.That(Errors(Plan(prep, ctrl)).Any(e => e.Contains("AttackPrepare")), Is.True);

        ScriptableObject post = Data(beats: new[] { Beat("Base Layer.A") });
        SetFloat(post, "Post.ExtraDelay", 0.5f);
        Assert.That(Errors(Plan(post, ctrl)).Any(e => e.Contains("Post")), Is.True);

        ScriptableObject moving = Data(beats: new[] { Beat("Base Layer.A") });
        SetBool(moving, "MovingAttack.Enabled", true);
        Assert.That(Errors(Plan(moving, ctrl)).Any(e => e.Contains("MovingAttack")), Is.True);
    }

    // ── 실패 시 무변경 ─────────────────────────────────────────────

    [Test]
    public void Bake_Failure_LeavesNoAssetAndNoBinding()
    {
        AnimatorController ctrl = Controller(("A", Clip(0.5f)));
        ScriptableObject data = Data(beats: new[] { Beat("Base Layer.A"), Beat("") });
        SetInt(data, "SkillIndex", 999991);

        var go = Track(new GameObject("Unit_JigBakeTest_99991"));
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;

        const string expectedPath = "Assets/ASB_Work/Skills/Timelines/Skill999991_99991.playable";
        Assume.That(File.Exists(expectedPath), Is.False, "테스트 경로에 이미 파일이 있습니다.");

        object[] args = { data, go, null };
        object result = BakerType.GetMethod("Bake", BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        Assert.That(result, Is.Null);
        Assert.That((string)args[2], Does.Contain("Attack.Beats[1]"));
        Assert.That(File.Exists(expectedPath), Is.False, ".playable이 만들어지면 안 됩니다.");
        var so = new SerializedObject(data);
        Assert.That(so.FindProperty("AnimationRail").enumValueIndex, Is.EqualTo(0), "AnimationRail이 바뀌면 안 됩니다.");
        Assert.That(so.FindProperty("SkillTimelines").arraySize, Is.EqualTo(0), "SkillTimelines 바인딩이 생기면 안 됩니다.");
    }

    // ── 헬퍼: 데이터 구성 ──────────────────────────────────────────

    private sealed class BeatSpec
    {
        public string State; public float Blend; public bool Wait; public CueSpec[] Cues;
    }

    private sealed class CueSpec
    {
        public string Name; public int Timing; public float Time;
    }

    private static BeatSpec Beat(string state, float blend = 0.1f, bool wait = false, CueSpec[] cues = null) =>
        new BeatSpec { State = state, Blend = blend, Wait = wait, Cues = cues ?? new CueSpec[0] };

    private static CueSpec Cue(string name, int timing, float time) =>
        new CueSpec { Name = name, Timing = timing, Time = time };

    private ScriptableObject Data(string topState = "", BeatSpec[] beats = null)
    {
        var data = Track(ScriptableObject.CreateInstance(DataType));
        var so = new SerializedObject(data);
        so.FindProperty("PresentationSchemaVersion").intValue = 1;
        so.FindProperty("AnimationStateName").stringValue = topState;
        so.FindProperty("Attack.Enabled").boolValue = true;

        SerializedProperty arr = so.FindProperty("Attack.Beats");
        arr.ClearArray();
        beats = beats ?? new BeatSpec[0];
        for (int i = 0; i < beats.Length; i++)
        {
            arr.InsertArrayElementAtIndex(i);
            SerializedProperty b = arr.GetArrayElementAtIndex(i);
            b.FindPropertyRelative("AnimationStateName").stringValue = beats[i].State;
            b.FindPropertyRelative("BlendInSeconds").floatValue = beats[i].Blend;
            b.FindPropertyRelative("AdvanceOnEvent").boolValue = false;
            b.FindPropertyRelative("WaitForHitEvent").boolValue = beats[i].Wait;

            SerializedProperty cues = b.FindPropertyRelative("Cues");
            cues.ClearArray();
            for (int c = 0; c < beats[i].Cues.Length; c++)
            {
                cues.InsertArrayElementAtIndex(c);
                SerializedProperty cue = cues.GetArrayElementAtIndex(c);
                cue.FindPropertyRelative("CueName").stringValue = beats[i].Cues[c].Name;
                cue.FindPropertyRelative("Timing").enumValueIndex = beats[i].Cues[c].Timing;
                cue.FindPropertyRelative("Time").floatValue = beats[i].Cues[c].Time;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return data;
    }

    private AnimationClip Clip(float length, (string name, float time)[] cueEvents = null, float onHit = -1f)
    {
        var clip = Track(new AnimationClip { name = $"TestClip_{length:F2}_{_created.Count}" });
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, length, 0f));

        var events = new List<AnimationEvent>();
        if (cueEvents != null)
            foreach (var e in cueEvents)
                events.Add(new AnimationEvent { functionName = "AniEvent_PresentationCue", stringParameter = e.name, time = e.time });
        if (onHit >= 0f)
            events.Add(new AnimationEvent { functionName = "AniEvent_OnHit", time = onHit });
        AnimationUtility.SetAnimationEvents(clip, events.ToArray());
        return clip;
    }

    private AnimatorController Controller(params (string state, AnimationClip clip)[] states)
    {
        var ctrl = Track(new AnimatorController { name = "JigBakeTestController" });
        ctrl.AddLayer("Base Layer");
        AnimatorStateMachine machine = ctrl.layers[0].stateMachine;
        foreach (var s in states)
        {
            AnimatorState st = machine.AddState(s.state);
            st.motion = s.clip;
            st.speed = 1f;
        }
        return ctrl;
    }

    private static void SetString(ScriptableObject o, string path, string v) => Mutate(o, p => p.FindProperty(path).stringValue = v);
    private static void SetFloat(ScriptableObject o, string path, float v) => Mutate(o, p => p.FindProperty(path).floatValue = v);
    private static void SetBool(ScriptableObject o, string path, bool v) => Mutate(o, p => p.FindProperty(path).boolValue = v);
    private static void SetInt(ScriptableObject o, string path, int v) => Mutate(o, p => p.FindProperty(path).intValue = v);

    private static void Mutate(ScriptableObject o, Action<SerializedObject> edit)
    {
        var so = new SerializedObject(o);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── 헬퍼: 계획 조회 ───────────────────────────────────────────

    private static object Plan(ScriptableObject data, RuntimeAnimatorController ctrl) =>
        BakerType.GetMethod("Plan", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { data, ctrl });

    private static object Get(object o, string field) =>
        o.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance).GetValue(o);

    private static IList Clips(object plan) => (IList)Get(plan, "Clips");

    private static List<string> Errors(object plan) => Strings(plan, "Errors");

    private static List<string> Strings(object plan, string field) => ((IList)Get(plan, field)).Cast<string>().ToList();

    private static double ClipStart(object plan, int index) => (double)Get(Clips(plan)[index], "Start");

    private static string[] ClipStates(object plan) => Clips(plan).Cast<object>().Select(c => (string)Get(c, "StateName")).ToArray();

    private static Dictionary<string, double> CueTimes(object plan)
    {
        var map = new Dictionary<string, double>();
        foreach (object c in (IList)Get(plan, "Cues"))
        {
            object cue = Get(c, "Cue");
            string name = (string)cue.GetType().GetProperty("NormalizedCueName").GetValue(cue);
            map[name] = (double)Get(c, "Time");
        }
        return map;
    }

    private static Type FindType(string fullName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName, false))
            .FirstOrDefault(t => t != null);
        Assert.That(type, Is.Not.Null, $"타입 '{fullName}'을 찾지 못했습니다.");
        return type;
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        _created.Add(value);
        return value;
    }
}
