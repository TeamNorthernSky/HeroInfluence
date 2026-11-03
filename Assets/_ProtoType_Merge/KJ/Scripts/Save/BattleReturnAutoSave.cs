using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Saves a committed battle return, then updates it just before its first automatic chat.</summary>
public sealed class BattleReturnAutoSave : MonoBehaviour
{
    private static BattleReturnAutoSave instance;
    public static bool IsRestoringCheckpoint { get; private set; }
    private GameSaveData.BattleReturnCheckpoint checkpoint;
    private bool waitingForFollowup;
    private int returnSceneHandle;
    private int returnFrame;
    private int quietFrames;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Ensure();
        BattleReturnSnapshotSection.Ensure(DHTurnStartSnapshotStore.EnsureInstance());
    }

    private static BattleReturnAutoSave Ensure()
    {
        if (instance != null) return instance;
        var host = new GameObject("[KJ_BattleReturnAutoSave]");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<BattleReturnAutoSave>();
        return instance;
    }

    public static void Commit(GameSaveData.BattleReturnCheckpoint resumeChat, string defeatedPartyId)
    {
        if (SaveSlotRepository.CurrentSlot < 0 || DHGameStateRestoreService.IsRestoring) return;
        var owner = Ensure();
        owner.checkpoint = resumeChat ?? new GameSaveData.BattleReturnCheckpoint();
        owner.checkpoint.defeatedPartyId = defeatedPartyId;
        owner.returnSceneHandle = SceneManager.GetActiveScene().handle;
        owner.returnFrame = Time.frameCount;
        owner.quietFrames = 0;
        owner.waitingForFollowup = owner.checkpoint.chatId <= 0;
        owner.SaveCheckpoint();
    }

    /// <summary>Called before Begin: rewards/flags in the first chat row have not executed yet.</summary>
    public static void BeforeChat(int zoneId, int chatId)
    {
        var owner = instance;
        if (owner == null || !owner.waitingForFollowup || chatId <= 0 ||
            DHGameStateRestoreService.IsRestoring ||
            SceneManager.GetActiveScene().handle != owner.returnSceneHandle) return;

        owner.waitingForFollowup = false;
        owner.checkpoint.zoneId = zoneId;
        owner.checkpoint.chatId = chatId;
        foreach (var union in FindObjectsByType<HeroUnionUnit>(FindObjectsSortMode.None))
            if (union.IsClaimedByHero && union.CaptureChatZoneId == zoneId && union.CaptureChatId == chatId)
            {
                owner.checkpoint.heroUnionChatKey = union.GetCaptureChatProgressKey();
                break;
            }
        owner.SaveCheckpoint();
    }

    public static void CancelPendingFollowup()
    {
        IsRestoringCheckpoint = false;
        if (instance != null) instance.waitingForFollowup = false;
    }

    public static void BeginCheckpointRestore(GameSaveData.BattleReturnCheckpoint saved)
    {
        IsRestoringCheckpoint = saved != null;
        if (instance != null) instance.waitingForFollowup = false;
    }

    private void SaveCheckpoint()
    {
        var session = EnemyTurnSessionRepository.Instance;
        checkpoint.enemyTurn = session != null && session.IsActive
            ? JsonUtility.FromJson<GameSaveData.EnemyTurnCheckpoint>(JsonUtility.ToJson(session)) : null;
        if (!GameSaveService.SaveBattleReturnToSlot(SaveSlotRepository.CurrentSlot, checkpoint))
            Debug.LogWarning("[BattleReturnAutoSave] 전투 복귀 자동 저장에 실패했습니다.");
    }

    private void LateUpdate()
    {
        if (!waitingForFollowup) return;
        if (SceneManager.GetActiveScene().handle != returnSceneHandle || DHGameEndState.IsEnding)
        {
            waitingForFollowup = false;
            return;
        }
        if (Time.frameCount - returnFrame < 6 ||
            (SceneFadeController.Instance != null && SceneFadeController.Instance.IsTransitioning) ||
            GateCameraFocusController.IsBusy ||
            (DHWorldEventConditionRuntimeManager.Instance != null && DHWorldEventConditionRuntimeManager.Instance.IsCheckPending) ||
            (DHWorldEventRuntimeManager.Instance != null && DHWorldEventRuntimeManager.Instance.IsRunning) ||
            ChatModalController.IsOpen)
        {
            quietFrames = 0;
            return;
        }
        if (++quietFrames >= 3) waitingForFollowup = false;
    }

    /// <summary>Restores only unfinished presentation/turn flow; combat rewards are already in the snapshot.</summary>
    public static void Restore(GameSaveData.BattleReturnCheckpoint saved)
    {
        if (instance != null) instance.waitingForFollowup = false;
        if (saved == null)
        {
            IsRestoringCheckpoint = false;
            return;
        }
        BattleReturnSnapshotSection.Ensure(DHTurnStartSnapshotStore.EnsureInstance()).SetCheckpoint(saved);
        if (!string.IsNullOrWhiteSpace(saved.defeatedPartyId))
            foreach (var party in FindObjectsByType<PartyGridMover>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var identity = party.GetComponent<PartyIdentity>();
                if (identity != null && identity.PartyId == saved.defeatedPartyId)
                {
                    DefeatedPartyReturnController.ScheduleReturn(party);
                    break;
                }
            }

        IsRestoringCheckpoint = false;
        DHEventBattleRuntimeManager.ResumeCompletedEventChat(saved);
        if (saved.enemyTurn == null || !saved.enemyTurn.isActive) return;
        RestoreEnemyTurn(saved.enemyTurn);
        FindFirstObjectByType<TurnManager>()?.EndPlayerTurn();
    }

    private static void RestoreEnemyTurn(GameSaveData.EnemyTurnCheckpoint saved)
    {
        var session = EnemyTurnSessionRepository.Instance;
        if (session == null)
            session = new GameObject("[EnemyTurnSessionRepository]").AddComponent<EnemyTurnSessionRepository>();
        session.BeginSession();
        if (saved.processedEnemyPlacementKeys != null)
            foreach (string key in saved.processedEnemyPlacementKeys)
            {
                session.SetCurrentEnemy(key, string.Empty, 0);
                session.CompleteCurrentEnemy();
            }
        session.InterruptForCombat(saved.currentEnemyPlacementKey, saved.currentEnemyId, saved.remainingMovePoints);
    }
}
