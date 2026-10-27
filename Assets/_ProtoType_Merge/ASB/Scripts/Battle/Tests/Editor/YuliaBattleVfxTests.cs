using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>율리아 보스전 Ultra Mega 연출(Y01 손 구체, Y02 피격 점멸, Y05·Y06 줄 번개, Y07~Y10 굴렁쇠, Y11 정지장, A03·A04 증폭기) 계약.</summary>
public sealed class YuliaBattleVfxTests
{
    private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const string HoopPresetPath = "Assets/RenderFX/EnemySkill/Yulia/Presets/Hoop/HoopVfxPreset.asset";
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
        var orb = (GameObject)preset.GetType().GetField("wheelVisualPrefab").GetValue(preset);
        // 보이는 외형은 켜진 MagicOrb08 하나다(율리아 연출 시뮬레이션: 월드 배율 = 율리아 키 ÷ 5.3, 중심 = 키의 55%).
        const float yuliaHeight = 2.061252f;
        var effect = orb.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true).FirstOrDefault(v => v.enabled);
        Assert.That(effect, Is.Not.Null);
        Assert.That(effect.transform.lossyScale.x, Is.EqualTo(yuliaHeight / 5.3f).Within(.005f), "구체 크기는 시뮬레이션과 같아야 한다.");

        MethodInfo resolve = FindRuntimeType("HoopWheelVisual").GetMethod("ResolveWheelHeight", AnyStatic);
        float height = (float)resolve.Invoke(null, new object[] { preset });
        Assert.That(height, Is.EqualTo(yuliaHeight * .55f).Within(.01f), "중심 높이는 시뮬레이션과 같아야 한다.");
        Assert.That(height - hover - radius, Is.GreaterThanOrEqualTo(.19f), "부유 최저점에서도 외형 아래쪽과 바닥 사이에 간격이 있어야 한다.");
        Renderer body = orb.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Dense_Purple_Inner_Body");
        Assert.That(body, Is.Not.Null, "안쪽 보라 구체가 없으면 배경이 비쳐 색이 연해진다.");
        Assert.That(height - hover - body.transform.lossyScale.x * .5f, Is.GreaterThan(0f), "안쪽 구체도 바닥에 묻히지 않아야 한다.");
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
    public void Stasis_EntersOnSpawnOnlyInEncounterSceneWhileBossAlive()
    {
        MethodInfo should = FindRuntimeType("YuliaStasisVfx").GetMethod("ShouldEnterStasisOnSpawn", AnyStatic);
        Assert.That((bool)should.Invoke(null, new object[] { true, false, true }), Is.True, "페이즈 없이 등장부터 감금한다.");
        Assert.That((bool)should.Invoke(null, new object[] { false, false, true }), Is.False, "블랙보드가 없는 씬(스킬 프리뷰 등)은 감금하지 않는다.");
        Assert.That((bool)should.Invoke(null, new object[] { true, true, true }), Is.False, "한 번만 들어간다.");
        Assert.That((bool)should.Invoke(null, new object[] { true, false, false }), Is.False, "율리아가 죽었으면 연출하지 않는다.");
    }

    [Test]
    public void StasisShield_ShownOnlyWhileAnyAmplifierAlive()
    {
        MethodInfo should = FindRuntimeType("YuliaStasisVfx").GetMethod("ShouldShowShield", AnyStatic);
        Assert.That((bool)should.Invoke(null, new object[] { 2 }), Is.True);
        Assert.That((bool)should.Invoke(null, new object[] { 1 }), Is.True, "하나라도 살아 있으면 막을 유지한다.");
        Assert.That((bool)should.Invoke(null, new object[] { 0 }), Is.False, "전부 쓰러져 부활 대기 중이면 막을 없앤다.");
    }

    [Test]
    public void MadonnaUnit_HasStasisShieldOnly()
    {
        GameObject unit = AssetDatabase.LoadAssetAtPath<GameObject>(MadonnaUnitPath);
        Type stasisType = FindRuntimeType("YuliaStasisVfx");
        Component stasis = unit.GetComponent(stasisType);
        Assert.That(stasis, Is.Not.Null);
        var so = new SerializedObject(stasis);
        var shield = so.FindProperty("shieldPrefab").objectReferenceValue as GameObject;
        Assert.That(shield, Is.Not.Null);
        // 막은 수명이 있는 VFX(5초)가 아니라 계속 유지되는 메시여야 한다.
        Assert.That(shield.GetComponent<MeshRenderer>(), Is.Not.Null, "막은 감금 동안 계속 유지되는 메시다.");
        Assert.That(shield.GetComponentInChildren<Collider>(true), Is.Null, "데모 충돌체가 남으면 전투 클릭·대상 선택을 가린다.");
        Assert.That(shield.GetComponentInChildren<Rigidbody>(true), Is.Null);

        // [2026-10-23 변경] 감금은 막만 띄운다. 정지 포즈·손 구체 숨김·피격 점멸 끄기·손 충전 억제·흔들림 없이 1·2페이즈처럼 나온다.
        foreach (string removed in new[] { "hitStateName", "hideWhileStasis", "disableWhileStasis", "suppressEffectsWhileStasis", "shakeSeconds" })
            Assert.That(so.FindProperty(removed), Is.Null, $"{removed}: 감금 중에도 애니메이션·이펙트는 평소대로 나온다.");
        Assert.That(stasisType.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), Is.Null,
            "정지 포즈로 되돌리는 LateUpdate가 없어야 한다.");
        Assert.That(FindRuntimeType("IPresentationEffectSpawnFilter").IsAssignableFrom(stasisType), Is.False, "손 충전(FX_400010)을 막지 않는다.");

        Transform orb = unit.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "FX_YuliaIdleHandOrb");
        Assert.That(orb, Is.Not.Null);
        Assert.That(orb.gameObject.activeSelf, Is.True, "감금 코드가 끄지 않으므로 프리팹 기본값이 켜져 있어야 손 구체가 보인다.");
        var flash = (Behaviour)unit.GetComponent(FindRuntimeType("UnitHitFlashVfx"));
        Assert.That(flash.enabled, Is.True, "프리팹 기본값이 켜져 있어야 피격 점멸이 나온다.");
    }

    // ----- 2026-10-19 미적용 항목 보완(Y01·Y05·Y06·Y07·Y10·Y11·A04) -----

    private const string WheelUnitPath = "Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanWheel_40005.prefab";
    private const string RowLightningPath = "Assets/RenderFX/EnemySkill/Yulia/Boss/FX_Yulia_400011_RowLightning.prefab";
    private const string RowBeamPath = "Assets/RenderFX/EnemySkill/Yulia/Boss/FX_Yulia_400012_RowBeam.prefab";

    [Test]
    public void Stasis_ShieldShrinksAwayOnDeath()
    {
        MethodInfo scale = FindRuntimeType("YuliaStasisVfx").GetMethod("DismissScale", AnyStatic);
        Assert.That((float)scale.Invoke(null, new object[] { 0f }), Is.EqualTo(1f).Within(0.0001f));
        Assert.That((float)scale.Invoke(null, new object[] { 1f }), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void HandOrb_ShrinksForChargeThenHit()
    {
        MethodInfo resolve = FindRuntimeType("YuliaHandOrbState").GetMethod("ResolveTargetScale", AnyStatic);
        Assert.That((float)resolve.Invoke(null, new object[] { true, true, 0f, 0.45f }), Is.EqualTo(0f), "충전이 피격보다 우선한다.");
        Assert.That((float)resolve.Invoke(null, new object[] { false, true, 0f, 0.45f }), Is.EqualTo(0.45f));
        Assert.That((float)resolve.Invoke(null, new object[] { false, false, 0f, 0.45f }), Is.EqualTo(1f), "평소에는 원래 크기.");
    }

    [Test]
    public void MadonnaIdleHandOrb_HasStateComponent()
    {
        Transform orb = AssetDatabase.LoadAssetAtPath<GameObject>(MadonnaUnitPath).GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "FX_YuliaIdleHandOrb");
        Assert.That(orb, Is.Not.Null);
        Assert.That(orb.GetComponent(FindRuntimeType("YuliaHandOrbState")), Is.Not.Null, "시전·피격 때 손 구체를 줄인다.");
    }

    [Test]
    public void RowArc_DrawsEachLineInTurn_AndSpreadsAroundCenter()
    {
        Type row = FindRuntimeType("RowLightningCueEffect");
        MethodInfo progress = row.GetMethod("ArcDrawProgress", AnyStatic);
        Assert.That((float)progress.Invoke(null, new object[] { 0.04f, 0, 0.05f, 0.08f }), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That((float)progress.Invoke(null, new object[] { 0.04f, 1, 0.05f, 0.08f }), Is.EqualTo(0f), "두 번째 줄은 아직 시작 전.");
        Assert.That((float)progress.Invoke(null, new object[] { 1f, 2, 0.05f, 0.08f }), Is.EqualTo(1f));

        MethodInfo offset = row.GetMethod("LineOffset", AnyStatic);
        Assert.That(((Vector3)offset.Invoke(null, new object[] { Vector3.right, 0, 3, 0.3f })).x, Is.EqualTo(-0.3f).Within(0.0001f));
        Assert.That(((Vector3)offset.Invoke(null, new object[] { Vector3.right, 1, 3, 0.3f })).x, Is.EqualTo(0f).Within(0.0001f), "가운데 줄은 중심선.");
        Assert.That(((Vector3)offset.Invoke(null, new object[] { Vector3.right, 0, 1, 0.3f })).magnitude, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void RowBeam_KeepsOnlyTargetSideCells()
    {
        MethodInfo same = FindRuntimeType("RowLightningCueEffect").GetMethod("IsSameSide", AnyStatic);
        Assert.That((bool)same.Invoke(null, new object[] { 0, 1 }), Is.True, "아군 뒷줄·앞줄.");
        Assert.That((bool)same.Invoke(null, new object[] { 2, 3 }), Is.True);
        Assert.That((bool)same.Invoke(null, new object[] { 1, 2 }), Is.False, "아군 앞줄과 적 앞줄은 다른 진영.");

        var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(RowBeamPath).GetComponent(FindRuntimeType("RowLightningCueEffect")));
        Assert.That(so.FindProperty("_targetSideOnly").boolValue, Is.True, "기획 Y06: 대상 진영 전열에서 라인 끝까지.");
    }

    [TestCase(RowLightningPath, false)]
    [TestCase(RowBeamPath, true)]
    public void RowLightningPrefabs_UseUltraMegaArcs(string path, bool expectOriginOrb)
    {
        Component row = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent(FindRuntimeType("RowLightningCueEffect"));
        Assert.That(row, Is.Not.Null);
        var so = new SerializedObject(row);
        var arc = so.FindProperty("_arcPrefab").objectReferenceValue as Component;
        Assert.That(arc, Is.Not.Null, "아크가 비면 예전 코드 번개로 돌아간다.");
        Assert.That(arc.GetComponent(FindRuntimeType("ElectricArcEndpoints")), Is.Not.Null);
        Assert.That(ReadMode(arc.gameObject), Is.EqualTo("Loop"), "줄을 유지하다 Stop으로 사라진다.");
        var orb = so.FindProperty("_originOrbPrefab").objectReferenceValue as Component;
        Assert.That(orb != null, Is.EqualTo(expectOriginOrb), "전열 구체는 공멸의 궤적(Y06)에만.");
        bool layerBolt = so.FindProperty("_layerBoltWithArcs").boolValue;
        Assert.That(layerBolt, Is.EqualTo(!expectOriginOrb), "나락의 폭풍(Y05)에만 볼트 코어와 보라색 외곽 광을 겹친다.");
        float arcScale = so.FindProperty("_arcThicknessScale").floatValue;
        Assert.That(arcScale, Is.EqualTo(expectOriginOrb ? 0f : 0.25f).Within(0.0001f),
            "400011만 공유 아크 프리팹의 굵기를 1/4로 줄인다.");
        if (layerBolt)
        {
            Assert.That(so.FindProperty("_boltTemplate").objectReferenceValue, Is.Not.Null);
            Assert.That(so.FindProperty("_widthRatio").floatValue, Is.EqualTo(0.14f).Within(0.0001f));
            Color glow = so.FindProperty("_boltGlowColor").colorValue;
            Assert.That(glow.b, Is.GreaterThan(glow.g * 3f), "외곽 광은 보라색 채도가 드러나야 한다.");
        }
    }

    [Test]
    public void AmplifierRevive_KeepsBottomAnchored()
    {
        Type type = FindRuntimeType("AmplifierOrbHealthVisual");
        MethodInfo drop = type.GetMethod("RevealDrop", AnyStatic);
        float radius = 0.125f;
        foreach (float p in new[] { 0f, 0.3f, 1f })
        {
            float center = -(float)drop.Invoke(null, new object[] { radius, p });
            float bottom = center - radius * p;
            Assert.That(bottom, Is.EqualTo(-radius).Within(0.0001f), $"진행도 {p}에서도 아래 끝은 그대로.");
        }

        MethodInfo should = type.GetMethod("ShouldAnimateRevive", AnyStatic);
        Assert.That((bool)should.Invoke(null, new object[] { true, true, 0.6f }), Is.True);
        Assert.That((bool)should.Invoke(null, new object[] { true, false, 0.6f }), Is.False, "전투 시작(첫 활성화)은 연출하지 않는다.");
        Assert.That((bool)should.Invoke(null, new object[] { false, true, 0.6f }), Is.False, "살아 있을 때의 체력 변화는 연출하지 않는다.");
    }

    [Test]
    public void AmplifierModel_ReviveFlashOnlyOnRevive()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(AmplifierModelPath);
        Component orbVisual = model.GetComponentInChildren(FindRuntimeType("AmplifierOrbHealthVisual"), true);
        var so = new SerializedObject(orbVisual);
        var flash = so.FindProperty("reviveFlash").objectReferenceValue as Component;
        Assert.That(flash, Is.Not.Null);
        Assert.That(flash.gameObject.activeSelf, Is.False, "켜 두면 전투 시작에 구체가 켜질 때도 번쩍인다.");
        Assert.That(so.FindProperty("idleElectric").objectReferenceValue, Is.Not.Null, "다 차오른 뒤 대기 전기를 켠다.");
    }

    [Test]
    public void HoopSummon_UsesPortalSlotInsteadOfUnitChild()
    {
        var preset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(HoopPresetPath);
        var portal = (GameObject)preset.GetType().GetField("summonPortalPrefab").GetValue(preset);
        Assert.That(portal, Is.Not.Null, "비면 코드 소환진(보라 링)이 나온다.");
        Assert.That(ReadMode(portal), Is.EqualTo("OneShot"), "소환 뒤 링만 사라진다.");

        GameObject unit = AssetDatabase.LoadAssetAtPath<GameObject>(WheelUnitPath);
        Assert.That(unit.GetComponentsInChildren<Transform>(true).Any(t => t.name == portal.name), Is.False,
            "유닛 자식 포털은 소환 슬롯과 겹쳐 두 번 재생된다.");
    }

    [Test]
    public void HoopBreakFlash_BrightensColorOnly()
    {
        Type type = FindRuntimeType("HoopWheelVisual");
        var scaled = (Vector4)type.GetMethod("ScaleColor", AnyStatic).Invoke(null, new object[] { new Vector4(1f, 0.5f, 2f, 0.7f), 2f });
        Assert.That(scaled, Is.EqualTo(new Vector4(2f, 1f, 4f, 0.7f)), "알파는 유지한다.");

        MethodInfo progress = type.GetMethod("BreakFlashProgress", AnyStatic);
        Assert.That((float)progress.Invoke(null, new object[] { 0f }), Is.EqualTo(0f));
        Assert.That((float)progress.Invoke(null, new object[] { 1f }), Is.EqualTo(1f), "끝에서 최고 밝기로 연기와 바뀐다.");
    }

    [Test]
    public void HoopSummonBurst_PopsThenSettles()
    {
        MethodInfo scale = FindRuntimeType("HoopWheelVisual").GetMethod("SummonBurstScale", AnyStatic);
        float At(float t) => (float)scale.Invoke(null, new object[] { t, 0.045f, 0.115f, 1.22f });
        Assert.That(At(0f), Is.EqualTo(1f).Within(0.0001f), "나타나는 첫 프레임은 원래 크기.");
        Assert.That(At(0.045f), Is.EqualTo(1.22f).Within(0.0001f), "0.045초에 최대로 튄다.");
        Assert.That(At(0.1f), Is.InRange(1f, 1.22f));
        Assert.That(At(0.16f), Is.EqualTo(1f).Within(0.0001f), "0.16초에 원래 크기로 돌아온다.");
    }

    [TestCase("Assets/RenderFX/_Seam/Data/Legacy/Legacy_EffectRegistry.asset")]
    [TestCase("Assets/RenderFX/_Seam/Data/Merged/JC_Merged_EffectRegistry.asset")]
    public void HoopSummonCast_WiredToYuliaSummonChargeCue(string registryPath)
    {
        const int effectId = 400013;
        var presentation = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/ASB_Work/Skills/SkillPresentation_400013.asset"));
        SerializedProperty cue = presentation.FindProperty("Attack.Beats.Array.data[0].Cues.Array.data[0]");
        Assert.That(cue.FindPropertyRelative("CueName").stringValue, Is.EqualTo("yulia_cast_charge"));
        SerializedProperty ids = cue.FindPropertyRelative("EffectIds");
        Assert.That(Enumerable.Range(0, ids.arraySize).Select(i => ids.GetArrayElementAtIndex(i).intValue), Does.Contain(effectId),
            "충전 Cue에서 소환 연출이 시작되고, 충전 종료 Cue(Stop)가 칸으로 보내는 신호가 된다.");

        SerializedProperty entries = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(registryPath)).FindProperty("_entries");
        GameObject prefab = Enumerable.Range(0, entries.arraySize).Select(entries.GetArrayElementAtIndex)
            .Where(e => e.FindPropertyRelative("Id").intValue == effectId)
            .Select(e => e.FindPropertyRelative("Prefab").objectReferenceValue as GameObject)
            .FirstOrDefault();
        Assert.That(prefab, Is.Not.Null, "전투 씬은 병합 레지스트리를 읽는다. 원본과 병합본 모두 있어야 재생성 후에도 유지된다.");

        Component effect = prefab.GetComponent(FindRuntimeType("HoopSummonCastEffect"));
        Assert.That(effect, Is.Not.Null);
        var so = new SerializedObject(effect);
        foreach (string field in new[] { "orbPrefab", "ringPrefab", "gatherPrefab", "splitTrailMaterial", "preset" })
            Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
    }

    // ----- 3페이즈 진입: 쓰러진 증폭기 즉시 부활 → 율리아 정지장 -----

    [Test]
    public void Phase3_RevivesOnlyDownedCorpseParticipantsOnTargetPhase()
    {
        MethodInfo should = FindRuntimeType("EncounterParticipant").GetMethod("ShouldReviveOnPhaseEntry", AnyStatic);
        Assert.That((bool)should.Invoke(null, new object[] { 3, 3, true, true, 5 }), Is.True);
        Assert.That((bool)should.Invoke(null, new object[] { 2, 3, true, true, 5 }), Is.False, "2페이즈는 기존 카운트다운.");
        Assert.That((bool)should.Invoke(null, new object[] { 3, 3, true, false, 5 }), Is.False, "살아 있으면 부활할 게 없다.");
        Assert.That((bool)should.Invoke(null, new object[] { 3, 0, true, true, 5 }), Is.False, "0 = 사용 안 함.");
        Assert.That((bool)should.Invoke(null, new object[] { 3, 3, false, true, 5 }), Is.False, "시체 부활을 안 쓰는 참여자.");
        Assert.That((bool)should.Invoke(null, new object[] { 3, 3, true, true, -1 }), Is.False, "사망 셀을 모르면 부활할 곳이 없다.");
    }

    [TestCase("Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanAmplifier_40002.prefab")]
    [TestCase("Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanAmplifier_40003.prefab")]
    public void Amplifier_RevivesAfterCountdownAtHalfHp_WithoutPhaseEntryRevive(string path)
    {
        Type type = FindRuntimeType("EncounterParticipant");
        var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent(type));
        Assert.That(so.FindProperty("_immediateRevivePhase").intValue, Is.EqualTo(0), "율리아전 페이즈가 없어 페이즈 진입 즉시 부활은 쓰지 않는다.");
        Assert.That(so.FindProperty("_reviveHpRatio").floatValue, Is.EqualTo(0.5f).Within(0.0001f), "시체 부활은 HP 50%.");
        Assert.That(so.FindProperty("_corpseReviveDelayRounds").intValue, Is.EqualTo(2), "사망 후 2라운드 뒤 부활.");
    }

    [Test]
    public void TurnInterludes_PlayInOrderThenFifo()
    {
        var go = new GameObject("Flow");
        try
        {
            Type flowType = FindRuntimeType("BattleFlowManager");
            Component flow = go.AddComponent(flowType);
            MethodInfo enqueue = flowType.GetMethod("EnqueueTurnInterlude");
            var log = new List<string>();
            Func<System.Collections.IEnumerator> Make(string name) => () => { log.Add(name); return null; };
            enqueue.Invoke(flow, new object[] { Make("stasis"), 0 });
            enqueue.Invoke(flow, new object[] { Make("revive"), -10 });
            enqueue.Invoke(flow, new object[] { Make("other"), 0 });

            var list = (System.Collections.IList)flowType.GetField("turnInterludes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flow);
            foreach (object item in list)
                ((Func<System.Collections.IEnumerator>)item.GetType().GetField("Presentation").GetValue(item))();
            Assert.That(log, Is.EqualTo(new[] { "revive", "stasis", "other" }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
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
