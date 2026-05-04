using UnityEngine;

[DisallowMultipleComponent]
public class PlayerEquipmentHandler : MonoBehaviour
{
    [SerializeField] private PlayerDataSO playerData;

    public void Configure(PlayerDataSO data)
    {
        playerData = data;
        // 외형 교체/장비 장착 반영 포인트.
    }
}
