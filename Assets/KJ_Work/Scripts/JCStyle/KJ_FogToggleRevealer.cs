using UnityEngine;

/// <summary>
/// 특정 위치의 안개를 토글(On/Off) 형식으로 제거하는 컴포넌트.
/// PartyGridMover와 달리 이력을 남기지 않는 옵션을 선택할 수 있어 즉각적인 안개 복구가 가능합니다.
/// </summary>
public class KJ_FogToggleRevealer : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("안개 제거 활성화 여부")]
    public bool isOpen = false;
    
    [Tooltip("안개를 밝힐 그리드 반경")]
    public int revealRadius = 3;
    
    [Tooltip("방문 이력을 남길지 여부. False면 토글을 끄는 즉시 안개가 다시 덮입니다.")]
    public bool recordHistory = false;

    /// <summary>
    /// 현재 월드 좌표를 그리드 좌표로 변환하여 반환합니다.
    /// </summary>
    public Vector2Int GetGridPos()
    {
        return KJ_PlayGridManager.WorldToGrid(transform.position);
    }
}
