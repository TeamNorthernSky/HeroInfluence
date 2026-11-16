// [JC 테스트 씬 전용 / 기준 0705af74]
// 원본: Assets/_ProtoType_Merge/ASB/Scripts/Battle/BattleGridManager.cs
// 원본 객체 BattleGridManager → JcBattleGridManager. 동일 이름 함수는 원본 함수와 1:1 대응합니다.
// 차이: JC 전투 흐름/입력을 참조하며 원본 씬·컴포넌트·데이터를 수정하지 않습니다.
// 원본 BattleGridManager는 공용 GridCell/BattleCharactor 등록용으로 유지합니다. JC 인스턴스의 원본 타일 표시만 끄고 이 컴포넌트가 표시를 담당합니다.
using System.Collections.Generic;
using UnityEngine;
using ASB.Work.Battle.SkillExecution;

namespace ASB.Work.BattleGrid
{
    [DisallowMultipleComponent]
    public class JcBattleGridManager : MonoBehaviour
    {
        private readonly Dictionary<Vector2Int, GridCell> cellsByCoords = new Dictionary<Vector2Int, GridCell>();
        private readonly Dictionary<BattleCharactor, GridCell> cellByUnit = new Dictionary<BattleCharactor, GridCell>();
        private readonly List<GridCell> _previewHighlightedCells = new List<GridCell>();
        private GridCell _previewMainTargetCell;
        // JC | 2026-11-16 | 본게임과 동일한 아군 회복/적 공격 색 구분 및 정보 호버 표시 문맥입니다.
        private SkillData previewSkill;
        private BattleCharactor previewCaster;
        private UnityEngine.Object infoHoverOwner;
        private BattleCharactor infoHoverTarget;
        public void SetInfoHover(UnityEngine.Object owner, BattleCharactor target)
        {
            if (owner == null) return;
            if (target != null && (target.IsPlayer || target.IsDead || target.gameObject.scene != gameObject.scene)) target = null;
            if (target == null && infoHoverOwner != owner) return;
            if (infoHoverOwner == owner && infoHoverTarget == target) return;
            infoHoverOwner = target != null ? owner : null;
            infoHoverTarget = target;
            RefreshTilePresentation();
        }
        private GridCell InfoHoverCell()
        {
            if (infoHoverOwner == null || infoHoverTarget == null || infoHoverTarget.IsDead ||
                !infoHoverTarget.gameObject.activeInHierarchy || selectingTargets ||
                (flowManager != null && (flowManager.IsEndingBattle || flowManager.IsActionInProgress))) return null;
            return FindCellByUnit(infoHoverTarget);
        }
        [Tooltip("직접 선택 가능한 셀의 보라색 재질입니다.")]
        [SerializeField] private Material TargetMaterial;
        [Tooltip("기존 범위 미리보기의 추가 대상 셀 재질입니다.")]
        [SerializeField] private Material AdditionalTargetMaterial;
        [Tooltip("선택하지 않은 셀의 기본 재질입니다.")]
        [SerializeField] private Material ClearMaterial;
        [Tooltip("실행할 공격 범위를 표시하는 빨간색 재질입니다.")]
        [SerializeField] private Material MainTargetHighlightMaterial;
        [Header("Battle Tile Presentation")]
        [Tooltip("개편 전투씬에서만 켭니다. 노랑 현재 턴, 보라 선택 가능, 빨강 확정 범위, 검정 선택 불가를 각각 유지합니다.")]
        [SerializeField] private bool useBattleTilePresentation;
        [Tooltip("현재 턴·행동 진행·전투 종료를 읽는 전투 흐름입니다. 전투 진행을 변경하지 않습니다.")]
        [SerializeField] private JcBattleFlowManager flowManager;
        [Tooltip("선택한 스킬의 직접 클릭 방식(유닛·열·진영)을 읽습니다.")]
        [SerializeField] private JcInputHandler inputHandler;
        [Tooltip("현재 행동 유닛의 노란 셀 재질입니다. 대상 선택을 취소하면 복원됩니다.")]
        [SerializeField] private Material currentTurnMaterial;
        [Tooltip("스킬 선택 중 직접 클릭할 수 없는 셀의 검정 재질입니다.")]
        [SerializeField] private Material unavailableMaterial;
        [Tooltip("JC_BattleUI_VFX/CellBox의 셀 투명도 설정입니다. 미연결이면 기존 재질 그대로 표시합니다.")]
        [SerializeField] private BattleCellVisualSettings cellVisualSettings;
        private readonly HashSet<GridCell> selectableCells = new HashSet<GridCell>();
        // 입력에서 확정한 직접 선택 대상의 진형만 비선택 셀 표시를 허용합니다.
        private readonly HashSet<bool> selectableSides = new HashSet<bool>();
        private bool selectingTargets;
        public bool UsesBattleTilePresentation => useBattleTilePresentation;

