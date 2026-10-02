using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

public sealed class TimelineRailPlayModeTests
{
    private const BindingFlags AllInstance =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private const int SkillIndex = 990201;

    [UnityTest]
    public IEnumerator MultiTargetPlan_PlaysTwoSegmentsAndCommitsEachSlotOnce()
    {
        Fixture fixture = BuildFixture(0.12d);
        TimelineAsset second = ScriptableObject.CreateInstance<TimelineAsset>();
        AnimationClip secondClip = new AnimationClip { name = "SecondSegment", frameRate = 30f };
        GameObject secondTargetObject = new GameObject("TimelineRailSecondTarget");
        try
        {
            Type characterType = fixture.CharacterType;
            Component secondTarget = CreateInitializedUnit(secondTargetObject, characterType, false, 1f);
            secondTargetObject.transform.position = new Vector3(5f, 0f, 0f);
            var track = second.CreateTrack<AnimationTrack>(null, "Animation");
            secondClip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x",
                AnimationCurve.Linear(0f, 0f, 0.12f, 0f));
            track.CreateClip(secondClip);
            AddImpactMarker(fixture.Timeline, 0.05d);
            AddImpactMarker(second, 0.05d);
            AddMoveMarkers(second, 0.02d, 0.08d);
            SetEnumField(fixture.Data.GetType(), fixture.Data, "PresentationArchetype", "Melee");
            foreach (string fieldName in new[] { "Move", "Return" })
            {
                object phase = fixture.Data.GetType().GetField(fieldName).GetValue(fixture.Data);
                phase.GetType().GetField("Enabled").SetValue(phase, false);
            }

            Type bindingType = FindRuntimeType("SkillTimelineBinding");
            Type segmentType = FindRuntimeType("SkillTimelineSegment");
            Type listType = typeof(List<>).MakeGenericType(bindingType);
            var bindings = (IList)Activator.CreateInstance(listType);
            var two = Activator.CreateInstance(bindingType);
            bindingType.GetField("CharacterKey").SetValue(two,
                characterType.GetProperty("UnitName").GetValue(fixture.Actor));
            bindingType.GetField("TargetCount").SetValue(two, 2);
            var segmentList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(segmentType));
            foreach (TimelineAsset timeline in new[] { fixture.Timeline, second })
            {
                object segment = Activator.CreateInstance(segmentType);
                segmentType.GetField("Timeline").SetValue(segment, timeline);
                segmentType.GetField("TargetSlot").SetValue(segment, segmentList.Count + 1);
                segmentList.Add(segment);
            }
            bindingType.GetField("Segments").SetValue(two, segmentList);
            bindings.Add(two);
            fixture.Data.GetType().GetField("SkillTimelines").SetValue(fixture.Data, bindings);

