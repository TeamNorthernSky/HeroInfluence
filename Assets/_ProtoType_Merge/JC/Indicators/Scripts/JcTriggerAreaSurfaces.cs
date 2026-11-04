using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JC.Indicators
{
    /// <summary>기존 표시 메시를 읽어 도로 높이와 가림 종류를 제공하며 원본은 변경하지 않습니다.</summary>
    internal sealed class JcTriggerAreaSurfaces
    {
        private sealed class Surface { internal MeshFilter filter; internal MeshRenderer renderer; internal Transform root; internal int kind; internal bool split; }
        // JC | 2026-11-04 | 원본 메시를 읽는 표시 전용 캐시입니다. 충돌체·트리거 판정과 모델은 변경하지 않습니다.
        private sealed class Road { internal MeshFilter filter; internal Mesh mesh; internal Matrix4x4 matrix; internal bool pavement; }
        private readonly List<Road> roads = new List<Road>();
        private readonly List<MeshFilter> roadFilters = new List<MeshFilter>();
        private readonly HashSet<MeshFilter> pavementFilters = new HashSet<MeshFilter>();
        private readonly Dictionary<Vector2Int, float> pavementHeights = new Dictionary<Vector2Int, float>();
        private readonly Plane[] frustum = new Plane[6];
        private float cellSize;
        private Vector3 gridOrigin;
        private readonly List<Surface> surfaces = new List<Surface>();
        private readonly Dictionary<Vector2Int, float> roadHeights = new Dictionary<Vector2Int, float>();
        private static readonly Vector2Int[] touchingOffsets = { Vector2Int.zero, Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        private float nextScan;
        private GridManager grid;

        internal void Refresh(Scene scene, GridManager value)
        {
            if (grid == value && Time.unscaledTime < nextScan) return;
            bool gridChanged = grid != value || cellSize != value.CellSize || gridOrigin != value.GridToWorldCenter(Vector2Int.zero);
            grid = value; cellSize = grid.CellSize; gridOrigin = grid.GridToWorldCenter(Vector2Int.zero); nextScan = Time.unscaledTime + 2;
            surfaces.Clear(); roadFilters.Clear(); pavementFilters.Clear();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!f.sharedMesh || !f.gameObject.activeInHierarchy || f.name == "JC_LampSurface" ||
                        f.GetComponentInParent<JcTriggerAreaIndicator>() || f.GetComponentInParent<JcMovementIndicatorController>()) continue;
                    var r = f.GetComponent<MeshRenderer>(); if (!r) continue;
                    bool tile = f.name.StartsWith("TileChunk_") || f.name.StartsWith("Detail_TileChunk_");
                    if (tile && f.name.Contains("_Road_")) roadFilters.Add(f);
                    bool pavement = (tile && f.name.EndsWith("_tilemap_01_RaisedCube")) || f.name.Contains("연속 연석") || f.name.Contains("보도 외곽");
                    // JC | 2026-11-04 | 인도 상면도 같은 캐시에서 읽어 영역 전체가 인도일 때 높이를 올립니다.
                    if (pavement) { pavementFilters.Add(f); roadFilters.Add(f); }
                    var union = f.GetComponentInParent<HeroUnionUnit>();
                    var outpost = f.GetComponentInParent<Outpost>();
                    string name = f.sharedMesh.name;
                    bool building = union || outpost || ((f.GetComponentInParent<PartyOcclusionFadeTarget>() || name.StartsWith("BG")) && r.bounds.size.y > .5f);
                    if (!pavement && !building) continue;
                    if (f.GetComponentInParent<PartyGridMover>() || f.GetComponentInParent<EnemyGridMover>() || f.GetComponentInParent<TutorialEnemyObject>()) continue;
                    int kind = pavement ? 2 : name.Contains("HeroAssociation") ? 5 : name == "Bank" ? 6 : 1;
                    surfaces.Add(new Surface { filter = f, renderer = r, kind = kind,
                        root = union ? union.transform : outpost ? outpost.transform : f.transform,
                        split = f.GetComponent<JC.EnvironmentDetail.JcLampSurfaceChunks>() != null });
                }
            // JC | 2026-11-04 | 정적인 도로의 정점·삼각형은 변경될 때만 다시 읽어 주기적 갱신 비용을 줄입니다.
            bool rebuild = gridChanged || roads.Count != roadFilters.Count;
            if (!rebuild) for (int i = 0; i < roads.Count; i++)
                if (roads[i].filter != roadFilters[i] || roads[i].mesh != roadFilters[i].sharedMesh || roads[i].matrix != roadFilters[i].transform.localToWorldMatrix || roads[i].pavement != pavementFilters.Contains(roadFilters[i])) { rebuild = true; break; }
            if (rebuild)
            {
                roads.Clear(); roadHeights.Clear(); pavementHeights.Clear();
                foreach (var f in roadFilters)
                {
                    roads.Add(new Road { filter = f, mesh = f.sharedMesh, matrix = f.transform.localToWorldMatrix, pavement = pavementFilters.Contains(f) });
                    CacheRoad(f, pavementFilters.Contains(f));
                }
            }
            if (roadHeights.Count == 0) nextScan = Time.unscaledTime + .25f;
        }
        private void CacheRoad(MeshFilter filter, bool pavement)
        {
            var mesh = JC.BattleTesting.JcBattleSurfaceDressing.ReadableRoadMesh(filter.sharedMesh);
            if (!mesh || !mesh.isReadable) return;
            var positions = mesh.vertices; var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = filter.transform.TransformPoint(positions[indices[i]]);
                var b = filter.transform.TransformPoint(positions[indices[i + 1]]);
                var c = filter.transform.TransformPoint(positions[indices[i + 2]]);
                if (Mathf.Abs(Vector3.Cross(b - a, c - a).normalized.y) < .8f) continue;
                var min = grid.WorldToGrid(Vector3.Min(a, Vector3.Min(b, c)));
                var max = grid.WorldToGrid(Vector3.Max(a, Vector3.Max(b, c)));
                for (int x = min.x; x <= max.x; x++) for (int z = min.y; z <= max.y; z++)
                {
                    var key = new Vector2Int(x, z); var p = grid.GridToWorldCenter(key);
                    float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(denominator) < .000001f) continue;
                    float u = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.z - c.z)) / denominator;
                    float v = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.z - c.z)) / denominator;
                    if (u < -.001f || v < -.001f || u + v > 1.001f) continue;
                    float height = u * a.y + v * b.y + (1 - u - v) * c.y;
                    var heights = pavement ? pavementHeights : roadHeights;
                    if (!heights.TryGetValue(key, out float previous) || (pavement ? height > previous : height < previous)) heights[key] = height;
                }
            }
        }
        internal float GroundHeight(IReadOnlyList<InteractionOverlayCell> cells)
        {
            // JC | 2026-11-04 | 도로를 포함하거나 변을 맞댄 영역만 도로 평면에 둡니다. 대각선 근접은 제외합니다.
            float road = float.PositiveInfinity;
            foreach (var cell in cells)
                foreach (var offset in touchingOffsets)
                    if (roadHeights.TryGetValue(cell.Grid + offset, out float y)) road = Mathf.Min(road, y);
            if (!float.IsPositiveInfinity(road)) return road;
            float pavement = float.NegativeInfinity;
            foreach (var cell in cells)
            {
                if (!pavementHeights.TryGetValue(cell.Grid, out float y)) return grid.GetLandSurfaceY();
                pavement = Mathf.Max(pavement, y);
            }
            return float.IsNegativeInfinity(pavement) ? grid.GetLandSurfaceY() : pavement;
        }
        internal void Draw(CommandBuffer command, Camera camera, Material material, MaterialPropertyBlock properties)
        {
            int pass = material.FindPass("TriggerOccluderDepth"); if (pass < 0) return;
            // JC | 2026-11-04 | DrawMesh에는 자동 장면 컬링이 없어 화면 밖 건물은 미리 제외합니다.
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            foreach (var surface in surfaces)
            {
                var r = surface.renderer; var f = surface.filter;
                if (!r || !f || !f.sharedMesh || !r.gameObject.activeInHierarchy || (!surface.split && (!r.enabled || r.forceRenderingOff)) ||
                    (camera.cullingMask & (1 << r.gameObject.layer)) == 0 || !GeometryUtility.TestPlanesAABB(frustum, r.bounds)) continue;
                properties.Clear(); properties.SetFloat("_TriggerSurfaceKind", surface.kind);
                properties.SetMatrix("_TriggerSurfaceRoot", surface.root.worldToLocalMatrix);
                for (int sub = 0; sub < f.sharedMesh.subMeshCount; sub++) command.DrawMesh(f.sharedMesh, f.transform.localToWorldMatrix, material, sub, pass, properties);
            }
        }
    }
}