        public static JcBattleGridManager Instance { get; private set; }

        public Material MainTargetHighlightMat => MainTargetHighlightMaterial;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[JcBattleGridManager] 중복 인스턴스가 감지되었습니다.");
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
                    Debug.LogWarning($"[JcBattleGridManager] 중복 좌표 감지: {key}");
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


        public void ShowPreviewHighlight(SkillData skill, GridCell mainCell, List<GridCell> splashCells, BattleCharactor caster = null)
        {
            // JC | 2026-11-16 | 본게임처럼 범위 변경 도중 빈 상태를 그리지 않고 시전자/스킬을 함께 유지합니다.
            ClearPreviewHighlight(false);
            if (mainCell == null) { RefreshTilePresentation(); return; }
            previewSkill = skill;
            previewCaster = caster != null ? caster : (flowManager != null ? flowManager.CurrentUnit : null);

            bool allConfirmed = useBattleTilePresentation || (skill != null && skill.classSkillTarget == 2);
            mainCell.SetMainTargetHighlight();
            _previewMainTargetCell = mainCell;
            // 랜덤·조건부 추가 타깃은 효과 계산에만 남기고 후보 셀은 미리 표시하지 않습니다.
            bool randomExtras = SkillAreaPreviewHelper.HasRandomSecondaryTargets(skill);
            if (splashCells != null && !(useBattleTilePresentation && randomExtras))
            {
                foreach (var cell in splashCells)
                {
                    if (cell == null || cell == mainCell) continue;
                    if (allConfirmed) cell.SetMainTargetHighlight();
                    else cell.SetAdditionalHighlight();
                    _previewHighlightedCells.Add(cell);
                }
            }
            RefreshTilePresentation();
        }

        public void ClearPreviewHighlight()
        {
            ClearPreviewHighlight(true);
        }
        private void ClearPreviewHighlight(bool refresh)
        {
            // JC | 2026-11-16 | 이전 복합 스킬의 회복 색상이 다음 선택에 남지 않도록 함께 해제합니다.
            previewSkill = null;
            previewCaster = null;
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
            if (refresh) RefreshTilePresentation();
        }

        /// <summary>기존 입력이 계산한 직접 선택 대상을 셀 표시로 전달합니다.</summary>
        public void SetSelectableUnits(IEnumerable<BattleCharactor> units)
        {
            if (!useBattleTilePresentation) return;
            selectableCells.Clear();
            selectableSides.Clear();
            selectingTargets = true;
            var actor = flowManager != null ? flowManager.CurrentUnit : null;
            var kind = SkillActivationRules.Kind(actor, inputHandler != null ? inputHandler.PendingSkill : null);
            if (units != null)
                foreach (var unit in units)
                {
                    var occupied = FindCellByUnit(unit);
                    if (occupied == null) continue;
                    selectableSides.Add(occupied.Coords.x >= 2);
                    foreach (var cell in cellsByCoords.Values)
                    {
                        bool sameSide = (cell.Coords.x >= 2) == (occupied.Coords.x >= 2);
                        if (cell == occupied || (sameSide && (kind == SkillActivationKind.Side ||
                            (kind == SkillActivationKind.Column && cell.Coords.x == occupied.Coords.x))))
                            selectableCells.Add(cell);
                    }
                }
            RefreshTilePresentation();
        }

