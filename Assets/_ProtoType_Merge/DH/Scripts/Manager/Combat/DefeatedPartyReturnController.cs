using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DefeatedPartyReturnController : MonoBehaviour
{
    private const string GameObjectName = "[DefeatedPartyReturnController]";
    private const string FallbackLobbySceneName = "LobbyScene_New";

    public static DefeatedPartyReturnController Instance { get; private set; }

    private readonly Dictionary<string, PartyGridMover> waitingParties =
        new Dictionary<string, PartyGridMover>();
    private Coroutine pendingReturnCoroutine;

    public static bool IsPartyWaiting(PartyGridMover party)
    {
        return Instance != null && Instance.ContainsParty(party);
    }

    public static void ScheduleReturn(PartyGridMover party)
    {
        if (party == null || DHGameEndState.IsEnding)
            return;

        EnsureInstance();
        Instance.SchedulePartyReturn(party);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        GameObject host = new GameObject(GameObjectName);
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<DefeatedPartyReturnController>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SubscribeTurnManager(FindFirstObjectByType<TurnManager>());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeTurnManager(FindFirstObjectByType<TurnManager>());
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SubscribeTurnManager(FindFirstObjectByType<TurnManager>());
    }

    private void SubscribeTurnManager(TurnManager turnManager)
    {
        if (turnManager == null)
            return;

        turnManager.DayAdvanced -= HandleDayAdvanced;
        turnManager.DayAdvanced += HandleDayAdvanced;
        turnManager.EnemyTurnStateChanged -= HandleEnemyTurnStateChanged;
        turnManager.EnemyTurnStateChanged += HandleEnemyTurnStateChanged;
    }

    private void UnsubscribeTurnManager(TurnManager turnManager)
    {
        if (turnManager == null)
            return;

        turnManager.DayAdvanced -= HandleDayAdvanced;
        turnManager.EnemyTurnStateChanged -= HandleEnemyTurnStateChanged;
    }

    private void SchedulePartyReturn(PartyGridMover party)
    {
        if (DHGameEndState.IsEnding)
            return;

        string partyId = ResolvePartyId(party);
        if (string.IsNullOrWhiteSpace(partyId))
            return;

        waitingParties[partyId] = party;
        ClearPartyPresentation(party);
        party.gameObject.SetActive(false);
        HQVisitState.Instance?.ClearVisitingParties();

        CastleHQVisitDetector[] detectors = FindObjectsByType<CastleHQVisitDetector>(FindObjectsSortMode.None);
        for (int i = 0; i < detectors.Length; i++)
        {
            CastleHQVisitDetector detector = detectors[i];
            if (detector != null)
                detector.ReevaluateNow();
        }
    }

    private void HandleDayAdvanced(int _)
    {
        CompleteScheduledReturns();
    }

    private void HandleEnemyTurnStateChanged(bool isEnemyTurnRunning)
    {
        if (isEnemyTurnRunning || waitingParties.Count == 0 || pendingReturnCoroutine != null)
            return;

        pendingReturnCoroutine = StartCoroutine(CompleteScheduledReturnsAfterTurnSettles());
    }

    private System.Collections.IEnumerator CompleteScheduledReturnsAfterTurnSettles()
    {
        yield return null;
        pendingReturnCoroutine = null;
        CompleteScheduledReturns();
    }

    private void CompleteScheduledReturns()
    {
        if (waitingParties.Count == 0)
            return;

        if (DHGameEndState.IsEnding)
        {
            waitingParties.Clear();
            HQVisitState.Instance?.ClearVisitingParties();
            return;
        }

        List<string> partyIds = new List<string>(waitingParties.Keys);
        for (int i = 0; i < partyIds.Count; i++)
        {
            string partyId = partyIds[i];
            if (!waitingParties.TryGetValue(partyId, out PartyGridMover party) || party == null)
            {
                waitingParties.Remove(partyId);
                continue;
            }

            party.gameObject.SetActive(true);
        }

        HQVisitState.Instance?.SetVisitingParties(partyIds);
        waitingParties.Clear();
        LoadLobbyScene();
    }

    private static void ClearPartyPresentation(PartyGridMover party)
    {
        PartyMovePointGaugeManager.Instance?.UnregisterParty(party);
        PartyMovePointWorldGaugeManager.Instance?.UnregisterParty(party);

        ClickSelectionController[] clickControllers = FindObjectsByType<ClickSelectionController>(FindObjectsSortMode.None);
        for (int i = 0; i < clickControllers.Length; i++)
        {
            ClickSelectionController clickController = clickControllers[i];
            if (clickController != null)
                clickController.ClearMovePreview();
        }
    }

    private bool ContainsParty(PartyGridMover party)
    {
        if (party == null)
            return false;

        string partyId = ResolvePartyId(party);
        return !string.IsNullOrWhiteSpace(partyId) && waitingParties.ContainsKey(partyId);
    }

    private static string ResolvePartyId(PartyGridMover party)
    {
        if (party == null)
            return string.Empty;

        PartyIdentity identity = party.GetComponent<PartyIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.PartyId))
            return identity.PartyId;

        return party.name;
    }

    private static void LoadLobbyScene()
    {
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadLobby();
            return;
        }

        SceneManager.LoadScene(FallbackLobbySceneName);
    }
}
