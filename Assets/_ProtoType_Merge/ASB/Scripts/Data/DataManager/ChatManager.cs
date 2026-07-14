using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChatManager : MonoBehaviour
{
    public static ChatManager Instance { get; private set; }

    [SerializeField] private EventScriptCatalog catalog;
    [SerializeField] private bool dontDestroyOnLoad = true;

    private static readonly IReadOnlyList<BranchDBEventData> EmptyOptions = Array.Empty<BranchDBEventData>();

    private readonly List<BranchDBEventData> currentOptions = new List<BranchDBEventData>();

    private EventScriptChannel currentChannel;
    private ChatDBEventData currentChat;

    public event Action<ChatDBEventData> OnChatShown;
    public event Action<IReadOnlyList<BranchDBEventData>> OnBranchShown;
    public event Action OnChatEnded;

    public bool IsRunning { get; private set; }
    public EventScriptChannel CurrentChannel => currentChannel;
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

    public void StartChat(EventScriptChannel channel, int startChatId)
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

        if (!catalog.TryGetChat(channel, startChatId, out ChatDBEventData chat) || chat == null)
        {
            Debug.LogWarning($"[ChatManager] Chat ID was not found. Channel: {channel}, Chat_ID: {startChatId}", this);
            EndChat();
            return;
        }

        currentChannel = channel;
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
        if (!catalog.TryGetChat(currentChannel, nextChatId, out ChatDBEventData next) || next == null)
        {
            Debug.LogWarning($"[ChatManager] Next Chat_ID was not found. Channel: {currentChannel}, Chat_ID: {nextChatId}", this);
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
            Debug.LogWarning("[ChatManager] Selected branch option does not belong to the current chat.", this);
            return;
        }

        if (option.Target_Talk_ID <= 0)
        {
            EndChat();
            return;
        }

        if (!catalog.TryGetChat(currentChannel, option.Target_Talk_ID, out ChatDBEventData target) || target == null)
        {
            Debug.LogWarning($"[ChatManager] Branch target Chat_ID was not found. Channel: {currentChannel}, Chat_ID: {option.Target_Talk_ID}", this);
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
        IsRunning = false;

        OnBranchShown?.Invoke(EmptyOptions);
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
        IsRunning = true;

        OnChatShown?.Invoke(chat);

        if (chat == null || chat.Branch_Group_ID == 0)
        {
            OnBranchShown?.Invoke(EmptyOptions);
            return;
        }

        if (catalog.TryGetBranchOptions(currentChannel, chat.Branch_Group_ID, out IReadOnlyList<BranchDBEventData> options) &&
            options != null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                BranchDBEventData option = options[i];
                if (option != null && IsBranchAvailable(option))
                {
                    currentOptions.Add(option);
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ChatManager] Branch options were not found. Channel: {currentChannel}, Branch_Group_ID: {chat.Branch_Group_ID}", this);
        }

        OnBranchShown?.Invoke(currentOptions.Count > 0 ? currentOptions.ToArray() : EmptyOptions);
    }

    private bool IsBranchAvailable(BranchDBEventData option)
    {
        // TODO(trigger): Evaluate Trigger_Type, Trigger_Value, and Trigger_Effect when branch conditions are defined.
        return true;
    }
}
