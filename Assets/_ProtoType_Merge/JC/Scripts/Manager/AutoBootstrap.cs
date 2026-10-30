using System.Collections.Generic;
using UnityEngine;

public static class AutoBootstrap
{
    private const string PrefabResourceKey = "GameManager";
    public const string AudioPrefabResourceKey = "BootAudioManager";

    // All BeforeSceneLoad bootstraps and scene Awake calls have completed here.
    // Preserve scene-authored audio (including preview scenes) instead of racing its Awake.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CompleteStartup()
    {
        EnsureAudioManager();
        string missing = GetMissingServices();
        if (missing.Length != 0)
            Debug.LogError("[AutoBootstrap] 필수 서비스 준비 실패: " + missing);
    }

    public static AudioManager EnsureAudioManager()
    {
        if (AudioManager.Instance != null) return AudioManager.Instance;
        var prefab = Resources.Load<GameObject>(AudioPrefabResourceKey);
        if (prefab == null || prefab.GetComponent<AudioManager>() == null)
        {
            Debug.LogError("[AutoBootstrap] Boot 사운드 원본 프리팹을 찾을 수 없습니다: Resources/" + AudioPrefabResourceKey);
            return null;
        }
        var go = Object.Instantiate(prefab);
        go.name = "AudioManager";
        return go.GetComponent<AudioManager>();
    }

    // Validation only: never reset game/save state or preload common UI here.
    // Also usable by diagnostics without running any initializers.
    public static string GetMissingServices()
    {
        var missing = new List<string>();
        var game = GameManager.Instance;
        Check(game, "GameManager", missing);
        if (game != null)
        {
            Check(game.Economy, "Economy", missing);
            Check(game.HQ, "HQ", missing);
            Check(game.Publicity, "Publicity", missing);
            Check(game.Training, "Training", missing);
            Check(game.Lab, "Lab", missing);
            Check(game.Workshop, "Workshop", missing);
            Check(game.Infirmary, "Infirmary", missing);
        }
        Check(GameSceneManager.Instance, "GameSceneManager", missing);
        Check(SceneFadeController.Instance, "SceneFadeController", missing);
        Check(CommonUIManager.Instance, "CommonUIManager", missing);
        Check(UnityEngine.EventSystems.EventSystem.current, "EventSystem", missing);
        Check(PersistentUnitRepository.Instance, "PersistentUnitRepository", missing);
        Check(PartyPersistentRepository.Instance, "PartyPersistentRepository", missing);
        Check(WeaponPersistentRepository.Instance, "WeaponPersistentRepository", missing);
        Check(EnemyGroupPersistentRepository.Instance, "EnemyGroupPersistentRepository", missing);
        Check(MapProgressRepository.Instance, "MapProgressRepository", missing);
        Check(CombatContext.Instance, "CombatContext", missing);
        Check(AudioManager.Instance, "AudioManager", missing);
        if (AudioManager.Instance != null && !AudioManager.Instance.IsReady)
            missing.Add("AudioManager catalog/sources");
        return string.Join(", ", missing);
    }

    private static void Check(Object service, string name, List<string> missing)
    {
        if (service == null) missing.Add(name);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Execute()
    {
        if (GameManager.Instance != null) return;

        var prefab = Resources.Load<GameObject>(PrefabResourceKey);
        if (prefab != null)
        {
            var go = Object.Instantiate(prefab);
            go.name = "GameManager";
            Object.DontDestroyOnLoad(go);
        }
        else
        {
            Debug.LogError("[AutoBootstrap] GameManager 프리팹 로드 실패 (Resources/GameManager 누락)");
        }
    }
}
