using UnityEngine;
/// <summary>[JC 260629] 출전 버튼 Transform을 LobbyUIRegistry.GoButton에 등록(SortieController 지연 해석).</summary>
[DisallowMultipleComponent]
public class GoButtonRegistrar : MonoBehaviour {
  [SerializeField] private RectTransform goButton;
  private void OnEnable() { LobbyUIRegistry.GoButton = goButton; }
  private void OnDisable() { if (LobbyUIRegistry.GoButton == goButton) LobbyUIRegistry.GoButton = null; }
}
