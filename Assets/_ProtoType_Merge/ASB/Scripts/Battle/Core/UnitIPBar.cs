using UnityEngine;
using UnityEngine.UI;

public class UnitIPBar : MonoBehaviour
{
    [SerializeField] private BattleCharactor battleCharactor;
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform ipBarRect;
    [SerializeField] private Canvas ipCanvas;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 80f);
    public float YaxisValue = 0.0f;

    private void Awake()
    {
        if (battleCharactor == null)
            battleCharactor = GetComponentInParent<BattleCharactor>();

        ResolveReferences();
    }

    private void OnEnable()
    {
        if (battleCharactor != null)
        {
            battleCharactor.OnInfluenceChanged += UpdateIPBar;
            UpdateIPBar(battleCharactor.CurrentInfluence, battleCharactor.MaxInfluence);
        }
    }

    private void OnDisable()
    {
        if (battleCharactor != null)
            battleCharactor.OnInfluenceChanged -= UpdateIPBar;
    }

    private void LateUpdate()
    {
        UpdateScreenPosition();
    }

    private void UpdateIPBar(float currentIP, float maxIP)
    {
        if (fillImage == null) return;

        if (maxIP <= 0f)
        {
            fillImage.fillAmount = 0f;
            return;
        }

        fillImage.fillAmount = Mathf.Clamp01(currentIP / maxIP);
    }

    private void UpdateScreenPosition()
    {
        if (battleCharactor == null || ipBarRect == null)
        {
            SetVisible(false);
            return;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 screenPosition = targetCamera.WorldToScreenPoint(battleCharactor.transform.position);
        if (screenPosition.z <= 0f)
        {
            SetVisible(false);
            return;
        }

        screenPosition.x += screenOffset.x;
        screenPosition.y += screenOffset.y;

        Vector3 worldPos = targetCamera.ScreenToWorldPoint(screenPosition);
        ipBarRect.position = new Vector3(worldPos.x, worldPos.y + YaxisValue, worldPos.z);
        ipBarRect.rotation = targetCamera.transform.rotation;
        SetVisible(true);
    }

    private void ResolveReferences()
    {
        if (ipBarRect == null)
        {
            Transform existingCanvas = transform.Find("IPCanvas");
            if (existingCanvas != null)
                ipBarRect = existingCanvas as RectTransform;
        }

        if (ipBarRect == null)
            ipBarRect = transform as RectTransform;

        if (ipCanvas == null && ipBarRect != null)
            ipCanvas = ipBarRect.GetComponent<Canvas>();
    }

    private void SetVisible(bool isVisible)
    {
        if (ipCanvas != null)
        {
            ipCanvas.enabled = isVisible;
            return;
        }

        if (fillImage != null)
            fillImage.enabled = isVisible;
    }

#if UNITY_EDITOR
    [ContextMenu("머리 위 IP 바 생성")]
    private void GenerateIPBarInEditor()
    {
        if (battleCharactor == null)
            battleCharactor = GetComponentInParent<BattleCharactor>();

        Transform existingCanvas = transform.Find("IPCanvas");
        GameObject canvasObj = existingCanvas != null ? existingCanvas.gameObject : new GameObject("IPCanvas");
        if (existingCanvas == null)
            canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null)
            canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        if (canvasObj.GetComponent<CanvasScaler>() == null)
            canvasObj.AddComponent<CanvasScaler>();

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(100f, 15f);
        canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        canvasRect.localPosition = Vector3.zero;
        ipBarRect = canvasRect;
        ipCanvas = canvas;

        Transform existingBg = canvasObj.transform.Find("Background");
        GameObject bgObj = existingBg != null ? existingBg.gameObject : new GameObject("Background");
        if (existingBg == null)
            bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bgObj.GetComponent<Image>() ?? bgObj.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.8f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

        Transform existingFill = canvasObj.transform.Find("Fill");
        GameObject fillObj = existingFill != null ? existingFill.gameObject : new GameObject("Fill");
        if (existingFill == null)
            fillObj.transform.SetParent(canvasObj.transform, false);
        fillImage = fillObj.GetComponent<Image>() ?? fillObj.AddComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.color = new Color(0.2f, 0.5f, 1f);

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        if (battleCharactor != null)
            UpdateIPBar(battleCharactor.CurrentInfluence, battleCharactor.MaxInfluence);

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[UnitIPBar] IP 바 UI 생성 완료!");
    }
#endif
}
