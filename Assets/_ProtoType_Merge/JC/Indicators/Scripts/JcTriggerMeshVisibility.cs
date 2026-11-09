using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JC.Indicators
{
    // JC | 2026-11-09 | 등록부/프리팹을 바꾸지 않고 메시 소속·깊이·채움 보호를 기록해 자기 이벤트만 가림에서 제외합니다.
    // 카메라 깊이·Sorting Layer·Order·렌더 큐는 변경하지 않으며, 임시 아이콘 재질은 해제 시 복원합니다.
    internal sealed class JcTriggerMeshVisibility
    {
        // JC | 2026-11-09 | 느낌표마다 소속 이벤트를 구분합니다. 배경/다른 이벤트는 가림 예외가 아닙니다.
        private sealed class Marker { internal SpriteRenderer renderer; internal Material original, copy; internal bool occlusion; internal int ownerId; }
        private static int nextOwnerId;
        private readonly Dictionary<Renderer, int> meshOwners = new Dictionary<Renderer, int>();
        private readonly Dictionary<SpriteRenderer, Marker> markers = new Dictionary<SpriteRenderer, Marker>();
        private readonly HashSet<SpriteRenderer> currentMarkers = new HashSet<SpriteRenderer>();
        private readonly List<SpriteRenderer> removed = new List<SpriteRenderer>();
        private readonly HashSet<Renderer> protectedMeshes = new HashSet<Renderer>();
        private readonly List<Renderer> worldMeshes = new List<Renderer>();
        private readonly Dictionary<(Renderer renderer, int slot), Material> depthCopies = new Dictionary<(Renderer, int), Material>();
        private readonly List<(Renderer renderer, int slot)> staleDepth = new List<(Renderer, int)>();
        private readonly List<Material> materials = new List<Material>();
        // JC | 2026-11-09 | Unity 네이티브 객체는 MonoBehaviour 필드 생성자가 아닌 렌더 호출의 메인 스레드에서 만듭니다.
        private MaterialPropertyBlock block, slotBlock;
        private readonly Plane[] frustum = new Plane[6];
        private Shader shader;
        private float nextScan;

        internal void Collect(IReadOnlyList<InteractionOverlayCell> cells, Scene scene)
        {
            if (!shader) shader = Resources.Load<Shader>("JcTriggerMeshVisibility");
            protectedMeshes.Clear(); currentMarkers.Clear(); meshOwners.Clear();
            var owners = new HashSet<Component>();
            foreach (var cell in cells) if (cell.Owner is Component owner && owner) owners.Add(owner);
            foreach (var owner in owners)
            {
                foreach (var r in owner.GetComponentsInChildren<Renderer>(true))
                    if ((r is MeshRenderer || r is SkinnedMeshRenderer) && !r.GetComponentInParent<Canvas>()) protectedMeshes.Add(r);
                // 기존 메인 이벤트의 느낌표만 식별합니다. 파티 UI/월드 표식 스프라이트에는 적용하지 않습니다.
                if (!(owner is MainEventObject)) continue;
                var slot = owner.transform.Find("iconSlot/iconImage");
                var sprite = slot ? slot.GetComponent<SpriteRenderer>() : null;
                if (!sprite || !sprite.sharedMaterial || !shader) continue;
                currentMarkers.Add(sprite);
                if (!markers.TryGetValue(sprite, out var marker))
                {
                    var original = sprite.sharedMaterial;
                    var copy = new Material(shader) { name = original.name + " (소속 이벤트만 가림 제외)", hideFlags = HideFlags.HideAndDontSave,
                        renderQueue = original.renderQueue };
                    copy.CopyMatchingPropertiesFromMaterial(original);
                    marker = new Marker { renderer = sprite, original = original, copy = copy, occlusion = sprite.allowOcclusionWhenDynamic, ownerId = ++nextOwnerId };
                    markers.Add(sprite, marker);
                    copy.SetFloat("_JcMarkerOwner", marker.ownerId);
                    sprite.sharedMaterial = copy;
                    sprite.allowOcclusionWhenDynamic = false;
                }
                foreach (var r in owner.GetComponentsInChildren<Renderer>(true))
                    if (protectedMeshes.Contains(r)) meshOwners[r] = marker.ownerId;
            }
            removed.Clear();
            foreach (var pair in markers) if (!pair.Key || !currentMarkers.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var key in removed) { Restore(markers[key]); markers.Remove(key); }
            // 배경 목록만 주기적으로 캐시합니다. 스킨 포즈와 Transform/재질 값은 그릴 때 최신값을 사용합니다.
            if (Time.unscaledTime >= nextScan || worldMeshes.Count == 0)
            {
                nextScan = Time.unscaledTime + 2; worldMeshes.Clear();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        if ((r is MeshRenderer || r is SkinnedMeshRenderer) && !r.GetComponentInParent<Canvas>() &&
                            !r.GetComponentInParent<BattleCellBorderVisual>() && !r.GetComponentInParent<JcMovementIndicatorController>()) worldMeshes.Add(r);
                staleDepth.Clear();
                foreach (var pair in depthCopies) if (!pair.Key.renderer) staleDepth.Add(pair.Key);
                foreach (var key in staleDepth) { Destroy(depthCopies[key]); depthCopies.Remove(key); }
            }
        }

        internal void Draw(CommandBuffer cmd, Camera camera, string passName)
        {
            if (!shader || (markers.Count == 0 && protectedMeshes.Count == 0)) return;
            if (block == null) { block = new MaterialPropertyBlock(); slotBlock = new MaterialPropertyBlock(); }
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            var drawn = new HashSet<Renderer>();
            foreach (var r in worldMeshes) DrawOne(r);
            // 새로 스폰된 이벤트는 배경 캐시 주기를 기다리지 않고 같은 프레임에 보호합니다.
            foreach (var r in protectedMeshes) if (!drawn.Contains(r)) DrawOne(r);
            void DrawOne(Renderer r)
            {
                if (!r || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy ||
                    (camera.cullingMask & (1 << r.gameObject.layer)) == 0 || !GeometryUtility.TestPlanesAABB(frustum, r.bounds)) return;
                drawn.Add(r); r.GetSharedMaterials(materials); block.Clear(); r.GetPropertyBlock(block);
                bool protect = protectedMeshes.Contains(r);
                for (int slot = 0; slot < materials.Count; slot++)
                {
                    var source = materials[slot]; if (!source) continue;
                    // 투명 안내 메시의 깊이를 월드로 오인하지 않습니다. 이벤트 소품은 투명 재질도 보호 대상으로 기록합니다.
                    bool worldDepth = markers.Count > 0 && source.renderQueue <= 2500;
                    if (!worldDepth && !protect) continue;
                    // JC | 2026-11-09 | 원본 재질을 공유해도 인스턴스 속성 블록의 알파가 섞이지 않도록 렌더러/슬롯별로 보관합니다.
                    var key = (r, slot);
                    if (!depthCopies.TryGetValue(key, out var copy)) depthCopies.Add(key, copy = new Material(shader) { hideFlags = HideFlags.HideAndDontSave });
                    slotBlock.Clear(); r.GetPropertyBlock(slotBlock, slot);
                    string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                    string tint = source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                    var texture = slotBlock.GetTexture(map) ?? block.GetTexture(map) ?? (source.HasProperty(map) ? source.GetTexture(map) : null);
                    copy.SetTexture("_MeshMap", texture ? texture : Texture2D.whiteTexture);
                    var st = slotBlock.HasVector(map + "_ST") ? slotBlock.GetVector(map + "_ST") : block.HasVector(map + "_ST") ? block.GetVector(map + "_ST") :
                        source.HasProperty(map) ? new Vector4(source.GetTextureScale(map).x, source.GetTextureScale(map).y, source.GetTextureOffset(map).x, source.GetTextureOffset(map).y) : new Vector4(1, 1, 0, 0);
                    copy.SetVector("_MeshMap_ST", st);
                    copy.SetColor("_MeshTint", slotBlock.HasColor(tint) ? slotBlock.GetColor(tint) : block.HasColor(tint) ? block.GetColor(tint) : source.HasProperty(tint) ? source.GetColor(tint) : Color.white);
                    copy.SetFloat("_MeshCull", source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 2);
                    copy.SetFloat("_MeshCutoff", source.IsKeywordEnabled("_ALPHATEST_ON") && source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : .001f);
                    // 재질을 공유하는 다른 메시도 있으므로 역할 값은 DrawRenderer 직전에 명령으로 기록합니다.
                    cmd.SetGlobalVector("_JcMeshDepthRoles", new Vector4(worldDepth ? 1 : 0, protect ? 1 : 0, 0, 0));
                    cmd.SetGlobalFloat("_JcMeshOwner", meshOwners.TryGetValue(r, out int ownerId) ? ownerId : 0);
                    cmd.DrawRenderer(r, copy, slot, copy.FindPass(passName));
                }
            }
        }
        // JC | 2026-11-09 | 실제 표시와 같은 알파/가림 판정으로 호버 윤곽선에서 보호할 픽셀만 기록합니다.
        internal void DrawMarkerCoverage(CommandBuffer cmd, Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            foreach (var marker in markers.Values)
            {
                var r = marker.renderer;
                if (!r || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy ||
                    r.sharedMaterial != marker.copy || (camera.cullingMask & (1 << r.gameObject.layer)) == 0 ||
                    !GeometryUtility.TestPlanesAABB(frustum, r.bounds)) continue;
                cmd.DrawRenderer(r, marker.copy, 0, marker.copy.FindPass("MarkerCoverage"));
            }
        }
        internal void Clear()
        {
            foreach (var marker in markers.Values) Restore(marker);
            markers.Clear(); protectedMeshes.Clear(); worldMeshes.Clear(); meshOwners.Clear();
            foreach (var m in depthCopies.Values) Destroy(m);
            depthCopies.Clear(); nextScan = 0;
        }
        private static void Restore(Marker marker)
        {
            if (marker.renderer && marker.renderer.sharedMaterial == marker.copy)
            { marker.renderer.sharedMaterial = marker.original; marker.renderer.allowOcclusionWhenDynamic = marker.occlusion; }
            Destroy(marker.copy);
        }
        private static void Destroy(Object value) { if (!value) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}
