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
            Transform found = transform.Find("HPCanvas") ?? transform.Find("IPCanvas");
            if (found != null)
                ipBarRect = found as RectTransform;
        }

        if (ipBarRect == null)
            ipBarRect = transform as RectTransform;

        if (ipCanvas == null && ipBarRect != null)
            ipCanvas = ipBarRect.GetComponent<Canvas>();

        if (fillImage == null && ipBarRect != null)
        {
            Transform ipFill = ipBarRect.Find("IPFill");
            if (ipFill != null)
                fillImage = ipFill.GetComponent<Image>();
        }
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
    [ContextMenu("HPCanvas 하위에 IP Fill 생성")]
    private void GenerateIPBarInEditor()
    {
        if (battleCharactor == null)
            battleCharactor = GetComponentInParent<BattleCharactor>();

        // HPCanvas를 부모로 사용
        Transform hpCanvasTransform = transform.Find("HPCanvas");
        if (hpCanvasTransform == null)
        {
            Debug.LogError("[UnitIPBar] HPCanvas를 찾을 수 없습니다. UnitHPBar의 HPCanvas가 먼저 생성되어야 합니다.");
            return;
        }

        ipBarRect = hpCanvasTransform as RectTransform;
        ipCanvas = hpCanvasTransform.GetComponent<Canvas>();

        // HPCanvas 하위에 IP Fill만 생성
        Transform existingFill = hpCanvasTransform.Find("IPFill");
        GameObject fillObj = existingFill != null ? existingFill.gameObject : new GameObject("IPFill");
        if (existingFill == null)
            fillObj.transform.SetParent(hpCanvasTransform, false);

        fillImage = fillObj.GetComponent<Image>() ?? fillObj.AddComponent<Image>();

        Sprite ipSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Resources/UI_Sprite/UI_Battle/UI_chracter_barIP.png");
        if (ipSprite != null)
            fillImage.sprite = ipSprite;

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.color = Color.white;

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        if (battleCharactor != null)
            UpdateIPBar(battleCharactor.CurrentInfluence, battleCharactor.MaxInfluence);

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[UnitIPBar] HPCanvas 하위에 IP Fill 생성 완료!");
    }
#endif
}
