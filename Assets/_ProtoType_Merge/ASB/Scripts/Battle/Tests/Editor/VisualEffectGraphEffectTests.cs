using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class VisualEffectGraphEffectTests
{
    private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private static readonly string[] LoopCuePrefabPaths =
    {
        "Assets/RenderFX/EnemySkill/Yulia/Boss/FX_Yulia_400010_CastCharge.prefab",
        "Assets/RenderFX/EnemySkill/Yulia/Amplifier/FX_Yulia_400020_AmplifierCastCharge.prefab"
    };

    private static readonly string[] PersistentPrefabPaths =
    {
        "Assets/RenderFX/EnemySkill/Yulia/Amplifier/FX_Yulia_AmplifierIdleElectric.prefab",
        "Assets/RenderFX/EnemySkill/Yulia/Boss/FX_Yulia_IdleHandOrb.prefab"
    };

    private static readonly string[] OneShotPrefabPaths =
    {
        "Assets/RenderFX/EnemySkill/Yulia/Boss/FX_Yulia_400019_LightningHit.prefab",
        "Assets/RenderFX/EnemySkill/Yulia/Hoop/FX_Yulia_HoopSummonPortal.prefab"
    };

    private static readonly string[] YuliaLightningSkills = { "400011", "400012", "400023", "400033" };

    private const string MadonnaUnitPath = "Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanMadonna_40001.prefab";
    private const string MergedRegistryPath = "Assets/RenderFX/_Seam/Data/Merged/JC_Merged_EffectRegistry.asset";

    [Test]
    public void ResolvePlayRate_ClampsToMinimumAndGuardsNaN()
    {
        Assert.That(Invoke<float>("ResolvePlayRate", 2f), Is.EqualTo(2f));
        Assert.That(Invoke<float>("ResolvePlayRate", 0f), Is.EqualTo(0.01f).Within(0.0001f));
        Assert.That(Invoke<float>("ResolvePlayRate", -1f), Is.EqualTo(0.01f).Within(0.0001f));
        Assert.That(Invoke<float>("ResolvePlayRate", float.NaN), Is.EqualTo(1f));
    }

    [Test]
    public void ShouldFinishNaturally_WaitsForMinimumPlayTimeAndSleep()
    {
        Assert.That(Invoke<bool>("ShouldFinishNaturally", 0.05f, false), Is.False, "초기 이벤트 처리 전에는 끝내지 않는다.");
        Assert.That(Invoke<bool>("ShouldFinishNaturally", 0.5f, true), Is.False, "입자가 남아 있으면 끝내지 않는다.");
        Assert.That(Invoke<bool>("ShouldFinishNaturally", 0.5f, false), Is.True);
    }

    [Test]
    public void ShouldCompleteStop_EndsWhenParticlesGoneOrFadeOutCapReached()
    {
        Assert.That(Invoke<bool>("ShouldCompleteStop", 0.01f, 2f, false), Is.False, "Stop 직후 상태 갱신 지연을 기다린다.");
        Assert.That(Invoke<bool>("ShouldCompleteStop", 0.1f, 2f, false), Is.True);
        Assert.That(Invoke<bool>("ShouldCompleteStop", 1f, 2f, true), Is.False);
        Assert.That(Invoke<bool>("ShouldCompleteStop", 2f, 2f, true), Is.True, "남은 입자가 있어도 상한에서 정리한다.");
    }

    [Test]
    public void HasExceededLifetime_ZeroMeansUnlimited()
    {
        Assert.That(Invoke<bool>("HasExceededLifetime", 100f, 0f), Is.False);
        Assert.That(Invoke<bool>("HasExceededLifetime", 7.9f, 8f), Is.False);
        Assert.That(Invoke<bool>("HasExceededLifetime", 8f, 8f), Is.True);
    }

    [Test]
    public void EffectFallbackRelease_SkipsSelfManagedEffects()
    {
        Type effectType = FindRuntimeType("VisualEffectGraphEffect");
        Type releaseType = FindRuntimeType("EffectFallbackRelease");
        var go = new GameObject("VisualEffectGraphEffectTests_Instance");
        try
        {
            go.AddComponent(effectType);
            MethodInfo ensure = releaseType.GetMethod("Ensure", AnyStatic);
            ensure.Invoke(null, new object[] { go, 2f });
            Assert.That(go.GetComponent(releaseType), Is.Null, "스스로 수명을 관리하는 재료에 고정 상한이 붙으면 끝이 잘린다.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [TestCaseSource(nameof(LoopCuePrefabPaths))]
    public void YuliaCastChargePrefab_UsesSingleLoopEffectWithSocketAttach(string prefabPath)
    {
        Component effect = LoadEffect(prefabPath);
        Assert.That(ReadMode(effect), Is.EqualTo("Loop"));
        Assert.That(new SerializedObject(effect).FindProperty("attachToSocket").boolValue, Is.True);
        AssertSingleSkillEffectBehaviour(effect.gameObject, prefabPath);
    }

    [TestCaseSource(nameof(PersistentPrefabPaths))]
    public void UnitAttachedPrefab_UsesPersistentMode(string prefabPath)
    {
        Component effect = LoadEffect(prefabPath);
        Assert.That(ReadMode(effect), Is.EqualTo("Persistent"));
        AssertSingleSkillEffectBehaviour(effect.gameObject, prefabPath);
    }

    [TestCaseSource(nameof(OneShotPrefabPaths))]
    public void OneShotPrefab_UsesOneShotModeWithLifetimeCap(string prefabPath)
    {
        Component effect = LoadEffect(prefabPath);
        Assert.That(ReadMode(effect), Is.EqualTo("OneShot"));
        Assert.That(new SerializedObject(effect).FindProperty("maxLifetimeSeconds").floatValue, Is.GreaterThan(0f),
            "루프 그래프도 상한에서 정리되어야 한다.");
        AssertSingleSkillEffectBehaviour(effect.gameObject, prefabPath);
    }

    [Test]
    public void MadonnaUnit_HasIdleHandOrbUnderRightHandSocket()
    {
        Transform orb = FindChild(MadonnaUnitPath, "FX_YuliaIdleHandOrb");
        Assert.That(orb.parent.name, Is.EqualTo("R_HandSocket"));
        Assert.That(orb.lossyScale.x, Is.EqualTo(0.3f).Within(0.005f), "손 소켓 스케일(200)을 상쇄한 월드 크기여야 한다.");
    }

    // 굴렁쇠 소환 포털은 유닛 자식이 아니라 HoopVfxPreset.summonPortalPrefab 슬롯으로 옮겼다
    // (YuliaBattleVfxTests.HoopSummon_UsesPortalSlotInsteadOfUnitChild).

    [TestCase("Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanAmplifier_40002.prefab")]
    [TestCase("Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanAmplifier_40003.prefab")]
    public void AmplifierUnit_ReviveFlashReplaysWhenOrbReactivates(string unitPath)
    {
        // AmplifierOrbHealthVisual이 부활 때마다 다시 켜 재생하므로 끝나도 오브젝트를 지우면 안 된다(평소에는 꺼 둔다).
        Transform flash = FindChild(unitPath, "FX_AmplifierReviveFlash");
        Assert.That(flash.parent.name, Is.EqualTo("2010_Orb"));
        Component effect = flash.GetComponent(FindRuntimeType("VisualEffectGraphEffect"));
        Assert.That(effect, Is.Not.Null);
        Assert.That(ReadMode(effect), Is.EqualTo("OneShot"));
        Assert.That(new SerializedObject(effect).FindProperty("destroyOnFinish").boolValue, Is.False);
    }

    [TestCaseSource(nameof(YuliaLightningSkills))]
    public void YuliaLightningSkill_HitEffectResolvesInMergedRegistry(string skill)
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"Assets/ASB_Work/Skills/SkillPresentation_{skill}.asset");
        Assert.That(data, Is.Not.Null);
        int hitEffectId = new SerializedObject(data).FindProperty("HitEffectId").intValue;
        Assert.That(hitEffectId, Is.EqualTo(400019));

        var registry = AssetDatabase.LoadAssetAtPath<ScriptableObject>(MergedRegistryPath);
        var prefab = (GameObject)registry.GetType().GetMethod("Get").Invoke(registry, new object[] { hitEffectId });
        Assert.That(prefab, Is.Not.Null, "병합 레지스트리에 피격 이펙트가 없다.");
    }

    private static string ReadMode(Component effect)
    {
        SerializedProperty mode = new SerializedObject(effect).FindProperty("mode");
        return mode.enumNames[mode.enumValueIndex];
    }

    private static Transform FindChild(string prefabPath, string childName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, $"프리팹을 불러오지 못했습니다: {prefabPath}");
        Transform found = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == childName);
        Assert.That(found, Is.Not.Null, $"{childName}이 없습니다: {prefabPath}");
        return found;
    }

    private static Component LoadEffect(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, $"프리팹을 불러오지 못했습니다: {prefabPath}");
        Component effect = prefab.GetComponent(FindRuntimeType("VisualEffectGraphEffect"));
        Assert.That(effect, Is.Not.Null, $"VisualEffectGraphEffect가 없습니다: {prefabPath}");
        return effect;
    }

    private static void AssertSingleSkillEffectBehaviour(GameObject root, string prefabPath)
    {
        // 큐 시스템은 GetComponent<ISkillEffectBehaviour>()로 하나만 부르므로 재생 컴포넌트는 하나여야 한다.
        Type behaviourType = FindRuntimeType("ISkillEffectBehaviour");
        Assert.That(root.GetComponents(behaviourType).Length, Is.EqualTo(1), $"재생 컴포넌트가 하나가 아닙니다: {prefabPath}");
    }

    private static T Invoke<T>(string methodName, params object[] args)
    {
        MethodInfo method = FindRuntimeType("VisualEffectGraphEffect").GetMethod(methodName, AnyStatic);
        Assert.That(method, Is.Not.Null, $"메서드를 찾지 못했습니다: {methodName}");
        return (T)method.Invoke(null, args);
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
