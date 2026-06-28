using UnityEngine;
using UnityEngine.UI;

/// <summary>[JC 260628] 본부 버튼을 LobbyUIRegistry.HqButton에 등록(HQBuildModeController가 지연 해석).</summary>
[DisallowMultipleComponent]
public class HqButtonRegistrar : MonoBehaviour
{
    [SerializeField] private Button hqButton;
    private void OnEnable() { LobbyUIRegistry.HqButton = hqButton; }
    private void OnDisable() { if (LobbyUIRegistry.HqButton == hqButton) LobbyUIRegistry.HqButton = null; }
}
