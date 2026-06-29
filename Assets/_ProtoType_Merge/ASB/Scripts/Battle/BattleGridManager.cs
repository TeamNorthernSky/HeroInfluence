using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.BattleGrid
{
    [DisallowMultipleComponent]
    public class BattleGridManager : MonoBehaviour
    {
        private readonly Dictionary<Vector2Int, GridCell> cellsByCoords = new Dictionary<Vector2Int, GridCell>();
        private readonly Dictionary<BattleCharactor, GridCell> cellByUnit = new Dictionary<BattleCharactor, GridCell>();
        private readonly List<GridCell> _previewHighlightedCells = new List<GridCell>();
        private GridCell _previewMainTargetCell;
        [SerializeField] private Material TargetMaterial;
        [SerializeField] private Material AdditionalTargetMaterial;
        [SerializeField] private Material ClearMaterial;
        [SerializeField] private Material MainTargetHighlightMaterial;
        public static BattleGridManager Instance { get; private set; }

        public Material MainTargetHighlightMat => MainTargetHighlightMaterial;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[BattleGridManager] 중복 인스턴스가 감지되었습니다.");
            }

            Instance = this;
            RebuildCache();
            SetAllMaterial();
        }

        public void RebuildCache()
        {
            cellsByCoords.Clear();
            ClearUnitCache();
            var all = FindObjectsByType<GridCell>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                GridCell cell = all[i];
                if (cell == null)
                {
                    continue;
                }

                Vector2Int key = cell.Coords;
                if (cellsByCoords.ContainsKey(key))
                {
                    Debug.LogWarning($"[BattleGridManager] 중복 좌표 감지: {key}");
                    continue;
                }

                cellsByCoords.Add(key, cell);

                BattleCharactor occupant = cell.OccupyingUnit;
                if (occupant != null)
                {
                    cellByUnit[occupant] = cell;
                }
            }
        }

        private void ClearUnitCache()
        {
            cellByUnit.Clear();
        }

        public void RegisterUnitToCell(BattleCharactor unit, GridCell cell)
        {
            if (unit == null || cell == null)
            {
                return;
            }

            cellByUnit[unit] = cell;
        }

        public void UnregisterUnit(BattleCharactor unit)
        {
            if (unit == null)
            {
                return;
            }

            cellByUnit.Remove(unit);
        }

        public bool TryGetCell(Vector2Int coords, out GridCell cell)
        {
            return cellsByCoords.TryGetValue(coords, out cell);
        }

        /// <summary>
        /// 현재 등록된 절대 좌표 키 목록 스냅샷을 반환합니다.
        /// </summary>
        public List<Vector2Int> GetAllCoordsSnapshot()
        {
            return new List<Vector2Int>(cellsByCoords.Keys);
        }

        public IReadOnlyCollection<GridCell> AllCells => cellsByCoords.Values;

        public void SetAllHighlight()
        {
            foreach (GridCell cell in cellsByCoords.Values)
                cell.SetHighlight();
        }

        public void ClearAllHighlight()
        {
            foreach (GridCell cell in cellsByCoords.Values)
                cell.ClearHighlight();
        }

        public void SetAllMaterial()
        {
            //foreach (GridCell cell in cellsByCoords.Values)
            //    cell.SetMaterial(mat);

            foreach(GridCell cell in cellsByCoords.Values)
            {
                cell.SetTargetMaterial(TargetMaterial);
                cell.SetAdditionalTargetMaterial(AdditionalTargetMaterial);
                cell.SetTransparentTargetMaterial(ClearMaterial);
            }
        }


        public void ShowPreviewHighlight(SkillData skill, GridCell mainCell, List<GridCell> splashCells)
        {
            ClearPreviewHighlight();
            if (mainCell == null) return;

            bool isFullSide = skill != null && skill.classSkillTarget == 2;

            if (isFullSide)
            {
                mainCell.SetMainTargetHighlight();
                _previewHighlightedCells.Add(mainCell);
                if (splashCells != null)
                {
                    for (int i = 0; i < splashCells.Count; i++)
                    {
                        if (splashCells[i] == null) continue;
                        splashCells[i].SetMainTargetHighlight();
                        _previewHighlightedCells.Add(splashCells[i]);
                    }
                }
                return;
            }

            mainCell.SetMainTargetHighlight();
            _previewMainTargetCell = mainCell;

            if (splashCells != null)
            {
                for (int i = 0; i < splashCells.Count; i++)
                {
                    if (splashCells[i] == null) continue;
                    splashCells[i].SetAdditionalHighlight();
                    _previewHighlightedCells.Add(splashCells[i]);
                }
            }
        }

        public void ClearPreviewHighlight()
        {
            for (int i = 0; i < _previewHighlightedCells.Count; i++)
            {
                if (_previewHighlightedCells[i] != null)
                    _previewHighlightedCells[i].ClearHighlight();
            }
            _previewHighlightedCells.Clear();

            if (_previewMainTargetCell != null)
            {
                _previewMainTargetCell.ClearHighlight();
                _previewMainTargetCell = null;
            }
        }

        public GridCell FindCellByUnit(BattleCharactor unit)
        {
            if (unit == null)
            {
                return null;
            }

            if (unit.OccupiedCell != null)
            {
                return unit.OccupiedCell;
            }

            if (cellByUnit.TryGetValue(unit, out GridCell cachedCell) && cachedCell != null)
            {
                return cachedCell;
            }

            GridCell parentCell = unit.GetComponentInParent<GridCell>();
            if (parentCell != null)
            {
                cellByUnit[unit] = parentCell;
                return parentCell;
            }

            return null;
        }
    }
}
