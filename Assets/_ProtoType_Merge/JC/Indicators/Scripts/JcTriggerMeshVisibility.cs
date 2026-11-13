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
        private sealed class Marker { internal SpriteRenderer renderer; internal Material original, copy; internal bool occlusion, ownSurfaceOnly; internal int ownerId, coveragePass; }
        // JC | 2026-11-10 | 수집 주기는 유지하며 호출별 임시 배열/집합만 재사용합니다. 다음 수집에서 다시 채웁니다.
        private readonly HashSet<Component> collectedOwners = new HashSet<Component>();
        private readonly List<GameObject> scanRoots = new List<GameObject>();
        private readonly List<Renderer> scanRenderers = new List<Renderer>();
        private static int nextOwnerId;
        private readonly Dictionary<Renderer, int> meshOwners = new Dictionary<Renderer, int>();
        private readonly Dictionary<SpriteRenderer, Marker> markers = new Dictionary<SpriteRenderer, Marker>();
        private readonly HashSet<SpriteRenderer> currentMarkers = new HashSet<SpriteRenderer>();
        private readonly List<SpriteRenderer> removed = new List<SpriteRenderer>();
        private readonly HashSet<Renderer> protectedMeshes = new HashSet<Renderer>();
        // JC | 2026-11-10 | 패스마다 내용만 비워 중복 검사 공간을 재사용하고 반복 생성·확장 할당을 줄입니다.
        private readonly HashSet<Renderer> drawn = new HashSet<Renderer>();
        private readonly List<Renderer> worldMeshes = new List<Renderer>();
        private readonly Dictionary<(Renderer renderer, int slot), Material> depthCopies = new Dictionary<(Renderer, int), Material>();
        private readonly List<(Renderer renderer, int slot)> staleDepth = new List<(Renderer, int)>();
        private readonly List<Material> materials = new List<Material>();
        // JC | 2026-11-09 | Unity 네이티브 객체는 MonoBehaviour 필드 생성자가 아닌 렌더 호출의 메인 스레드에서 만듭니다.
        private MaterialPropertyBlock block, slotBlock;
        // JC | 2026-11-12 | 후보는 카메라당 한 번만 판정하고 깊이 준비·보호 마스크에서 같은 목록을 사용합니다.
        private readonly List<Marker> visibleMarkers = new List<Marker>();
        private readonly HashSet<int> visibleOwners = new HashSet<int>();
        internal bool NeedsWorldDepth { get; private set; }
        // JC | 2026-11-10 | 한 카메라 깊이 처리에서만 공유하는 슬롯별 준비 결과입니다. 다음 처리에서는 다시 읽습니다.
        private struct PreparedMesh { internal Renderer renderer; internal Material material; internal int slot, owner; internal Vector4 roles; }
        private readonly List<PreparedMesh> prepared = new List<PreparedMesh>();
        private Shader shader;
        private float nextScan;
        private Scene sourceScene;

        internal void Collect(IReadOnlyList<InteractionOverlayCell> cells, Scene scene)
        {
            sourceScene = scene;
            if (!shader) shader = Resources.Load<Shader>("JcTriggerMeshVisibility");
            protectedMeshes.Clear(); currentMarkers.Clear(); meshOwners.Clear();
            var owners = collectedOwners; owners.Clear();
            foreach (var cell in cells) if (cell.Owner is Component owner && owner) owners.Add(owner);
            foreach (var owner in owners)
            {
                // JC | 2026-11-10 | 같은 소유자의 보호 대상/소속 지정에 동일 수집 결과를 사용합니다.
                scanRenderers.Clear(); owner.GetComponentsInChildren<Renderer>(true, scanRenderers);
                foreach (var r in scanRenderers)
                    if ((r is MeshRenderer || r is SkinnedMeshRenderer) && !r.GetComponentInParent<Canvas>()) protectedMeshes.Add(r);
                // JC | 2026-11-12 | 방문 팻말도 기존 소속별 깊이에 함께 등록합니다. 팻말마다 별도 장면 수집/깊이 패스를 만들지 않습니다.
                string markerPath = owner is MainEventObject ? "iconSlot/iconImage" : owner is HeroUnionUnit ? "VisitorIndicator" : null;
                if (markerPath == null) continue;
                var slot = owner.transform.Find(markerPath);
                var sprite = slot ? slot.GetComponent<SpriteRenderer>() : null;
                if (!sprite || !sprite.sharedMaterial || !shader) continue;
                currentMarkers.Add(sprite);
                if (!markers.TryGetValue(sprite, out var marker))
                {
                    var original = sprite.sharedMaterial;
                    var copy = new Material(shader) { name = original.name + " (소속 이벤트만 가림 제외)", hideFlags = HideFlags.HideAndDontSave,
                        renderQueue = original.renderQueue };
                    copy.CopyMatchingPropertiesFromMaterial(original);
                    marker = new Marker { renderer = sprite, original = original, copy = copy, occlusion = sprite.allowOcclusionWhenDynamic, ownerId = ++nextOwnerId, ownSurfaceOnly = owner is HeroUnionUnit, coveragePass = copy.FindPass("MarkerCoverage") };
                    markers.Add(sprite, marker);
                    copy.SetFloat("_JcMarkerOwner", marker.ownerId);
                    // JC | 2026-11-12 | 방문 팻말은 자기 건물 표면만 확인합니다. 다른 이벤트의 다층 가림 준비를 팻말 때문에 시작하지 않습니다.
                    copy.SetFloat("_JcMarkerOwnSurfaceOnly", marker.ownSurfaceOnly ? 1 : 0);
                    sprite.sharedMaterial = copy;
                    sprite.allowOcclusionWhenDynamic = false;
                }
                foreach (var r in scanRenderers)
                    if (protectedMeshes.Contains(r)) meshOwners[r] = marker.ownerId;
            }
            removed.Clear();
            foreach (var pair in markers) if (!pair.Key || !currentMarkers.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var key in removed) { Restore(markers[key]); markers.Remove(key); }
        }

        // JC | 2026-11-12 | 전체 월드 깊이를 실제로 소비할 때만 기존 2초 캐시를 갱신합니다.
        // 팻말만 보이거나 모든 마커가 화면 밖인 동안은 배경 재수집/슬롯 준비를 시작하지 않습니다.
        private void RefreshWorldMeshes()
        {
            // 배경 목록만 주기적으로 캐시합니다. 스킨 포즈와 Transform/재질 값은 그릴 때 최신값을 사용합니다.
            if (Time.unscaledTime >= nextScan || worldMeshes.Count == 0)
            {
                nextScan = Time.unscaledTime + 2; worldMeshes.Clear();
                scanRoots.Clear(); sourceScene.GetRootGameObjects(scanRoots);
                foreach (var root in scanRoots)
                {
                    scanRenderers.Clear(); root.GetComponentsInChildren<Renderer>(true, scanRenderers);
                    foreach (var r in scanRenderers)
                        if ((r is MeshRenderer || r is SkinnedMeshRenderer) && !r.GetComponentInParent<Canvas>() &&
                            !r.GetComponentInParent<BattleCellBorderVisual>() && !r.GetComponentInParent<JcMovementIndicatorController>()) worldMeshes.Add(r);
                }
                staleDepth.Clear();
                foreach (var pair in depthCopies) if (!pair.Key.renderer) staleDepth.Add(pair.Key);
                foreach (var key in staleDepth) { Destroy(depthCopies[key]); depthCopies.Remove(key); }
            }
        }

        // JC | 2026-11-10 | 어느 컨트롤러에도 표시 후보가 없으면 월드 R만 생략하고 채움 보호 G는 유지합니다.
        internal void PrepareDraw(Camera camera, bool needsWorldDepth, Plane[] frustum)
        {
            prepared.Clear();
            if (!shader || (markers.Count == 0 && protectedMeshes.Count == 0)) return;
            if (block == null) { block = new MaterialPropertyBlock(); slotBlock = new MaterialPropertyBlock(); }
            drawn.Clear();
            // JC | 2026-11-12 | 전체 월드 깊이가 필요 없으면 방문 팻말 소속과 기존 채움 보호 대상만 준비합니다.
            if (needsWorldDepth)
            {
                RefreshWorldMeshes();
                foreach (var r in worldMeshes) DrawOne(r);
            }
            // 새로 스폰된 이벤트는 배경 캐시 주기를 기다리지 않고 같은 프레임에 보호합니다.
            foreach (var r in protectedMeshes) DrawOne(r);
            void DrawOne(Renderer r)
            {
                // JC | 2026-11-12 | 월드/보호 목록에 함께 있는 메시의 화면 밖 판정도 한 번만 수행합니다.
                if (!r || !drawn.Add(r) || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy ||
                    (camera.cullingMask & (1 << r.gameObject.layer)) == 0 || !GeometryUtility.TestPlanesAABB(frustum, r.bounds)) return;
                r.GetSharedMaterials(materials); block.Clear(); r.GetPropertyBlock(block);
                bool protect = protectedMeshes.Contains(r);
                int ownerId = meshOwners.TryGetValue(r, out int id) ? id : 0;
                bool markerMesh = needsWorldDepth || visibleOwners.Contains(ownerId);
                for (int slot = 0; slot < materials.Count; slot++)
                {
                    var source = materials[slot]; if (!source) continue;
                    // 투명 안내 메시의 깊이를 월드로 오인하지 않습니다. 이벤트 소품은 투명 재질도 보호 대상으로 기록합니다.
                    bool worldDepth = markerMesh && source.renderQueue <= 2500;
                    if (!worldDepth && !protect) continue;
                    // JC | 2026-11-09 | 원본 재질을 공유해도 인스턴스 속성 블록의 알파가 섞이지 않도록 렌더러/슬롯별로 보관합니다.
                    var key = (r, slot);
                    if (!depthCopies.TryGetValue(key, out var copy)) depthCopies.Add(key, copy = new Material(shader) { hideFlags = HideFlags.HideAndDontSave });
                    slotBlock.Clear(); r.GetPropertyBlock(slotBlock, slot);
                    string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                    // JC | 2026-11-10 | 같은 속성 이름을 문자열 결합 없이 선택하여 슬롯별 임시 문자열 할당을 없앱니다.
                    string mapST = map == "_BaseMap" ? "_BaseMap_ST" : "_MainTex_ST";
                    string tint = source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                    var texture = slotBlock.GetTexture(map) ?? block.GetTexture(map) ?? (source.HasProperty(map) ? source.GetTexture(map) : null);
                    copy.SetTexture("_MeshMap", texture ? texture : Texture2D.whiteTexture);
                    var st = slotBlock.HasVector(mapST) ? slotBlock.GetVector(mapST) : block.HasVector(mapST) ? block.GetVector(mapST) :
                        source.HasProperty(map) ? new Vector4(source.GetTextureScale(map).x, source.GetTextureScale(map).y, source.GetTextureOffset(map).x, source.GetTextureOffset(map).y) : new Vector4(1, 1, 0, 0);
                    copy.SetVector("_MeshMap_ST", st);
                    copy.SetColor("_MeshTint", slotBlock.HasColor(tint) ? slotBlock.GetColor(tint) : block.HasColor(tint) ? block.GetColor(tint) : source.HasProperty(tint) ? source.GetColor(tint) : Color.white);
                    copy.SetFloat("_MeshCull", source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 2);
                    copy.SetFloat("_MeshCutoff", source.IsKeywordEnabled("_ALPHATEST_ON") && source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : .001f);
                    prepared.Add(new PreparedMesh { renderer = r, material = copy, slot = slot,
                        roles = new Vector4(worldDepth ? 1 : 0, protect ? 1 : 0, 0, 0),
                        owner = ownerId });
                }
            }
        }
        // JC | 2026-11-10 | 준비한 순서와 슬롯을 그대로 제출하며 역할/소속은 각 명령 직전에 기록합니다.
        internal void Draw(CommandBuffer cmd, Camera camera, string passName)
        {
            // JC | 2026-11-10 | 소속/외부 깊이 셰이더가 전 픽셀을 버리는 보호 전용 슬롯은 제출하지 않습니다.
            bool worldOnly = passName != "WorldMeshDepth";
            // JC | 2026-11-12 | 모든 준비 재질이 같은 셰이더이므로 패스 검색은 슬롯별로 반복하지 않습니다.
            if (prepared.Count == 0) return;
            int pass = prepared[0].material.FindPass(passName);
            foreach (var item in prepared)
            {
                if (worldOnly && item.roles.x < .5f) continue;
                cmd.SetGlobalVector("_JcMeshDepthRoles", item.roles);
                cmd.SetGlobalFloat("_JcMeshOwner", item.owner);
                cmd.DrawRenderer(item.renderer, item.material, item.slot, pass);
            }
        }
        // JC | 2026-11-10 | 카메라 처리 종료 시 참조를 비워 다음 카메라/프레임으로 준비 결과가 넘어가지 않게 합니다.
        internal void ClearPrepared() { prepared.Clear(); visibleMarkers.Clear(); visibleOwners.Clear(); NeedsWorldDepth = false; }
        // JC | 2026-11-09 | 실제 표시와 같은 알파/가림 판정으로 호버 윤곽선에서 보호할 픽셀만 기록합니다.
        internal void DrawMarkerCoverage(CommandBuffer cmd, Camera camera)
        {
            // JC | 2026-11-12 | 같은 카메라의 후보 판정을 재사용하여 절두체/활성 검사와 재질 조회를 반복하지 않습니다.
            foreach (var marker in visibleMarkers)
                cmd.DrawRenderer(marker.renderer, marker.copy, 0, marker.coveragePass);
        }
        internal bool PrepareMarkerCandidates(Camera camera, Plane[] frustum)
        {
            visibleMarkers.Clear(); visibleOwners.Clear(); NeedsWorldDepth = false;
            foreach (var marker in markers.Values)
            {
                var r = marker.renderer;
                if (r && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy &&
                    r.sharedMaterial == marker.copy && (camera.cullingMask & (1 << r.gameObject.layer)) != 0 &&
                    GeometryUtility.TestPlanesAABB(frustum, r.bounds))
                {
                    visibleMarkers.Add(marker); visibleOwners.Add(marker.ownerId);
                    NeedsWorldDepth |= !marker.ownSurfaceOnly;
                }
            }
            return visibleMarkers.Count > 0;
        }
        internal void Clear()
        {
            foreach (var marker in markers.Values) Restore(marker);
            markers.Clear(); protectedMeshes.Clear(); worldMeshes.Clear(); meshOwners.Clear();
            // JC | 2026-11-10 | 재사용 중복 검사 목록도 해제 시 렌더러 참조를 비웁니다.
            drawn.Clear();
            foreach (var m in depthCopies.Values) Destroy(m);
            depthCopies.Clear(); prepared.Clear(); visibleMarkers.Clear(); visibleOwners.Clear(); NeedsWorldDepth = false; nextScan = 0;
            // JC | 2026-11-10 | 수집 버퍼가 보유한 참조도 정리합니다.
            collectedOwners.Clear(); scanRoots.Clear(); scanRenderers.Clear();
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
