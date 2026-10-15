using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace JC.EnvironmentDetail.Editor
{
    // 비플레이 회귀 검사. 씬/원본 데이터/확정 에셋을 저장하거나 고치지 않습니다.
    public static class JcExploreDetailBakeValidation
    {
        public static string Run(JcExploreDetailOverlay overlay)
        {
            if (Application.isPlaying) throw new InvalidOperationException("편집 모드 검사입니다.");
            var scene = overlay.gameObject.scene;
            var catalog = JcExploreDetailBakeCatalog.Load();
            var entry = catalog?.scenes.FirstOrDefault(x => x.scenePath == scene.path);
            if (entry?.current == null) throw new InvalidOperationException("먼저 실제 씬을 베이크해 주세요.");
            var bake = entry.current; bool forced = overlay.ForceBakedOnMismatch;
            var sources = JcExploreDetailBake.Sources(scene);
            var meshes = sources.Select(f => f.sharedMesh).ToArray();
            var materials = sources.Select(f => f.GetComponent<MeshRenderer>().sharedMaterials).ToArray();
            var colliders = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true)).ToArray();
            string profile = EditorJsonUtility.ToJson(overlay.profile);
            var report = new StringBuilder(); int checks = 0;
            Action<bool, string> assert = (ok, text) => { if (!ok) throw new InvalidOperationException("검증 실패: " + text); checks++; report.AppendLine("PASS " + text); };
            JcExploreDetailBake changed = null;
            GameObject brokenPrefab=null;
            try
            {
                overlay.ForceBakedOnMismatch = false; overlay.Refresh();
                assert(overlay.VisualRoot != null, "일치하는 확정본 적용");
                assert(bake.Compare(JcExploreDetailBake.CaptureInputs(scene)) == null, "원본 입력 동일 (원본 숨김 후에도 일치)");
                var dependencyWarning=JcExploreDetailBaker.CheckDependencies(bake);
                assert(dependencyWarning==null || overlay.Status.Contains(dependencyWarning), "생성 의존성 변경은 확정본 유지 및 상태 경고");
                assert(typeof(JcExploreDetailOverlay).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null, "주기적 Update 없음");
                assert(overlay.VisualRoot.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null && f.sharedMesh.isReadable && EditorUtility.IsPersistent(f.sharedMesh)), "전투에서 읽을 수 있는 영구 메시");
                assert(overlay.VisualRoot.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 && overlay.VisualRoot.GetComponentsInChildren<Collider>(true).Length == 0, "출력은 외형만 포함");
                var waterController=JcWaterController.Ensure(overlay);
                var initialVisual=overlay.VisualRoot;
                var initialWater=initialVisual.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).First(waterController.Owns);
                overlay.Refresh();overlay.Refresh();
                assert(overlay.VisualRoot==initialVisual && waterController.Owns(initialWater),"중복 재적용은 메시·물 재질 동일 인스턴스 유지");
                assert(overlay.KeepBakeOnMismatch,"런타임·편집 공통 확정본 유지 정책");
                assert(bake.prefab.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r=>r.sharedMaterials).All(m=>m!=null && EditorUtility.IsPersistent(m)), "베이크 원본 재질은 모두 영구 에셋");
                foreach (var renderer in overlay.VisualRoot.GetComponentsInChildren<MeshRenderer>())
                    foreach (var material in renderer.sharedMaterials)
                    {
                        bool liveWater=material!=null && material.shader.name=="JC/Environment/Calm Canal Water";
                        assert(material != null && (liveWater ? waterController.Owns(material) && !EditorUtility.IsPersistent(material) && material.hideFlags==HideFlags.HideAndDontSave : EditorUtility.IsPersistent(material)), "베이크 재질 / 소유권 있는 실시간 물 재질 " + renderer.name);
                        foreach (string name in material.GetTexturePropertyNames())
                        { var texture = material.GetTexture(name); assert(texture == null || EditorUtility.IsPersistent(texture), "텍스처 수명 " + name); }
                    }
                changed = Object.Instantiate(bake);
                changed.dependencies=new[]{new JcExploreDetailBake.Dependency{path=JcExploreDetailBaker.Root+"/Editor/JcWaterValidation.cs",hash="changed-test"},
                    new JcExploreDetailBake.Dependency{path=JcExploreDetailBaker.Root+"/Editor/JcExploreDetailEditor.cs",hash="changed-inspector"}};
                assert(JcExploreDetailBaker.CheckDependencies(changed)==null,"과거 베이크의 검증·인스펙터 변경 제외");
                changed.dependencies=new[]{new JcExploreDetailBake.Dependency{path=JcExploreDetailBaker.Root+"/Editor/JcExploreDetailBaker.cs",hash="changed-generator"}};
                assert(JcExploreDetailBaker.CheckDependencies(changed)!=null,"실제 생성기 변경 감지 유지");
                changed.inputs[0].hash = "deliberate-mismatch";
                entry.current = changed;
                overlay.Refresh();
                assert(overlay.VisualRoot != null && overlay.Status.Contains("마지막 확정 베이크 유지"), "편집 모드 입력 불일치는 확정본 유지 및 경고");
                assert(waterController.RendererCount>0 && waterController.MaterialCount>0,"불일치 상태에서도 실시간 물 연결 유지");
                var replacedKeys = new System.Collections.Generic.HashSet<string>(bake.inputs.Where(i => i.replace).Select(i => i.key));
                var key=JcExploreDetailBake.CreateKeyResolver(scene);
                assert(sources.Where(f => replacedKeys.Contains(key(f.transform))).All(f => f.GetComponent<MeshRenderer>().forceRenderingOff), "불일치 후 원본 중복 표시 방지");
                overlay.ForceBakedOnMismatch = true; overlay.Refresh();
                assert(overlay.VisualRoot != null, "강제 토글은 불일치 확정본 표시");
                overlay.ForceBakedOnMismatch = false; overlay.Refresh();
                assert(overlay.VisualRoot != null,"런타임 강제 옵션 해제해도 편집 모드 확정본 유지");
                var beforeCheck=overlay.VisualRoot;
                var bootstrap=typeof(JcExploreDetailOverlayEditor).Assembly.GetType("JC.EnvironmentDetail.Editor.JcExploreDetailEditorBootstrap");
                bootstrap.GetMethod("CheckChangedInputs",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                assert(overlay.VisualRoot==beforeCheck && waterController.RendererCount>0,"편집기 변경 감지에서 외형·물 유지");
                overlay.Restore();
                assert(overlay.VisualRoot==null && waterController.MaterialCount==0,"수명 종료 시 임시 표시 자원 해제");
                bootstrap.GetMethod("ApplyLoaded",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                assert(overlay.VisualRoot!=null && waterController.RendererCount>0,"편집 모드 복귀 재적용 경로에서 베이크·물 복원");
                var retained=overlay.VisualRoot;
                var retainedWater=retained.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).First(waterController.Owns);
                entry.current = null; overlay.Refresh();
                assert(overlay.VisualRoot==retained && waterController.Owns(retainedWater),"재적용 중 베이크 누락은 기존 외형·물 보존");
                entry.current=bake;
                brokenPrefab=new GameObject("Invalid bake validation"){hideFlags=HideFlags.HideAndDontSave};
                brokenPrefab.AddComponent<MeshFilter>();
                changed.prefab=brokenPrefab;entry.current=changed;overlay.Refresh();
                assert(overlay.VisualRoot==retained && waterController.Owns(retainedWater),"손상된 교체 메시 검증 실패 시 기존 외형·물 보존");
                changed.prefab=bake.prefab;
                var settingsField=typeof(JcWaterController).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic);
                var waterSettings=waterController.Settings;
                // 실제 재질 준비 단계에서 실패시켜 기존 바인딩이 유지되는지 확인합니다.
                settingsField.SetValue(waterController,null);
                try { entry.current=bake;overlay.Refresh(); }
                finally { settingsField.SetValue(waterController,waterSettings); }
                assert(overlay.VisualRoot==retained && waterController.Owns(retainedWater),"새 물 재질 준비 실패 시 기존 외형·물 재질 보존");
                entry.current=null;overlay.Restore();overlay.Refresh();
                assert(overlay.VisualRoot==null,"처음부터 확정본이 없으면 원본 표시");
                entry.current = bake; overlay.Refresh();
                var visual = overlay.VisualRoot;
                float tint = overlay.profile.waterWaveStrength;
                overlay.profile.waterWaveStrength = tint + .1f;
                try { assert(overlay.VisualRoot == visual, "초안 변경만으로 재생성하지 않음"); }
                finally { overlay.profile.waterWaveStrength = tint; }
                assert(meshes.SequenceEqual(sources.Select(f => f.sharedMesh)), "원본 MeshFilter 참조 보존");
                assert(sources.Select((f, i) => f.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(materials[i])).All(x => x), "원본 재질 참조 보존");
                assert(colliders.SequenceEqual(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true))), "Collider 보존");
                assert(profile == EditorJsonUtility.ToJson(overlay.profile), "사용자 설정 보존");
                report.Insert(0, "비플레이 검사 " + checks + "개 통과\n");
                return report.ToString();
            }
            finally
            {
                entry.current = bake; overlay.ForceBakedOnMismatch = forced;
                if (changed != null) Object.DestroyImmediate(changed);
                if (brokenPrefab != null) Object.DestroyImmediate(brokenPrefab);
                overlay.Refresh();
            }
        }
    }
}
