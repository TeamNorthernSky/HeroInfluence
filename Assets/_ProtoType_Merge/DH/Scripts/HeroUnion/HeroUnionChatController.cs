using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class HeroUnionChatController : MonoBehaviour
{
    private const string RuntimeObjectName = "[DH_HeroUnionChatController]";
    // Scene transitions can create HeroUnions before ChatManager/DataStorage is ready; retry briefly instead of dropping the first-zone chat.
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
            // JC | 2026-11-11 | 에디터·빌드 공통 이펙트 준비 중에는 탐사씬이 로드돼도 로딩씬이 활성 상태입니다.
            // 진입·복원·페이드 완료를 기다리되, 로딩 시간을 대화 데이터 재시도 180프레임에서 소모하지 않습니다.
            while (IsExplorationEntryPending() ||
                   (SceneFadeController.Instance != null && SceneFadeController.Instance.IsTransitioning))
                yield return null;

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

        // JC | 2026-11-11 | 상태 변경 이벤트로 직접 호출돼도 로딩씬 소속 대화창이 생성되지 않도록 차단합니다.
        // 초기 점령 대화는 위 코루틴이 준비 완료 후 재시도하며, 협회가 속한 활성씬에 대화창을 생성합니다.
        if (IsExplorationEntryPending() || heroUnion.gameObject.scene != SceneManager.GetActiveScene())
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
