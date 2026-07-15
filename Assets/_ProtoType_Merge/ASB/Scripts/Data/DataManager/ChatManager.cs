using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChatManager : MonoBehaviour
{
    public static ChatManager Instance { get; private set; }

    [SerializeField] private EventScriptCatalog catalog;
    [SerializeField] private bool dontDestroyOnLoad = true;

    private static readonly IReadOnlyList<ChatBranchOptionState> EmptyOptionStates = Array.Empty<ChatBranchOptionState>();

    private readonly List<BranchDBEventData> currentOptions = new List<BranchDBEventData>();
    private readonly List<ChatBranchOptionState> currentOptionStates = new List<ChatBranchOptionState>();

    private int currentZoneId;
    private ChatDBEventData currentChat;

    public event Action<ChatDBEventData> OnChatShown;
    public event Action<IReadOnlyList<ChatBranchOptionState>> OnBranchShown;
    public event Action OnChatEnded;

    public bool IsRunning { get; private set; }
    public int CurrentZoneId => currentZoneId;
    public ChatDBEventData CurrentChat => currentChat;
    public IReadOnlyList<BranchDBEventData> CurrentOptions => currentOptions.ToArray();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ChatManager] Duplicate instance detected. Destroying this instance.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void StartChat(int zoneId, int startChatId)
    {
        if (!ResolveCatalog())
        {
            Debug.LogWarning("[ChatManager] EventScriptCatalog was not found. Chat cannot start.", this);
            EndChat();
            return;
        }

        if (IsRunning)
        {
            Debug.LogWarning("[ChatManager] StartChat was called while another chat is running. Restarting chat.", this);
            EndChat();
        }

        if (!catalog.TryGetChat(zoneId, startChatId, out ChatDBEventData chat) || chat == null)
        {
            Debug.LogWarning($"[ChatManager] Chat ID was not found. Zone: {zoneId}, Chat_ID: {startChatId}", this);
            EndChat();
            return;
        }

        currentZoneId = zoneId;
        ShowChat(chat);
    }

    public void Advance()
    {
        if (!ResolveCatalog())
        {
            Debug.LogWarning("[ChatManager] EventScriptCatalog was not found. Chat cannot advance.", this);
            EndChat();
            return;
        }

        if (!IsRunning || currentChat == null)
        {
            return;
        }

        if (currentOptions.Count > 0)
        {
            Debug.LogWarning("[ChatManager] Current chat has branch options. Select an option instead of advancing.", this);
            return;
        }

        if (currentChat.Next_Chat_ID <= 0)
        {
            EndChat();
            return;
        }

        int nextChatId = currentChat.Next_Chat_ID;
        if (!catalog.TryGetChat(currentZoneId, nextChatId, out ChatDBEventData next) || next == null)
        {
            Debug.LogWarning($"[ChatManager] Next Chat_ID was not found. Zone: {currentZoneId}, Chat_ID: {nextChatId}", this);
            EndChat();
            return;
        }

        ShowChat(next);
    }

    public void Select(BranchDBEventData option)
    {
        if (!ResolveCatalog())
        {
            Debug.LogWarning("[ChatManager] EventScriptCatalog was not found. Branch option cannot be selected.", this);
            EndChat();
            return;
        }

        if (!IsRunning)
        {
            return;
        }

        if (option == null)
        {
            Debug.LogWarning("[ChatManager] Selected branch option is null.", this);
            return;
        }

        if (!currentOptions.Contains(option))
        {
            Debug.LogWarning("[ChatManager] Selected branch option does not belong to the current interactable options.", this);
            return;
        }

        SelectBranch(option);
    }

    private void SelectBranch(BranchDBEventData option)
    {
        DHChatBranchRuleEvaluator.ExecuteTriggerEffect(option);

        if (option.Target_Talk_ID <= 0)
        {
            EndChat();
            return;
        }

        if (!catalog.TryGetChat(currentZoneId, option.Target_Talk_ID, out ChatDBEventData target) || target == null)
        {
            Debug.LogWarning($"[ChatManager] Branch target Chat_ID was not found. Zone: {currentZoneId}, Chat_ID: {option.Target_Talk_ID}", this);
            EndChat();
            return;
        }

        ShowChat(target);
    }

    public void Select(int optionIndex)
    {
        if (!IsRunning)
        {
            return;
        }

        if (optionIndex < 0 || optionIndex >= currentOptions.Count)
        {
            Debug.LogWarning($"[ChatManager] Branch option index is out of range. Index: {optionIndex}, Count: {currentOptions.Count}", this);
            return;
        }

        Select(currentOptions[optionIndex]);
    }

    public void EndChat()
    {
        currentChat = null;
        currentOptions.Clear();
        currentOptionStates.Clear();
        IsRunning = false;

        OnBranchShown?.Invoke(EmptyOptionStates);
        OnChatEnded?.Invoke();
    }

    private bool ResolveCatalog()
    {
        if (catalog != null)
        {
            return true;
        }

        catalog = EventScriptCatalog.Instance;
        return catalog != null;
    }

    private void ShowChat(ChatDBEventData chat)
    {
        currentChat = chat;
        currentOptions.Clear();
        currentOptionStates.Clear();
        IsRunning = true;

        OnChatShown?.Invoke(chat);

        if (chat == null || chat.Branch_Group_ID == 0)
        {
            OnBranchShown?.Invoke(EmptyOptionStates);
            return;
        }

        if (catalog.TryGetBranchOptions(currentZoneId, chat.Branch_Group_ID, out IReadOnlyList<BranchDBEventData> options) &&
            options != null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                BranchDBEventData option = options[i];
                if (option == null)
                {
                    continue;
                }

                bool isAvailable = IsBranchAvailable(option);

                if (string.IsNullOrWhiteSpace(option.Selection_Text))
                {
                    if (isAvailable)
                    {
                        SelectBranch(option);
                        return;
                    }

                    continue;
                }

                currentOptionStates.Add(new ChatBranchOptionState(option, isAvailable));
                if (isAvailable)
                {
                    currentOptions.Add(option);
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ChatManager] Branch options were not found. Zone: {currentZoneId}, Branch_Group_ID: {chat.Branch_Group_ID}", this);
        }

        OnBranchShown?.Invoke(currentOptionStates.Count > 0 ? currentOptionStates.ToArray() : EmptyOptionStates);
    }

    private bool IsBranchAvailable(BranchDBEventData option)
    {
        return DHChatBranchRuleEvaluator.IsBranchAvailable(option);
    }
}
