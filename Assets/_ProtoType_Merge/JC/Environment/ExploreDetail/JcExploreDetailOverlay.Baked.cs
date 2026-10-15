using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace JC.EnvironmentDetail
{
    public sealed partial class JcExploreDetailOverlay
    {
        // 과거 씬 직렬화 호환용. 현재는 실행/편집 모두 확정 외형 유지 정책입니다.
        [SerializeField, HideInInspector]
        bool forceBakedOnMismatch;
        public bool ForceBakedOnMismatch { get => forceBakedOnMismatch; set => forceBakedOnMismatch = value; }
        public bool IsPreview { get; private set; }
        public GameObject VisualRoot => output;
        public JcExploreDetailBake SelectedBake => selectedBake;
        public bool KeepBakeOnMismatch => true;
        JcExploreDetailBake selectedBake;
        JcExploreDetailBake appliedBake;
        GameObject appliedPrefab;
        bool initialized, runtimeEnabled, applyQueued;
        string lastWarning;
        // Editor 전용 에셋 의존성 검증을 연결합니다. Player에는 파일 검색을 넣지 않습니다.
        public static Func<JcExploreDetailBake, string> EditorDependencyCheck;

        public static JcExploreDetailOverlay Ensure(Scene scene)
        {
            var p = Resources.Load<JcExploreDetailProfile>("JC_ExploreDetail_Default");
            if (p == null || !p.sceneNames.Contains(scene.name)) return null;
            var world = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "JC_WorldDressing");
            if (world == null) return null;
            var existing = world.GetComponentInChildren<JcExploreDetailOverlay>(true);
            if (existing != null) return existing;
            var go = new GameObject("JC_ExploreDetail") { hideFlags = HideFlags.DontSave };
            SceneManager.MoveGameObjectToScene(go, scene); go.transform.SetParent(world.transform, false);
            var component = go.AddComponent<JcExploreDetailOverlay>(); component.profile = p;
            component.forceBakedOnMismatch = p.forceBakedOnMismatch;
            return component;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            EditorDependencyCheck = null;
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
            LevelZoneLayoutLoader.RuntimeLayoutLoaded -= LayoutLoaded; LevelZoneLayoutLoader.RuntimeLayoutLoaded += LayoutLoaded;
            LevelLoader.RuntimeLevelLoaded -= LevelLoaded; LevelLoader.RuntimeLevelLoaded += LevelLoaded;
        }
        static void LayoutLoaded(LevelZoneLayoutLoader loader) => MapReady(loader.gameObject.scene);
        static void LevelLoaded(LevelLoader loader) => MapReady(loader.gameObject.scene);
        static void MapReady(Scene scene)
        {
            var overlay = Ensure(scene);
            if (overlay == null || !overlay.isActiveAndEnabled) return;
            overlay.QueueApply();
        }
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            var overlay = Ensure(scene);
            if (overlay != null && overlay.isActiveAndEnabled) overlay.QueueApply();
        }
        void QueueApply()
        { if(applyQueued) return; applyQueued=true; StartCoroutine(ApplyAfterStart()); }
        IEnumerator ApplyAfterStart()
        {
            // DH ClearTileMeshes는 Destroy 지연 제거입니다. 완료 이벤트 직후에는 구/신 청크가 공존하므로 다음 프레임에 한 번 적용합니다.
            yield return null;
            applyQueued=false;
            Refresh();
        }
        void OnEnable()
        {
            if (profile == null) return;
            if (Application.isPlaying)
            { if (initialized) Refresh(); else QueueApply(); }
#if UNITY_EDITOR
            else UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled && !Application.isPlaying && !IsPreview) Refresh(); };
