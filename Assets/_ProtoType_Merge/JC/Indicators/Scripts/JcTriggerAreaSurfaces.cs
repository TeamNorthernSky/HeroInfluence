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
        // JC | 2026-11-10 | 전체 수집과 재분류는 그대로 수행하되 배열과 Surface 객체 저장 공간을 재사용합니다.
        private readonly List<GameObject> scanRoots = new List<GameObject>();
        private readonly List<MeshFilter> scanFilters = new List<MeshFilter>();
        private readonly List<Surface> surfacePool = new List<Surface>();
        private readonly List<Surface> surfaces = new List<Surface>();
        private readonly Dictionary<Vector2Int, float> roadHeights = new Dictionary<Vector2Int, float>();
        // JC | 2026-11-10 | 공유 메시의 이름 판정만 한 스캔 안에서 재사용합니다. 배치/소속/활성 상태는 공유하지 않습니다.
        private struct MeshNameInfo { internal bool background, association, bank; }
        private readonly Dictionary<Mesh, MeshNameInfo> meshNames = new Dictionary<Mesh, MeshNameInfo>();
        // JC | 2026-11-10 | 현재 이름은 매번 읽고 같은 이름의 도로/인도 판정만 재사용합니다. 두 스캔의 저장 공간을 교대해 오래된 이름이 누적되지 않습니다.
        private struct ObjectNameInfo { internal bool road, pavement; }
        private Dictionary<string, ObjectNameInfo> previousObjectNames = new Dictionary<string, ObjectNameInfo>(System.StringComparer.Ordinal);
        private Dictionary<string, ObjectNameInfo> currentObjectNames = new Dictionary<string, ObjectNameInfo>(System.StringComparer.Ordinal);
        private System.Globalization.CompareInfo objectNameComparison;
        private float nextScan;
        private GridManager grid;

        internal void Refresh(Scene scene, GridManager value)
        {
            if (grid == value && Time.unscaledTime < nextScan) return;
            bool gridChanged = grid != value || cellSize != value.CellSize || gridOrigin != value.GridToWorldCenter(Vector2Int.zero);
            grid = value; cellSize = grid.CellSize; gridOrigin = grid.GridToWorldCenter(Vector2Int.zero); nextScan = Time.unscaledTime + 2;
            // JC | 2026-11-10 | 다음 스캔에서는 이름 변경도 기존 주기에 맞춰 다시 반영합니다.
            meshNames.Clear();
            BeginObjectNameScan();
            surfaces.Clear(); roadFilters.Clear(); pavementFilters.Clear();
            scanRoots.Clear(); scene.GetRootGameObjects(scanRoots);
            foreach (var root in scanRoots)
            {
                scanFilters.Clear(); root.GetComponentsInChildren<MeshFilter>(true, scanFilters);
                foreach (var f in scanFilters)
                {
                    var mesh = f.sharedMesh;
                    if (!mesh || !f.gameObject.activeInHierarchy) continue;
                    // JC | 2026-11-10 | Unity 이름 조회로 생기는 문자열을 같은 항목 분류 안에서만 재사용합니다.
                    string objectName = f.name;
                    if (objectName == "JC_LampSurface" ||
                        f.GetComponentInParent<JcTriggerAreaIndicator>() || f.GetComponentInParent<JcMovementIndicatorController>()) continue;
                    var r = f.GetComponent<MeshRenderer>(); if (!r) continue;
                    // JC | 2026-11-10 | 이름이 바뀌면 기존 비교식으로 다시 분류하며 도로 등록 순서와 중복 등록 여부는 유지합니다.
                    var objectInfo = GetObjectNameInfo(objectName);
                    if (objectInfo.road) roadFilters.Add(f);
                    bool pavement = objectInfo.pavement;
                    // JC | 2026-11-04 | 인도 상면도 같은 캐시에서 읽어 영역 전체가 인도일 때 높이를 올립니다.
                    if (pavement) { pavementFilters.Add(f); roadFilters.Add(f); }
                    var union = f.GetComponentInParent<HeroUnionUnit>();
                    // JC | 2026-11-10 | 협회가 있으면 건물/루트 판정에서 전초기지보다 우선하므로 사용하지 않을 조회만 생략합니다.
                    var outpost = union ? null : f.GetComponentInParent<Outpost>();
                    // JC | 2026-11-10 | 인도는 건물 판정 없이 채택되므로 건물 전용 조회만 생략합니다. 소속 루트 조회는 유지합니다.
                    MeshNameInfo info = default;
                    if (!pavement)
                    {
                        info = GetMeshNameInfo(mesh);
                        // JC | 2026-11-10 | BG 메시이면 OR 조건이 이미 충족되므로 Fade 부모 조회를 생략하고 기존 높이 조건은 유지합니다.
                        bool building = union || outpost || ((info.background || f.GetComponentInParent<PartyOcclusionFadeTarget>()) && r.bounds.size.y > .5f);
                        if (!building) continue;
                    }
                    if (f.GetComponentInParent<PartyGridMover>() || f.GetComponentInParent<EnemyGridMover>() || f.GetComponentInParent<TutorialEnemyObject>()) continue;
                    int kind = pavement ? 2 : info.association ? 5 : info.bank ? 6 : 1;
                    // JC | 2026-11-10 | 분류 결과는 매번 갱신하여 활성/부모/메시 변경의 기존 반영 시점을 유지합니다.
                    int index = surfaces.Count;
                    if (index == surfacePool.Count) surfacePool.Add(new Surface());
                    var surface = surfacePool[index];
                    surface.filter = f; surface.renderer = r; surface.kind = kind;
                    surface.root = union ? union.transform : outpost ? outpost.transform : f.transform;
                    surface.split = f.GetComponent<JC.EnvironmentDetail.JcLampSurfaceChunks>() != null;
                    surfaces.Add(surface);
                }
            }
            // JC | 2026-11-10 | 감소한 항목의 객체는 재사용하되 이전 씬 객체 참조는 남기지 않습니다.
            for (int i = surfaces.Count; i < surfacePool.Count; i++)
            { surfacePool[i].filter = null; surfacePool[i].renderer = null; surfacePool[i].root = null; }
            // JC | 2026-11-04 | 정적인 도로의 정점·삼각형은 변경될 때만 다시 읽어 주기적 갱신 비용을 줄입니다.
            bool rebuild = gridChanged || roads.Count != roadFilters.Count;
            if (!rebuild) for (int i = 0; i < roads.Count; i++)
                if (roads[i].filter != roadFilters[i] || roads[i].mesh != roadFilters[i].sharedMesh || roads[i].matrix != roadFilters[i].transform.localToWorldMatrix || roads[i].pavement != pavementFilters.Contains(roadFilters[i])) { rebuild = true; break; }
            if (rebuild)
            {
                roads.Clear(); roadHeights.Clear(); pavementHeights.Clear();
                foreach (var f in roadFilters)
                {
                    // JC | 2026-11-04 | 트리거와 도로·건물의 깊이를 비교해 가려진 부분의 불투명도를 따로 적용합니다.
                    roads.Add(new Road { filter = f, mesh = f.sharedMesh, matrix = f.transform.localToWorldMatrix, pavement = pavementFilters.Contains(f) });
                    CacheRoad(f, pavementFilters.Contains(f));
                }
            }
            if (roadHeights.Count == 0) nextScan = Time.unscaledTime + .25f;
        }
        // JC | 2026-11-10 | 기본 StartsWith/EndsWith의 문화권 비교를 유지하며 비교 기준 변경 시 이전 결과를 폐기합니다.
        private void BeginObjectNameScan()
        {
            var comparison = System.Globalization.CultureInfo.CurrentCulture.CompareInfo;
            if (!comparison.Equals(objectNameComparison))
            {
                previousObjectNames.Clear(); currentObjectNames.Clear(); objectNameComparison = comparison;
            }
            var reusable = previousObjectNames;
            previousObjectNames = currentObjectNames; currentObjectNames = reusable;
            currentObjectNames.Clear();
        }
        // JC | 2026-11-10 | 정확히 같은 문자열만 공유합니다. Mesh/부모/활성 상태에는 이 캐시를 사용하지 않습니다.
        private ObjectNameInfo GetObjectNameInfo(string name)
        {
            if (currentObjectNames.TryGetValue(name, out var info)) return info;
            if (!previousObjectNames.TryGetValue(name, out info))
            {
                bool tile = name.StartsWith("TileChunk_") || name.StartsWith("Detail_TileChunk_");
                info = new ObjectNameInfo {
                    road = tile && name.Contains("_Road_"),
                    pavement = (tile && name.EndsWith("_tilemap_01_RaisedCube")) || name.Contains("연속 연석") || name.Contains("보도 외곽")
                };
            }
            currentObjectNames.Add(name, info);
            return info;
        }
        // JC | 2026-11-10 | 기존 문자열 비교 조건을 유지하고 동일 메시의 이름 조회·검사를 한 번만 수행합니다.
        private MeshNameInfo GetMeshNameInfo(Mesh mesh)
        {
            if (meshNames.TryGetValue(mesh, out var info)) return info;
            string name = mesh.name;
            info = new MeshNameInfo { background = name.StartsWith("BG"), association = name.Contains("HeroAssociation"), bank = name == "Bank" };
            meshNames.Add(mesh, info);
            return info;
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
        // JC | 2026-11-09 | 단차 예외 대상만 실제 인도 상면을 읽으며 일반 영역의 평면 높이 정책은 그대로 둡니다.
        internal float CellHeight(Vector2Int cell, float ground)
        { return pavementHeights.TryGetValue(cell, out float pavement) ? Mathf.Max(ground, pavement) : ground; }
        internal float GroundHeight(IReadOnlyList<InteractionOverlayCell> cells)
        {
            // JC | 2026-11-06 | 인접 도로를 제외하고 실제 트리거 셀의 과반(50% 초과)이 도로일 때만 도로 평면을 사용합니다.
            float road = float.PositiveInfinity;
            int roadCells = 0;
            foreach (var cell in cells)
                if (roadHeights.TryGetValue(cell.Grid, out float y)) { roadCells++; road = Mathf.Min(road, y); }
            if (roadCells * 2 > cells.Count) return road;
            // JC | 2026-11-06 | 도로가 과반이 아닌 혼합 영역도 보도 높이를 사용합니다. 도로 한 셀 때문에 지면 기본값으로 떨어지지 않습니다.
            float pavement = float.NegativeInfinity;
            foreach (var cell in cells)
            {
                if (pavementHeights.TryGetValue(cell.Grid, out float y)) pavement = Mathf.Max(pavement, y);
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
