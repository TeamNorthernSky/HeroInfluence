using System;
using System.Collections.Generic;
using System.Linq;
using JC.BattleTesting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BackgroundEditing
{
    public static partial class BackgroundOps
    {
        // JC | 2026-11-16 | 사용자 확정: 단일 원본만 편집하며 저장 버튼이 작업 보관·원본 적용·씬 저장을 함께 수행합니다.
        public const string OriginalCityName = "도시 원경 (베이크)";
        public const string SingleWorkspacePath = Local + "/Original/Workspace.unity";

        public static Transform FindOriginalCity(string scenePath = null)
        {
            var candidates = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .Where(s => s.isLoaded && !EditorSceneManager.IsPreviewScene(s) &&
                    s.path != Local + "/Editor/PreviewTemplate.unity" && (string.IsNullOrEmpty(scenePath) || s.path == scenePath))
                .SelectMany(s => s.GetRootGameObjects()).SelectMany(g => g.GetComponentsInChildren<JcBossCityTime>(true))
                .Where(c => c.name == OriginalCityName && !c.GetComponentInParent<BackgroundDocument>(true)).ToArray();
            if (candidates.Length != 1)
                throw new InvalidOperationException("열린 대상 씬에 '도시 원경 (베이크)' 원본이 정확히 하나 있어야 합니다. 복사본은 다른 이름으로 구분해 주세요.");
            return candidates[0].transform;
        }

        public static BackgroundDocument FindSingleDocument()
        {
            // 예전 전체 Workspace가 함께 열려 있어도 도시 작업본과 혼용하지 않습니다.
            var docs = OpenDocuments().Where(d => d.IsCityTile && d.cityName == OriginalCityName).ToArray();
            return docs.Length == 1 ? docs[0] : null;
        }

        public static BackgroundDocument OpenSingleOriginal()
        {
            Guard();
            var target = FindOriginalCity();
            var docs = OpenDocuments().Where(d => d.IsCityTile && d.cityName == OriginalCityName).ToArray();
            if (docs.Length > 1) throw new InvalidOperationException("도시 작업본이 여러 개 열려 있습니다. 보관할 작업본을 하나만 열어 주세요.");
            var document = docs.SingleOrDefault();
            if (!document && AssetDatabase.LoadAssetAtPath<SceneAsset>(SingleWorkspacePath)) document = OpenCityWorkspace(SingleWorkspacePath);
            if (!document) document = OpenCity(target);
            if (!string.IsNullOrEmpty(document.originalScenePath) && document.originalScenePath != target.gameObject.scene.path)
                throw new InvalidOperationException("저장된 도시 작업본의 원본 씬을 열어 주세요: " + document.originalScenePath);
            document.singleOriginal = true;
            document.originalScenePath = target.gameObject.scene.path;
            EditorUtility.SetDirty(document);
            SceneManager.SetActiveScene(document.gameObject.scene);
            return document;
        }

        public static void SaveOriginalCity(BackgroundDocument document)
        {
            Guard();
            if (!document || !document.IsCityTile || document.cityName != OriginalCityName)
                throw new InvalidOperationException("도시 원경 원본 편집을 먼저 열어 주세요.");
            var target = FindOriginalCity(document.originalScenePath);
            var scene = target.gameObject.scene;
            if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("원본이 있는 씬을 먼저 저장해 주세요.");
            document.singleOriginal = true; document.originalScenePath = scene.path;
            // 고정 결과 경로를 사용해 매 저장마다 별도의 결과 프리팹을 쌓거나 사용자가 적용 대상을 고르게 하지 않습니다.
            var result = ExportCity(document, document.assetFolder + "/SavedCity");
            Save(document);
            ReplaceCityContents(target, AssetDatabase.LoadAssetAtPath<GameObject>(result));
            if (!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException("원본 씬 저장 실패. 적용된 내용을 확인한 뒤 다시 저장해 주세요.");
        }

        // JC | 2026-11-16 | 표시 가능한 작업본 메시의 경계만 사용해 위에서 보기의 중심·크기를 계산합니다.
        public static Bounds EditBounds(BackgroundDocument document)
        {
            var all = document.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (all.Length == 0) throw new InvalidOperationException("화면에 표시할 도시 메시가 없습니다.");
            var bounds = all[0].bounds;
            foreach (var renderer in all.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // JC | 2026-11-16 | 씬 배치 또는 프로젝트의 완성 도시 프리팹을 편집 시작 원본으로 선택합니다.
        public static Transform SelectedCity(Transform selection)
        {
            if (!selection) return null;
            var time = selection.GetComponentInParent<JcBossCityTime>(true);
            return time ? time.transform : null;
        }

        // JC | 2026-11-16 | 작업본 자체로 다시 열며 원본 씬 오브젝트의 존속이나 이름에 의존하지 않습니다.
        public static BackgroundDocument OpenCityWorkspace(string path)
        {
            Guard();
            if (string.IsNullOrEmpty(path) || !AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
                throw new InvalidOperationException("저장한 도시 작업 씬을 선택해 주세요.");
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var documents = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BackgroundDocument>(true)).ToArray();
            if (documents.Length != 1)
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                throw new InvalidOperationException("도시 편집 작업본이 하나 있는 씬을 선택해 주세요.");
            }
            // JC | 2026-11-17 | 저장된 작업본을 다시 열 때도 가로등을 편집 가능한 배치물로 연결합니다.
            ApplyLocks(documents[0]);
            SceneManager.SetActiveScene(scene);
            return documents[0];
        }

        public static BackgroundDocument OpenCity(Transform selection)
        {
            Guard();
            var city = SelectedCity(selection);
            if (!city) throw new InvalidOperationException("도시 원경 베이크 또는 그 안의 오브젝트를 선택해 주세요.");
            // 같은 원본으로도 서로 다른 크기의 결과를 만들 수 있도록 매 작업본은 독립 폴더를 갖습니다.
            string folder = Local + "/CityWorkspaces/" + Guid.NewGuid().ToString("N");

            // JC | 2026-11-16 | 원본 메시·재질은 공유하되 배치는 별도 기준 프리팹에 보관합니다. 수정된 메시/색상만 기존 도구가 별도 생성합니다.
            Folder(folder);
            string baselinePath = folder + "/Baseline.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(baselinePath))
                throw new InvalidOperationException("이 도시의 기준 파일이 이미 있습니다. 저장하지 않은 작업본이 있는지 확인해 주세요: " + folder);
            var scratch = EditorSceneManager.NewPreviewScene();
            GameObject baseline;
            try
            {
                var source = UnityEngine.Object.Instantiate(city.gameObject);
                SceneManager.MoveGameObjectToScene(source, scratch);
                source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                source.transform.localScale = Vector3.one;
                source.SetActive(true);
                var input = new GameObject(city.name + " · 기준");
                SceneManager.MoveGameObjectToScene(input, scratch);
                var props = Child(input.transform, "건물 배치").transform;
                var terrain = Child(input.transform, "지형 결과").transform;
                var attached = new HashSet<Light>();
                foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.sharedMesh || !filter.GetComponent<MeshRenderer>()) continue;
                    var copy = Visual(filter, IsProp(filter) ? props : terrain);
                    copy.SetActive(filter.gameObject.activeInHierarchy);
                    copy.GetComponent<MeshRenderer>().enabled = filter.GetComponent<MeshRenderer>().enabled;
                    foreach (var light in filter.GetComponentsInChildren<Light>(true)) attached.Add(light);
                }
                foreach (var light in source.GetComponentsInChildren<Light>(true))
                    if (!attached.Contains(light))
                    {
                        // 독립 가로등은 지형 재생성 때 지워지지 않는 배치 그룹에 보관합니다.
                        var holder = Child(props, light.name + " 배치");
                        holder.transform.SetPositionAndRotation(light.transform.position, light.transform.rotation);
                        CopyLights(light.transform, holder.transform);
                    }
                var time = input.AddComponent<JcBossCityTime>();
                EditorUtility.CopySerialized(city.GetComponent<JcBossCityTime>(), time);
                time.worldLighting = true;
                // 임포트 시점에만 복제합니다. 이후 원본 배치나 공유 메시/재질에는 쓰지 않습니다.
                new AssetCopier(folder + "/SourceAssets", false).RemapHierarchy(input);
                baseline = PrefabUtility.SaveAsPrefabAsset(input, baselinePath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scratch); }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var work = UnityEngine.Object.Instantiate(baseline);
            SceneManager.MoveGameObjectToScene(work, scene);
            UnityEngine.Object.DestroyImmediate(work.GetComponent<JcBossCityTime>());
            work.name = city.name + " · 원경 편집";
            var result = work.AddComponent<BackgroundDocument>();
            result.baseline = baseline; result.assetFolder = folder;
            result.cityOnly = true; result.cityName = city.name;
            result.stage = JcBossBattleBackground.Load()?.visualPrefab;
            result.preserveWorld = true; result.limitArea = false; result.blueHighWindows = false;
            var renderers = work.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                result.editMin = new Vector2Int(Mathf.FloorToInt(bounds.min.x), Mathf.FloorToInt(bounds.min.z));
                result.editMax = new Vector2Int(Mathf.CeilToInt(bounds.max.x), Mathf.CeilToInt(bounds.max.z));
            }
            ApplyLocks(result);
            SceneManager.SetActiveScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            return result;
        }

        // JC | 2026-11-16 | 도시 결과는 무대 전체/공용 보스 프로필을 내보내거나 연결하지 않습니다.
        public static string ExportCity(BackgroundDocument document, string outputFolder = null)
        {
            Guard();
            if (!document || !document.baseline) throw new InvalidOperationException("도시 편집 작업본을 먼저 열어 주세요.");
            Rebuild(document);
            string folder = outputFolder ?? document.assetFolder + "/Results/" + Guid.NewGuid().ToString("N");
            Folder(folder);
            var scratch = EditorSceneManager.NewPreviewScene();
            try
            {
                var output = new GameObject(string.IsNullOrWhiteSpace(document.cityName) ? "도시 원경 (베이크)" : document.cityName);
                SceneManager.MoveGameObjectToScene(output, scratch);
                CopyCityVisuals(document, output.transform);
                var time = output.AddComponent<JcBossCityTime>();
                var savedTime = document.baseline.GetComponent<JcBossCityTime>();
                if (savedTime) EditorUtility.CopySerialized(savedTime, time);
                time.worldLighting = true;
                // 불변 배포 메시/재질은 공유하고 작업본 안의 편집 에셋만 결과에 복제해 이후 재편집과 분리합니다.
                // JC | 2026-11-17 | 전체 도시 복제 후 수천 개 에셋의 개별 임포트가 누적되지 않도록 한 번에 처리합니다.
                AssetDatabase.StartAssetEditing();
                try { new AssetCopier(folder + "/Assets", false).RemapHierarchy(output); }
                finally { AssetDatabase.StopAssetEditing(); }
                string file = folder + "/City.prefab";
                PrefabUtility.SaveAsPrefabAsset(output, file);
                return file;
            }
            finally { EditorSceneManager.ClosePreviewScene(scratch); }
        }

        // JC | 2026-11-16 | 씬 적용은 작업본의 필수 단계가 아닙니다. 사용자가 지금 지정한 도시 한 개에만 완성 결과를 적용합니다.
        public static string ApplyCity(BackgroundDocument document, Transform selection)
        {
            Guard();
            var target = SelectedCity(selection);
            if (!target || EditorUtility.IsPersistent(target) || !target.gameObject.scene.IsValid())
                throw new InvalidOperationException("적용할 씬의 도시 원경 한 개를 지정해 주세요. 결과 프리팹은 프로젝트 창에서 별도로 배치할 수도 있습니다.");
            string file = ExportCity(document);
            ReplaceCityContents(target, AssetDatabase.LoadAssetAtPath<GameObject>(file));
            return file;
        }

        // JC | 2026-11-16 | 루트와 시간대 컴포넌트는 유지하고 자식 외형만 Undo 가능한 한 작업으로 교체합니다.
        // 대상 루트의 저장 ID·위치·회전·크기와 형제 도시/실내 프리팹 참조는 바꾸지 않습니다.
        public static void ReplaceCityContents(Transform target, GameObject cityPrefab)
        {
            Guard();
            if (!target || !target.GetComponent<JcBossCityTime>() || !cityPrefab) throw new InvalidOperationException("도시 원경과 결과 프리팹이 필요합니다.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("선택 도시 원경 적용");
            GameObject output = null;
            try
            {
                output = UnityEngine.Object.Instantiate(cityPrefab);
                SceneManager.MoveGameObjectToScene(output, target.gameObject.scene);
                foreach (var child in target.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
                foreach (var child in output.transform.Cast<Transform>().ToArray())
                {
                    child.SetParent(target, false);
                    Undo.RegisterCreatedObjectUndo(child.gameObject, "도시 외형 생성");
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            finally { if (output) UnityEngine.Object.DestroyImmediate(output); }
        }

        // JC | 2026-11-16 | 저장된 보스 무대 프리팹을 미리보기 배경으로 사용합니다. 씬의 임시 복사본은 필요하지 않습니다.
        public static GameObject CreateCityPreview(BackgroundDocument document, Scene scene)
        {
            if (!document.stage) throw new InvalidOperationException("작업본의 전투 구도 미리보기 무대가 지정되지 않았습니다.");
            var stage = UnityEngine.Object.Instantiate(document.stage);
            SceneManager.MoveGameObjectToScene(stage, scene);
            var cities = JcBossCityTime.Roots(stage.transform);
            if (cities.Length != 1)
            {
                UnityEngine.Object.DestroyImmediate(stage);
                throw new InvalidOperationException("미리보기 무대에는 교체 기준 도시 한 개가 필요합니다. 편집과 베이크 출력은 계속 사용할 수 있습니다.");
            }
            var copy = cities[0];
            foreach (var child in copy.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            CopyCityVisuals(document, copy);
            return stage;
        }
    }
}
