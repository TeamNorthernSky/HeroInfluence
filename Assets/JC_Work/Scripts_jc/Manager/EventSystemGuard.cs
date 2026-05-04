using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(EventSystem))]
public class EventSystemGuard : MonoBehaviour
{
    private EventSystem self;

    private void Awake()
    {
        self = GetComponent<EventSystem>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        // 초기 Recheck는 하지 않는다.
        // BeforeSceneLoad 시점에 다른 씬의 EventSystem이 아직 생성되지 않아 판정 불가.
        // AfterSceneLoad 훅과 sceneLoaded 이벤트가 이후 enable 여부를 결정한다.
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m) => Recheck();
    private void OnSceneUnloaded(Scene s) => Recheck();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitialRecheck()
    {
        foreach (EventSystemGuard guard in FindObjectsOfType<EventSystemGuard>())
            guard.Recheck();
    }

    private void Recheck()
    {
        if (self == null) self = GetComponent<EventSystem>();
        if (self == null) return;

        EventSystem[] all = FindObjectsOfType<EventSystem>();
        bool duplicateActive = false;
        foreach (EventSystem other in all)
        {
            if (other == self) continue;
            if (other.gameObject.activeInHierarchy && other.enabled) { duplicateActive = true; break; }
        }

        self.enabled = !duplicateActive;
    }
}
