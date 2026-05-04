using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.BattleGrid
{
    [DisallowMultipleComponent]
    public class GridManager : MonoBehaviour
    {
        private readonly Dictionary<Vector2Int, GridCell> cellsByCoords = new Dictionary<Vector2Int, GridCell>();

        public static GridManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GridManager] 중복 인스턴스가 감지되었습니다.");
            }

            Instance = this;
            RebuildCache();
        }

        public void RebuildCache()
        {
            cellsByCoords.Clear();
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
                    Debug.LogWarning($"[GridManager] 중복 좌표 감지: {key}");
                    continue;
                }

                cellsByCoords.Add(key, cell);
            }
        }

        public bool TryGetCell(Vector2Int coords, out GridCell cell)
        {
            return cellsByCoords.TryGetValue(coords, out cell);
        }

        public GridCell FindCellByUnit(BattleCharactor unit)
        {
            if (unit == null)
            {
                return null;
            }

            foreach (var kv in cellsByCoords)
            {
                if (kv.Value != null && kv.Value.OccupyingUnit == unit)
                {
                    return kv.Value;
                }
            }

            return null;
        }
    }
}
