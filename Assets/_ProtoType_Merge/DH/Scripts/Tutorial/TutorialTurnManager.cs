using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialTurnManager : MonoBehaviour
{
    public event Action<int> TutorialTurnAdvanced;

    [Header("State")]
    [SerializeField] private bool turnControlEnabled = true;
    [SerializeField] private TMP_Text turnStateText;

    [Header("Party")]
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private bool resetMovePointsOnTurnAdvanced = true;

    [Header("Options")]
    [SerializeField] private bool resetTurnOnStart;

    private TutorialProgressRepository repository;
    private bool turnAdvancing;

    public int CurrentTurn => ResolveRepository() != null ? repository.CurrentTurn : 1;
    public bool IsTurnAdvancing => turnAdvancing;
    public bool TurnControlEnabled
    {
        get => turnControlEnabled;
        set => turnControlEnabled = value;
    }

    private void Awake()
    {
        ResolveReferences();
        ResolveRepository();
        if (resetTurnOnStart)
            repository?.ResetTurn();

        RefreshTurnText();
    }

    public void EndTutorialTurn()
    {
        if (!turnControlEnabled || turnAdvancing)
            return;

        turnAdvancing = true;

        TutorialProgressRepository progress = ResolveRepository();
        int nextTurn = progress != null ? progress.AdvanceTurn() : 1;
        ResetPartyMovePoints();
        TutorialTurnAdvanced?.Invoke(nextTurn);

        turnAdvancing = false;
        RefreshTurnText();
    }

    public void SetTutorialTurn(int turn)
    {
        ResolveRepository()?.SetCurrentTurn(turn);
        RefreshTurnText();
    }

    public void ResetTutorialTurn()
    {
        ResolveRepository()?.ResetTurn();
        RefreshTurnText();
    }

    private TutorialProgressRepository ResolveRepository()
    {
        if (repository == null)
            repository = TutorialProgressRepository.EnsureInstance();

        return repository;
    }

    private void ResolveReferences()
    {
        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();
    }

    private void ResetPartyMovePoints()
    {
        if (!resetMovePointsOnTurnAdvanced)
            return;

        ResolveReferences();
        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party != null)
            party.ResetMovePointsToMax();
    }

    private void RefreshTurnText()
    {
        if (turnStateText == null)
            return;

        turnStateText.text = $"Turn {CurrentTurn}";
    }
}
