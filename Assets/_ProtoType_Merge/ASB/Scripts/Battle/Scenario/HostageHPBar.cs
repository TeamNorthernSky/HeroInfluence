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

    private void Awake()
    {
        ResolveReferences();
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
