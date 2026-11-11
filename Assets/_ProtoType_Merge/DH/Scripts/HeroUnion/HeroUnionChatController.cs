using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class HeroUnionChatController : MonoBehaviour
{
    private const string RuntimeObjectName = "[DH_HeroUnionChatController]";

    private bool isPlayingChat;
    private string pendingChatKey = string.Empty;
    private ChatManager subscribedChatManager;
    private readonly HashSet<HeroUnionUnit> pendingCaptureChats = new HashSet<HeroUnionUnit>();

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
        SceneFadeController.SceneEntryCompleted += HandleSceneEntryCompleted;
        TryPlayPendingOrInitialChat();
    }

    private void OnDisable()
    {
        pendingCaptureChats.Clear();
        HeroUnionUnit.HeroUnionStateChanged -= HandleHeroUnionStateChanged;
        SceneFadeController.SceneEntryCompleted -= HandleSceneEntryCompleted;
        UnsubscribeFromChatManagerEnded();
    }

    private void HandleSceneEntryCompleted(Scene scene)
    {
        if (scene == SceneManager.GetActiveScene())
            TryPlayPendingOrInitialChat();
    }

    private void TryPlayPendingOrInitialChat()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !IsExplorationReadyForChat())
            return;

        var pending = new List<HeroUnionUnit>(pendingCaptureChats);
        pendingCaptureChats.Clear();
        foreach (HeroUnionUnit heroUnion in pending)
        {
            if (heroUnion != null && heroUnion.isActiveAndEnabled && heroUnion.IsClaimedByHero &&
                TryPlayCaptureChat(heroUnion))
                return;
        }

        TryPlayAnyInitialClaimedChat();
    }

    private static bool IsExplorationReadyForChat()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        string explorationScene = GameSceneManager.Instance != null
            ? GameSceneManager.Instance.ExplorationScene : "DHScene";
        string loadingScene = GameSceneManager.Instance != null
            ? GameSceneManager.Instance.GameLoadScene : "GameLoadScene";
        return activeScene.IsValid() && activeScene.isLoaded &&
            activeScene.name == explorationScene &&
            !SceneManager.GetSceneByName(loadingScene).IsValid() &&
            !IsExplorationEntryPending() &&
            SceneFadeController.Instance != null && SceneFadeController.Instance.IsSceneEntryCompleted(activeScene);
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

        if (!IsExplorationReadyForChat())
        {
            pendingCaptureChats.Add(heroUnion);
            return;
        }

        TryPlayCaptureChat(heroUnion);
    }

    private bool TryPlayCaptureChat(HeroUnionUnit heroUnion)
    {
        if (!Application.isPlaying || isPlayingChat || heroUnion == null ||
            !IsExplorationReadyForChat() || heroUnion.gameObject.scene != SceneManager.GetActiveScene())
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
            // Missing chat data is tolerated while later zones are still being authored.
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

    // JC | 2026-11-11 | 기존 탐사 진입·저장 복원 상태를 재사용하여 초기화 도중 대화가 시작되는 것을 막습니다.
    private static bool IsExplorationEntryPending()
    {
        return DHGameStateRestoreService.IsRestoring ||
            (GameManager.Instance != null && GameManager.Instance.IsExplorationEntryPending);
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
