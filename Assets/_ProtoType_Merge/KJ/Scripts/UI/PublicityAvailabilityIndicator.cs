using UnityEngine;

// Keep this on the button so hiding the child marker does not stop refreshes.
public sealed class PublicityAvailabilityIndicator : MonoBehaviour
{
    private GameObject marker;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Publicity Button" && t.Find("Publicity Alarm") != null
                    && t.GetComponent<PublicityAvailabilityIndicator>() == null)
                    t.gameObject.AddComponent<PublicityAvailabilityIndicator>();
    }

    private void Awake()
    {
        var child = transform.Find("Publicity Alarm");
        if (child != null) marker = child.gameObject;
    }

    private void OnEnable() => Refresh();
    private void Update() => Refresh();

    private void Refresh()
    {
        if (marker == null) return;
        var gm = GameManager.Instance;
        bool available = false;
        if (gm != null && gm.Publicity != null && gm.Economy != null)
        {
            int cost = gm.Publicity.GetProgressCost();
            available = gm.Publicity.CanProgress(1) && cost >= 0
                && gm.Economy.Has(ResourceType.Money, cost);
        }
        if (marker.activeSelf != available) marker.SetActive(available);
    }
}
