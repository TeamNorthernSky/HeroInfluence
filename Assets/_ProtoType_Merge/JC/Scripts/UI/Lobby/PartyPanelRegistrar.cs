using UnityEngine;

/// <summary>[JC 260628] 파티패널의 panelRoot를 LobbyUIRegistry에 등록(Sortie가 지연 해석).
/// [JC 260703] 로비 로스터가 LobbyRosterView(표시전용)로 교체되며 HeroListController(roster) 등록은 제거.
/// 편성 selection 로스터는 UI_Sortie 번들이 전담(dormant). RosterPanelRoot는 Sortie가 모달 중 원본 비활성에 사용.</summary>
[DisallowMultipleComponent]
public class PartyPanelRegistrar : MonoBehaviour
{
    [SerializeField] private RectTransform panelRoot;

    private void OnEnable()
    {
        LobbyUIRegistry.RosterPanelRoot = panelRoot;
    }

    private void OnDisable()
    {
        if (LobbyUIRegistry.RosterPanelRoot == panelRoot) LobbyUIRegistry.RosterPanelRoot = null;
    }
}
