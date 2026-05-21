using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class FogGridBootstrap : MonoBehaviour
{
    [SerializeField] private FogSceneReferences references;
    [SerializeField] private bool autoFindOnAwake = true;
    [SerializeField] private bool verboseLogging = true;

    public FogSceneReferences References => references;

    private void Awake()
    {
        if (references == null)
            references = GetComponent<FogSceneReferences>();

        if (references == null)
            references = FindFirstObjectByType<FogSceneReferences>();

        if (references == null)
        {
            Debug.LogWarning("[FogGridBootstrap] FogSceneReferences not found.");
            return;
        }

        if (autoFindOnAwake)
            references.CollectReferences();

        if (verboseLogging)
            LogReferenceState();
    }

    [ContextMenu("Log Reference State")]
    public void LogReferenceState()
    {
        if (references == null)
        {
            Debug.LogWarning("[FogGridBootstrap] References object is null.");
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("[FogGridBootstrap] Reference state");
        builder.AppendLine($"- MainCamera: {GetName(references.MainCamera)}");
        builder.AppendLine($"- GridManager: {GetName(references.GridManager)}");
        builder.AppendLine($"- Pathfinder: {GetName(references.Pathfinder)}");
        builder.AppendLine($"- PartyRegistry: {GetName(references.PartyRegistry)}");
        builder.AppendLine($"- ResourceManager: {GetName(references.ResourceManager)}");
        builder.AppendLine($"- TurnManager: {GetName(references.TurnManager)}");
        builder.AppendLine($"- FogManager: {GetName(references.FogManager)}");
        builder.AppendLine($"- PartyMovers: {(references.PartyMovers == null ? 0 : references.PartyMovers.Length)}");

        Debug.Log(builder.ToString(), this);
    }

    private static string GetName(Object target)
    {
        return target == null ? "null" : target.name;
    }
}
