using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DefeatedPartyReturnController : MonoBehaviour
{
    private const string GameObjectName = "[DefeatedPartyReturnController]";
    private const string FallbackLobbySceneName = "HQLobbyScene"; // [JC 260616] LobbyScene_New 폐기 → 정본 HQLobbyScene으로 폴백 교체

    public static DefeatedPartyReturnController Instance { get; private set; }

    private readonly Dictionary<string, PartyGridMover> waitingParties =
        new Dictionary<string, PartyGridMover>();
    private readonly Dictionary<string, int> waitingPartyStartDays =
        new Dictionary<string, int>();
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
        waitingPartyStartDays[partyId] = ResolveCurrentDay();
        ClearPartyPresentation(party);
        party.gameObject.SetActive(false);
        HQVisitState.Instance?.ClearVisitingParties();

        HeroUnionHQVisitDetector[] detectors = FindObjectsByType<HeroUnionHQVisitDetector>(FindObjectsSortMode.None);
        for (int i = 0; i < detectors.Length; i++)
        {
            HeroUnionHQVisitDetector detector = detectors[i];
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
            waitingPartyStartDays.Clear();
            HQVisitState.Instance?.ClearVisitingParties();
            return;
        }

        List<string> partyIds = new List<string>(waitingParties.Keys);
        List<string> completedPartyIds = new List<string>();
        int currentDay = ResolveCurrentDay();
        for (int i = 0; i < partyIds.Count; i++)
        {
            string partyId = partyIds[i];
            if (!waitingParties.TryGetValue(partyId, out PartyGridMover party) || party == null)
            {
                waitingParties.Remove(partyId);
                waitingPartyStartDays.Remove(partyId);
                continue;
            }

            if (waitingPartyStartDays.TryGetValue(partyId, out int startDay) && currentDay <= startDay)
                continue;

            party.gameObject.SetActive(true);
            RecoverPartyBeforeLobby(partyId, party);
            completedPartyIds.Add(partyId);
            waitingParties.Remove(partyId);
            waitingPartyStartDays.Remove(partyId);
        }

        if (completedPartyIds.Count == 0)
            return;

        HQVisitState.Instance?.SetVisitingParties(HQVisitState.SourceHQ, completedPartyIds);
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
            if (clickController == null)
                continue;

            clickController.ClearActiveMoverIf(party);
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

    private static void RecoverPartyBeforeLobby(string partyId, PartyGridMover party)
    {
        IReadOnlyList<int> unitIndices = ResolvePartyUnitIndices(partyId, party);
        if (unitIndices == null || unitIndices.Count == 0)
            return;

        ExplorationDefeatResultHandler.RecoverDefeatedUnitsForReturn(unitIndices);
    }

    private static IReadOnlyList<int> ResolvePartyUnitIndices(string partyId, PartyGridMover party)
    {
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        if (partyRepository != null &&
            !string.IsNullOrWhiteSpace(partyId) &&
            partyRepository.TryGetParty(partyId, out PartyPersistentData partyData) &&
            partyData != null &&
            partyData.UnitIndices.Count > 0)
        {
            return partyData.UnitIndices;
        }

        PartyComposition composition = party != null ? party.GetComponent<PartyComposition>() : null;
        return composition != null ? composition.UnitIndices : System.Array.Empty<int>();
    }

    private static int ResolveCurrentDay()
    {
        return GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
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