            Type contextType = FindRuntimeType("ASB.Work.Battle.Core.DamageContext");
            var contexts = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(contextType));
            foreach (object target in new[] { fixture.Target, secondTarget })
            {
                object context = Activator.CreateInstance(contextType);
                contextType.GetField("Caster").SetValue(context, fixture.Actor);
                contextType.GetField("Target").SetValue(context, target);
                contextType.GetField("SkillIndex").SetValue(context, SkillIndex);
                contexts.Add(context);
            }
            Type callbackType = fixture.OnHit.GetType();
            var callbacks = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(callbackType));
            bool firstResolvedBeforeSecond = false;
            Action countFirst = () =>
            {
                fixture.HitBox[0]++;
                PlayableDirector current = fixture.ActorObject.GetComponent<PlayableDirector>();
                firstResolvedBeforeSecond = current != null && current.playableAsset == fixture.Timeline;
            };
            Type hitResultType = FindRuntimeType("ASB.Work.Battle.Core.BattleHitResult");
            callbacks.Add(Expression.Lambda(callbackType,
                Expression.Block(Expression.Invoke(Expression.Constant(countFirst)),
                    Expression.Default(hitResultType))).Compile());
            int secondHits = 0;
            Action countSecond = () => secondHits++;
            callbacks.Add(Expression.Lambda(callbackType,
                Expression.Block(Expression.Invoke(Expression.Constant(countSecond)),
                    Expression.Default(hitResultType))).Compile());

            MethodInfo canRun = fixture.PresentationDirector.GetType().GetMethod("CanRunMultiTargetPlan");
            FieldInfo forceAnimator = fixture.Catalog.GetType().GetField("_forceAnimatorRail", AllInstance);
            forceAnimator.SetValue(fixture.Catalog, true);
            Assert.That((bool)canRun.Invoke(fixture.PresentationDirector,
                new[] { fixture.Actor, fixture.Skill, contexts }), Is.False);
            forceAnimator.SetValue(fixture.Catalog, false);
            PropertyInfo chainState = fixture.PresentationDirector.GetType().GetProperty("ChainState");
            chainState.SetValue(fixture.PresentationDirector,
                Activator.CreateInstance(chainState.PropertyType));
            Assert.That((bool)canRun.Invoke(fixture.PresentationDirector,
                new[] { fixture.Actor, fixture.Skill, contexts }), Is.False);
            chainState.SetValue(fixture.PresentationDirector, null);
            Type sequenceRegistry = FindRuntimeType("SkillPresentationSequenceRegistry");
            IDictionary customSequences = (IDictionary)sequenceRegistry
                .GetField("_map", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            string skillKey = (string)fixture.Skill.GetType().GetField("skillKey").GetValue(fixture.Skill);
            customSequences[skillKey] = null;
            Assert.That((bool)canRun.Invoke(fixture.PresentationDirector,
                new[] { fixture.Actor, fixture.Skill, contexts }), Is.False);
            customSequences.Remove(skillKey);
            Assert.That((bool)canRun.Invoke(fixture.PresentationDirector,
                new[] { fixture.Actor, fixture.Skill, contexts }), Is.True);
            MethodInfo run = fixture.PresentationDirector.GetType().GetMethod("RunMultiTargetSkillSequence");
            IEnumerator routine = (IEnumerator)run.Invoke(fixture.PresentationDirector,
                new[] { fixture.Actor, fixture.Skill, contexts, callbacks });
            var coroutine = ((MonoBehaviour)fixture.Manager).StartCoroutine(routine);
            Assert.That((bool)fixture.PresentationDirector.GetType().GetProperty("IsSequenceRunning")
                .GetValue(fixture.PresentationDirector), Is.True);
            PlayableDirector director = null;
            bool sawSecond = false;
            bool reachedSecondApproach = false;
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                director = fixture.ActorObject.GetComponent<PlayableDirector>();
                if (director != null && director.playableAsset == second) sawSecond = true;
                if (director != null && director.playableAsset == second && director.time >= 0.08d &&
                    Mathf.Abs(fixture.ActorObject.transform.position.x - 3.8f) < 0.25f)
                    reachedSecondApproach = true;
                if (director != null && director.playableAsset == null && fixture.HitBox[0] == 1 && secondHits == 1)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.That(sawSecond, Is.True, "두 번째 Timeline 세그먼트를 재생해야 합니다.");
            Assert.That(firstResolvedBeforeSecond, Is.True, "첫 Impact 마커가 두 번째 세그먼트 전에 확정돼야 합니다.");
            Assert.That(reachedSecondApproach, Is.True,
                "Move End에서 두 번째 대상의 접근 지점까지 Timeline 시간에 맞춰 이동해야 합니다.");
            Assert.That(fixture.HitBox[0], Is.EqualTo(1));
            Assert.That(secondHits, Is.EqualTo(1));
            Assert.That(director.playableAsset, Is.Null);
        }
        finally
        {
            fixture.Dispose();
            UnityEngine.Object.DestroyImmediate(secondTargetObject);
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(secondClip);
        }
    }

    private static void AddImpactMarker(TimelineAsset timeline, double time)
    {
        if (timeline.markerTrack == null) timeline.CreateMarkerTrack();
        Type markerType = FindRuntimeType("PresentationSignalMarker");
        MethodInfo create = typeof(TrackAsset).GetMethods()
            .Single(method => method.Name == "CreateMarker" && method.IsGenericMethodDefinition);
        object marker = create.MakeGenericMethod(markerType).Invoke(timeline.markerTrack,
            new object[] { time });
        Type kindType = FindRuntimeType("PresentationSignalKind");
        markerType.GetMethod("Configure").Invoke(marker,
            new[] { Enum.Parse(kindType, "Impact"), null, null, (object)0 });
    }

    private static void AddMoveMarkers(TimelineAsset timeline, double start, double end)
    {
        Type markerType = FindRuntimeType("PresentationMoveMarker");
        Type boundaryType = FindRuntimeType("PresentationSectionBoundary");
        MethodInfo create = typeof(TrackAsset).GetMethods()
            .Single(method => method.Name == "CreateMarker" && method.IsGenericMethodDefinition);
        foreach (var entry in new[] { (start, "Start"), (end, "End") })
        {
            object marker = create.MakeGenericMethod(markerType).Invoke(timeline.markerTrack,
                new object[] { entry.Item1 });
            markerType.GetMethod("Configure").Invoke(marker,
                new[] { Enum.Parse(boundaryType, entry.Item2), (object)0 });
        }
    }

    /// <summary>
    /// Marker가 없는 합성 Timeline: 종료 폴백이 히트 콜백을 1회만 부르고 Director를 정리하는지(smoke).
    /// </summary>
    [UnityTest]
    public IEnumerator TimelineRail_NoMarkerFallback_CallsHitOnceAndCleansDirector()
    {
        Fixture fixture = BuildFixture(clipSeconds: 0.1d);
        try
        {
            IEnumerator routine = fixture.CreateRoutine();
            yield return ((MonoBehaviour)fixture.Manager).StartCoroutine(routine);

            Assert.That(fixture.HitBox[0], Is.EqualTo(1), "Timeline 종료 폴백이 히트 콜백을 정확히 1회 호출해야 합니다.");

            PlayableDirector director = fixture.ActorObject.GetComponent<PlayableDirector>();
            Assert.That(director, Is.Not.Null, "Timeline Rail은 actor에 PlayableDirector를 확보해야 합니다.");
            Assert.That(director.playableAsset, Is.Null,
                "Timeline 종료 후 playableAsset을 해제해 Animator 소유권을 반환해야 합니다.");
            Assert.That(director.state, Is.Not.EqualTo(PlayState.Playing));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    /// <summary>
    /// §2-1 Dead 인터럽트: 재생 중 시전자 사망 시 Director를 '동기' 정지하고, Idle로 덮지 않으며,
    /// '커밋된 공격은 1회 확정' 규칙에 따라 대미지 콜백은 정확히 1회. hang 없이 종료.
    /// </summary>
    [UnityTest]
    public IEnumerator TimelineRail_DeathInterrupt_StopsDirectorAndPreservesDead()
    {
        Fixture fixture = BuildFixture(clipSeconds: 3d);
        try
        {
            IEnumerator routine = fixture.CreateRoutine();
            ((MonoBehaviour)fixture.Manager).StartCoroutine(routine);

            // 재생 시작(Director bind+Play) 대기.
            yield return null;
            yield return null;

            PlayableDirector director = fixture.ActorObject.GetComponent<PlayableDirector>();
            Assert.That(director, Is.Not.Null);
            Assert.That(director.state, Is.EqualTo(PlayState.Playing), "인터럽트 전에는 재생 중이어야 합니다.");

            // 치명 피해 → Die() → RequestPresentationInterrupt(Dead) → director 동기 정지.
            fixture.CharacterType.GetMethod("TakeDamage").Invoke(fixture.Actor, new object[] { 9999f });

            Assert.That((bool)fixture.CharacterType.GetProperty("IsDead").GetValue(fixture.Actor), Is.True,
                "치명 피해 후 시전자는 사망 상태여야 합니다.");
            Assert.That(director.state, Is.Not.EqualTo(PlayState.Playing),
                "Dead 인터럽트는 Dead CrossFade보다 먼저 Director를 동기 정지해야 합니다.");

            // 루틴이 finally 정리 + Delivery 폴백까지 진행하도록 대기(자연 종료(3s)보다 훨씬 빨라야 함).
            float elapsed = 0f;
            while (director.playableAsset != null && elapsed < 2f)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            Assert.That(director.playableAsset, Is.Null, "인터럽트 후 playableAsset을 해제해야 합니다.");
            Assert.That(elapsed, Is.LessThan(1.5f), "인터럽트로 재생이 즉시 끊겨야 합니다(자연 종료 대기 금지).");
            Assert.That(fixture.HitBox[0], Is.EqualTo(1),
                "커밋된 공격은 시전자 사망과 무관하게 대미지를 정확히 1회 확정해야 합니다.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    /// <summary>
    /// §2-2 Hit 인터럽트: 재생 중 비치명 피격 시 Director를 동기 정지하고, 생존 상태를 유지하며,
    /// 대미지 콜백은 1회.
    /// </summary>
    [UnityTest]
    public IEnumerator TimelineRail_HitInterrupt_StopsDirectorAndSurvives()
    {
        Fixture fixture = BuildFixture(clipSeconds: 3d);
        try
        {
            IEnumerator routine = fixture.CreateRoutine();
            ((MonoBehaviour)fixture.Manager).StartCoroutine(routine);

            yield return null;
            yield return null;

            PlayableDirector director = fixture.ActorObject.GetComponent<PlayableDirector>();
            Assert.That(director, Is.Not.Null);
            Assert.That(director.state, Is.EqualTo(PlayState.Playing), "인터럽트 전에는 재생 중이어야 합니다.");

            // 비치명 피해 → RequestPresentationInterrupt(Hit) → director 동기 정지.
            fixture.CharacterType.GetMethod("TakeDamage").Invoke(fixture.Actor, new object[] { 1f });

            Assert.That((bool)fixture.CharacterType.GetProperty("IsDead").GetValue(fixture.Actor), Is.False,
                "비치명 피격 후 시전자는 생존해야 합니다.");
            Assert.That(director.state, Is.Not.EqualTo(PlayState.Playing),
                "Hit 인터럽트는 Hit CrossFade보다 먼저 Director를 동기 정지해야 합니다.");

            float elapsed = 0f;
            while (director.playableAsset != null && elapsed < 2f)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            Assert.That(director.playableAsset, Is.Null, "인터럽트 후 playableAsset을 해제해야 합니다.");
            Assert.That(fixture.HitBox[0], Is.EqualTo(1), "커밋된 공격의 대미지는 1회 확정되어야 합니다.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private sealed class Fixture
    {
        public GameObject ManagerObject;
        public GameObject ActorObject;
        public GameObject TargetObject;
        public Component Manager;
        public Component Actor;
        public Component Target;
        public ScriptableObject Data;
        public ScriptableObject Catalog;
        public TimelineAsset Timeline;
        public AnimationClip Clip;
        public object PresentationDirector;
        public MethodInfo Run;
        public object Skill;
        public object DeliveryGate;
        public Delegate OnHit;
        public int[] HitBox;
        public Type CharacterType;

        public IEnumerator CreateRoutine()
        {
            return (IEnumerator)Run.Invoke(PresentationDirector,
                new[] { Actor, Target, Skill, false, true, OnHit, DeliveryGate, null });
        }

        public void Dispose()
        {
            if (ManagerObject != null) UnityEngine.Object.DestroyImmediate(ManagerObject);
            if (ActorObject != null) UnityEngine.Object.DestroyImmediate(ActorObject);
            if (TargetObject != null) UnityEngine.Object.DestroyImmediate(TargetObject);
            if (Data != null) UnityEngine.Object.DestroyImmediate(Data);
            if (Catalog != null) UnityEngine.Object.DestroyImmediate(Catalog);
            if (Timeline != null) UnityEngine.Object.DestroyImmediate(Timeline);
            if (Clip != null) UnityEngine.Object.DestroyImmediate(Clip);
        }
    }

    private static Fixture BuildFixture(double clipSeconds)
    {
        Type managerType = FindRuntimeType("BattleManager");
        Type characterType = FindRuntimeType("BattleCharactor");
        Type skillType = FindRuntimeType("SkillData");
        Type dataType = FindRuntimeType("SkillPresentationData");
        Type catalogType = FindRuntimeType("SkillPresentationCatalog");

        var fixture = new Fixture { CharacterType = characterType };
        fixture.ManagerObject = new GameObject("TimelineRailTestManager");
        fixture.ActorObject = new GameObject("TimelineRailActor");
        fixture.TargetObject = new GameObject("TimelineRailTarget");

        fixture.Manager = fixture.ManagerObject.AddComponent(managerType);
        fixture.ActorObject.AddComponent<Animator>();
        fixture.Actor = CreateInitializedUnit(fixture.ActorObject, characterType, true, 10f);
        fixture.Target = CreateInitializedUnit(fixture.TargetObject, characterType, false, 1f);

        fixture.Timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AnimationTrack track = fixture.Timeline.CreateTrack<AnimationTrack>(null, "Animation");
        fixture.Clip = new AnimationClip { name = "TimelineRailTestClip", frameRate = 30f };
        fixture.Clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x",
            AnimationCurve.Linear(0f, 0f, (float)clipSeconds, 0f));
        track.CreateClip(fixture.Clip);

        fixture.Data = ScriptableObject.CreateInstance(dataType);
        dataType.GetField("SkillIndex").SetValue(fixture.Data, SkillIndex);
        dataType.GetField("PresentationSchemaVersion").SetValue(fixture.Data, 1);
        SetEnumField(dataType, fixture.Data, "AnimationRail", "Timeline");
        SetEnumField(dataType, fixture.Data, "PresentationArchetype", "Stationary");
        string actorKey = (string)characterType.GetProperty("UnitName").GetValue(fixture.Actor);
        SetTimelineBinding(dataType, fixture.Data, actorKey, fixture.Timeline);

        fixture.Catalog = ScriptableObject.CreateInstance(catalogType);
        SetCatalogBinding(catalogType, fixture.Catalog, SkillIndex, fixture.Data);
        managerType.GetField("_presentationCatalog", AllInstance).SetValue(fixture.Manager, fixture.Catalog);

        fixture.Skill = CreateSkill(skillType, SkillIndex);
        fixture.HitBox = new int[] { 0 };
        int[] box = fixture.HitBox;
        Action countHit = () => box[0]++;
        Type hitResultType = FindRuntimeType("ASB.Work.Battle.Core.BattleHitResult");
        Type callbackType = typeof(Func<>).MakeGenericType(hitResultType);
        fixture.OnHit = Expression.Lambda(
            callbackType,
            Expression.Block(
                Expression.Invoke(Expression.Constant(countHit)),
                Expression.Default(hitResultType)))
            .Compile();
        Type deliveryGateType = FindRuntimeType("ASB.Work.Battle.Core.HitDeliveryGate");
        fixture.DeliveryGate = Activator.CreateInstance(deliveryGateType);

        fixture.PresentationDirector = managerType.GetProperty("Presentation", AllInstance).GetValue(fixture.Manager);
        fixture.Run = fixture.PresentationDirector.GetType().GetMethods()
            .Single(method => method.Name == "RunSkillSequenceCore"
                              && method.GetParameters().Length == 8);
        return fixture;
    }

    private static void SetTimelineBinding(Type dataType, ScriptableObject data, string characterKey,
        TimelineAsset timeline)
    {
        FieldInfo listField = dataType.GetField("SkillTimelines");
        Type bindingType = FindRuntimeType("SkillTimelineBinding");
        Type listType = typeof(List<>).MakeGenericType(bindingType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType);
        object binding = Activator.CreateInstance(bindingType);
        bindingType.GetField("CharacterKey").SetValue(binding, characterKey);
        bindingType.GetField("Timeline").SetValue(binding, timeline);
        list.Add(binding);
        listField.SetValue(data, list);
    }

    private static void SetCatalogBinding(Type catalogType, ScriptableObject catalog, int skillIndex,
        ScriptableObject data)
    {
        Type bindingType = catalogType.GetNestedType("Binding", BindingFlags.Public);
        Array bindings = Array.CreateInstance(bindingType, 1);
        object binding = Activator.CreateInstance(bindingType);
        bindingType.GetField("SkillIndex").SetValue(binding, skillIndex);
        bindingType.GetField("Presentation").SetValue(binding, data);
        bindings.SetValue(binding, 0);
        catalogType.GetField("_bindings", AllInstance).SetValue(catalog, bindings);
    }

    private static void SetEnumField(Type ownerType, object owner, string fieldName, string value)
    {
        FieldInfo field = ownerType.GetField(fieldName);
        field.SetValue(owner, Enum.Parse(field.FieldType, value));
    }

    private static object CreateSkill(Type skillType, int skillIndex)
    {
        object skill = Activator.CreateInstance(skillType);
        skillType.GetField("skillIndex").SetValue(skill, skillIndex);
        skillType.GetField("skillKey").SetValue(skill, "TimelineRailPlayMode");
        skillType.GetField("skillName").SetValue(skill, "Timeline Rail PlayMode");
        skillType.GetField("classSkillEffect").SetValue(skill, 0);
        skillType.GetField("skillValue").SetValue(skill, 1f);
        skillType.GetField("HitDelay").SetValue(skill, 0f);
        skillType.GetField("TotalDelay").SetValue(skill, 0f);
        skillType.GetField("UseAnimEvent").SetValue(skill, false);
        return skill;
    }

    private static Component CreateInitializedUnit(GameObject gameObject, Type characterType, bool isPlayer,
        float speed)
    {
        Type statType = FindRuntimeType("StatBlock");
        Component unit = gameObject.AddComponent(characterType);
        characterType.GetProperty("IsPlayer").SetValue(unit, isPlayer);
        characterType.GetMethod("SetLevelScaling").Invoke(unit, new object[] { false });
        object stats = Activator.CreateInstance(statType,
            new object[] { 100f, 10f, 0f, 0f, speed, 0f, 1f, 0f, 0f, 100f });
        characterType.GetMethod("SetBaseStats").Invoke(unit, new[] { stats });
        characterType.GetMethod("RecalculateStats").Invoke(unit, new object[] { false });
        characterType.GetMethod("InitializeCurrentHpToMax").Invoke(unit, null);
        return unit;
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
