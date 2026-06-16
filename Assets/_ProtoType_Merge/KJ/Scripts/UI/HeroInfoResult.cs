using TMPro;
using UnityEngine;

public class HeroInfoResult : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI expValueText;
    [SerializeField] private TextMeshProUGUI ipValueText;

    public void Apply(UnitRewardPreview preview)
    {
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
        //else
        //{
        //    ipValueText.text = "NULL";
        //}
    }
}
