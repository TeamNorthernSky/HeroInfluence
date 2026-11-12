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
    // JC | 2026-11-11 | 전투 HUD는 탐사 배지를 재사용합니다. 미연결된 기존 프리뷰 UI는 원래 텍스트 표시를 유지합니다.
    [SerializeField, Tooltip("이름 오른쪽에 표시할 탐사 공용 랭크 배지입니다. 연결 시 기존 Rank Text 문구는 표시하지 않습니다.")]
    private HeroRankBadge rankBadge;
    [Tooltip("현재 HP와 최대 HP를 표시합니다.")]
    [SerializeField] private TMP_Text hpText;
    [Tooltip("현재 IP를 표시합니다. Show Max Ip를 켜면 최대 IP도 표시합니다.")]
    [SerializeField] private TMP_Text ipText;
    [Tooltip("HP 비율을 0~1 Fill Amount로 표시합니다. Image Type은 Filled를 사용합니다.")]
    [SerializeField] private UnityEngine.UI.Image hpGauge;
    [Tooltip("기존 씬의 IP 게이지입니다. 새 하단 UI는 숫자만 표시하므로 비워 둡니다.")]
    [SerializeField] private UnityEngine.UI.Image ipGauge;

    [SerializeField, Tooltip("HP 레이블과 수치를 한 줄로 표시합니다.")] private bool inlineHp;
    private BattleCharactor currentUnit;
    // JC | 2026-11-12 | 이름 옆 성장 랭크와 별도로 IP 행에 현재 영향력 칭호를 표시합니다.
    private JcHeroInfluenceBadge influenceBadge;
    [Tooltip("씬에 미리 배치한 초상화 Image입니다. 연결하면 실행 중 초상화 오브젝트를 생성하거나 배치를 변경하지 않습니다.")]
    [SerializeField] private Image portraitImage;
    [Tooltip("켜면 현재 IP / 최대 IP로 표시합니다. 끄면 현재 IP만 표시합니다. 기존 씬 호환용 기본값은 켜짐입니다.")]
    [SerializeField] private bool showMaxIp = true;

    private void Awake()
    {
        Resources.Load<JC.BattleTesting.JcBattleHudLayout>("JC_BattleHud_Layout")?.ApplyBattlePanel(gameObject.scene);
        influenceBadge = JcHeroInfluenceBadge.Create(ipText);
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
            flowManager.OnEntryUiPrepared += OnTurnStarted;
            OnTurnStarted(0, flowManager.CurrentUnit);
        }
    }

    private void OnDisable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted -= OnTurnStarted;
            flowManager.OnEntryUiPrepared -= OnTurnStarted;
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
        if (influenceBadge) influenceBadge.SetUnit(unit);

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
            // JC | 2026-11-11 | 행동 유닛이 없으면 이전 유닛의 배지가 남지 않도록 숨깁니다.
            if (rankText != null) rankText.text = rankBadge != null ? string.Empty : "-";
            if (rankBadge != null) rankBadge.SetRank(null);
            if (hpText != null) hpText.text = inlineHp ? "HP  -" : "-";
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

        if (rankText != null || rankBadge != null)
        {
            // 현재 성장표의 F~SSS 등급을 사용한다.
            int rankLevel = currentUnit.SourceData != null ? currentUnit.SourceData.Level : currentUnit.Level;
            string rank = currentUnit.IsPlayer && (currentUnit.UsesTutorialEquipment || gameObject.scene.name == "TutorialBattleScene")
                // JC | 2026-11-04 | 튜토리얼 영웅은 해당 성장표의 레벨별 등급을 사용해 탐사와 전투 배지를 일치시킵니다.
                ? UnitRankLookup.GetTutorialRank(rankLevel, TutorialCatalog.Instance)
                : UnitRankLookup.GetRank(rankLevel);
            // JC | 2026-11-11 | 일반·튜토리얼의 기존 등급 판정은 유지하고 결과만 이름 옆 배지에 전달하며 랭크 문구는 비웁니다.
            if (rankText != null) rankText.text = rankBadge != null ? string.Empty : $"랭크: {rank}";
            if (rankBadge != null) rankBadge.SetRank(rank);
        }

        UpdateHpText(currentUnit.CurrentHp, currentUnit.MaxHp);
        UpdateIpText(currentUnit.CurrentInfluence, currentUnit.MaxInfluence);
        UpdatePortrait();
    }

    private void UpdateHpText(float current, float max)
    {
        if (hpText != null)
            hpText.text = $"{(inlineHp ? "HP  " : "")}{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        if (hpGauge != null)
        {
            hpGauge.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }
        
    }

    private void UpdateIpText(float current, float max)
    {
        if (influenceBadge) influenceBadge.SetInfluence(current, max);
        if (ipText != null)
            ipText.text = showMaxIp ? $"{(int)current} / {(int)max}" : $"{(int)current}";
        if (ipGauge != null)
            ipGauge.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    // JC | 2026-11-12 | 테스트 랭크 변경 후 턴 이벤트를 재발행하지 않고 표시만 갱신합니다.
    public void RefreshSimulationDisplay()
    {
        if (CombatContext.Instance != null && CombatContext.Instance.IsSimulation) Refresh();
    }

    private void UpdatePortrait()
    {
        if (portraitImage == null || currentUnit == null) return;

        // 턴 순서 UI와 같은 초상화 라이브러리를 사용합니다.
        Sprite sp;
        if (!currentUnit.IsPlayer)
            sp = Sprites.Portrait.EnemyByActor(currentUnit);
        else
            sp = currentUnit.SourceData != null
                ? Sprites.Portrait.Hero(currentUnit.SourceData.UnitTemplateKey)
                : Sprites.Portrait.Unselected;
        portraitImage.sprite = sp;
        JC.BattleTesting.JcBattleHudLayout.FitMaskedArt(portraitImage);
        portraitImage.enabled = sp != null;
    }

}
