using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [Tooltip("보상 적용 후 EXP 비율을 표시합니다. Image Type은 Filled로 설정합니다.")]
    [SerializeField] private Image expFill;
    [Tooltip("보상 적용 후 잔여 EXP / 다음 레벨 필요 EXP를 표시합니다. 데이터가 없으면 -를 표시합니다.")]
    [SerializeField] private TMP_Text expProgressText;
    [Tooltip("성장 테이블에서 조회한 보상 적용 후 랭크 아이콘입니다. 알 수 없는 랭크는 숨깁니다.")]
    [SerializeField] private Image rankImage;
    [Tooltip("UI_icon_rankF 등 이름으로 랭크와 연결하는 기존 아이콘 목록입니다.")]
    [SerializeField] private Sprite[] rankSprites;

    private const string ProfileFolder = "UI_Sprite/UI_Icon/CharacterProfile_temp/";
    //private const string fileName = "character icon sample";

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
        if (preview.HasExpPreview && preview.NewMaxExp > 0)
        {
            if (expFill != null) expFill.fillAmount = Mathf.Clamp01((float)preview.NewExp / preview.NewMaxExp);
            if (expProgressText != null) expProgressText.text = $"{preview.NewExp} / {preview.NewMaxExp}";
        }
        string rank = UnitRankLookup.GetRank(preview.NewLevel);
        if (rankImage != null && rankSprites != null)
        {
            foreach (var sprite in rankSprites)
                if (sprite != null && sprite.name == "UI_icon_rank" + rank)
                {
                    rankImage.sprite = sprite;
                    rankImage.enabled = true;
                    break;
                }
        }
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
