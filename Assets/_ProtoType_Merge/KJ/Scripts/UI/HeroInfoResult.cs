using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroInfoResult : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI expValueText;
    [SerializeField] private TextMeshProUGUI ipValueText;
    [SerializeField] private Transform portrait;

    private const string ProfileFolder = "UI_Sprite/UI_Icon/CharacterProfile_temp/";
    //private const string fileName = "character icon sample";

    public void Apply(UnitRewardPreview preview)
    {
        Debug.Log($"[HeroInfoResult] Apply called | UnitIndex={preview.UnitIndex} | portrait={(portrait != null ? portrait.name : "NULL")}");
        if (portrait != null)
        {
            // [JC 260621] 포트레이트 = PortraitLibrary(키=HeroIndex). 함수 LoadPortraitByPartySlot은 존치(미사용).
            Sprite sp = EntityPortraits.HeroByUnit(preview.UnitIndex);
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
