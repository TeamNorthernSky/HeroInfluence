using UnityEngine;

namespace ASB.Work.BattleGrid
{
    [DisallowMultipleComponent]
    public class GridCell : MonoBehaviour
    {
        [SerializeField] private Vector2Int coords;

        [SerializeField] private BattleCharactor occupyingUnit;
        [Header("Visuals (Material Swap)")]
        [SerializeField] private Renderer cellRenderer;
        [SerializeField] private Material transparentMat;
        [SerializeField] private Material redHighlightMat;

        public Vector2Int Coords => coords;
        public bool IsFrontRow => coords.x == 1 || coords.x == 2;

        public BattleCharactor OccupyingUnit => occupyingUnit;

        /// <summary>살아있든 시체든 점유자가 있으면 true. 살아있는 다른 유닛의 진입/스폰은 막습니다.</summary>
        public bool IsBlockedForLivingEntry => occupyingUnit != null;

        private void Awake()
        {
            if (!TryParseCoordsFromName(gameObject.name, out coords))
            {
                Debug.LogWarning($"[GridCell] 좌표 파싱 실패: {gameObject.name} (예: Grid_1_2)");
            }
        }

        public void SetOccupyingUnit(BattleCharactor unit)
        {
            occupyingUnit = unit;
        }

        /// <summary>
        /// 전투 보드 절대 좌표계를 강제 적용합니다.
        /// 외부 호출부 호환을 위해 Coords 프로퍼티는 그대로 사용합니다.
        /// </summary>
        public void SetCoords(Vector2Int absoluteCoords)
        {
            coords = absoluteCoords;
        }

        public void ClearIfOccupying(BattleCharactor unit)
        {
            if (occupyingUnit == unit)
            {
                occupyingUnit = null;
            }
        }

        /// <summary>
        /// Prototype 선택 기능: 필요 시 점유 여부에 따라 Collider 활성 상태를 갱신합니다.
        /// 기본 흐름에서는 호출하지 않습니다.
        /// </summary>
        public void RefreshColliderState(bool occupiedOnly)
        {
            if (!occupiedOnly)
            {
                return;
            }

            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = occupyingUnit != null;
            }
        }

        public void SetHighlight()
        {
            if (cellRenderer == null || redHighlightMat == null)
            {
                return;
            }

            cellRenderer.sharedMaterial = redHighlightMat;
        }

        public void ClearHighlight()
        {
            if (cellRenderer == null || transparentMat == null)
            {
                return;
            }

            cellRenderer.sharedMaterial = transparentMat;
        }

        private static bool TryParseCoordsFromName(string objectName, out Vector2Int parsed)
        {
            parsed = Vector2Int.zero;
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            string[] parts = objectName.Split('_');
            if (parts.Length < 3)
            {
                return false;
            }

            if (!int.TryParse(parts[1], out int x) || !int.TryParse(parts[2], out int y))
            {
                return false;
            }

            parsed = new Vector2Int(x, y);
            return true;
        }
    }
}
