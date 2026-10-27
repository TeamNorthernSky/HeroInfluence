// [CHEAT_ONLY / 다른 기능 재사용 금지 — 2026-10-27 사용자 요청]
// 이 파일은 1구역 플레이테스트 진입 시간을 줄이는 치트 전용입니다.
// 치트 착지용 검사·정사영 카메라·임시 셀 표시입니다. 정상 이동 경로 탐색, 이동범위 렌더러, 카메라 기능으로 재사용하지 마세요.
// 의존 방향은 치트 → 기존 게임 API만 허용합니다. 일반 게임 코드는 이 폴더를 참조하지 마세요.
// 공통 기능이 필요하면 기존 정식 시스템을 사용하세요. 치트 구현을 공용 코드로 승격하거나 복사하지 마세요.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.DebugTools
{
    public sealed partial class JcZone1WarpCheat
    {
        readonly HashSet<Vector2Int> excluded = new HashSet<Vector2Int>();
        readonly HashSet<Vector2Int> safeCells = new HashSet<Vector2Int>();
        readonly List<Vector2Int> gateCells = new List<Vector2Int>();
        GameObject cellRoot;
        Mesh cellMesh;
        Material cellMaterial;
        Vector3 savedCameraPosition;
        Quaternion savedCameraRotation;
        Rect savedCameraRect;
        bool savedOrtho, cameraCaptured;
        float savedSize, savedFar;

        void CaptureCamera()
        {
            savedCameraPosition = viewCamera.transform.position; savedCameraRotation = viewCamera.transform.rotation;
            savedCameraRect = viewCamera.rect; savedOrtho = viewCamera.orthographic;
            savedSize = viewCamera.orthographicSize; savedFar = viewCamera.farClipPlane;
            followerEnabled = follower != null && follower.enabled;
            cameraCaptured = true;
        }

        void ShowOverview()
        {
            if (follower != null) follower.enabled = false;
            var min = grid.GridToWorldCenter(zoneBounds.min);
            var max = grid.GridToWorldCenter(zoneBounds.max - Vector2Int.one);
            var center = (min + max) * .5f;
            float width = max.x - min.x + grid.CellSize * 3, depth = max.z - min.z + grid.CellSize * 3;
            viewCamera.rect = new Rect(.29f, .03f, .70f, .94f);
            viewCamera.orthographic = true;
            float aspect = Mathf.Max(.1f, Screen.width * .70f / (Screen.height * .94f));
            viewCamera.orthographicSize = Mathf.Max(depth * .5f, width * .5f / aspect);
            float height = Mathf.Max(200f, width + depth);
            viewCamera.farClipPlane = Mathf.Max(savedFar, height * 3);
            viewCamera.transform.SetPositionAndRotation(center + Vector3.up * height, Quaternion.Euler(90, 0, 0));
        }

        void RestoreCamera(bool moved)
        {
            if (!cameraCaptured || viewCamera == null) return;
            viewCamera.rect = savedCameraRect; viewCamera.orthographic = savedOrtho;
            viewCamera.orthographicSize = savedSize; viewCamera.farClipPlane = savedFar;
            viewCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
            if (follower != null)
            {
                follower.enabled = followerEnabled;
                if (moved) follower.SnapToFollowTarget();
            }
            if (!active) cameraCaptured = false;
        }

        void AddAround(Vector2Int p, int radius = 1)
        {
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++) excluded.Add(p + new Vector2Int(x, y));
        }

        void BuildCells()
        {
            ClearCells(); excluded.Clear();
            foreach (var e in FindObjectsByType<MainEventObject>(FindObjectsSortMode.None))
                AddAround(e.GetCurrentGrid(grid), e.InteractionRadius);
            foreach (var e in FindObjectsByType<SubEventObject>(FindObjectsSortMode.None)) AddAround(e.GetCurrentGrid(grid));
            foreach (var e in FindObjectsByType<WorldEventObject>(FindObjectsSortMode.None))
                foreach (var cell in e.GetInteractionCells()) excluded.Add(cell);
            foreach (var gate in FindObjectsByType<GateRuntimeController>(FindObjectsSortMode.None))
            {
                gateCells.Clear(); gate.CollectTeleportCells(gateCells);
                foreach (var cell in gateCells) excluded.Add(cell);
            }
            // 반대 분기 Disable 후 생성되는 대체 적도 같은 위치의 1셀 범위이므로 위 예약에 포함됩니다.
            foreach (var ground in level.GroundTilePlacements)
            {
                var cell = ground.GridPosition + anchor;
                if (IsSafe(cell)) safeCells.Add(cell);
            }
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (var cell in safeCells)
            {
                Vector3 p = grid.GridToWorldCenter(cell); p.y = grid.GetCellSurfaceY(cell) + .08f;
                float half = grid.CellSize * .43f;
                int i = vertices.Count;
                vertices.Add(p + new Vector3(-half, 0, -half)); vertices.Add(p + new Vector3(-half, 0, half));
                vertices.Add(p + new Vector3(half, 0, half)); vertices.Add(p + new Vector3(half, 0, -half));
                triangles.Add(i); triangles.Add(i+1); triangles.Add(i+2); triangles.Add(i); triangles.Add(i+2); triangles.Add(i+3);
            }
            cellMesh = new Mesh { name = "JC 치트 착지 가능 셀", indexFormat = IndexFormat.UInt32 };
            cellMesh.SetVertices(vertices); cellMesh.SetTriangles(triangles, 0); cellMesh.RecalculateBounds();
            var shader = Resources.Load<Shader>("JcZone1WarpCells");
            if (shader == null) throw new System.InvalidOperationException("치트 셀 표시 셰이더가 없습니다.");
            cellMaterial = new Material(shader) { name = "JC 치트 착지 셀" };
            cellRoot = new GameObject("JC_WarpDestinationCells", typeof(MeshFilter), typeof(MeshRenderer));
            cellRoot.transform.SetParent(transform, false);
            cellRoot.GetComponent<MeshFilter>().sharedMesh = cellMesh;
            var renderer = cellRoot.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = cellMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        bool IsSafe(Vector2Int cell)
        {
            return zoneBounds.Contains(cell) && !excluded.Contains(cell) && grid.CanOccupyCell(cell, party.transform, true)
                && grid.GetGridEnemyEncounterZoneState(cell, out _) == EnemyEncounterZoneState.None
                && !grid.TryGetAdjacentItemGrid(cell, out _) && !grid.TryGetAdjacentEventGrid(cell, out _)
                && !grid.TryGetAdjacentWorldEventGrid(cell, out _) && !grid.TryGetMainEventAtInteractionCell(cell, out _)
                && !grid.TryGetOutpostAtInteractionCell(cell, out _) && !grid.TryGetVillainUnionAtInteractionCell(cell, out _)
                && !grid.TryGetAdjacentHeroUnionObject(cell, out _);
        }

        bool TryPickCell(Vector2 screen, out Vector2Int cell)
        {
            cell = default;
            if (viewCamera == null || !viewCamera.pixelRect.Contains(screen)) return false;
            Ray ray = viewCamera.ScreenPointToRay(screen);
            // 전체 보기 카메라는 수직 정사영이므로 건물 Collider와 관계없이 바닥 좌표를 고를 수 있습니다.
            if (!new Plane(Vector3.up, Vector3.up * grid.GetLandSurfaceY()).Raycast(ray, out float distance)) return false;
            cell = grid.WorldToGrid(ray.GetPoint(distance));
            return safeCells.Contains(cell);
        }

        void UpdateHover()
        {
            if (cellMaterial == null) return;
            if (TryPickCell(Input.mousePosition, out var cell))
            {
                Vector3 world = grid.GridToWorldCenter(cell);
                cellMaterial.SetVector("_Hover", new Vector4(world.x, world.z, grid.CellSize * .49f, 1));
            }
            else cellMaterial.SetVector("_Hover", Vector4.zero);
        }

        void ClearCells()
        {
            if (cellRoot != null) Destroy(cellRoot);
            if (cellMesh != null) Destroy(cellMesh);
            if (cellMaterial != null) Destroy(cellMaterial);
            cellRoot = null; cellMesh = null; cellMaterial = null; safeCells.Clear();
        }
    }
}
