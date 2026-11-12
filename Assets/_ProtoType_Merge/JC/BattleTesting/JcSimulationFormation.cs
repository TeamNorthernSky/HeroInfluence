using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

namespace JC.BattleTesting
{
    // JC | 2026-11-10 | 시뮬레이션 진형의 초안과 확정을 분리합니다. 저장 파티·HP·스킬·턴 큐는 변경하지 않습니다.
    public sealed class JcSimulationFormation
    {
        public GridCellRef[] Cells { get; }
        public BattleCharactor[] Draft { get; }
        readonly BattleCharactor[] original;
        readonly bool[] fixedCells;

        public JcSimulationFormation(IEnumerable<GridCellRef> cells, IReadOnlyList<BattleCharactor> participants)
        {
            Cells = cells.Where(c => c && c.Coords.x >= 0 && c.Coords.x < 4 && c.Coords.y >= 0 && c.Coords.y < 3)
                .OrderBy(c => c.Coords.x).ThenBy(c => c.Coords.y).ToArray();
            original = Cells.Select(c => c.OccupyingUnit).ToArray();
            Draft = (BattleCharactor[])original.Clone();
            fixedCells = Cells.Select((c, i) => IsFixed(c, original[i], participants)).ToArray();
        }

        static bool IsFixed(GridCellRef cell, BattleCharactor occupant, IReadOnlyList<BattleCharactor> participants)
        {
            // JC | 2026-11-10 | 점유 해제 후 남아 있는 시체·인질·비참가자도 빈 칸으로 오인하지 않습니다.
            // JC | 2026-11-12 | 보스 전용 씬의 본체는 페이즈/무대 기준 위치를 유지합니다. 일반 적과 아군은 기존 교환을 사용합니다.
            return (occupant && occupant.GetComponent<BossController>() && JcStandaloneBattleTest.IsTestScene(cell.gameObject.scene)) || !cell.gameObject.activeInHierarchy || (occupant && (occupant.IsDead || !participants.Contains(occupant))) ||
                cell.GetComponentInChildren<HostageBattleActor>(true) != null ||
                cell.GetComponentsInChildren<BattleCharactor>(true).Any(u => u != occupant);
        }

        public bool IsFixed(int index) => fixedCells[index];

        public bool TrySwap(int from, int to)
        {
            if (from < 0 || to < 0 || from >= Cells.Length || to >= Cells.Length ||
                fixedCells[from] || fixedCells[to] || (Cells[from].Coords.x < 2) != (Cells[to].Coords.x < 2)) return false;
            (Draft[from], Draft[to]) = (Draft[to], Draft[from]);
            return true;
        }

        public bool TryApply(IReadOnlyList<BattleCharactor> participants, out string error)
        {
            error = "";
            if (Cells.Length != 12 || Cells.Select(c => c.Coords).Distinct().Count() != 12)
            {
                error = "아군·적군의 12개 전투 칸을 확인할 수 없습니다.";
                return false;
            }
            var seen = new HashSet<BattleCharactor>();
            for (int i = 0; i < Cells.Length; i++)
            {
                var unit = Draft[i];
                if (!Cells[i] || Cells[i].OccupyingUnit != original[i] ||
                    IsFixed(Cells[i], original[i], participants) != fixedCells[i] ||
                    (original[i] && original[i].OccupiedCell != Cells[i]) ||
                    (fixedCells[i] && unit != original[i]) ||
                    (unit && (!seen.Add(unit) || !original.Contains(unit) || unit.IsPlayer != (Cells[i].Coords.x < 2))) ||
                    (unit && !fixedCells[i] && (unit.IsDead || !participants.Contains(unit))))
                {
                    error = "편집 중 전투 상태가 바뀌었습니다. 취소 후 다시 열어 주세요.";
                    return false;
                }
            }
            if (original.Count(u => u) != seen.Count)
            {
                error = "진형의 캐릭터 수가 일치하지 않습니다.";
                return false;
            }

            var offsets = new Dictionary<BattleCharactor, Vector3>();
            for (int i = 0; i < Cells.Length; i++)
            {
                if (original[i] && original[i] != Draft[i])
                    offsets.Add(original[i], original[i].transform.position - Cells[i].transform.position);
            }
            // JC | 2026-11-10 | 교환 대상의 기존 점유를 모두 비운 다음 재등록하여 중간 충돌과 역색인 잔여를 없앱니다.
            foreach (var unit in offsets.Keys) unit.ClearOccupiedCell();
            for (int i = 0; i < Cells.Length; i++)
            {
                var unit = Draft[i];
                if (!unit || !offsets.TryGetValue(unit, out var offset)) continue;
                unit.transform.SetParent(Cells[i].transform, true);
                unit.transform.position = Cells[i].transform.position + offset;
                unit.AssignToCell(Cells[i]);
            }
            return true;
        }
    }
}
