using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>튜토리얼은 디스크에 저장하지 않으며 타이틀/오프닝으로 나가면 처음부터 시작한다.</summary>
public static class TutorialSessionPolicy
{
    private static bool tutorialSession;

    public static bool IsTutorialScene(string sceneName)
        => !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("Tutorial", StringComparison.OrdinalIgnoreCase);

    public static bool BlocksSaving
    {
        get
        {
            if (tutorialSession || IsTutorialScene(SceneManager.GetActiveScene().name)) return true;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (IsTutorialScene(SceneManager.GetSceneAt(i).name)) return true;
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        tutorialSession = false;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnActiveSceneChanged(Scene previous, Scene next)
    {
        if (IsTutorialScene(previous.name)) tutorialSession = true;
        HandleScene(next.name);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HandleScene(scene.name);

    private static void HandleScene(string sceneName)
    {
        if (IsTutorialScene(sceneName))
        {
            tutorialSession = true;
            return;
        }
        if (!tutorialSession) return;
        if (sceneName == "TitleScene" || sceneName == "Opening CutScene" || sceneName == "OpeningCutScene")
        {
            // 목적지의 Start보다 먼저 튜토리얼 진행 상태를 비운다.
            BattleReturnAutoSave.CancelPendingFollowup();
            CombatContext.Instance?.ClearTutorial();
            TutorialProgressRepository.ClearProgress();
            TutorialCatalog.DestroyAllTutorialCatalogs();
            DHTurnStartSnapshotStore.Instance?.ClearSnapshot();
            tutorialSession = false;
        }
        else if (sceneName == "GameLoadScene" || sceneName == "DHScene_3")
        {
            // 정상 완료 후 본게임으로 진입하면 본게임 저장은 허용한다.
            tutorialSession = false;
        }
    }
}
