using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance { get; private set; }

    [SerializeField] private EffectRegistry _registry;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public GameObject SpawnById(int id, Vector3 position, Quaternion rotation)
    {
        if (_registry == null)
        {
            return null;
        }

        GameObject prefab = _registry.Get(id);
        if (prefab == null)
        {
            return null;
        }

        return Instantiate(prefab, position, rotation);
    }

    public GameObject SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        return Instantiate(prefab, position, rotation);
    }
}
