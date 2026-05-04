using UnityEngine;
using System.Collections.Generic;

public class FogOfWarUnit : MonoBehaviour
{
    [Tooltip("시야 반경 (World Distance)")]
    public float visionRadius = 5f;
    
    // 시야를 뿌릴 모든 유닛의 리스트 (Manager에서 접근)
    public static readonly List<FogOfWarUnit> AllUnits = new List<FogOfWarUnit>();

    private void OnEnable()
    {
        AllUnits.Add(this);
    }

    private void OnDisable()
    {
        AllUnits.Remove(this);
    }
}
