using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroInfoPanel : MonoBehaviour
{
    [Tooltip("현재 행동 유닛과 턴 시작 이벤트를 제공하는 전투 흐름입니다.")]
    [SerializeField] private BattleFlowManager flowManager;
    [Tooltip("기존 씬의 초상화 생성 부모입니다. Portrait Image가 연결되어 있으면 사용하지 않습니다.")]
    [SerializeField] private Transform portrait;
    [Tooltip("현재 행동 유닛의 이름을 표시합니다.")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("현재 행동 유닛의 레벨에서 계산한 랭크를 표시합니다.")]
    [SerializeField] private TMP_Text rankText;
    [Tooltip("현재 HP와 최대 HP를 표시합니다.")]
    [SerializeField] private TMP_Text hpText;
    [Tooltip("현재 IP를 표시합니다. Show Max Ip를 켜면 최대 IP도 표시합니다.")]
    [SerializeField] private TMP_Text ipText;
    [Tooltip("HP 비율을 0~1 Fill Amount로 표시합니다. Image Type은 Filled를 사용합니다.")]
    [SerializeField] private UnityEngine.UI.Image hpGauge;
    [Tooltip("기존 씬의 IP 게이지입니다. 새 하단 UI는 숫자만 표시하므로 비워 둡니다.")]
    [SerializeField] private UnityEngine.UI.Image ipGauge;

    private BattleCharactor currentUnit;
    [Tooltip("씬에 미리 배치한 초상화 Image입니다. 연결하면 실행 중 초상화 오브젝트를 생성하거나 배치를 변경하지 않습니다.")]
    [SerializeField] private Image portraitImage;
    [Tooltip("켜면 현재 IP / 최대 IP로 표시합니다. 끄면 현재 IP만 표시합니다. 기존 씬 호환용 기본값은 켜짐입니다.")]
    [SerializeField] private bool showMaxIp = true;

    private void Awake()
    {
        if (portraitImage != null) return;

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
        {
            flowManager.OnTurnStarted += OnTurnStarted;
            OnTurnStarted(0, flowManager.CurrentUnit);
        }
    }

    private void OnDisable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted -= OnTurnStarted;
        UnsubscribeUnit();
    }

    private void LateUpdate()
    {
        // 현재 유닛은 턴 팝업 전에 결정됩니다. 행동 시작 이벤트를 기다리지 않고 표시만 맞춥니다.
        var unit = flowManager != null ? flowManager.CurrentUnit : null;
        if (currentUnit != unit) SetUnit(unit);
    }

    private void OnTurnStarted(int round, BattleCharactor unit) => SetUnit(unit);

    private void SetUnit(BattleCharactor unit)
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
            if (portraitImage != null)
            {
                portraitImage.sprite = null;
                portraitImage.enabled = false;
            }
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
        }
        
    }

    private void UpdateIpText(float current, float max)
    {
        if (ipText != null)
            ipText.text = showMaxIp ? $"{(int)current} / {(int)max}" : $"{(int)current}";
        if (ipGauge != null)
            ipGauge.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    private void UpdatePortrait()
    {
        if (portraitImage == null || currentUnit == null) return;

        // 턴 순서 UI와 같은 초상화 라이브러리를 사용합니다.
        Sprite sp;
        if (!currentUnit.IsPlayer)
            sp = Sprites.Portrait.Enemy(currentUnit.SourceEnemyData != null ? currentUnit.SourceEnemyData.UnitTemplateKey : null);
        else
            sp = currentUnit.SourceData != null
                ? Sprites.Portrait.Hero(currentUnit.SourceData.UnitTemplateKey)
                : Sprites.Portrait.Unselected;
        portraitImage.sprite = sp;
        portraitImage.enabled = sp != null;
    }

}
