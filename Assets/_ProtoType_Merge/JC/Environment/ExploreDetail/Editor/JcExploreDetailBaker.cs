using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JC.EnvironmentDetail.Editor
{
    public static class JcExploreDetailBaker
    {
        public const string Root = "Assets/_ProtoType_Merge/JC/Environment/ExploreDetail";
        const string CatalogPath = Root + "/Resources/JC_ExploreDetail_Bakes.asset";
        const string OutputPath = Root + "/Baked";
        static bool DisplayOnlyDependency(string path) =>
            path == Root + "/CalmCanalWater.shader" || path == Root + "/JcWaterProfile.cs" ||
            path == Root + "/JcWaterController.cs" || path == Root + "/Editor/JcWaterEditor.cs" ||
            path == Root + "/Resources/JC_Water_Default.asset" ||
            path == Root + "/Editor/JcWaterValidation.cs" ||
            path == Root + "/Editor/JcExploreDetailBakeValidation.cs" ||
            path == Root + "/Editor/JcExploreDetailEditor.cs";
        public static string Settings(JcExploreDetailProfile profile)
        {
            var copy = Object.Instantiate(profile);
            try { copy.forceBakedOnMismatch = false; copy.enableDetails = true; return EditorJsonUtility.ToJson(copy); }
            finally { Object.DestroyImmediate(copy); }
        }
        public static void Preview(JcExploreDetailOverlay overlay)
        {
            RequireEdit();
            overlay.PreviewDependencies = Dependencies(overlay).ToArray();
            overlay.GeneratePreview(Settings(overlay.profile));
        }
        public static string CheckDependencies(JcExploreDetailBake bake)
        {
            // 저장된 과거 베이크에도 같은 제외 규칙을 적용합니다. 생성기·메시 입력 검사는 유지합니다.
            foreach (var item in bake.dependencies.Where(x=>!DisplayOnlyDependency(x.path)))
                if (!File.Exists(item.path) || SourceDependencyHash(item.path) != item.hash)
                    return "베이크 입력 에셋 변경: " + item.path;
            return null;
        }
        // Library의 임포트 해시는 같은 파일도 다른 checkout에서 달라질 수 있습니다.
        // 의존 파일 목록은 AssetDatabase로 수집하되 저장 지문은 원본+meta의 내용으로 만듭니다.
        public static string SourceDependencyHash(string path)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                foreach (string file in new[] { path, path + ".meta" })
                {
                    writer.Write(File.Exists(file));
                    if (!File.Exists(file)) continue;
                    byte[] data = File.ReadAllBytes(file);
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (new[] { ".cs", ".shader", ".hlsl", ".cginc", ".compute", ".meta", ".json", ".asmdef", ".asmref", ".txt", ".csv" }.Contains(ext) ||
                        ((ext == ".asset" || ext == ".mat" || ext == ".prefab" || ext == ".unity") &&
                         System.Text.Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 16)).TrimStart('\uFEFF').StartsWith("%YAML")))
                        data = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(data).TrimStart('\uFEFF').Replace("\r\n", "\n"));
                    writer.Write(data.Length); writer.Write(data);
                }
                writer.Flush();
                return "source-v1:" + BitConverter.ToString(sha.ComputeHash(buffer.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        public static string Check(JcExploreDetailOverlay overlay)
        {
            var bake = JcExploreDetailBakeCatalog.Load()?.Find(overlay.gameObject.scene.path);
            if (bake == null) return "확정 베이크가 없습니다.";
            try { return bake.Compare(JcExploreDetailBake.CaptureInputs(overlay.gameObject.scene)) ?? CheckDependencies(bake) ??
                (bake.settingsSnapshot != Settings(overlay.profile) ? "설정 변경: 재생성·베이크 확정 전까지 기존 외형을 유지합니다." : "일치: 확정 베이크 사용 가능"); }
            catch (Exception e) { return "구조 검증 실패: " + e.Message; }
        }
        public static JcExploreDetailBake Commit(JcExploreDetailOverlay overlay)
        {
            RequireEdit();
            if (!overlay.IsPreview || overlay.VisualRoot == null) throw new InvalidOperationException("먼저 드레싱 재생성·미리보기를 실행해 주세요.");
            if (overlay.PreviewSettings != Settings(overlay.profile)) throw new InvalidOperationException("미리보기 이후 설정이 변경되었습니다. 다시 재생성해 주세요.");
            var probe = ScriptableObject.CreateInstance<JcExploreDetailBake>();
            probe.inputs = overlay.PreviewInputs;
            probe.dependencies = overlay.PreviewDependencies ?? Array.Empty<JcExploreDetailBake.Dependency>();
            string mismatch;
            try { mismatch = probe.Compare(JcExploreDetailBake.CaptureInputs(overlay.gameObject.scene)) ?? CheckDependencies(probe); }
            finally { Object.DestroyImmediate(probe); }
            if (mismatch != null) throw new InvalidOperationException(mismatch + " 미리보기를 다시 생성해 주세요.");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string folder = OutputPath + "/" + overlay.gameObject.scene.name + "_" + Guid.NewGuid().ToString("N");
            EnsureFolder(folder);
            GameObject copy = null;
            JcExploreDetailBake bake = null;
            bool published = false;
            try
            {
                copy = Object.Instantiate(overlay.VisualRoot);
                copy.transform.SetParent(null, true); copy.name = "JC_ExploreDetail_Visuals";
                var export = new Export(folder);
                foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                { t.gameObject.hideFlags = HideFlags.None; foreach (var c in t.GetComponents<Component>()) c.hideFlags = HideFlags.None; }
                foreach (var f in copy.GetComponentsInChildren<MeshFilter>(true)) f.sharedMesh = export.Mesh(f.sharedMesh);
                // Instantiate가 MPB를 복제한다고 가정하지 않고 원본 Renderer에서 직접 읽습니다.
                var sourceRenderers = overlay.VisualRoot.GetComponentsInChildren<MeshRenderer>(true);
                var renderers = copy.GetComponentsInChildren<MeshRenderer>(true);
                if (sourceRenderers.Length != renderers.Length) throw new InvalidOperationException("베이크 Renderer 대응 불일치");
                for (int i = 0; i < renderers.Length; i++)
                {
                    var block = new MaterialPropertyBlock(); sourceRenderers[i].GetPropertyBlock(block);
                    renderers[i].sharedMaterials = sourceRenderers[i].sharedMaterials.Select(m => export.Material(m, block)).ToArray();
                    renderers[i].SetPropertyBlock(null);
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(copy, folder + "/Visuals.prefab");
                if (prefab == null) throw new InvalidOperationException("드레싱 프리팹 저장 실패");
                bake = ScriptableObject.CreateInstance<JcExploreDetailBake>();
                bake.scenePath = overlay.gameObject.scene.path; bake.prefab = prefab;
                bake.createdUtc = DateTime.UtcNow.ToString("O"); bake.inputs = overlay.PreviewInputs;
                bake.settingsSnapshot = overlay.PreviewSettings; bake.report = overlay.Status.Replace("PREVIEW (미확정): ", "");
                bake.dependencies = overlay.PreviewDependencies;
                AssetDatabase.CreateAsset(bake, folder + "/Bake.asset");
                AssetDatabase.SaveAssetIfDirty(bake);
                var catalog = AssetDatabase.LoadAssetAtPath<JcExploreDetailBakeCatalog>(CatalogPath);
                if (catalog == null) { catalog = ScriptableObject.CreateInstance<JcExploreDetailBakeCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
                // 모든 에셋 저장이 성공한 뒤 마지막으로 카탈로그 포인터를 교체합니다.
                var entry = catalog.scenes.FirstOrDefault(x => x.scenePath == bake.scenePath);
                bool addedEntry = entry == null;
                if (entry == null) { entry = new JcExploreDetailBakeCatalog.Entry { scenePath = bake.scenePath }; catalog.scenes = catalog.scenes.Concat(new[] { entry }).ToArray(); }
                string oldBackup = entry.previousAssetPath;
                var oldCurrent = entry.current; string oldWarning = entry.buildInputWarning;
                entry.previousAssetPath = entry.current != null ? AssetDatabase.GetAssetPath(entry.current) : "";
                entry.current = bake;
                entry.buildInputWarning = null;
                try { EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog); published = true; }
                catch
                {
                    entry.current=oldCurrent; entry.previousAssetPath=oldBackup; entry.buildInputWarning=oldWarning;
                    if(addedEntry) catalog.scenes=catalog.scenes.Where(x=>x!=entry).ToArray();
                    throw;
                }
                overlay.Refresh();
                // 직전 세대까지 보존합니다. 도구가 생성한 정확한 세대 폴더만 정리합니다.
                if (!string.IsNullOrEmpty(oldBackup) && oldBackup != entry.previousAssetPath &&
                    oldBackup.StartsWith(OutputPath + "/", StringComparison.Ordinal) && oldBackup.EndsWith("/Bake.asset", StringComparison.Ordinal))
                {
                    string oldFolder = Path.GetDirectoryName(oldBackup).Replace('\\', '/');
                    if (!catalog.scenes.Any(x => AssetDatabase.GetAssetPath(x.current).StartsWith(oldFolder + "/", StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(x.previousAssetPath) && x.previousAssetPath.StartsWith(oldFolder + "/", StringComparison.Ordinal))))
                        AssetDatabase.DeleteAsset(oldFolder);
                }
                Debug.Log($"[JC Bake] 확정: {folder}, {watch.ElapsedMilliseconds}ms, 원본 입력 {bake.inputs.Length}. 씬은 저장하지 않았습니다.");
                return bake;
            }
            finally
            {
                if (copy != null) Object.DestroyImmediate(copy);
                if (!published) AssetDatabase.DeleteAsset(folder);
            }
        }
        public static void RestorePrevious(JcExploreDetailOverlay overlay)
        {
            RequireEdit();
            var catalog = JcExploreDetailBakeCatalog.Load();
            var entry = catalog?.scenes.FirstOrDefault(x => x.scenePath == overlay.gameObject.scene.path);
            var previous = entry != null ? AssetDatabase.LoadAssetAtPath<JcExploreDetailBake>(entry.previousAssetPath) : null;
            if (previous == null) throw new InvalidOperationException("직전 확정본이 없습니다.");
            string current = AssetDatabase.GetAssetPath(entry.current);
            entry.current = previous; entry.previousAssetPath = current;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog); overlay.Refresh();
        }
        static IEnumerable<JcExploreDetailBake.Dependency> Dependencies(JcExploreDetailOverlay overlay)
        {
            // 지형 생성 데이터·레지스트리·메시·텍스처·생성기 코드를 추적합니다. 씬/UI 변경 전체를 의존성으로 잡지 않습니다.
            var paths = new HashSet<string>(StringComparer.Ordinal);
            Action<Object> add = o => { string path = AssetDatabase.GetAssetPath(o); if (!string.IsNullOrEmpty(path)) paths.Add(path); };
            foreach (var root in overlay.gameObject.scene.GetRootGameObjects())
            {
                foreach (var loader in root.GetComponentsInChildren<LevelZoneLayoutLoader>(true)) add(loader.LayoutData);
                foreach (var loader in root.GetComponentsInChildren<LevelLoader>(true)) add(loader.LevelData);
                foreach(var generator in root.GetComponentsInChildren<LevelTileMeshGenerator>(true))
                {
                    var serialized=new SerializedObject(generator); var property=serialized.GetIterator();
                    while(property.Next(true)) if(property.propertyType==SerializedPropertyType.ObjectReference) add(property.objectReferenceValue);
                }
            }
            foreach (var f in JcExploreDetailBake.Sources(overlay.gameObject.scene))
            {
                add(f.sharedMesh);
                foreach (var m in f.GetComponent<MeshRenderer>().sharedMaterials)
                {
                    if(m==null) continue; add(m); add(m.shader);
                    // DH 타일 재질 자체는 임시 복제본이므로 그 안의 영구 텍스처도 직접 추적합니다.
                    foreach(string name in m.GetTexturePropertyNames()) add(m.GetTexture(name));
                }
            }
            add(Resources.Load<JC.BattleTesting.JcBattleSurfaceProfile>("JC_BattleSurface_Default"));
            var settings = new SerializedObject(overlay.profile); var iterator = settings.GetIterator();
            while (iterator.Next(true)) if (iterator.propertyType == SerializedPropertyType.ObjectReference) add(iterator.objectReferenceValue);
            foreach (string path in AssetDatabase.FindAssets("t:MonoScript", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath)) paths.Add(path);
            foreach (string path in paths.SelectMany(p => AssetDatabase.GetDependencies(p, true)).Distinct().OrderBy(p => p, StringComparer.Ordinal))
                if (!DisplayOnlyDependency(path) && path.StartsWith("Assets/", StringComparison.Ordinal) && !path.StartsWith(OutputPath + "/", StringComparison.Ordinal) && path != CatalogPath &&
                    path != AssetDatabase.GetAssetPath(overlay.profile))
                    yield return new JcExploreDetailBake.Dependency { path = path, hash = SourceDependencyHash(path) };
        }
        static void RequireEdit()
        { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("재생성·베이크는 편집 모드에서만 가능합니다."); }
        static void EnsureFolder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }

        sealed class Export
        {
            readonly string folder;
            readonly Dictionary<Object, Object> copies = new Dictionary<Object, Object>();
            int next;
            public Export(string folder) { this.folder = folder; }
            public Mesh Mesh(Mesh source)
            {
                if (source == null) throw new InvalidOperationException("생성 메시 누락");
                if (copies.TryGetValue(source, out var value)) return (Mesh)value;
                var copy = Object.Instantiate(source); copy.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(copy, folder + "/Mesh_" + next++ + ".asset"); copies[source] = copy; return copy;
            }
            public Material Material(Material source, MaterialPropertyBlock block)
            {
                if (source == null) throw new InvalidOperationException("생성 재질 누락");
                if (block.isEmpty && copies.TryGetValue(source, out var value)) return (Material)value;
                var copy = new Material(source) { name = source.name, hideFlags = HideFlags.None };
                bool liveWater = copy.shader.name == "JC/Environment/Calm Canal Water";
                for (int i = 0; i < copy.shader.GetPropertyCount(); i++)
                {
                    string p = copy.shader.GetPropertyName(i); int id = Shader.PropertyToID(p);
                    // 카메라별 반사 영상은 저장할 입력이 아닙니다. 표시 시 물 컨트롤러가 다시 연결합니다.
                    if (liveWater && (p == "_PlanarReflection" || p == "_PlanarAvailable")) continue;
                    switch (copy.shader.GetPropertyType(i))
                    {
                        case ShaderPropertyType.Color: if (block.HasColor(id)) copy.SetColor(id, block.GetColor(id)); break;
                        case ShaderPropertyType.Vector: if (block.HasVector(id)) copy.SetVector(id, block.GetVector(id)); break;
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range: if (block.HasFloat(id)) copy.SetFloat(id, block.GetFloat(id)); break;
                        case ShaderPropertyType.Texture:
                            if (block.HasTexture(id)) copy.SetTexture(id, block.GetTexture(id));
                            copy.SetTexture(id, Texture(copy.GetTexture(id))); break;
                    }
                }
                if (liveWater)
                {
                    copy.SetTexture("_PlanarReflection", null);
                    copy.SetFloat("_PlanarAvailable", 0);
                    copy.SetMatrix("_PlanarVP", Matrix4x4.identity);
                }
                if(copy.HasProperty("_UseWaterTime"))copy.SetFloat("_UseWaterTime",0);
                AssetDatabase.CreateAsset(copy, folder + "/Material_" + next++ + ".mat");
                if (block.isEmpty) copies[source] = copy;
                return copy;
            }
            Texture Texture(Texture source)
            {
                if (source == null || EditorUtility.IsPersistent(source)) return source;
                if (copies.TryGetValue(source, out var value)) return (Texture)value;
                if (!(source is Texture2D)) throw new InvalidOperationException("베이크 저장을 지원하지 않는 임시 텍스처: " + source.GetType().Name);
                var copy = Object.Instantiate(source); copy.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(copy, folder + "/Texture_" + next++ + ".asset"); copies[source] = copy; return copy;
            }
        }
    }

    // 오래된 베이크여도 빌드는 허용합니다. 자동 생성·씬 저장은 하지 않습니다.
    public sealed class JcExploreDetailBakeBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            var profile = Resources.Load<JcExploreDetailProfile>("JC_ExploreDetail_Default");
            if (profile == null || !profile.enableDetails) return;
            var catalog = JcExploreDetailBakeCatalog.Load();
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled && profile.sceneNames.Contains(Path.GetFileNameWithoutExtension(s.path))))
            {
                var bake = catalog?.Find(scene.path);
                string reason = bake == null ? "확정 베이크 없음" : JcExploreDetailBaker.CheckDependencies(bake);
                // Player에서 읽을 수 없는 에셋 변경은 진단 경고로 전달합니다. 확정 외형은 유지합니다.
                var entry = catalog?.scenes.FirstOrDefault(x => x.scenePath == scene.path);
                if (entry != null && entry.buildInputWarning != reason)
                { entry.buildInputWarning = reason; EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog); }
                if (reason == null && bake.settingsSnapshot != JcExploreDetailBaker.Settings(profile)) reason = "설정 미베이크";
                if (reason != null) Debug.LogWarning("[JC Bake / 빌드 허용] " + scene.path + ": " + reason);
            }
        }
    }
}
