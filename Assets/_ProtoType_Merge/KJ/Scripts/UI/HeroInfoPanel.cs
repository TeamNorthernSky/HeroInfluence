using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroInfoPanel : MonoBehaviour
{
    [SerializeField] private BattleFlowManager flowManager;
    [SerializeField] private Transform portrait;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text ipText;
    [SerializeField] private UnityEngine.UI.Image hpGauge;
    [SerializeField] private UnityEngine.UI.Image ipGauge;

    private BattleCharactor currentUnit;
    private Image portraitImage; // [JC 260621] RawImage→Image (PortraitLibrary Sprite 직접 사용)

    private void Awake()
    {
        if (portrait != null)
        {
            GameObject rawObj = new GameObject("Portrait_Image", typeof(RectTransform), typeof(Image));
            rawObj.transform.SetParent(portrait, false);
            RectTransform rt = rawObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            portraitImage = rawObj.GetComponent<Image>();
        }
    }

    private void OnEnable()
    {
        if (flowManager == null) flowManager = FindFirstObjectByType<BattleFlowManager>();
        if (flowManager != null)
            flowManager.OnTurnStarted += OnTurnStarted;
        if(hpGauge == null)
        {

        }
    }

    private void OnDisable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted -= OnTurnStarted;
        UnsubscribeUnit();
    }

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        UnsubscribeUnit();
        currentUnit = unit;

        if (currentUnit != null)
        {
            currentUnit.OnHpChanged += OnHpChanged;
            currentUnit.OnInfluenceChanged += OnIpChanged;
        }

        Refresh();
    }

    private void UnsubscribeUnit()
    {
        if (currentUnit == null) return;
        currentUnit.OnHpChanged -= OnHpChanged;
        currentUnit.OnInfluenceChanged -= OnIpChanged;
        currentUnit = null;
    }

    private void OnHpChanged(float current, float max) => UpdateHpText(current, max);
    private void OnIpChanged(float current, float max) => UpdateIpText(current, max);

    private void Refresh()
    {
        if (currentUnit == null)
        {
            if (nameText != null) nameText.text = "-";
            if (rankText != null) rankText.text = "-";
            if (hpText != null) hpText.text = "-";
            if (ipText != null) ipText.text = "-";
            if (portraitImage != null) portraitImage.sprite = null;
            if (hpGauge != null) hpGauge.fillAmount = 0f;
            if (ipGauge != null) ipGauge.fillAmount = 0f;
            return;
        }

        if (nameText != null)
            nameText.text = currentUnit.DisplayName; // [JC 260621] 클래스명 아닌 히어로명

        if (rankText != null)
        {
            // [JC 260622] 로비/HeroInfoModal과 동일 랭크 체계(RankUtil, 성장 V6.0). 레벨 파생.
            int rankLevel = currentUnit.SourceData != null ? currentUnit.SourceData.Level : currentUnit.Level;
            rankText.text = RankUtil.FromLevel(rankLevel);
        }

        UpdateHpText(currentUnit.CurrentHp, currentUnit.MaxHp);
        UpdateIpText(currentUnit.CurrentInfluence, currentUnit.MaxInfluence);
        UpdatePortrait();
    }

    private void UpdateHpText(float current, float max)
    {
        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        if (hpGauge != null)
        {
            hpGauge.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            Debug.Log($" HpGauge Percent : { hpGauge.fillAmount}");
        }
        
    }

    private void UpdateIpText(float current, float max)
    {
        if (ipText != null)
            ipText.text = $"{(int)current} / {(int)max}";
        if (ipGauge != null)
            ipGauge.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    private void UpdatePortrait()
    {
        if (portraitImage == null || currentUnit == null) return;

        // [JC 260621] 포트레이트 = PortraitLibrary(키=HeroIndex). 적/빌런은 SourceData null → Unselected.
        Sprite sp = currentUnit.SourceData != null
            ? Sprites.Portrait.Hero(currentUnit.SourceData.UnitTemplateKey)
            : Sprites.Portrait.Unselected;
        portraitImage.sprite = sp;
        portraitImage.enabled = sp != null;
    }

}
