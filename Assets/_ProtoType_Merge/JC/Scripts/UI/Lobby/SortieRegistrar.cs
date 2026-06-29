using UnityEngine;

/// <summary>[JC 260629] 출전 컨트롤러를 LobbyUIRegistry.Sortie에 등록. UI_Sortie 번들 루트(상시 active)에 부착 —
/// Modal_Sortie 자체는 비활성 시작이라 SortieController.OnEnable로는 등록 시점이 안 잡힘.</summary>
[DisallowMultipleComponent]
public class SortieRegistrar : MonoBehaviour
{
    [SerializeField] private SortieController controller;
    private void OnEnable() { if (controller != null) LobbyUIRegistry.Sortie = controller; }
    private void OnDisable() { if (LobbyUIRegistry.Sortie == controller) LobbyUIRegistry.Sortie = null; }
}
