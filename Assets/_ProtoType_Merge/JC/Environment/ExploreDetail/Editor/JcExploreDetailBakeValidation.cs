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
            try
            {
                overlay.ForceBakedOnMismatch = false; overlay.Refresh();
                assert(overlay.VisualRoot != null, "일치하는 확정본 적용");
                assert(bake.Compare(JcExploreDetailBake.CaptureInputs(scene)) == null, "원본 입력 동일 (원본 숨김 후에도 일치)");
                assert(JcExploreDetailBaker.CheckDependencies(bake) == null, "영구 에셋 의존성 일치");
                assert(typeof(JcExploreDetailOverlay).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null, "주기적 Update 없음");
                assert(overlay.VisualRoot.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null && f.sharedMesh.isReadable && EditorUtility.IsPersistent(f.sharedMesh)), "전투에서 읽을 수 있는 영구 메시");
                assert(overlay.VisualRoot.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 && overlay.VisualRoot.GetComponentsInChildren<Collider>(true).Length == 0, "출력은 외형만 포함");
                foreach (var renderer in overlay.VisualRoot.GetComponentsInChildren<MeshRenderer>())
                    foreach (var material in renderer.sharedMaterials)
                    {
                        assert(material != null && EditorUtility.IsPersistent(material), "영구 재질 " + renderer.name);
                        foreach (string name in material.GetTexturePropertyNames())
                        { var texture = material.GetTexture(name); assert(texture == null || EditorUtility.IsPersistent(texture), "텍스처 수명 " + name); }
                    }
                changed = Object.Instantiate(bake);
                changed.inputs[0].hash = "deliberate-mismatch";
                entry.current = changed;
                overlay.Refresh();
                assert(overlay.VisualRoot == null, "불일치 기본값은 DH 원본");
                var replacedKeys = new System.Collections.Generic.HashSet<string>(bake.inputs.Where(i => i.replace).Select(i => i.key));
                var key=JcExploreDetailBake.CreateKeyResolver(scene);
                assert(sources.Where(f => replacedKeys.Contains(key(f.transform))).All(f => !f.GetComponent<MeshRenderer>().forceRenderingOff), "불일치 후 원본 표시 복원");
                overlay.ForceBakedOnMismatch = true; overlay.Refresh();
                assert(overlay.VisualRoot != null, "강제 토글은 불일치 확정본 표시");
                overlay.ForceBakedOnMismatch = false; overlay.Refresh();
                assert(overlay.VisualRoot == null, "강제 해제 시 원본 복귀");
                entry.current = null; overlay.Refresh();
                assert(overlay.VisualRoot == null, "베이크 누락 시 원본 유지");
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
                overlay.Refresh();
            }
        }
    }
}
