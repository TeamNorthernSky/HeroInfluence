using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ChatRewardResultView : MonoBehaviour
{
    private static readonly Color Blue = new Color32(0, 70, 180, 255);
    private static readonly Color Red = new Color32(210, 30, 40, 255);
    private static readonly string[] HeroKeys = { "10001", "10004", "10003", "10002" };
    [SerializeField] private Sprite portraitMaskSprite;

    public void Bind(DHEventRewardTemplate reward)
    {
        if (reward == null) return;
        Set("Money", reward.Money); Set("Medal", reward.Medal); Set("Gem", reward.Gem); Set("Supply", reward.Supply);
        TMP_Text exp = FindText("Experience");
        exp.gameObject.SetActive(reward.Exp != 0);
        exp.text = "EXP " + Signed(reward.Exp);
        BindCharacters(reward);
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }

    private void BindCharacters(DHEventRewardTemplate reward)
    {
        Transform previous = transform.Find("CharacterRewards");
        if (previous != null)
        {
            previous.gameObject.SetActive(false);
            Destroy(previous.gameObject);
        }
        var root = new GameObject("CharacterRewards", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;
        TMP_Text style = FindText("Money");
        for (int i = 0; i < HeroKeys.Length; i++)
        {
            string key = HeroKeys[i];
            float x = -311 + i * 207;
            CreatePortrait(root.transform, key, i);
            int hp = 0, atk = 0, def = 0, influence = 0, heal = 0;
            foreach (DHEventCharacterRewardEntry entry in reward.CharacterRewards)
            {
                if (entry.UnitTemplateKey != key) continue;
                hp += entry.MaxHp; atk += entry.Atk; def += entry.Def;
                influence += entry.Influence; heal += entry.Heal;
            }
            int row = 0;
            AddStat(root.transform, style, key, x, ref row, "IP", influence);
            AddStat(root.transform, style, key, x, ref row, "HP", hp);
            AddStat(root.transform, style, key, x, ref row, "ATK", atk);
            AddStat(root.transform, style, key, x, ref row, "DEF", def);
            AddStat(root.transform, style, key, x, ref row, "Heal", heal);
        }
    }

    private void CreatePortrait(Transform parent, string key, int index)
    {
        // Match the four portrait openings baked into UI_event_box_result (835 x 570).
        // Normalized anchors keep the masks aligned when the result card stretches.
        float centerX = (127.5f + index * 193.5f) / 835f;
        float centerY = 1f - 284f / 570f;
        var maskObject = new GameObject("PortraitMask_" + key,
            typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
        maskObject.transform.SetParent(parent, false);
        var maskRect = (RectTransform)maskObject.transform;
        var halfSize = new Vector2(48f / 835f, 48f / 570f);
        var center = new Vector2(centerX, centerY);
        maskRect.anchorMin = center - halfSize;
        maskRect.anchorMax = center + halfSize;
        maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;
        var maskImage = maskObject.GetComponent<UnityEngine.UI.Image>();
        maskImage.sprite = portraitMaskSprite;
        maskImage.raycastTarget = false;
        maskObject.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

        CreateIcon(maskRect, "Portrait_" + key, Sprites.Portrait.Hero(key), Vector2.zero, 100);
        var portrait = maskRect.GetChild(0).GetComponent<UnityEngine.UI.Image>();
        portrait.rectTransform.anchorMin = Vector2.zero;
        portrait.rectTransform.anchorMax = Vector2.one;
        portrait.rectTransform.offsetMin = portrait.rectTransform.offsetMax = Vector2.zero;
        portrait.preserveAspect = false;
    }

    private static void AddStat(Transform parent, TMP_Text style, string key, float x, ref int row, string stat, int value)
    {
        if (value == 0) return;
        float y = -85 - row++ * 36;
        string iconKey = stat == "Heal" ? "HP" : stat;
        Sprite sprite = Resources.Load<Sprite>("Icon_UI_Sprite/Icon_UI_StatusIcon_Sprite/UI_icon_" + iconKey);
        CreateIcon(parent, key + "_" + stat + "Icon", sprite, new Vector2(x - 60, y), 30);
        TMP_Text label = Instantiate(style, parent, false);
        label.name = key + "_" + stat;
        label.rectTransform.anchoredPosition = new Vector2(x + 22, y);
        label.rectTransform.sizeDelta = new Vector2(130, 34);
        label.fontSizeMax = 28;
        label.fontSizeMin = 16;
        label.text = (stat == "Heal" ? "회복 " : "") + Signed(value);
        label.color = value < 0 ? Red : Blue;
        label.raycastTarget = false;
    }

    private static void CreateIcon(Transform parent, string name, Sprite sprite, Vector2 position, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
    }

    private void Set(string key, int value)
    {
        TMP_Text text = FindText(key);
        text.gameObject.SetActive(true);
        text.text = value == 0 ? "-" : Signed(value);
        text.color = value < 0 ? Red : Blue;
    }

    private TMP_Text FindText(string key)
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            if (text.name == key) return text;
        return GetComponentInChildren<TMP_Text>(true);
    }

    private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
}