#endif
        }
        void OnDisable() { StopAllCoroutines(); applyQueued=false; Restore(); IsPreview = false; }
        void OnDestroy() => Restore();

        // 기존 호출부 호환용입니다. force도 재생성을 의미하지 않습니다.
        public void Refresh(bool force = false)
        {
            if (!initialized || !Application.isPlaying)
            {
                selectedBake = JcExploreDetailBakeCatalog.Load()?.Find(gameObject.scene.path);
                runtimeEnabled = profile != null && profile.enableDetails;
            }
            initialized = true;
            waterController=JcWaterController.Ensure(this);
            if (!isActiveAndEnabled || !runtimeEnabled) { Restore(); IsPreview=false; status = "OFF: DH 원본 외형"; return; }
            if (selectedBake == null || selectedBake.prefab == null)
            { Warn("확정 베이크가 없습니다. " + (output!=null?"기존 표시를 유지합니다.":"표시 가능한 확정본이 없어 DH 원본을 표시합니다.")); return; }
            string mismatch;
            try
            {
                mismatch = selectedBake.Compare(JcExploreDetailBake.CaptureInputs(gameObject.scene));
                if (mismatch == null && EditorDependencyCheck != null) mismatch = EditorDependencyCheck(selectedBake);
                if (mismatch == null && !Application.isEditor)
                    mismatch = JcExploreDetailBakeCatalog.Load()?.scenes.FirstOrDefault(x => x.scenePath == gameObject.scene.path)?.buildInputWarning;
                if (string.IsNullOrEmpty(mismatch)) mismatch = null;
            }
            catch (Exception e) { mismatch = "입력 구조 확인 실패: " + e.Message; }
            if (mismatch != null)
            {
                Warn(mismatch + " 마지막 확정 베이크 유지 중 (재베이크 필요).");
            }
            GameObject candidate=null;
            try
            {
                var key=JcExploreDetailBake.CreateKeyResolver(gameObject.scene);
                var sources = JcExploreDetailBake.Sources(gameObject.scene).ToDictionary(f => key(f.transform));
                var replaced=selectedBake.inputs.Where(x=>x.replace && sources.ContainsKey(x.key))
                    .Select(x=>sources[x.key].GetComponent<MeshRenderer>()).Distinct().ToArray();
                if(output==null || appliedBake!=selectedBake || appliedPrefab!=selectedBake.prefab || IsPreview)
                {
                    // 준비 실패 시 기존 표시와 물 재질을 건드리지 않습니다.
                    candidate=Instantiate(selectedBake.prefab);
                    candidate.SetActive(false);
                    candidate.name="JC_ExploreDetail_Visuals";candidate.hideFlags=HideFlags.DontSave;
                    candidate.transform.SetParent(transform,true);
                    if(candidate.GetComponentsInChildren<MeshFilter>(true).Any(f=>f.sharedMesh==null) ||
                       candidate.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.sharedMaterials.Any(m=>m==null || m.shader==null)))
                        throw new InvalidOperationException("확정본의 메시 또는 재질이 누락되었습니다.");
                    waterController.Bind(candidate); // 새 재질 준비 실패 시 이전 연결을 유지합니다.
                    ReleaseVisual(false);
                    output=candidate;candidate=null;appliedBake=selectedBake;appliedPrefab=selectedBake.prefab;IsPreview=false;
                    output.SetActive(true);
                }
                // 같은 확정본의 중복 로드 알림에서는 메시/물 재질을 재생성하지 않습니다.
                foreach(var r in originals.Keys.Where(r=>r==null || !replaced.Contains(r)).ToArray())
                { if(r!=null)r.forceRenderingOff=originals[r];originals.Remove(r); }
                foreach(var r in replaced)
                { if(!originals.ContainsKey(r))originals.Add(r,r.forceRenderingOff);r.forceRenderingOff=true; }
                if (mismatch == null) { lastWarning = null; status = "BAKED: " + selectedBake.createdUtc + " / " + selectedBake.report; }
            }
            catch (Exception e)
            {
                if(candidate!=null){candidate.SetActive(false);Dispose(candidate);}
                Warn("베이크 적용 실패: " + e.Message + (output!=null?" / 기존 표시와 물 연결 유지":" / 적용 가능한 외형이 없어 DH 원본 표시"));
            }
        }
        void Warn(string reason)
        {
            status = reason;
            if (lastWarning == reason) return;
            lastWarning = reason; Debug.LogWarning("[JC World Dressing] " + reason, this);
        }
        public void ShowOriginal() { Restore(); IsPreview = false; status = "DH 원본 표시 (수동)"; }
        // 편집기 변경 감지는 표시 객체를 제거하지 않고 상태만 알립니다.
        public void ReportEditorInputChange(string reason)
        {
            if (Application.isPlaying || output == null || IsPreview || selectedBake==null || appliedBake!=selectedBake || appliedPrefab!=selectedBake.prefab) return;
            status = reason == null ? "BAKED: " + selectedBake.createdUtc + " / " + selectedBake.report :
                reason + " 마지막 확정 베이크 유지 중 (재베이크 필요).";
        }

#if UNITY_EDITOR
        public JcExploreDetailBake.Input[] PreviewInputs { get; private set; }
        public string PreviewSettings { get; private set; }
        public JcExploreDetailBake.Dependency[] PreviewDependencies { get; set; }
        public void GeneratePreview(string settingsSnapshot)
        {
            if (Application.isPlaying) throw new InvalidOperationException("재생성은 편집 모드에서만 가능합니다.");
            Restore(); IsPreview = false;
            if (profile == null || !profile.enableDetails) throw new InvalidOperationException("Enable Details를 켜 주세요.");
            var sources = JcExploreDetailBake.Sources(gameObject.scene);
            if (sources.Length == 0) throw new InvalidOperationException("DH 지형이 먼저 생성되어 있어야 합니다.");
            try
            {
                PreviewInputs = JcExploreDetailBake.CaptureInputs(gameObject.scene);
                PreviewSettings = settingsSnapshot;
                Build(sources);
                var key=JcExploreDetailBake.CreateKeyResolver(gameObject.scene);
                var replaced = new System.Collections.Generic.HashSet<string>(originals.Keys.Select(r => key(r.transform)));
                foreach (var input in PreviewInputs) input.replace = replaced.Contains(input.key);
                BindWater();
                IsPreview = true; status = "PREVIEW (미확정): " + status;
            }
            catch { Refresh(); throw; }
        }
#endif
    }
}
