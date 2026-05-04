using UnityEngine;

public class KJ_FOWManager : MonoBehaviour
{
    public static KJ_FOWManager Instance { get; private set; }

    public KJ_PlayGridManager Grid { get; private set; }
    public KJ_PlayFogManager FogOfWar { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeManagers();
    }

    private void InitializeManagers()
    {
        Grid = GetComponentInChildren<KJ_PlayGridManager>(true);
        if (Grid != null)
            Grid.Initialize();

        FogOfWar = GetComponentInChildren<KJ_PlayFogManager>(true);
        if (FogOfWar != null)
            FogOfWar.Initialize();
    }
}
