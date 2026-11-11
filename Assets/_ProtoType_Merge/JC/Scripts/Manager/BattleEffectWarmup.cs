using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

/// <summary>JC | 2026-11-11 | 빌드·명시적 에디터 검증의 시각 준비입니다. 게임 로직 없이 실행 세션 동안 결과를 재사용합니다.</summary>
public sealed class BattleEffectWarmup : MonoBehaviour
{
    public static bool IsPreparing { get; private set; }

    public static IEnumerator PrepareParty()
    {
        // JC | 2026-11-11 | 일반 Play에서는 데이터 로드·스킬 수집·렌더링 전에 종료하며 호출자의 진입 완료 대기는 유지합니다.
        if (!JcPlayModePolicy.UsePlayerPreparation) yield break;
        var skills = new HashSet<int>();
        var repository = PersistentUnitRepository.Instance;
        var catalog = DHCsvTemplateCatalog.Instance;
        if (repository != null && catalog != null)
            foreach (var unit in repository.Units)
            {
                if (unit == null) continue;
                if (unit.CurrentSkillIndex > 0) skills.Add(unit.CurrentSkillIndex);
                if (int.TryParse(unit.UnitTemplateKey, out int classIndex))
                    foreach (var skill in catalog.GetAvailableSkillsByClassIndex(classIndex, unit.Level))
                        if (skill != null) skills.Add(skill.skillIndex);
            }
        if (catalog != null)
            foreach (var party in FindObjectsByType<TutorialPartyRuntime>(FindObjectsSortMode.None))
                foreach (string key in party.GetJoinedUnitTemplateKeys())
                    if (int.TryParse(key, out int classIndex))
                        foreach (var skill in catalog.GetSkillsByClassIndex(classIndex))
                            if (skill != null) skills.Add(skill.skillIndex);
        yield return GetInstance().PrepareSafely(skills);
    }

    public static IEnumerator PrepareBattle(IReadOnlyList<BattleCharactor> units)
    {
        // JC | 2026-11-11 | 전투 직접 테스트도 같은 정책을 사용합니다. 빌드에서는 항상 기존 예열 경로를 실행합니다.
        if (!JcPlayModePolicy.UsePlayerPreparation) yield break;
        var skills = new HashSet<int>();
        foreach (var unit in units)
        {
            if (unit == null) continue;
            foreach (var skill in unit.availableSkills)
                if (skill != null) skills.Add(skill.skillIndex);
            if (unit.SelectedSkillData != null) skills.Add(unit.SelectedSkillData.skillIndex);
            foreach (var skill in unit.ClassSkillSlots)
                if (skill != null) skills.Add(skill.skillIndex);
        }
        yield return GetInstance().PrepareSafely(skills);
    }

    private static BattleEffectWarmup instance;
    private BattleEffectWarmupData data;
    // Strong asset references intentionally survive scene changes and new/save-game loads.
    private readonly HashSet<GameObject> prepared = new HashSet<GameObject>();
    private readonly Dictionary<GameObject, BattleEffectWarmupData.Recipe> recipes =
        new Dictionary<GameObject, BattleEffectWarmupData.Recipe>();

