using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class SceneEntry
{
    public string sceneName;
    public string displayName;
    public bool isCircularNav;
}

[System.Serializable]
public class SceneRegistryData
{
    public List<SceneEntry> scenes = new List<SceneEntry>();
}

public class SceneLoader : MonoBehaviour
{
    private const string RegistryPath = "Assets/SceneRegistry.json";

    // 하드코딩 기본값 (JSON 파일이 없을 때 사용)
    private static readonly SceneEntry[] DefaultEntries =
    {
        new SceneEntry { sceneName = "TitleScene",  displayName = "타이틀 씬", isCircularNav = true },
        new SceneEntry { sceneName = "LobbyScene",  displayName = "로비 씬",   isCircularNav = true },
        new SceneEntry { sceneName = "PlayScene",   displayName = "플레이 씬", isCircularNav = true },
        new SceneEntry { sceneName = "EndingScene", displayName = "엔딩 씬",   isCircularNav = true },
    };

    // 등록된 모든 씬
    private List<SceneEntry> allEntries = new List<SceneEntry>();
    // 순환 순서 씬만 (이전/다음 버튼용)
    private List<string> circularOrder = new List<string>();
    // 표시명 캐시
    private Dictionary<string, string> displayNames = new Dictionary<string, string>();

    // 이력: List + Index (브라우저 뒤로/앞으로 패턴)
    private List<string> history = new List<string>();
    private int currentIndex = -1;
    private bool isLoading;

    // --- 이벤트 ---
    public event Action<string> OnSceneLoadStarted;
    public event Action<string> OnSceneLoadCompleted;

    public string CurrentSceneName { get; private set; }

    // 이력 패널 크기/위치 (씬 전환 시에도 유지)
    public Vector2 HistoryPanelSize { get; set; } = new Vector2(280f, 300f);
    public Vector2 HistoryPanelPosition { get; set; } = new Vector2(1550f, -60f);

    public void Initialize()
    {
        LoadSceneRegistry();
        CurrentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // --- 씬 레지스트리 로드 ---

    private void LoadSceneRegistry()
    {
        allEntries.Clear();

        // 하드코딩 기본값 추가
        allEntries.AddRange(DefaultEntries);

        // JSON 파일에서 추가 씬 로드
        string fullPath = Path.Combine(Application.dataPath, "..", RegistryPath);
        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                var data = JsonUtility.FromJson<SceneRegistryData>(json);
                if (data?.scenes != null)
                {
                    foreach (var entry in data.scenes)
                    {
                        // 중복 방지
                        if (!allEntries.Exists(e => e.sceneName == entry.sceneName))
                        {
                            allEntries.Add(entry);
                        }
                    }
                    Debug.Log($"[SceneLoader] SceneRegistry.json 로드 완료: {data.scenes.Count}개 추가 씬");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SceneLoader] SceneRegistry.json 파싱 실패: {e.Message}");
            }
        }

        // 캐시 구축
        circularOrder.Clear();
        displayNames.Clear();

        foreach (var entry in allEntries)
        {
            displayNames[entry.sceneName] = entry.displayName;
            if (entry.isCircularNav)
            {
                circularOrder.Add(entry.sceneName);
            }
        }

        // 기본 표시명 (등록되지 않은 씬용)
        displayNames.TryAdd("TestScene", "테스트 씬");
        displayNames.TryAdd("BootScene", "부트 씬");
    }

    // --- 씬 로드 이벤트 ---

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CurrentSceneName = scene.name;

        // 최초 씬이 이력에 없으면 추가
        if (history.Count == 0 && IsRegisteredScene(scene.name))
        {
            history.Add(scene.name);
            currentIndex = 0;
        }

        // SceneNavigationCanvas 비활성화 (프로토타입 단계에서 불필요)
        // if (IsRegisteredScene(scene.name))
        // {
        //     StartCoroutine(SpawnNavigationCanvas());
        // }

        OnSceneLoadCompleted?.Invoke(scene.name);
    }

    // --- 씬 전환: 이력에 새 항목 추가 ---

    public void LoadScene(string sceneName)
    {
        if (isLoading || sceneName == CurrentSceneName) return;

        // currentIndex 이후의 앞쪽 이력 삭제 (브라우저와 동일)
        if (currentIndex < history.Count - 1)
        {
            history.RemoveRange(currentIndex + 1, history.Count - currentIndex - 1);
        }

        history.Add(sceneName);
        currentIndex = history.Count - 1;

        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    // --- 다음 씬 (순환) ---

    public void LoadNextScene()
    {
        if (isLoading) return;

        if (HasForwardHistory())
        {
            currentIndex++;
            StartCoroutine(LoadSceneRoutine(history[currentIndex]));
        }
        else
        {
            string next = GetNextInOrder(CurrentSceneName);
            if (next != null) LoadScene(next);
        }
    }

    // --- 이전 씬 ---

    public void LoadPreviousScene()
    {
        if (isLoading) return;

        if (HasBackHistory())
        {
            currentIndex--;
            StartCoroutine(LoadSceneRoutine(history[currentIndex]));
        }
        else
        {
            string prev = GetPreviousInOrder(CurrentSceneName);
            if (prev != null)
            {
                history.Insert(0, prev);
                StartCoroutine(LoadSceneRoutine(prev));
            }
        }
    }

    // --- 이력 조회 ---

    public IReadOnlyList<string> History => history;
    public int CurrentIndex => currentIndex;

    public bool HasBackHistory()
    {
        return currentIndex > 0;
    }

    public bool HasForwardHistory()
    {
        return currentIndex < history.Count - 1;
    }

    // --- 순환 순서 조회 ---

    public string GetNextInOrder(string sceneName)
    {
        int index = circularOrder.IndexOf(sceneName);
        if (index < 0) return null;
        return circularOrder[(index + 1) % circularOrder.Count];
    }

    public string GetPreviousInOrder(string sceneName)
    {
        int index = circularOrder.IndexOf(sceneName);
        if (index < 0) return null;
        return circularOrder[(index - 1 + circularOrder.Count) % circularOrder.Count];
    }

    // --- 등록 씬 조회 ---

    public string[] GetDirectJumpTargets(string currentScene)
    {
        var targets = new List<string>();
        foreach (var entry in allEntries)
        {
            if (entry.sceneName != currentScene)
                targets.Add(entry.sceneName);
        }
        return targets.ToArray();
    }

    public string GetDisplayName(string sceneName)
    {
        return displayNames.TryGetValue(sceneName, out string name) ? name : sceneName;
    }

    public bool IsRegisteredScene(string sceneName)
    {
        return allEntries.Exists(e => e.sceneName == sceneName);
    }

    public IReadOnlyList<SceneEntry> GetAllEntries()
    {
        return allEntries.AsReadOnly();
    }

    // --- 내부 ---

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isLoading = true;
        OnSceneLoadStarted?.Invoke(sceneName);

        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
        {
            while (!op.isDone) yield return null;
        }
        else
        {
            Debug.LogError($"[SceneLoader] {sceneName} 로드 실패. Build Settings 등록 여부 확인");
        }

        isLoading = false;
    }
}
