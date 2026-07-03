using UnityEngine;

[DisallowMultipleComponent]
public class DecorativeBuildingPlacement : MonoBehaviour
{
    [SerializeField] private string prefabKey;
    [SerializeField] private Vector3 anchorLocalOffset;

    public string PrefabKey => string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
    public Vector3 AnchorLocalOffset => anchorLocalOffset;

    public Vector3 GetRootPositionForAnchor(Vector3 anchorWorldPosition)
    {
        Matrix4x4 localToRoot = Matrix4x4.TRS(Vector3.zero, transform.rotation, transform.localScale);
        return anchorWorldPosition - localToRoot.MultiplyPoint3x4(anchorLocalOffset);
    }

    public void SetPrefabKey(string nextPrefabKey)
    {
        prefabKey = string.IsNullOrWhiteSpace(nextPrefabKey) ? string.Empty : nextPrefabKey.Trim();
    }

    public void SetAnchorLocalOffset(Vector3 nextAnchorLocalOffset)
    {
        anchorLocalOffset = nextAnchorLocalOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 anchorWorldPosition = transform.TransformPoint(anchorLocalOffset);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(anchorWorldPosition, transform.rotation, Vector3.one);
        Gizmos.color = new Color(1f, 0.65f, 0.15f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 0.02f, 1f));
        Gizmos.matrix = previousMatrix;

        Gizmos.color = new Color(1f, 0.85f, 0.15f, 1f);
        Gizmos.DrawWireSphere(anchorWorldPosition, 0.15f);
        Gizmos.DrawLine(transform.position, anchorWorldPosition);
    }
}
