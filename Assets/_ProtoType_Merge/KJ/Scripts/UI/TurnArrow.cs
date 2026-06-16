using UnityEngine;
using UnityEngine.UI;

public class TurnArrow : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Graphic arrowGraphic;
    [SerializeField] private Renderer arrowRenderer;

    private Transform target;
    private Vector2 screenOffset;
    private float Scale;
    public float YVal= 0.0f;
    private void Awake()
    {
        ResolveReferences();
        SetEnabled(false);
        Scale = Screen.height / 1080f;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            SetEnabled(false);
            return;
        }

        //Vector3 screenPosition = targetCamera.WorldToScreenPoint(target.position);
        Vector3 screenPosition = new Vector3(targetCamera.WorldToScreenPoint(target.position).x, targetCamera.WorldToScreenPoint(target.position).y + YVal, targetCamera.WorldToScreenPoint(target.position).z);

        if (screenPosition.z <= 0f)
        {
            SetEnabled(false);
            return;
        }

        //screenPosition.x += screenOffset.x;
        //screenPosition.y += screenOffset.y;
        screenPosition.x += (screenOffset.x * Scale);
        screenPosition.y += (screenOffset.y * Scale);

        transform.position = targetCamera.ScreenToWorldPoint(screenPosition);
        transform.rotation = targetCamera.transform.rotation;
        SetEnabled(true);
    }

    public void Follow(Transform followTarget, Vector2 offset)
    {
        target = followTarget;
        screenOffset = offset;
        SetEnabled(target != null);
    }

    public void Follow(Transform followTarget)
    {
        Follow(followTarget, Vector2.zero);
    }

    private void ResolveReferences()
    {
        if (arrowGraphic == null)
        {
            arrowGraphic = GetComponent<Graphic>();
        }

        if (arrowRenderer == null)
        {
            arrowRenderer = GetComponent<Renderer>();
        }
    }

    public void SetEnabled(bool isEnabled)
    {
        if (arrowGraphic != null)
        {
            arrowGraphic.enabled = isEnabled;
        }

        if (arrowRenderer != null)
        {
            arrowRenderer.enabled = isEnabled;
        }
    }
}
