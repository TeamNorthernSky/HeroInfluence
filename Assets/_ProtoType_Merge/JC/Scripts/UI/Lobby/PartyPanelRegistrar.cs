using UnityEngine;

/// <summary>[JC 260628] 파티패널의 roster/panelRoot를 LobbyUIRegistry에 등록(Sortie가 지연 해석).</summary>
[DisallowMultipleComponent]
public class PartyPanelRegistrar : MonoBehaviour
{
    [SerializeField] private HeroListController roster;
    [SerializeField] private RectTransform panelRoot;

    private void OnEnable()
    {
        LobbyUIRegistry.Roster = roster;
        LobbyUIRegistry.RosterPanelRoot = panelRoot;
    }

    private void OnDisable()
    {
        if (LobbyUIRegistry.Roster == roster) LobbyUIRegistry.Roster = null;
        if (LobbyUIRegistry.RosterPanelRoot == panelRoot) LobbyUIRegistry.RosterPanelRoot = null;
    }
}
