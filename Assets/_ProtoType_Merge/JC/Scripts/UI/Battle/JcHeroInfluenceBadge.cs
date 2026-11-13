using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// JC | 2026-11-12 | 현재 IP 칭호는 성장 랭크 배지와 분리합니다. 기존 IP 행과 공용 툴팁을 재사용합니다.
public sealed class JcHeroInfluenceBadge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // JC | 2026-11-13 | IP 최하위 칭호를 듣보로 변경하며 배지와 툴팁 안내에 같은 명칭을 표시합니다.
    static readonly string[] Titles = { "듣보", "유망주", "베테랑", "국가권력" };
    // JC | 2026-11-13 | 구형 미리보기의 기본 색도 공용 설정과 같은 정의를 사용합니다.
    static readonly JcInfluenceBadgeConfiguration DefaultColors = new JcInfluenceBadgeConfiguration();
    TMP_Text reference, label;
    RectTransform rect;
    JcInfluenceBadgeFace face;
    Material lettering;
    BattleCharactor unit;
    float current, maximum, sweepTime;
    int tier = -1;
    bool hovering;

    public static int Tier(float ip) => ip < 50 ? 0 : ip < 100 ? 1 : ip < 140 ? 2 : 3;
    public static string Title(float ip) => Titles[Tier(ip)];

    public static JcHeroInfluenceBadge Create(TMP_Text ipText)
    {
        if (!ipText) return null;
        var existing = ipText.transform.parent.Find("InfluenceBadge");
        if (existing) return existing.GetComponent<JcHeroInfluenceBadge>();
        var go = new GameObject("InfluenceBadge", typeof(RectTransform), typeof(JcInfluenceBadgeFace));
        go.layer = ipText.gameObject.layer;
        go.transform.SetParent(ipText.transform.parent, false);
        var badge = go.AddComponent<JcHeroInfluenceBadge>();
        badge.reference = ipText;
        badge.rect = (RectTransform)go.transform;
        badge.face = go.GetComponent<JcInfluenceBadgeFace>();
        var text = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.layer = go.layer;
        text.transform.SetParent(go.transform, false);
        badge.label = text.GetComponent<TextMeshProUGUI>();
        badge.label.font = ipText.font;
        badge.label.fontStyle = ipText.fontStyle;
        badge.label.color = Color.white;
        badge.label.alignment = TextAlignmentOptions.Midline;
        badge.label.enableWordWrapping = false;
        badge.label.raycastTarget = false;
        badge.label.margin = Vector4.zero;
        var tr = badge.label.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(8, 0); tr.offsetMax = new Vector2(-8, 0);
        // 원본 폰트 재질을 바꾸지 않고 밝은 글자 위쪽에 미세한 어두운 음각 경계를 줍니다.
        badge.lettering = new Material(ipText.fontSharedMaterial) { name = "JC IP 배지 음각 (임시)", hideFlags = HideFlags.DontSave };
        badge.lettering.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        badge.lettering.SetColor(ShaderUtilities.ID_OutlineColor, new Color(.04f,.05f,.07f,.9f));
        badge.lettering.SetFloat(ShaderUtilities.ID_OutlineWidth, .10f);
        badge.lettering.EnableKeyword("UNDERLAY_ON");
        badge.lettering.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0,0,0,.65f));
        badge.lettering.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, .35f);
        badge.lettering.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .15f);
        badge.label.fontSharedMaterial = badge.lettering;
        var icon = ipText.transform.parent.Find("IpIcon")?.GetComponent<Image>();
        if (icon) { icon.raycastTarget = true; icon.gameObject.AddComponent<JcInfluenceBadgeHover>().badge = badge; }
        ipText.raycastTarget = true;
        ipText.gameObject.AddComponent<JcInfluenceBadgeHover>().badge = badge;
        badge.SetUnit(null);
        return badge;
    }

    public void SetUnit(BattleCharactor value)
    {
        if (unit != value) EndHover();
        unit = value;
        gameObject.SetActive(unit && unit.IsPlayer);
        if (unit && unit.IsPlayer) SetInfluence(unit.CurrentInfluence, unit.MaxInfluence);
    }
    public void SetInfluence(float value, float cap)
    {
        current = value; maximum = cap;
        int next = Tier(value);
        if (tier != next)
        {
            tier = next; sweepTime = 0;
            label.text = Titles[tier];
            ApplyColors();
            face.SetSweep(-1);
        }
        Layout();
        if (hovering) ShowTip();
    }
    void LateUpdate()
    {
        Layout();
        ApplyColors();
        // JC | 2026-11-12 | 스윕은 배지 표면만 밝히며, HUD를 숨겼을 때의 잔상은 유지하지 않습니다.
        if (tier == 3)
        {
            sweepTime += Time.unscaledDeltaTime;
            float phase = sweepTime % 5.5f;
            face.SetSweep(phase >= 4.6f ? (phase - 4.6f) / .9f : -1);
        }
    }
    // JC | 2026-11-13 | 자기 전투씬의 임시 색상만 읽고 저장 프로필이나 다른 씬의 초안을 바꾸지 않습니다.
    void ApplyColors()
    {
        if (tier < 0 || !face || !label) return;
        var settings = JcInfluenceBadgeSettings.ForScene(gameObject.scene.handle);
        var colors = (settings ? settings.Draft : DefaultColors).Get(tier);
        face.SetStyle(colors.surface, colors.border);
        if (label.color != colors.text) label.color = colors.text;
    }
    void Layout()
    {
        if (!reference || !label || !rect) return;
        float size = reference.fontSize;
        label.fontSize = size;
        label.enableAutoSizing = false;
        float width = Mathf.Ceil(label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity).x) + 20;
        var source = reference.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, .5f);
        rect.anchoredPosition = new Vector2(0, source.anchoredPosition.y - source.rect.height * .5f);
        // JC | 2026-11-13 | 기존 측정 폭 기준으로 박스를 10% 확대하고 글자만 10% 줄입니다.
        rect.sizeDelta = new Vector2(width, source.rect.height) * 1.1f;
        label.fontSize = size * .9f;
    }
    public void OnPointerEnter(PointerEventData data)
    {
        if (!isActiveAndEnabled || !unit) return;
        hovering = true; ShowTip();
    }
    public void OnPointerExit(PointerEventData data) => EndHover();
    public void EndHover()
    {
        if (hovering && SkillTooltip.Instance) SkillTooltip.Instance.Hide();
        hovering = false;
    }
    void ShowTip()
    {
        if (!SkillTooltip.Instance || !unit) return;
        // JC | 2026-11-13 | 아이콘/중복 제목/등급 구간 표를 제외한 IP 전용 HUD형 패널입니다.
        SkillTooltip.Instance.ShowInfluence(current, maximum, Titles[tier], rect, reference.font);
    }
    void OnDisable() { EndHover(); sweepTime = 0; if (face) face.SetSweep(-1); }
    void OnDestroy() { if (lettering) { if (Application.isPlaying) Destroy(lettering); else DestroyImmediate(lettering); } }
}