        public void AddSelectableHostages(IEnumerable<HostageBattleActor> hostages)
        {
            if (!useBattleTilePresentation || hostages == null) return;
            foreach (var hostage in hostages)
            {
                if (hostage == null) continue;
                var cell = hostage.GetComponentInParent<GridCell>();
                if (cell != null)
                {
                    selectableCells.Add(cell);
                    selectableSides.Add(cell.Coords.x >= 2);
                }
            }
            RefreshTilePresentation();
        }

        public void ClearSelectableCells()
        {
            selectingTargets = false;
            selectableCells.Clear();
            selectableSides.Clear();
            RefreshTilePresentation();
        }

        private void LateUpdate() => RefreshTilePresentation();

        private void RefreshTilePresentation()
        {
            if (!useBattleTilePresentation) return;
            bool ended = flowManager != null && flowManager.IsEndingBattle;
            // JC | 2026-11-16 | 본게임 표시 우선순위와 사망 시 해제 규칙을 테스트 전투에도 적용합니다.
            var actor = !ended && flowManager != null ? flowManager.CurrentUnit : null;
            var current = actor != null && !actor.IsDead ? FindCellByUnit(actor) : null;
            bool choosing = !ended && selectingTargets && (flowManager == null || !flowManager.IsActionInProgress);
            var infoCell = !ended ? InfoHoverCell() : null;
            foreach (var cell in cellsByCoords.Values)
            {
                if (cell == null) continue;
                Material material = ClearMaterial;
                if (!ended)
                {
                    if (cell == _previewMainTargetCell || _previewHighlightedCells.Contains(cell)) material = MainTargetHighlightMaterial;
                    else if (choosing && selectableCells.Contains(cell)) material = TargetMaterial;
                    else if (cell == infoCell) material = MainTargetHighlightMaterial;
                    else if (cell == current) material = currentTurnMaterial;
                    else if (choosing && selectableSides.Contains(cell.Coords.x >= 2)) material = unavailableMaterial;
                }
                cell.SetMaterial(material);
                if (cellVisualSettings != null)
                {
                    float alpha = material == currentTurnMaterial ? cellVisualSettings.currentTurnAlpha :
                        material == TargetMaterial ? cellVisualSettings.selectableAlpha :
                        material == MainTargetHighlightMaterial ? cellVisualSettings.confirmedAreaAlpha :
                        material == unavailableMaterial ? cellVisualSettings.unavailableAlpha : 1f;
                    var visualState = material == ClearMaterial ? BattleCellVisualState.Hidden :
                        material == currentTurnMaterial ? BattleCellVisualState.CurrentTurn :
                        material == TargetMaterial ? BattleCellVisualState.Selectable :
                        material == MainTargetHighlightMaterial ? BattleCellVisualState.ConfirmedArea :
                        material == unavailableMaterial ? BattleCellVisualState.Unavailable : BattleCellVisualState.Hidden;
                    // JC | 2026-11-16 | 부활+전체 공격도 본게임의 동일 판정 함수를 사용해 아군만 흰색으로 표시합니다.
                    if (visualState == BattleCellVisualState.ConfirmedArea &&
                        SkillAreaPreviewHelper.IsHealingPreviewSide(previewSkill, previewCaster, cell.Coords.x < 2) &&
                        (cell == _previewMainTargetCell || _previewHighlightedCells.Contains(cell)))
                        visualState = BattleCellVisualState.HealingArea;
                    cellVisualSettings.Apply(cell, alpha, visualState);
                }
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