    // JC | 2026-11-11 | 도메인 리로드를 끈 에디터에서도 이전 Play의 준비 상태를 새 실행으로 넘기지 않습니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        instance = null;
        IsPreparing = false;
    }

    private static BattleEffectWarmup GetInstance()
    {
        if (instance != null) return instance;
        var go = new GameObject("Battle effect warmup");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<BattleEffectWarmup>();
        instance.data = Resources.Load<BattleEffectWarmupData>("BattleEffectWarmupData");
        if (instance.data != null)
        {
            foreach (var recipe in instance.data.Recipes)
                if (recipe.Source != null) instance.recipes[recipe.Source] = recipe;
        }
        else Debug.LogError("[VFX Warmup] 사전 준비 데이터가 없습니다. Play/빌드 전 데이터 생성 오류를 확인하세요.");
        return instance;
    }

    private IEnumerator PrepareSafely(HashSet<int> skills)
    {
        var routine = Prepare(skills);
        try
        {
            while (true)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    yield break; // A visual optimization must not strand a loading screen.
                }
                if (!next) yield break;
                yield return routine.Current;
            }
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }

    private IEnumerator Prepare(HashSet<int> skills)
    {
        while (IsPreparing) yield return null;
        if (data == null) yield break;
        var pending = new HashSet<GameObject>();
        foreach (var common in data.CommonEffects) Collect(common, pending);
        var cues = new List<CueBinding>();
        foreach (int id in skills)
            foreach (var catalog in data.Catalogs)
            {
                var presentation = catalog.Get(id);
                if (presentation == null) continue;
                cues.Clear();
                presentation.CollectAllCues(cues);
                foreach (var cue in cues)
                    if (cue?.EffectIds != null)
                        foreach (int effectId in cue.EffectIds) CollectId(effectId, pending);
                if (presentation.EnableHitEffect) CollectId(presentation.HitEffectId, pending);
                if (presentation.Move != null)
                    CollectId(presentation.Move.ApproachEffectId, pending);
                Collect(presentation.ChainLightningEffectPrefab, pending);
                // 투사체는 EffectRegistry ID 없이 별도 프리팹으로 연결될 수 있습니다.
                Collect(presentation.GetProjectileVisual()?.Prefab, pending);
            }
        pending.RemoveWhere(prepared.Contains);
        if (pending.Count == 0) yield break;

        IsPreparing = true;
        GameObject rig = null;
        RenderTexture target = null;
        Camera camera = null;
        int count = 0;
        try
        {
            rig = new GameObject("VFX warmup render rig");
            rig.transform.SetParent(transform);
            // Explicit offscreen rendering only; no listener, skill scripts, physics or audio.
            rig.transform.position = new Vector3(0f, -100000f, 0f);
            var cameraObject = new GameObject("Offscreen camera");
            cameraObject.transform.SetParent(rig.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cameraType = CameraType.Reflection; // Exclude gameplay-camera callbacks (water reflections, etc.).
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = true;
            camera.orthographic = true;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 2000f;
            var additional = camera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = false;
            additional.renderShadows = false;
            additional.requiresDepthTexture = true;
            additional.requiresColorTexture = true;
            additional.volumeLayerMask = 0;
            target = new RenderTexture(64, 64, 24, RenderTextureFormat.DefaultHDR);
            target.Create();
            foreach (var source in pending)
            {
                if (RenderRecipe(recipes[source], rig.transform, camera, target))
                {
                    prepared.Add(source);
                    count++;
                }
                // Spread different effects over loading frames, not active battle frames.
                yield return null;
            }
        }
        finally
        {
            if (camera != null) camera.targetTexture = null;
            if (target != null) { target.Release(); Destroy(target); }
            if (rig != null) Destroy(rig);
            IsPreparing = false;
            Debug.Log($"[VFX Warmup] Prepared {count} new effects; {prepared.Count} retained for this process.");
        }
    }

    private void CollectId(int id, HashSet<GameObject> pending)
    {
        if (id == 0) return;
        foreach (var registry in data.Registries) Collect(registry.Get(id), pending);
    }

    private void Collect(GameObject source, HashSet<GameObject> pending)
    {
        if (source == null || !pending.Add(source)) return;
        if (!recipes.TryGetValue(source, out var recipe))
        {
            pending.Remove(source);
            Debug.LogWarning($"[VFX Warmup] No rendering recipe for {source.name}.");
            return;
        }
        foreach (var dependency in recipe.Dependencies) Collect(dependency, pending);
    }

    private static bool RenderRecipe(BattleEffectWarmupData.Recipe recipe, Transform parent,
        Camera camera, RenderTexture target)
    {
        GameObject visual = null;
        try
        {
            if (recipe.Visual == null) return false;
            visual = Instantiate(recipe.Visual, parent.position, Quaternion.identity, parent);
            foreach (var node in visual.GetComponentsInChildren<Transform>(true))
            {
                node.gameObject.layer = 31;
                if (node.localScale == Vector3.zero) node.localScale = Vector3.one;
                node.gameObject.SetActive(true);
            }
            var particles = visual.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                var main = ps.main;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var sub = ps.subEmitters; sub.enabled = false;
                ps.Simulate(.1f, false, true, false);
                if (ps.particleCount == 0) ps.Emit(1);
                ps.Pause(false);
            }
            foreach (var vfx in visual.GetComponentsInChildren<VisualEffect>(true))
            {
                vfx.Reinit();
                vfx.Play();
                vfx.Simulate(1f / 30f, 6);
            }
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(parent.position, Vector3.one * 2f);
            foreach (var renderer in renderers)
            {
                renderer.enabled = true;
                renderer.forceRenderingOff = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (renderer is LineRenderer line && line.positionCount < 2)
                {
                    line.useWorldSpace = false;
                    line.positionCount = 2;
                    line.SetPositions(new[] { Vector3.zero, Vector3.right });
                }
                if (renderer is TrailRenderer trail)
                {
                    trail.Clear();
                    trail.AddPosition(trail.transform.position);
                    trail.AddPosition(trail.transform.position + Vector3.right);
                }
                bounds.Encapsulate(renderer.bounds);
            }
            float radius = Mathf.Clamp(bounds.extents.magnitude, 1f, 400f);
            camera.transform.position = bounds.center - Vector3.forward * (radius + 10f);
            camera.orthographicSize = radius;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new NotSupportedException("The active pipeline does not support offscreen StandardRequest.");
            RenderPipeline.SubmitRenderRequest(camera, request);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[VFX Warmup] {recipe.Source.name} was not prepared: {exception.Message}");
            return false; // Never cache a failed render as ready.
        }
        finally
        {
            if (visual != null) { visual.SetActive(false); Destroy(visual); }
        }
    }
}
