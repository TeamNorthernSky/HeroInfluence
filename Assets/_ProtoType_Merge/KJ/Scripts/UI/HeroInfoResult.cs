using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HeroInfoResult : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI expValueText;
    [SerializeField] private TextMeshProUGUI ipValueText;
    [SerializeField] private Transform portrait;

    [Header("Scene Result Card")]
    [Tooltip("미리 배치한 초상화입니다. 연결된 새 카드에서는 실행 중 초상화를 생성하지 않습니다.")]
    [SerializeField] private Image portraitImage;
    [Tooltip("결과 캐릭터 이름을 표시합니다.")]
    [SerializeField] private TMP_Text unitNameText;
    [Tooltip("EXP의 기존 구간을 표시합니다. 획득 연출 종료 후 최종 비율로 맞춥니다. Image Type은 Filled입니다.")]
    [SerializeField] private Image expFill;
    [Tooltip("EXP 충전 구간 전용 이미지입니다. 기존 ExpFill 뒤에 배치하고 같은 스프라이트·크기를 사용합니다. 미연결 카드는 기존 즉시 표시를 유지합니다.")]
    [SerializeField] private Image expGainFill;
    [Tooltip("보상 적용 후 잔여 EXP / 다음 레벨 필요 EXP를 표시합니다. 데이터가 없으면 -를 표시합니다.")]
    [SerializeField] private TMP_Text expProgressText;
    [Tooltip("성장 테이블에서 조회한 보상 적용 후 랭크 아이콘입니다. 알 수 없는 랭크는 숨깁니다.")]
    [SerializeField] private Image rankImage;
    [Tooltip("UI_icon_rankF 등 이름으로 랭크와 연결하는 기존 아이콘 목록입니다.")]
    [SerializeField] private Sprite[] rankSprites;

    private const string ProfileFolder = "UI_Sprite/UI_Icon/CharacterProfile_temp/";
    //private const string fileName = "character icon sample";

    private struct ExpStep { public int From, To, Maximum; }
    private enum RewardPhase { Idle, Pending, Exp, Boundary, Rank, Complete }
    private readonly List<ExpStep> expSteps = new List<ExpStep>();
    private UnitRewardPreview rewardPreview;
    private BattleResultRewardVisualSettings rewardSettings;
    private RewardPhase phase;
    private int stepIndex;
    private float elapsed, progress;
    private Sprite oldRank, newRank;
    private Material gainMaterial, flashMaterial, originalRankMaterial;
    private bool rankMaterialCaptured;
    private static readonly int Effect = Shader.PropertyToID("_Effect");
    private static readonly int EffectColor = Shader.PropertyToID("_EffectColor");

    private BattleResultIPPresentation ipPresentation;
    private bool ipStageReleased = true;
    public bool IsExpRankComplete => rewardPreview == null || phase == RewardPhase.Complete;
    public bool IsIPComplete => ipPresentation == null || ipPresentation.IsComplete;

    public void HoldIPPresentation() => ipStageReleased = false;
    public void ReleaseIPPresentation() => ipStageReleased = true;

    public void SkipExpToRankFlash()
    {
        if (IsExpRankComplete || phase == RewardPhase.Rank) return;
        ShowFinalExp();
        BeginRankFlash();
    }

    public void SkipIPToCompletionEcho() => ipPresentation?.SkipToCompletionEcho();

    private void OnEnable() => ipPresentation?.RestorePendingDisplay();

    private void Update()
    {
        // 카드가 비활성인 스킬 안내 중에는 Update가 실행되지 않아 진행되지 않는다.
        if (isActiveAndEnabled)
            AdvancePresentation(Time.unscaledDeltaTime);
    }

    private void AdvancePresentation(float delta)
    {
        AdvanceRewardPresentation(delta);
        if (IsExpRankComplete && ipStageReleased) ipPresentation?.Tick(delta);
    }

    private void AdvanceRewardPresentation(float delta)
    {
        if (phase != RewardPhase.Idle && phase != RewardPhase.Complete && rewardSettings == null)
        { CompleteExpRankPresentation(); return; }
        if (phase == RewardPhase.Pending)
        {
            if (expSteps.Count == 0) BeginRankFlash();
            else BeginExpStep();
            return;
        }
        if (phase == RewardPhase.Boundary)
        {
            stepIndex++;
            if (stepIndex < expSteps.Count) BeginExpStep();
            else { ShowFinalExp(); BeginRankFlash(); }
            return;
        }
        if (phase == RewardPhase.Exp)
        {
            var motion = rewardSettings != null ? rewardSettings.expGain : null;
            float duration = motion != null ? Mathf.Max(0f, motion.duration) : 0f;
            elapsed += Mathf.Max(0f, delta);
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            progress = Mathf.Max(progress, motion != null ? motion.Evaluate(t) : t);
            var step = expSteps[stepIndex];
            float value = Mathf.Lerp(step.From, step.To, progress);
            expGainFill.fillAmount = value / step.Maximum;
            SetEffect(expGainFill, gainMaterial, rewardSettings.expGainColors != null ? rewardSettings.expGainColors.Evaluate(t) : Color.white, 1f);
            if (expProgressText != null) expProgressText.text = $"{Mathf.FloorToInt(value)} / {step.Maximum}";
            if (t >= 1f)
            {
                expFill.fillAmount = (float)step.To / step.Maximum;
                expGainFill.enabled = false;
                // 가득 찬 프레임을 표시한 뒤 다음 프레임에 다음 레벨의 0으로 전환한다.
                phase = RewardPhase.Boundary;
            }
        }
        else if (phase == RewardPhase.Rank)
        {
            elapsed += Mathf.Max(0f, delta);
            float duration = rewardSettings != null ? Mathf.Max(0f, rewardSettings.rankFlashDuration) : 0f;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            rankImage.sprite = t < 0.5f ? oldRank : newRank;
            SetEffect(rankImage, flashMaterial, Color.white, t < 0.5f ? t * 2f : (1f - t) * 2f);
            if (t >= 1f) CompleteExpRankPresentation();
        }
    }

    private void BeginExpStep()
    {
        elapsed = progress = 0f;
        var step = expSteps[stepIndex];
        expFill.fillAmount = (float)step.From / step.Maximum;
        expGainFill.fillAmount = expFill.fillAmount;
        expGainFill.enabled = true;
        SetEffect(expGainFill, gainMaterial, Color.white, 1f);
        if (expProgressText != null) expProgressText.text = $"{step.From} / {step.Maximum}";
        phase = RewardPhase.Exp;
        if (rewardSettings.expGain == null || rewardSettings.expGain.duration <= 0f)
        { ShowFinalExp(); BeginRankFlash(); }
    }

    private void BeginRankFlash()
    {
        if (rewardPreview == null || !rewardPreview.HasLevelUp || oldRank == null || newRank == null || oldRank == newRank || rankImage == null ||
            flashMaterial == null || rewardSettings == null || rewardSettings.rankFlashDuration <= 0f)
        { CompleteExpRankPresentation(); return; }
        rankImage.sprite = oldRank;
        rankImage.enabled = true;
        rankImage.material = flashMaterial;
        SetEffect(rankImage, flashMaterial, Color.white, 0f);
        elapsed = 0f;
        phase = RewardPhase.Rank;
    }

    private static void SetEffect(Image image, Material material, Color color, float amount)
    {
        if (material == null || image == null) return;
        material.SetColor(EffectColor, color); material.SetFloat(Effect, amount);
        // Mask 하위에서는 Unity가 만든 스텐실 재질에도 같은 표시값을 전달한다.
        var rendered = image.materialForRendering;
        if (rendered != null && rendered != material)
        { rendered.SetColor(EffectColor, color); rendered.SetFloat(Effect, amount); }
    }

    private Sprite FindRank(int level)
    {
        string rank = UnitRankLookup.GetRank(level);
        if (rankSprites != null)
            foreach (var sprite in rankSprites)
                if (sprite != null && sprite.name == "UI_icon_rank" + rank) return sprite;
        return null;
    }

    private bool BuildExpSteps(UnitRewardPreview preview)
    {
        expSteps.Clear();
        if (!preview.HasExpPreview || preview.GainedExp <= 0 || preview.OldMaxExp <= 0) return true;
        // 표시용 스냅샷만 생성한다. Repository의 실제 유닛이나 보상을 변경하지 않는다.
        var snapshot = new UnitPersistentData(preview.UnitIndex, string.Empty, preview.OldLevel,
            default, default, 0, 0, default, default, 0f, preview.OldExp, preview.OldMaxExp);
        PersistentUnitRepository.SimulateExpProgress(snapshot, preview.GainedExp, out int finalLevel, out int finalExp, out int finalMax);
        if (finalLevel != preview.NewLevel || finalExp != preview.NewExp || finalMax != preview.NewMaxExp) return false;
        int remaining = preview.GainedExp, consumed = 0, current = preview.OldExp, maximum = preview.OldMaxExp;
        while (remaining > 0 && maximum > 0)
        {
            int amount = Mathf.Min(remaining, maximum - current);
            if (amount <= 0) return false;
            expSteps.Add(new ExpStep { From = current, To = current + amount, Maximum = maximum });
            consumed += amount; remaining -= amount;
            PersistentUnitRepository.SimulateExpProgress(snapshot, consumed, out _, out current, out maximum);
        }
        return true;
    }

    private void ShowFinalExp()
    {
        if (rewardPreview == null) return;
        bool valid = rewardPreview.HasExpPreview && rewardPreview.NewMaxExp > 0;
        if (expFill != null) expFill.fillAmount = valid ? Mathf.Clamp01((float)rewardPreview.NewExp / rewardPreview.NewMaxExp) : 0f;
        if (expProgressText != null) expProgressText.text = valid ? $"{rewardPreview.NewExp} / {rewardPreview.NewMaxExp}" : "-";
        if (expGainFill != null) expGainFill.enabled = false;
    }

    public void CompleteRewardPresentation()
    {
        CompleteExpRankPresentation();
        ipPresentation?.CompleteImmediately();
    }

    private void CompleteExpRankPresentation()
    {
        if (rewardPreview == null) return;
        ShowFinalExp();
        if (rankImage != null)
        {
            rankImage.sprite = newRank; rankImage.enabled = newRank != null;
            if (rankMaterialCaptured) rankImage.material = originalRankMaterial;
        }
        phase = RewardPhase.Complete;
    }

    private void ReleaseRewardMaterials()
    {
        if (expGainFill != null) { expGainFill.enabled = false; expGainFill.material = null; }
        if (rankImage != null && rankMaterialCaptured) rankImage.material = originalRankMaterial;
        if (gainMaterial != null) { if (Application.isPlaying) Destroy(gainMaterial); else DestroyImmediate(gainMaterial); }
        if (flashMaterial != null) { if (Application.isPlaying) Destroy(flashMaterial); else DestroyImmediate(flashMaterial); }
        gainMaterial = flashMaterial = null;
        rankMaterialCaptured = false;
    }

    private void OnDestroy()
    {
        ipPresentation?.Dispose();
        ReleaseRewardMaterials();
    }

    private void OnDisable()
    {
        if (ipPresentation != null && ipPresentation.HasStarted) CompleteRewardPresentation();
    }

    public void Apply(UnitRewardPreview preview)
    {
        if (portraitImage != null)
        {
            ApplySceneCard(preview);
            return;
        }
        Debug.Log($"[HeroInfoResult] Apply called | UnitIndex={preview.UnitIndex} | portrait={(portrait != null ? portrait.name : "NULL")}");
        if (portrait != null)
        {
            // [JC 260621] 포트레이트 = PortraitLibrary(키=HeroIndex). 함수 LoadPortraitByPartySlot은 존치(미사용).
            Sprite sp = Sprites.Portrait.HeroByUnit(preview.UnitIndex);
            if (sp != null)
            {
                GameObject rawObj = new GameObject("Portrait_Image", typeof(RectTransform), typeof(Image));
                rawObj.transform.SetParent(portrait, false);
                rawObj.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
                RectTransform rt = rawObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rawObj.GetComponent<Image>().sprite = sp;
            }
        }

        if (expValueText != null)
        {
            if (preview.GainedExp > 0)
            {
                string levelUp = preview.HasLevelUp ? $" (Lv.{preview.OldLevel}→{preview.NewLevel})" : "";
                expValueText.text = $"+{preview.GainedExp}{levelUp}";
            }
            else
            {
                expValueText.text = "-";
            }
        }

        if (ipValueText != null)
        {
            float delta = preview.InfluenceDelta;
            string sign = delta >= 0 ? "+" : "";
            ipValueText.text = $"{sign}{delta:F0}";
        }
    }

    public void ClearDisplay()
    {
        ipPresentation?.Dispose(); ipPresentation = null;
        ipStageReleased = true;
        phase = RewardPhase.Idle; rewardPreview = null; expSteps.Clear();
        ReleaseRewardMaterials();
        if (portraitImage != null) { portraitImage.sprite = null; portraitImage.enabled = false; }
        if (rankImage != null) { rankImage.sprite = null; rankImage.enabled = false; }
        if (unitNameText != null) unitNameText.text = "";
        if (expValueText != null) expValueText.text = "-";
        if (ipValueText != null) ipValueText.text = "-";
        if (expProgressText != null) expProgressText.text = "-";
        if (expFill != null) expFill.fillAmount = 0f;
    }

    public void ShowWithoutReward(int unitIndex)
    {
        ClearDisplay();
        SetPortrait(unitIndex);
    }

    private void SetPortrait(int unitIndex)
    {
        if (portraitImage == null) return;
        portraitImage.sprite = Sprites.Portrait.HeroByUnit(unitIndex);
        portraitImage.enabled = portraitImage.sprite != null;
    }

    private void ApplySceneCard(UnitRewardPreview preview)
    {
        ClearDisplay();
        if (preview == null) return;
        SetPortrait(preview.UnitIndex);
        if (unitNameText != null) unitNameText.text = preview.UnitName ?? "";
        if (expValueText != null) expValueText.text = $"+{preview.GainedExp}";
        if (ipValueText != null) ipValueText.text = $"{preview.OldInfluence:F0} → {preview.NewInfluence:F0}";
        rewardPreview = preview;
        var ipSettings = BattleResultIPGainSettings.ForScene(gameObject.scene);
        if (ipValueText != null && ipSettings != null && preview.InfluenceDelta >= 0)
            ipPresentation = new BattleResultIPPresentation(ipValueText, preview.OldInfluence, preview.NewInfluence, ipSettings);
        oldRank = FindRank(preview.OldLevel); newRank = FindRank(preview.NewLevel);
        rewardSettings = BattleResultRewardVisualSettings.ForScene(gameObject.scene);
        if (rewardSettings == null || rewardSettings.tintMaterial == null || expGainFill == null || expFill == null || !BuildExpSteps(preview))
        { CompleteExpRankPresentation(); return; }
        gainMaterial = new Material(rewardSettings.tintMaterial) { hideFlags = HideFlags.HideAndDontSave };
        flashMaterial = new Material(rewardSettings.tintMaterial) { hideFlags = HideFlags.HideAndDontSave };
        expGainFill.material = gainMaterial;
        if (rankImage != null)
        {
            originalRankMaterial = rankImage.material; rankMaterialCaptured = true;
            rankImage.sprite = oldRank; rankImage.enabled = oldRank != null;
        }
        if (preview.HasExpPreview && preview.OldMaxExp > 0)
        {
            expFill.fillAmount = Mathf.Clamp01((float)preview.OldExp / preview.OldMaxExp);
            if (expProgressText != null) expProgressText.text = $"{preview.OldExp} / {preview.OldMaxExp}";
        }
        stepIndex = 0; phase = RewardPhase.Pending;
    }

    public static Sprite LoadPortraitByPartySlot(int unitIndex)
    {
        var repo = PartyPersistentRepository.Instance;
        if (repo == null) { Debug.Log("[HeroInfoResult] repo is null"); return null; }
        if (repo.Parties.Count == 0) { Debug.Log("[HeroInfoResult] Parties is empty"); return null; }

        var unitIndices = repo.Parties[0].UnitIndices;
        Debug.Log($"[HeroInfoResult] Party[0] UnitIndices: [{string.Join(", ", unitIndices)}] | looking for unitIndex={unitIndex}");

        int slot = -1;
        for (int i = 0; i < unitIndices.Count; i++)
            if (unitIndices[i] == unitIndex) { slot = i; break; }

        if (slot < 0) { Debug.Log($"[HeroInfoResult] unitIndex={unitIndex} not found in party"); return null; }

        int iconNumber = (slot % 4) + 1;
        if (iconNumber == 2) iconNumber = 4;
        else if (iconNumber == 4) iconNumber = 2;
        string path = $"{ProfileFolder}character icon sample 0{iconNumber}";
        Sprite sp = Resources.Load<Sprite>(path);
        Debug.Log($"[HeroInfoResult] slot={slot} iconNumber={iconNumber} path={path} sprite={(sp != null ? "OK" : "NULL")}");
        return sp;
    }
}
