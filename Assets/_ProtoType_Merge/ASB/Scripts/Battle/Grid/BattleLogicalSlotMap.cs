using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// DH 이벤트 전투의 1-based 논리 슬롯을 ASB 적 진영 GridCell에 연결합니다.
/// 셀 정렬 계약은 Persistent 적 배치와 동일한 실제 그리드 키 오름차순입니다.
/// Grid_X_Y의 실제 키는 (X * 100) + Y이며, 현재 6칸 전투에서는
/// 1=Grid_2_0 ... 6=Grid_3_2 순서가 됩니다.
/// </summary>
public sealed class BattleLogicalSlotMap
{
    public sealed class Slot
    {
        internal Slot(int logicalSlot, int gridNumber, GridCellRef cell)
        {
            LogicalSlot = logicalSlot;
            GridNumber = gridNumber;
            Cell = cell;
        }

        public int LogicalSlot { get; }
        public int GridNumber { get; }
        public GridCellRef Cell { get; }
        public string GridName => Cell != null ? Cell.name : string.Empty;
        public Vector3 WorldPosition => Cell != null ? Cell.transform.position : Vector3.zero;
        public Quaternion WorldRotation => Cell != null ? Cell.transform.rotation : Quaternion.identity;
    }

    private readonly List<Slot> orderedSlots;
    private readonly Dictionary<int, Slot> slotsByLogicalNumber;
    private readonly Dictionary<int, Slot> slotsByGridNumber;

    private BattleLogicalSlotMap(Transform root, List<Slot> slots)
    {
        Root = root;
        orderedSlots = slots;
        slotsByLogicalNumber = new Dictionary<int, Slot>(slots.Count);
        slotsByGridNumber = new Dictionary<int, Slot>(slots.Count);

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            slotsByLogicalNumber.Add(slot.LogicalSlot, slot);
            slotsByGridNumber.Add(slot.GridNumber, slot);
        }
    }

    public Transform Root { get; }
    public int Count => orderedSlots.Count;
    public IReadOnlyList<Slot> OrderedSlots => orderedSlots;

    public bool TryResolve(int logicalSlot, out Slot slot)
    {
        slot = null;
        return logicalSlot > 0 && slotsByLogicalNumber.TryGetValue(logicalSlot, out slot);
    }

    public bool TryGetByGridNumber(int gridNumber, out Slot slot)
    {
        return slotsByGridNumber.TryGetValue(gridNumber, out slot);
    }

    public string BuildDebugSummary(string battleKey)
    {
        var builder = new StringBuilder();
        builder.Append("[BattleLogicalSlotMap] Battle=")
            .Append(string.IsNullOrWhiteSpace(battleKey) ? "<none>" : battleKey)
            .Append(", Root=")
            .Append(Root != null ? Root.name : "<null>");

        for (int i = 0; i < orderedSlots.Count; i++)
        {
            Slot slot = orderedSlots[i];
            builder.Append("\n  LogicalSlot ")
                .Append(slot.LogicalSlot)
                .Append(" -> ")
                .Append(slot.GridName)
                .Append(" (")
                .Append(slot.GridNumber)
                .Append(')');
        }

        return builder.ToString();
    }

    public static bool TryCreate(Transform root, out BattleLogicalSlotMap map, out string error)
    {
        map = null;
        error = string.Empty;

        if (root == null)
        {
            error = "Enemy grid root is null.";
            return false;
        }

        GridCellRef[] cells = root.GetComponentsInChildren<GridCellRef>(true);
        var cellsByGridNumber = new Dictionary<int, GridCellRef>();
        for (int i = 0; i < cells.Length; i++)
        {
            GridCellRef cell = cells[i];
            if (cell == null || !cell.transform.IsChildOf(root))
                continue;

            if (!TryParseGridNumber(cell.name, out int gridNumber))
                continue;

            if (cellsByGridNumber.TryGetValue(gridNumber, out GridCellRef duplicate))
            {
                error =
                    $"Duplicate enemy grid number {gridNumber}: " +
                    $"'{duplicate.name}' and '{cell.name}'.";
                return false;
            }

            cellsByGridNumber.Add(gridNumber, cell);
        }

        if (cellsByGridNumber.Count == 0)
        {
            error = $"No GridCell named Grid_N or Grid_X_Y was found under '{root.name}'.";
            return false;
        }

        var sortedGridNumbers = new List<int>(cellsByGridNumber.Keys);
        sortedGridNumbers.Sort();

        var slots = new List<Slot>(sortedGridNumbers.Count);
        for (int i = 0; i < sortedGridNumbers.Count; i++)
        {
            int gridNumber = sortedGridNumbers[i];
            slots.Add(new Slot(i + 1, gridNumber, cellsByGridNumber[gridNumber]));
        }

        map = new BattleLogicalSlotMap(root, slots);
        return true;
    }

    public static bool TryParseGridNumber(string objectName, out int gridNumber)
    {
        gridNumber = 0;
        if (string.IsNullOrWhiteSpace(objectName) ||
            !objectName.StartsWith("Grid_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = objectName.Substring("Grid_".Length);
        if (int.TryParse(suffix, out gridNumber))
            return true;

        string[] xy = suffix.Split('_');
        if (xy.Length != 2 ||
            !int.TryParse(xy[0], out int x) ||
            !int.TryParse(xy[1], out int y))
        {
            gridNumber = 0;
            return false;
        }

        gridNumber = (x * 100) + y;
        return true;
    }
}
