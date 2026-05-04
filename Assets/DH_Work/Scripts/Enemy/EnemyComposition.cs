using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyComposition : MonoBehaviour
{
    [SerializeField] private int[] unitIndices = Array.Empty<int>();

    public int[] UnitIndices => unitIndices ?? Array.Empty<int>();

    public int GetUnitIndexAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= UnitIndices.Length)
            return -1;

        return UnitIndices[slotIndex];
    }

    public void SetUnitIndexAt(int slotIndex, int unitIndex)
    {
        if (slotIndex < 0)
            return;

        EnsureSlotCount(slotIndex + 1);
        unitIndices[slotIndex] = unitIndex;
    }

    public void ClearSlot(int slotIndex)
    {
        SetUnitIndexAt(slotIndex, -1);
    }

    public void EnsureSlotCount(int slotCount)
    {
        slotCount = Mathf.Max(0, slotCount);

        if (unitIndices == null)
            unitIndices = Array.Empty<int>();

        if (unitIndices.Length >= slotCount)
            return;

        int oldLength = unitIndices.Length;
        Array.Resize(ref unitIndices, slotCount);
        for (int i = oldLength; i < unitIndices.Length; i++)
            unitIndices[i] = -1;
    }
}
