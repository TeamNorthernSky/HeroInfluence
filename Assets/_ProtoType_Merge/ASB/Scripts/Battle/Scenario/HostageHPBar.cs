using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HostageHPBar : MonoBehaviour
{
    [SerializeField] private HostageBattleActor hostage;
    [SerializeField] private Canvas hpCanvas;
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TMP_Text hpGaugeText;
    private RectTransform barRect;
    private RectTransform hpRow;
    private Image frame;
    private Camera targetCamera;
    private JC.BattleTesting.JcBattlePresentationProfile presentation;
    private float anchorHeight = 2f;

    private void Awake()
    {
        ResolveReferences();
        CreateModernBar();
        SetVisible(false);
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (hostage == null || hostage.MaxHp <= 0f)
        {
            SetVisible(false);
            return;
        }

        float hpRatio = Mathf.Clamp01(hostage.CurrentHp / hostage.MaxHp);
        if (hpFillImage != null)
            hpFillImage.fillAmount = hpRatio;

        if (hpGaugeText != null)
            hpGaugeText.text = $"{Mathf.CeilToInt(hostage.CurrentHp)} / {Mathf.CeilToInt(hostage.MaxHp)}";

        if (barRect != null) UpdatePresentation();
        else SetVisible(true);
    }

    private void CreateModernBar()
    {
        presentation = JC.BattleTesting.JcBattlePresentationProfile.Load();
        if (presentation == null || presentation.unitBarPrefab == null) return;
        // 기존 인질 수치/피해 로직은 그대로 두고 표시 Canvas만 교체합니다.
        if (hpCanvas != null) hpCanvas.gameObject.SetActive(false);
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            anchorHeight = Mathf.Max(anchorHeight, renderer.bounds.max.y - transform.position.y);
        var instance = Instantiate(presentation.unitBarPrefab, transform, false);
        instance.name = "HPCanvas_Hostage";
        barRect = instance.GetComponent<RectTransform>();
        hpCanvas = instance.GetComponent<Canvas>();
        hpRow = barRect.Find("HP Background") as RectTransform;
        frame = hpRow.GetComponent<Image>();
        hpFillImage = hpRow.Find("HP Fill").GetComponent<Image>();
        hpGaugeText = hpRow.Find("HP GaugeText").GetComponent<TMP_Text>();
        var loss = hpRow.Find("Loss");
        if (loss != null) loss.gameObject.SetActive(false);
        var ip = barRect.Find("IP Background");
        if (ip != null) ip.gameObject.SetActive(false);
        foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }

    private void UpdatePresentation()
    {
        if (targetCamera == null || !targetCamera.isActiveAndEnabled) targetCamera = Camera.main;
        if (targetCamera == null) { SetVisible(false); return; }
        var settings = BattleUnitBarVisualSettings.ForScene(gameObject.scene);
        float widthFactor = Mathf.Clamp(presentation.hostageWidthScale, .5f, 1f);
        float heightFactor = Mathf.Clamp(presentation.hostageThicknessScale, .5f, 1f);
        float height = (settings != null ? settings.rowHeight : 22f) * heightFactor / widthFactor;
        hpRow.anchorMin = hpRow.anchorMax = new Vector2(.5f, .5f);
        hpRow.anchoredPosition = Vector2.zero;
        foreach (var rect in hpRow.GetComponentsInChildren<RectTransform>(true))
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        hpGaugeText.fontSize = (settings != null ? settings.fontSize : 17f) * heightFactor / widthFactor;
        frame.color = presentation.hostageBorder;
        var screen = targetCamera.WorldToScreenPoint(transform.position + Vector3.up * anchorHeight);
        if (screen.z <= targetCamera.nearClipPlane || screen.z >= targetCamera.farClipPlane)
        { SetVisible(false); return; }
        float resolution = targetCamera.pixelHeight / 1080f;
        Vector2 offset = settings != null ? settings.screenOffset : new Vector2(0, 24);
        screen.x += offset.x * resolution; screen.y += offset.y * resolution;
        barRect.SetPositionAndRotation(targetCamera.ScreenToWorldPoint(screen), targetCamera.transform.rotation);
        float width = (settings != null ? settings.screenWidth : 170f) * widthFactor;
        float scale = Vector3.Distance(targetCamera.ScreenToWorldPoint(screen + Vector3.right * width * resolution), barRect.position) / barRect.rect.width;
        var parentScale = barRect.parent.lossyScale;
        barRect.localScale = new Vector3(scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
            scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.z)));
        hpCanvas.worldCamera = targetCamera;
        SetVisible(true);
    }

    private void ResolveReferences()
    {
        if (hostage == null)
            hostage = GetComponent<HostageBattleActor>();

        Transform canvasTransform = transform.Find("HPCanvas");
        if (hpCanvas == null && canvasTransform != null)
            hpCanvas = canvasTransform.GetComponent<Canvas>();

        if (hpFillImage == null && canvasTransform != null)
        {
            Transform fillTransform = canvasTransform.Find("Fill");
            if (fillTransform != null)
                hpFillImage = fillTransform.GetComponent<Image>();
        }

        if (hpGaugeText == null && canvasTransform != null)
        {
            Transform gaugeTextTransform = canvasTransform.Find("HP GaugeText");
            if (gaugeTextTransform != null)
                hpGaugeText = gaugeTextTransform.GetComponent<TMP_Text>();
        }
    }

    private void SetVisible(bool isVisible)
    {
        if (hpCanvas != null)
            hpCanvas.enabled = isVisible;
    }
}
