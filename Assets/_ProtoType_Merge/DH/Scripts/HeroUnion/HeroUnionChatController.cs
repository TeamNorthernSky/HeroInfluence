using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class HeroUnionChatController : MonoBehaviour
{
    private const string RuntimeObjectName = "[DH_HeroUnionChatController]";
    private const int InitialChatDelayFrames = 5;
    private const int InitialChatRetryFrames = 180;

    private bool isPlayingChat;
    private string pendingChatKey = string.Empty;
    private ChatManager subscribedChatManager;
    private Coroutine initialClaimedChatCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<HeroUnionChatController>() != null)
            return;

        GameObject root = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(root);
        root.AddComponent<HeroUnionChatController>();
    }

    private void OnEnable()
    {
        HeroUnionUnit.HeroUnionStateChanged += HandleHeroUnionStateChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        QueueInitialClaimedChatCheck();
    }

    private void OnDisable()
    {
        HeroUnionUnit.HeroUnionStateChanged -= HandleHeroUnionStateChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeFromChatManagerEnded();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        QueueInitialClaimedChatCheck();
    }

    private void QueueInitialClaimedChatCheck()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        if (initialClaimedChatCoroutine != null)
            StopCoroutine(initialClaimedChatCoroutine);

        initialClaimedChatCoroutine = StartCoroutine(PlayInitialClaimedChatsNextFrame());
    }

    private IEnumerator PlayInitialClaimedChatsNextFrame()
    {
        for (int i = 0; i < InitialChatDelayFrames; i++)
            yield return null;

        for (int frame = 0; frame < InitialChatRetryFrames; frame++)
        {
            if (TryPlayAnyInitialClaimedChat())
                break;

            if (isPlayingChat)
                break;

            yield return null;
        }

        initialClaimedChatCoroutine = null;
    }

    private bool TryPlayAnyInitialClaimedChat()
    {
        HeroUnionUnit[] heroUnions = FindObjectsByType<HeroUnionUnit>(FindObjectsSortMode.None);
        for (int i = 0; i < heroUnions.Length; i++)
        {
            HeroUnionUnit heroUnion = heroUnions[i];
            if (!IsInitialClaimedChatCandidate(heroUnion))
            {
                continue;
            }

            if (TryPlayCaptureChat(heroUnion))
                return true;
        }

        return false;
    }

    private static bool IsInitialClaimedChatCandidate(HeroUnionUnit heroUnion)
    {
        return heroUnion != null &&
            heroUnion.isActiveAndEnabled &&
            heroUnion.IsClaimedByHero &&
            heroUnion.AutoPlayWhenInitiallyClaimed;
    }

    private void HandleHeroUnionStateChanged(HeroUnionUnit heroUnion)
    {
        if (heroUnion == null || !heroUnion.IsClaimedByHero)
            return;

        TryPlayCaptureChat(heroUnion);
    }

    private bool TryPlayCaptureChat(HeroUnionUnit heroUnion)
    {
        if (!Application.isPlaying || isPlayingChat || heroUnion == null)
            return false;

        ZoneEntryGuidanceController.ApplyStoredGuidanceIfNeeded()?.TryCompleteFromClaimedHeroUnion(heroUnion);

        int chatId = heroUnion.CaptureChatId;
        if (chatId <= 0)
            return false;

        ChatManager chatManager = ChatManager.Instance;
        if (chatManager == null || chatManager.IsRunning)
            return false;

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null ||
            !catalog.TryGetChatTemplate(heroUnion.CaptureChatZoneId, chatId, out DHEventChatTemplate chat) ||
            chat == null)
        {
            return false;
        }

        string chatKey = heroUnion.GetCaptureChatProgressKey();
        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository != null && repository.IsHeroUnionChatCompleted(chatKey))
            return false;

        isPlayingChat = true;
        pendingChatKey = chatKey;
        SubscribeToChatManagerEnded(chatManager);
        ChatModalController.Show(heroUnion.CaptureChatZoneId, chatId, () => HandleChatClosed(chatKey));

        if (!chatManager.IsRunning)
        {
            CancelPendingChatWithoutCompletion();
            return false;
        }

        return true;
    }

    private void HandleChatClosed(string chatKey)
    {
        FinishPendingChat(chatKey);
    }

    private void HandleChatManagerEnded()
    {
        FinishPendingChat(pendingChatKey);
    }

    private void FinishPendingChat(string chatKey)
    {
        if (!isPlayingChat)
            return;

        TryCompleteGuidanceForClaimedHeroUnions();

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (!string.IsNullOrWhiteSpace(chatKey))
            repository?.MarkHeroUnionChatCompleted(chatKey);

        pendingChatKey = string.Empty;
        isPlayingChat = false;
        UnsubscribeFromChatManagerEnded();
    }

    private void CancelPendingChatWithoutCompletion()
    {
        pendingChatKey = string.Empty;
        isPlayingChat = false;
        UnsubscribeFromChatManagerEnded();
    }

    private void SubscribeToChatManagerEnded(ChatManager chatManager)
    {
        UnsubscribeFromChatManagerEnded();

        if (chatManager == null)
            return;

        subscribedChatManager = chatManager;
        subscribedChatManager.OnChatEnded += HandleChatManagerEnded;
    }

    private void UnsubscribeFromChatManagerEnded()
    {
        if (subscribedChatManager == null)
            return;

        subscribedChatManager.OnChatEnded -= HandleChatManagerEnded;
        subscribedChatManager = null;
    }

    private static void TryCompleteGuidanceForClaimedHeroUnions()
    {
        ZoneEntryGuidanceController guidanceController = ZoneEntryGuidanceController.ApplyStoredGuidanceIfNeeded();
        if (guidanceController == null || !guidanceController.Active)
            return;

        HeroUnionUnit[] heroUnions = FindObjectsByType<HeroUnionUnit>(FindObjectsSortMode.None);
        for (int i = 0; i < heroUnions.Length; i++)
        {
            HeroUnionUnit heroUnion = heroUnions[i];
            if (heroUnion == null || !heroUnion.isActiveAndEnabled || !heroUnion.IsClaimedByHero)
                continue;

            if (guidanceController.TryCompleteFromClaimedHeroUnion(heroUnion))
                return;
        }
    }
}
