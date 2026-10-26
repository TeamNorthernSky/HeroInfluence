using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.Indicators
{
    /// <summary>기존 게이트의 이동 판정을 읽어 진입 셀만 표시합니다. 해금·이동·저장 상태는 변경하지 않습니다.</summary>
    [DisallowMultipleComponent]
    public sealed class JcBridgeEntryIndicator : MonoBehaviour
    {
        [SerializeField, Tooltip("표시할 기존 게이트 식별자입니다. 이동 판정과 같은 게이트 쌍을 사용합니다.")]
        private string gateId = "gate_001";
        [SerializeField, Tooltip("인디케이터를 표시할 출발 구역입니다. 반대편 복귀 지점은 표시하지 않습니다.")]
        private string sourceZoneId = "zone_001";
        [SerializeField, Tooltip("씬에 배치된 출발 게이트입니다. 레벨 재생성으로 없어지면 구역 경계에서 다시 찾습니다.")]
        private GateRuntimeController sourceGate;
        [SerializeField, Tooltip("전투씬의 기존 노란 현재 턴 프리팹입니다. 원본 프리팹과 프로필은 변경하지 않습니다.")]
        private BattleCellBorderVisual borderPrefab;
        [SerializeField, Tooltip("기존 전투 테두리 프로필입니다. 실행 중 사본의 크기만 탐사 셀에 맞춥니다.")]
        private BattleCellBorderProfile borderProfile;
        [SerializeField, Tooltip("전투 셀의 기존 투명 채움 재질입니다. 흰 화살표에도 같은 셰이더를 사용합니다.")]
        private Material arrowMaterial;
        [SerializeField, Range(.1f, 1f), Tooltip("원래 화살표 묶음 대비 배율입니다. 0.8은 승인된 80% 크기로 안쪽 테두리와 여백을 둡니다.")]
        private float arrowScale = .8f;
        [SerializeField, Min(.3f), Tooltip("흰 화살표 세 개가 왼쪽부터 밝아지는 한 주기의 시간(초)입니다.")]
        private float cycleSeconds = 1.2f;
        [SerializeField, Range(0, 1), Tooltip("점멸 사이에도 보이는 흰 화살표의 최소 불투명도입니다.")]
        private float idleAlpha = .16f;

        private sealed class CellView
        {
            public Vector2Int cell;
            public GameObject root;
            public BattleCellBorderVisual border;
            public readonly MeshRenderer[] arrows = new MeshRenderer[3];
        }

        private readonly List<Vector2Int> cells = new List<Vector2Int>();
        private readonly List<CellView> views = new List<CellView>();
        private GateRuntimeController destinationGate;
        private GridManager grid;
        private PartyGridMover party;
        private BattleCellBorderConfiguration configuration;
        private MaterialPropertyBlock arrowProperties;
        private Mesh arrowMesh;
        private float nextRefresh;
        private float configuredSize = -1;
        private float configuredArrowScale = -1;

        private void OnEnable() { nextRefresh = 0; }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextRefresh)
            {
                RefreshPresentation();
                nextRefresh = Time.unscaledTime + .2f;
            }
            Render(Time.unscaledTime);
        }

        /// <summary>게이트 재생성·로드 후에도 기존 실제 상태로 표시를 다시 구성합니다.</summary>
        public void RefreshPresentation()
        {
            grid = Game.Grid != null && Game.Grid.gameObject.scene == gameObject.scene
                ? Game.Grid : FindInScene<GridManager>();
            var registry = FindInScene<PartyRegistry>();
            party = registry != null ? registry.PlayerParty : null;
            ResolveGates();
            cells.Clear();
            if (sourceGate != null) sourceGate.CollectTeleportCells(cells);
            if (grid == null || borderPrefab == null || borderProfile == null || arrowMaterial == null)
            {
                ClearViews();
                return;
            }

            bool rebuild = views.Count != cells.Count || !Mathf.Approximately(configuredSize, grid.CellSize)
                || !Mathf.Approximately(configuredArrowScale, arrowScale);
            for (int i = 0; !rebuild && i < cells.Count; i++) rebuild = views[i].cell != cells[i];
            if (rebuild) BuildViews();
            for (int i = 0; i < views.Count; i++)
            {
                bool available = sourceGate != null && sourceGate.isActiveAndEnabled && sourceGate.IsOpen
                    && destinationGate != null && destinationGate.isActiveAndEnabled && destinationGate.IsOpen
                    && destinationGate.TryGetTeleportCellByIndex(i, out var target)
                    && grid.CanOccupyCell(target, party != null ? party.transform : null, true);
                views[i].root.SetActive(available);
            }
            Render(Time.unscaledTime);
        }

        /// <summary>KJ 카메라 연출에서 사용할 출발 진입 셀들의 중심입니다. 게이트가 닫혀 있어도 조회할 수 있습니다.</summary>
        public bool TryGetFocusPosition(out Vector3 position)
        {
            position = Vector3.zero;
            if (grid == null || cells.Count == 0) RefreshPresentation();
            if (grid == null || cells.Count == 0) return false;
            foreach (var cell in cells)
            {
                var point = grid.GridToWorldCenter(cell);
                point.y = grid.GetCellSurfaceY(cell);
                position += point;
            }
            position /= cells.Count;
            return true;
        }

        private T FindInScene<T>() where T : Component
        {
            foreach (var item in FindObjectsByType<T>(FindObjectsSortMode.None))
                if (item.gameObject.scene == gameObject.scene) return item;
            return null;
        }

        private void ResolveGates()
        {
            destinationGate = null;
            var gates = FindObjectsByType<GateRuntimeController>(FindObjectsSortMode.None);
            if (sourceGate == null || sourceGate.gameObject.scene != gameObject.scene || !sourceGate.isActiveAndEnabled || sourceGate.GateId != gateId)
            {
                sourceGate = null;
                var layout = FindInScene<LevelZoneLayoutLoader>();
                var candidateCells = new List<Vector2Int>();
                foreach (var gate in gates)
                {
                    if (gate.gameObject.scene != gameObject.scene || gate.GateId != gateId || !gate.isActiveAndEnabled) continue;
                    candidateCells.Clear(); gate.CollectTeleportCells(candidateCells);
                    if (layout == null || layout.LoadedZones == null || candidateCells.Count == 0) continue;
                    foreach (var zone in layout.LoadedZones)
                        if (zone.ZoneId == sourceZoneId && new RectInt(zone.Anchor, zone.Size).Contains(candidateCells[0]))
                        { sourceGate = gate; break; }
                    if (sourceGate != null) break;
                }
            }
            if (sourceGate == null) return;
            int matches = 0;
            foreach (var gate in gates)
            {
                if (gate == sourceGate || gate.gameObject.scene != gameObject.scene || !gate.isActiveAndEnabled
                    || gate.GateId != gateId || !gate.IsOpen) continue;
                destinationGate = gate; matches++;
            }
            // 실제 GateTeleportController와 동일하게 열린 반대편이 정확히 한 개일 때만 표시합니다.
            if (matches != 1) destinationGate = null;
        }

        private void BuildViews()
        {
            ClearViews();
            configuration = borderProfile.settings.Copy();
            var shape = configuration.currentTurn.shape;
            float ratio = grid.CellSize / Mathf.Max(.001f, shape.size.x);
            shape.size = Vector2.one * grid.CellSize;
            shape.padding *= ratio; shape.cornerRadius *= ratio;
            shape.outerWidth *= ratio; shape.mainWidth *= ratio; shape.innerWidth *= ratio;
            shape.depth *= ratio; shape.floatHeight *= ratio;
            configuration.currentTurn.shape = shape;
            configuration.bobAmplitude = 0;
            configuredSize = grid.CellSize; configuredArrowScale = arrowScale;
            arrowMesh = BuildArrowMesh();
            foreach (var cell in cells)
            {
                var view = new CellView { cell = cell, root = new GameObject("다리 진입 " + cell) };
                view.root.hideFlags = HideFlags.DontSave;
                view.root.transform.SetParent(transform, false);
                view.border = Instantiate(borderPrefab, view.root.transform);
                view.border.SetDriven(true);
                view.border.transform.localPosition = Vector3.zero;
                view.border.transform.localRotation = Quaternion.identity;
                view.border.transform.localScale = Vector3.one;
                float interiorSize = grid.CellSize - 2 * (shape.padding + shape.outerWidth + shape.mainWidth + shape.innerWidth);
                // 커스텀 테두리가 두꺼워져도 화살표 묶음이 안쪽 영역의 80%를 넘지 않습니다.
                float scale = Mathf.Min(arrowScale, Mathf.Max(.01f, interiorSize * .8f / (grid.CellSize * .56f)));
                for (int i = 0; i < 3; i++)
                {
                    var arrow = new GameObject("흰 화살표 " + (i + 1));
                    arrow.transform.SetParent(view.root.transform, false);
                    arrow.transform.localPosition = new Vector3((i - 1) * .19f * grid.CellSize * scale, shape.depth + .008f, 0);
                    arrow.transform.localScale = Vector3.one * (grid.CellSize * scale);
                    arrow.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
                    var renderer = arrow.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = arrowMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    view.arrows[i] = renderer;
                }
                views.Add(view);
            }
        }

        private void Render(float clock)
        {
            if (configuration == null || grid == null) return;
            if (arrowProperties == null) arrowProperties = new MaterialPropertyBlock();
            foreach (var view in views)
            {
                if (!view.root.activeSelf) continue;
                var position = grid.GridToWorldCenter(view.cell);
                float ground = grid.GetCellSurfaceY(view.cell);
                position.y = ground + configuration.currentTurn.shape.floatHeight;
                view.root.transform.position = position;
                view.root.transform.rotation = Quaternion.identity;
                view.border.RenderProfile(configuration, BattleCellVisualState.CurrentTurn, 1, clock, ground);
                for (int i = 0; i < 3; i++)
                {
                    float phase = Mathf.Repeat(clock / Mathf.Max(.3f, cycleSeconds) - i * .22f, 1);
                    float pulse = phase < .3f ? Mathf.Pow(Mathf.Sin(phase / .3f * Mathf.PI), .65f) : 0;
                    arrowProperties.Clear();
                    arrowProperties.SetColor("_BaseColor", new Color(1, 1, 1, Mathf.Lerp(idleAlpha, 1, pulse)));
                    arrowProperties.SetFloat("_SweepEnabled", 0);
                    arrowProperties.SetFloat("_IsFill", 0);
                    view.arrows[i].SetPropertyBlock(arrowProperties);
                }
            }
        }

        private static Mesh BuildArrowMesh()
        {
            var mesh = new Mesh { name = "다리 흰 화살촉", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] {
                new Vector3(-.09f,0,.18f), new Vector3(.03f,0,0), new Vector3(-.09f,0,-.18f),
                new Vector3(-.03f,0,-.18f), new Vector3(.09f,0,0), new Vector3(-.03f,0,.18f)
            };
            mesh.triangles = new[] { 0,1,5, 1,4,5, 1,2,3, 1,3,4 };
            mesh.normals = new[] { Vector3.up,Vector3.up,Vector3.up,Vector3.up,Vector3.up,Vector3.up };
            mesh.RecalculateBounds(); return mesh;
        }

        private void ClearViews()
        {
            foreach (var view in views) if (view.root != null)
            {
                view.root.SetActive(false);
                if (Application.isPlaying) Destroy(view.root); else DestroyImmediate(view.root);
            }
            views.Clear();
            if (arrowMesh != null) { if (Application.isPlaying) Destroy(arrowMesh); else DestroyImmediate(arrowMesh); }
            arrowMesh = null; configuration = null; configuredSize = -1;
        }

        private void OnDisable() { ClearViews(); }
    }
}
