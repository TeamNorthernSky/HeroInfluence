using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace JC.EnvironmentDetail
{
    public sealed partial class JcExploreDetailOverlay
    {
        [SerializeField, Tooltip("원본과 불일치해도 마지막 확정 베이크를 표시합니다. 새 맵과 겹칠 수 있습니다. 자동 재생성하지 않습니다.")]
        bool forceBakedOnMismatch;
        public bool ForceBakedOnMismatch { get => forceBakedOnMismatch; set => forceBakedOnMismatch = value; }
        public bool IsPreview { get; private set; }
        public GameObject VisualRoot => output;
        public JcExploreDetailBake SelectedBake => selectedBake;
        JcExploreDetailBake selectedBake;
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
            IsPreview = false;
            if (!initialized || !Application.isPlaying)
            {
                selectedBake = JcExploreDetailBakeCatalog.Load()?.Find(gameObject.scene.path);
                runtimeEnabled = profile != null && profile.enableDetails;
            }
            initialized = true;
            Restore();
            if (!isActiveAndEnabled || !runtimeEnabled) { status = "OFF: DH 원본 외형"; return; }
            if (selectedBake == null || selectedBake.prefab == null)
            { Warn("확정 베이크가 없습니다. DH 원본을 표시합니다."); return; }
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
                Warn(mismatch + (forceBakedOnMismatch ? " 강제 베이크 표시 중." : " DH 원본을 표시합니다."));
                if (!forceBakedOnMismatch) return;
            }
            try
            {
                // 배치 좌표는 생성 당시 월드 기준. 관리 루트의 TRS가 외형에 추가 적용되지 않게 합니다.
                output = Instantiate(selectedBake.prefab);
                output.name = "JC_ExploreDetail_Visuals"; output.hideFlags = HideFlags.DontSave;
                output.transform.SetParent(transform, true);
                var key=JcExploreDetailBake.CreateKeyResolver(gameObject.scene);
                var sources = JcExploreDetailBake.Sources(gameObject.scene).ToDictionary(f => key(f.transform));
                foreach (var input in selectedBake.inputs.Where(x => x.replace))
                    if (sources.TryGetValue(input.key, out var source))
                    { var r = source.GetComponent<MeshRenderer>(); originals[r] = r.forceRenderingOff; r.forceRenderingOff = true; }
                if (mismatch == null) { lastWarning = null; status = "BAKED: " + selectedBake.createdUtc + " / " + selectedBake.report; }
            }
            catch (Exception e) { Restore(); Warn("베이크 적용 실패: " + e.Message + " / DH 원본 복원"); }
        }
        void Warn(string reason)
        {
            status = reason;
            if (lastWarning == reason) return;
            lastWarning = reason; Debug.LogWarning("[JC World Dressing] " + reason, this);
        }
        public void ShowOriginal() { Restore(); IsPreview = false; status = "DH 원본 표시 (수동)"; }

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
                IsPreview = true; status = "PREVIEW (미확정): " + status;
            }
            catch { Refresh(); throw; }
        }
#endif
    }
}
