using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>율리아 보스전 Ultra Mega 연출(Y02 피격 점멸, Y08·Y09·Y10 굴렁쇠, A03 증폭기 파괴) 계약.</summary>
public sealed class YuliaBattleVfxTests
{
    private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const string HoopPresetPath = "Assets/ASB_Work/Effect/Hoop/HoopVfxPreset.asset";
    private const string AmplifierModelPath = "Assets/Resources/prefab/CommonPrefab/EnemyUnit/CommonModel_VillanAmplifier.prefab";
    private const string MadonnaUnitPath = "Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanMadonna_40001.prefab";

    [Test]
    public void AmplifierFadeScale_ShrinksSmoothlyToZero()
    {
        MethodInfo fade = FindRuntimeType("AmplifierOrbHealthVisual").GetMethod("FadeScale", AnyStatic);
        Assert.That((float)fade.Invoke(null, new object[] { 0f }), Is.EqualTo(1f).Within(0.0001f));
        Assert.That((float)fade.Invoke(null, new object[] { 0.5f }), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That((float)fade.Invoke(null, new object[] { 1f }), Is.EqualTo(0f).Within(0.0001f));
        Assert.That((float)fade.Invoke(null, new object[] { 2f }), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void HitFlash_OnlyWhenHpActuallyDrops()
    {
        MethodInfo should = FindRuntimeType("UnitHitFlashVfx").GetMethod("ShouldFlash", AnyStatic);
        Assert.That((bool)should.Invoke(null, new object[] { 500f, 480f }), Is.True);
        Assert.That((bool)should.Invoke(null, new object[] { 480f, 500f }), Is.False, "회복은 점멸하지 않는다.");
        Assert.That((bool)should.Invoke(null, new object[] { 0f, 500f }), Is.False, "초기화·부활은 점멸하지 않는다.");
        Assert.That((bool)should.Invoke(null, new object[] { -1f, 0f }), Is.False, "이전 값을 모르면 점멸하지 않는다.");
        Assert.That((bool)should.Invoke(null, new object[] { 500f, 500f }), Is.False);
    }

    [Test]
    public void DashArcTail_LagsBehindProgress()
    {
        MethodInfo tail = FindRuntimeType("HoopDashPresentationSequence").GetMethod("ResolveArcTail", AnyStatic);
        Vector3 home = Vector3.zero, end = new Vector3(10f, 0f, 0f);
        Assert.That((Vector3)tail.Invoke(null, new object[] { home, end, 0.2f, 0.35f }), Is.EqualTo(home), "시작 직후에는 출발점에 남는다.");
        Assert.That(((Vector3)tail.Invoke(null, new object[] { home, end, 1f, 0.35f })).x, Is.EqualTo(6.5f).Within(0.001f));
    }

    [TestCase("wheelVisualPrefab", "Persistent")]
    [TestCase("wheelBreakPrefab", "OneShot")]
    [TestCase("dashArcPrefab", "Loop")]
    public void HoopPreset_UsesUltraMegaPrefab(string field, string expectedMode)
    {
        var preset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(HoopPresetPath);
        Assert.That(preset, Is.Not.Null);
        var prefab = (GameObject)preset.GetType().GetField(field).GetValue(preset);
        Assert.That(prefab, Is.Not.Null, $"{field}가 비어 있으면 코드로 그린 임시 외형으로 돌아간다.");
        Assert.That(ReadMode(prefab), Is.EqualTo(expectedMode));
        if (field == "dashArcPrefab")
            Assert.That(prefab.GetComponent(FindRuntimeType("ElectricArcEndpoints")), Is.Not.Null, "돌진 아크는 끝점 컴포넌트가 필요하다.");
    }

    [Test]
    public void HoopOrb_CenterHeightClearsFloor()
    {
        var preset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(HoopPresetPath);
        Assert.That(preset, Is.Not.Null);
        float radius = (float)preset.GetType().GetField("wheelVisualRadius").GetValue(preset);
        float hover = (float)preset.GetType().GetField("hoverHeight").GetValue(preset);
        Assert.That(radius, Is.EqualTo(3.3f * 3f / 5f / 2f).Within(0.01f), "지름 = 그리드 한 칸(3.3m)의 3/5.");

        MethodInfo resolve = FindRuntimeType("HoopWheelVisual").GetMethod("ResolveWheelHeight", AnyStatic);
        float height = (float)resolve.Invoke(null, new object[] { preset });
        Assert.That(height - hover - radius, Is.GreaterThanOrEqualTo(-0.0001f), "부유 최저점에서도 외형 아래쪽이 바닥 위에 있어야 한다.");
    }

    [Test]
    public void AmplifierModel_HasDeathSmoke()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(AmplifierModelPath);
        Component orbVisual = model.GetComponentInChildren(FindRuntimeType("AmplifierOrbHealthVisual"), true);
        Assert.That(orbVisual, Is.Not.Null);
        var smoke = new SerializedObject(orbVisual).FindProperty("deathSmokePrefab").objectReferenceValue as GameObject;
        Assert.That(smoke, Is.Not.Null, "사망 연기가 없으면 구체만 사라진다.");
        Assert.That(ReadMode(smoke), Is.EqualTo("OneShot"));
    }

    [Test]
    public void MadonnaUnit_HasChestHitFlash()
    {
        GameObject unit = AssetDatabase.LoadAssetAtPath<GameObject>(MadonnaUnitPath);
        Component flash = unit.GetComponent(FindRuntimeType("UnitHitFlashVfx"));
        Assert.That(flash, Is.Not.Null);
        var so = new SerializedObject(flash);
        var prefab = so.FindProperty("flashPrefab").objectReferenceValue as GameObject;
        Assert.That(prefab, Is.Not.Null);
        Assert.That(ReadMode(prefab), Is.EqualTo("OneShot"));
        SerializedProperty socket = so.FindProperty("socket");
        Assert.That(socket.enumNames[socket.enumValueIndex], Is.EqualTo("Chest"));
    }

    [Test]
    public void Stasis_QueuesOnlyOnFirstEntryToStasisPhaseWhileBossAlive()
    {
        MethodInfo should = FindRuntimeType("YuliaStasisVfx").GetMethod("ShouldQueueStasis", AnyStatic);
        Assert.That((bool)should.Invoke(null, new object[] { 3, 3, false, true }), Is.True);
        Assert.That((bool)should.Invoke(null, new object[] { 2, 3, false, true }), Is.False, "2페이즈는 연출이 없다.");
        Assert.That((bool)should.Invoke(null, new object[] { 3, 3, true, true }), Is.False, "한 번만 예약한다.");
        Assert.That((bool)should.Invoke(null, new object[] { 3, 3, false, false }), Is.False, "율리아 처치(즉시 승리)면 연출하지 않는다.");
    }

    [Test]
    public void StasisShake_FadesBackToHome()
    {
        MethodInfo offset = FindRuntimeType("YuliaStasisVfx").GetMethod("ShakeOffset", AnyStatic);
        var start = (Vector3)offset.Invoke(null, new object[] { 0f, 0.06f, new Vector2(1f, 0f) });
        var end = (Vector3)offset.Invoke(null, new object[] { 1f, 0.06f, new Vector2(1f, 0f) });
        Assert.That(start.x, Is.EqualTo(0.06f).Within(0.0001f));
        Assert.That(start.y, Is.EqualTo(0f), "수평으로만 흔든다.");
        Assert.That(end.magnitude, Is.EqualTo(0f).Within(0.0001f), "끝에는 원위치로 돌아온다.");
    }

    [Test]
    public void Stasis_SuppressesListedEffectsOnlyWhileInStasis()
    {
        MethodInfo should = FindRuntimeType("YuliaStasisVfx").GetMethod("ShouldSuppressEffect", AnyStatic);
        var charge = new GameObject("charge");
        var other = new GameObject("other");
        try
        {
            var list = new[] { charge };
            Assert.That((bool)should.Invoke(null, new object[] { true, charge, list }), Is.True);
            Assert.That((bool)should.Invoke(null, new object[] { false, charge, list }), Is.False, "감금 전에는 손 충전이 그대로 나온다.");
            Assert.That((bool)should.Invoke(null, new object[] { true, other, list }), Is.False, "목록에 없는 이펙트는 막지 않는다.");
            Assert.That((bool)should.Invoke(null, new object[] { true, charge, null }), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(charge);
            UnityEngine.Object.DestroyImmediate(other);
        }
    }

    [Test]
    public void MadonnaUnit_HasStasisWithShieldAndStasisToggles()
    {
        GameObject unit = AssetDatabase.LoadAssetAtPath<GameObject>(MadonnaUnitPath);
        Component stasis = unit.GetComponent(FindRuntimeType("YuliaStasisVfx"));
        Assert.That(stasis, Is.Not.Null);
        var so = new SerializedObject(stasis);
        Assert.That(so.FindProperty("stasisPhase").intValue, Is.EqualTo(3));
        var shield = so.FindProperty("shieldPrefab").objectReferenceValue as GameObject;
        Assert.That(shield, Is.Not.Null);
        // 막은 수명이 있는 VFX(5초)가 아니라 계속 유지되는 메시여야 한다.
        Assert.That(shield.GetComponent<MeshRenderer>(), Is.Not.Null, "막은 감금 동안 계속 유지되는 메시다.");
        Assert.That(shield.GetComponentInChildren<Collider>(true), Is.Null, "데모 충돌체가 남으면 전투 클릭·대상 선택을 가린다.");
        Assert.That(shield.GetComponentInChildren<Rigidbody>(true), Is.Null);

        SerializedProperty hide = so.FindProperty("hideWhileStasis");
        Assert.That(hide.arraySize, Is.GreaterThan(0));
        Assert.That(hide.GetArrayElementAtIndex(0).objectReferenceValue.name, Is.EqualTo("FX_YuliaIdleHandOrb"), "감금 중 손 구체를 끈다.");
        SerializedProperty disable = so.FindProperty("disableWhileStasis");
        Assert.That(disable.arraySize, Is.GreaterThan(0));
        Assert.That(disable.GetArrayElementAtIndex(0).objectReferenceValue.GetType().Name, Is.EqualTo("UnitHitFlashVfx"), "감금 중 피격 점멸 대신 흔들림.");
        SerializedProperty suppress = so.FindProperty("suppressEffectsWhileStasis");
        Assert.That(suppress.arraySize, Is.GreaterThan(0));
        Assert.That(suppress.GetArrayElementAtIndex(0).objectReferenceValue.name, Is.EqualTo("FX_400010_YuliaCastCharge"), "감금 중 손 충전을 스폰하지 않는다.");
    }

    private static string ReadMode(GameObject prefab)
    {
        Component effect = prefab.GetComponent(FindRuntimeType("VisualEffectGraphEffect"));
        Assert.That(effect, Is.Not.Null, $"{prefab.name}에 VisualEffectGraphEffect가 없다.");
        SerializedProperty mode = new SerializedObject(effect).FindProperty("mode");
        return mode.enumNames[mode.enumValueIndex];
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
