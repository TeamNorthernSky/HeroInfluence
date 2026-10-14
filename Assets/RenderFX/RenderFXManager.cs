using UnityEngine;

[DisallowMultipleComponent]
public class RenderFXManager : MonoBehaviour
{
    private const string PrefabResourcePath = "RenderFX/RenderFXManager";

    public static RenderFXManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab != null)
        {
            Instantiate(prefab);
            return;
        }

        new GameObject("RenderFXManager").AddComponent<RenderFXManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        gameObject.name = "RenderFXManager";
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
