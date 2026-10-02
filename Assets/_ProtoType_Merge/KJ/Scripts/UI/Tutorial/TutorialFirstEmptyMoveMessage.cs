using UnityEngine;

/// <summary>행동 게이지가 처음 양수에서 0이 되었을 때 이동 불가 안내를 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class TutorialFirstEmptyMoveMessage : MonoBehaviour
{
    [SerializeField] private GameObject messagePanel;
    private const string SeenKey = "KJ.Tutorial.Explore.FirstEmptyMove";
    private PartyRegistry registry;
    private PartyGridMover observedParty;
    private TutorialProgressRepository progress;
    private bool hadMovePoints;
    private bool showing;
    private float nextSearch;

    private void Awake()
    {
        if (messagePanel != null) messagePanel.SetActive(false);
    }

    private void LateUpdate()
    {
        if (gameObject.scene.name != "TutorialExploreScene" || messagePanel == null) return;
        if (registry == null && Time.unscaledTime >= nextSearch)
        {
            registry = FindFirstObjectByType<PartyRegistry>();
            nextSearch = Time.unscaledTime + 0.5f;
        }
        var party = registry != null ? registry.PlayerParty : null;
        if (observedParty != party)
        {
            observedParty = party;
            hadMovePoints = false;
            HideMessage();
        }
        if (party == null || !party.gameObject.activeInHierarchy || party.MaxMovePoints <= 0 ||
            DefeatedPartyReturnController.IsPartyWaiting(party)) return;
        if (progress == null) progress = TutorialProgressRepository.Instance;
        if (progress == null) return;
        ObserveMovePoints(party.RemainingMovePoints);
    }

    private void ObserveMovePoints(int remaining)
    {
        if (remaining > 0)
        {
            hadMovePoints = true;
            HideMessage();
            return;
        }
        if (!hadMovePoints || showing || progress.IsMessageSeen(SeenKey)) return;
        messagePanel.SetActive(true);
        showing = true;
        progress.MarkMessageSeen(SeenKey);
    }

    private void HideMessage()
    {
        if (showing && messagePanel != null) messagePanel.SetActive(false);
        showing = false;
    }

    private void OnDisable()
    {
        HideMessage();
        hadMovePoints = false;
        observedParty = null;
    }
}
