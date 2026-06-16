using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitHPBar : MonoBehaviour
{
    [SerializeField] private BattleCharactor battleCharactor;
    [SerializeField] private Quaternion rotateOffset;
    [SerializeField] private Image hpFillImage;
    [SerializeField] private Image ipFillImage;
    [SerializeField] private RectTransform hpBarRect;
    [SerializeField] private Canvas hpCanvas;
    [SerializeField] private RectTransform ipBarRect;
    [SerializeField] private Canvas ipCanvas;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 100f);
    [SerializeField] private RawImage HPFrame;
    [SerializeField] private TMP_Text hpGaugeText;
    [SerializeField] private TMP_Text ipGaugeText;
    public float YaxisValue = 0.0f;

    private void Awake()
    {
        if (battleCharactor == null)
        {
            battleCharactor = GetComponentInParent<BattleCharactor>();
        }
        
        ResolveReferences();
        
    }

    private void OnEnable()
    {
        if (battleCharactor != null)
        {
            battleCharactor.OnHpChanged += UpdateHPBar;
            battleCharactor.OnInfluenceChanged += UpdateIPBar;
            UpdateHPBar(battleCharactor.CurrentHp, battleCharactor.MaxHp);
            UpdateIPBar(battleCharactor.CurrentInfluence, battleCharactor.MaxInfluence);
            rotateOffset = hpCanvas.transform.rotation;
            screenOffset = new Vector2(0f, 100f);
        }
    }

    private void OnDisable()
    {
        if (battleCharactor != null)
        {
            battleCharactor.OnHpChanged -= UpdateHPBar;
            battleCharactor.OnInfluenceChanged -= UpdateIPBar;
        }
    }

    private void LateUpdate()
    {
        UpdateScreenPosition();
    }

    private void UpdateHPBar(float currentHp, float maxHp)
    {
        if (hpFillImage == null)
        {
            Debug.LogWarning($"[HPBar] hpFillImage is null on {gameObject.name}");
            return;
        }

        if (maxHp <= 0f)
        {
            Debug.LogWarning($"[HPBar] maxHp <= 0 on {gameObject.name}");
            hpFillImage.fillAmount = 0f;
            return;
        }

        hpFillImage.fillAmount = Mathf.Clamp01(currentHp / maxHp);

        if (hpGaugeText != null)
            hpGaugeText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";
    }

    private void UpdateIPBar(float currentIP, float maxIP)
    {
        if (ipFillImage == null) return;

        if (maxIP <= 0f)
        {
            ipFillImage.fillAmount = 0f;
            if (ipGaugeText != null)
                ipGaugeText.text = $"0 / 0";
            return;
        }

        ipFillImage.fillAmount = Mathf.Clamp01(currentIP / maxIP);

        if (ipGaugeText != null)
            ipGaugeText.text = $"{(int)currentIP} / {(int)maxIP}";
    }

    private void UpdateScreenPosition()
    {
        if (battleCharactor == null || hpBarRect == null)
        {
            SetVisible(false);
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

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

        //hpBarRect.position = targetCamera.ScreenToWorldPoint(screenPosition);
        hpBarRect.position = new Vector3 (targetCamera.ScreenToWorldPoint(screenPosition).x, targetCamera.ScreenToWorldPoint(screenPosition).y+ YaxisValue, targetCamera.ScreenToWorldPoint(screenPosition).z);
        //hpBarRect.rotation = targetCamera.transform.rotation;
        hpCanvas.transform.rotation = rotateOffset;
        SetVisible(true);
    }

    private void ResolveReferences()
    {
        if (hpBarRect == null)
        {
            Transform existingCanvas = transform.Find("HPCanvas");
            if (existingCanvas != null)
                hpBarRect = existingCanvas as RectTransform;
        }

        if (hpBarRect == null)
            hpBarRect = transform as RectTransform;

        if (hpCanvas == null && hpBarRect != null)
            hpCanvas = hpBarRect.GetComponent<Canvas>();

        if (hpFillImage == null && hpBarRect != null)
        {
            Transform fill = hpBarRect.Find("Fill");
            if (fill != null)
                hpFillImage = fill.GetComponent<Image>();
        }

        if (ipBarRect == null)
        {
            Transform existingIPCanvas = transform.Find("IPCanvas");
            if (existingIPCanvas != null)
                ipBarRect = existingIPCanvas as RectTransform;
        }

        if (ipCanvas == null && ipBarRect != null)
            ipCanvas = ipBarRect.GetComponent<Canvas>();

        if (ipFillImage == null && hpBarRect != null)
        {
            Transform ipFill = hpBarRect.Find("IPFill");
            if (ipFill != null)
                ipFillImage = ipFill.GetComponent<Image>();
        }
    }

    private void SetVisible(bool isVisible)
    {
        if (hpCanvas != null)
            hpCanvas.enabled = isVisible;
        else if (hpFillImage != null)
            hpFillImage.enabled = isVisible;

        if (ipCanvas != null)
            ipCanvas.enabled = isVisible;
        else if (ipFillImage != null)
            ipFillImage.enabled = isVisible;
    }

#if UNITY_EDITOR
    [ContextMenu("머리 위 HP 바 생성")]
    private void GenerateHPBarInEditor()
    {
        if (battleCharactor == null)
        {
            battleCharactor = GetComponentInParent<BattleCharactor>();
        }

        Transform existingCanvas = transform.Find("HPCanvas");
        GameObject canvasObj = existingCanvas != null ? existingCanvas.gameObject : new GameObject("HPCanvas");
        if (existingCanvas == null)
        {
            canvasObj.transform.SetParent(transform, false);
        }

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = canvasObj.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.WorldSpace;

        if (canvasObj.GetComponent<CanvasScaler>() == null)
        {
            canvasObj.AddComponent<CanvasScaler>();
        }

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(100f, 15f);
        canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        canvasRect.localPosition = Vector3.zero;
        hpBarRect = canvasRect;
        hpCanvas = canvas;

        Transform existingBg = canvasObj.transform.Find("Background");
        GameObject bgObj = existingBg != null ? existingBg.gameObject : new GameObject("Background");
        if (existingBg == null)
        {
            bgObj.transform.SetParent(canvasObj.transform, false);
        }
        Image bgImage = bgObj.GetComponent<Image>();
        if (bgImage == null)
        {
            bgImage = bgObj.AddComponent<Image>();
        }
        bgImage.color = new Color(0f, 0f, 0f, 0.8f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // HP Fill
        Transform existingFill = canvasObj.transform.Find("Fill");
        GameObject fillObj = existingFill != null ? existingFill.gameObject : new GameObject("Fill");
        if (existingFill == null)
            fillObj.transform.SetParent(canvasObj.transform, false);
        hpFillImage = fillObj.GetComponent<Image>() ?? fillObj.AddComponent<Image>();
        hpFillImage.type = Image.Type.Filled;
        hpFillImage.fillMethod = Image.FillMethod.Horizontal;
        hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpFillImage.color = (battleCharactor != null && battleCharactor.IsPlayer) ? Color.green : Color.red;

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        // IP Fill
        Transform existingIPFill = canvasObj.transform.Find("IPFill");
        GameObject ipFillObj = existingIPFill != null ? existingIPFill.gameObject : new GameObject("IPFill");
        if (existingIPFill == null)
            ipFillObj.transform.SetParent(canvasObj.transform, false);
        ipFillImage = ipFillObj.GetComponent<Image>() ?? ipFillObj.AddComponent<Image>();

        Sprite ipSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Resources/UI_Sprite/UI_Battle/UI_chracter_barIP.png");
        if (ipSprite != null)
            ipFillImage.sprite = ipSprite;

        ipFillImage.type = Image.Type.Filled;
        ipFillImage.fillMethod = Image.FillMethod.Horizontal;
        ipFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        ipFillImage.color = Color.white;

        RectTransform ipFillRect = ipFillObj.GetComponent<RectTransform>();
        ipFillRect.anchorMin = Vector2.zero;
        ipFillRect.anchorMax = Vector2.one;
        ipFillRect.offsetMin = ipFillRect.offsetMax = Vector2.zero;

        if (battleCharactor != null)
        {
            UpdateHPBar(battleCharactor.CurrentHp, battleCharactor.MaxHp);
            UpdateIPBar(battleCharactor.CurrentInfluence, battleCharactor.MaxInfluence);
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[UnitHPBar] HP/IP 바 UI 생성 완료!");
    }
#endif
}
