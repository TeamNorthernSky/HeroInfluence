using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [KJ 260714] 채팅 말풍선 뷰 (Bubble.prefab 루트). Content(VerticalLayoutGroup) 아래 런타임 Instantiate.
/// 데이터 바인딩만 담당 — Chat_Type으로 정렬 결정(0=좌, 1=우, 2=가운데). 진행 로직은 컨트롤러/러너 소관.
/// [KJ 260715] 좌/우 말풍선 색 구분 + 상대방(좌) 원형 초상화(카카오톡 스타일). 초상화는 HLG 첫 자식이라
/// [KJ 260910] 좌우 초상화/이름 분리, 배경 반전, 텍스트 높이에 맞춘 말풍선 레이아웃.
/// [KJ 260807] Chat_Type=2(상황 설명 나레이션)는 화자가 없으므로 가운데 정렬 + 이름/초상화 숨김.
/// </summary>
[DisallowMultipleComponent]
public class ChatBubbleView : MonoBehaviour
{
    private const string PortraitResourceFolder = "Portrait_Hero_Sprite/"; // Assets/Resources/Portrait_Hero_Sprite

    private const int TypePlayer = 1;    // 플레이어 — 우측
    private const int TypeNarration = 2; // 상황 설명 — 가운데

    [Tooltip("좌/우 정렬을 담당하는 행(row) 레이아웃. childAlignment 로 말풍선 박스 위치 결정.")]
    [SerializeField] private HorizontalLayoutGroup rowLayout;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text messageText;

    [Header("말풍선 색 (Chat_Type별)")]
    [SerializeField] private Image bubbleImage;
    [Tooltip("플레이어(우측, Chat_Type=1) 말풍선 색 — 하늘색.")]
    [SerializeField] private Color playerBubbleColor = new Color32(0xAE, 0xE1, 0xFA, 0xFF);
    [Tooltip("상대방(좌측, Chat_Type=0) 말풍선 색 — 파란색.")]
    [SerializeField] private Color otherBubbleColor = new Color32(0x4A, 0x86, 0xD9, 0xFF);
    [Tooltip("상황 설명(가운데, Chat_Type=2) 말풍선 색 — 중립 회색.")]
    [SerializeField] private Color narrationBubbleColor = new Color32(0xD8, 0xDC, 0xE2, 0xFF);

    [Header("초상화 (좌우 화자, 원형 마스크)")]
    [Tooltip("초상화 루트(원형 마스크 프레임). 화자가 있는 말풍선에서 활성화.")]
    [SerializeField] private GameObject portraitRoot;
    [Tooltip("실제 초상화 스프라이트가 들어가는 이미지. 리소스 없으면 비활성(프레임 원만 표시).")]
    [SerializeField] private Image portraitImage;


    [Header("말풍선 레이아웃")]
    [SerializeField] private Sprite singleLineSprite;
    [SerializeField] private Sprite multiLineSprite;
    [SerializeField] private Sprite narrationSprite;

    private void ApplyBubbleLayout(bool isPlayer, bool isNarration)
    {
        if (portraitRoot != null)
            portraitRoot.transform.SetSiblingIndex(isPlayer ? transform.childCount - 1 : 0);
        if (bubbleImage == null || messageText == null || singleLineSprite == null) return;
        var box = messageText.transform.parent.GetComponent<LayoutElement>();
        var layout = messageText.transform.parent.GetComponent<VerticalLayoutGroup>();
        if (box == null || layout == null) return;
        layout.padding = isNarration ? new RectOffset(30, 30, 18, 18)
            : isPlayer ? new RectOffset(30, 58, 18, 18) : new RectOffset(58, 30, 18, 18);
        float width = 440f;
        float height = messageText.GetPreferredValues(messageText.text, width, Mathf.Infinity).y;
        bool multiline = height > messageText.fontSize * 1.6f;
        box.layoutPriority = 2;
        box.minWidth = box.preferredWidth = width + layout.padding.horizontal;
        box.minHeight = box.preferredHeight = Mathf.Max(84f, height + layout.padding.vertical);
        bubbleImage.sprite = isNarration && narrationSprite != null ? narrationSprite
            : multiline && multiLineSprite != null ? multiLineSprite : singleLineSprite;
        bubbleImage.rectTransform.localScale = new Vector3(isPlayer && !isNarration ? -1f : 1f, 1f, 1f);
        if (portraitRoot != null && nameText != null)
        {
            var portraitLayout = portraitRoot.GetComponent<LayoutElement>();
            if (portraitLayout != null) portraitLayout.minHeight = portraitLayout.preferredHeight = 120f + nameText.GetPreferredValues(nameText.text, 150f, Mathf.Infinity).y;
        }
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }

    /// <summary>
    /// 말풍선 채우기. type 0=좌(상대, 초상화 표시), 1=우(플레이어), 2=가운데(상황 설명).
    /// profileKey=Char_Profile 리소스 키.
    /// </summary>
    public void Bind(string charName, string message, int type, string profileKey = null)
    {
        bool isPlayer = type == TypePlayer;
        bool isNarration = type == TypeNarration;

        if (nameText != null)
        {
            nameText.text = charName ?? string.Empty;
            if (nameText.GetPreferredValues(nameText.text).x > 150f && nameText.text.Contains(" "))
                nameText.text = nameText.text.Replace(" ", "\n");
            nameText.gameObject.SetActive(!isNarration); // 나레이션은 화자가 없다 (레이아웃에서 제외)
        }
        if (messageText != null)
        {
            messageText.text = message;
            messageText.alignment = isNarration ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
        }
        if (rowLayout != null)
            rowLayout.childAlignment = isNarration ? TextAnchor.UpperCenter
                                     : isPlayer ? TextAnchor.UpperRight
                                     : TextAnchor.UpperLeft;
        if (bubbleImage != null)
            bubbleImage.color = isNarration ? narrationBubbleColor
                              : isPlayer ? playerBubbleColor
                              : otherBubbleColor;

        BindPortrait(!isNarration, profileKey);
        ApplyBubbleLayout(isPlayer, isNarration);
    }

    /// <summary>캐릭터 키를 영웅/적/NPC 초상화 리소스에서 찾는다.</summary>
    private static Sprite LoadPortrait(string key)
    {
        if (key.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) key = key.Substring(0, key.Length - 4);
        return Resources.Load<Sprite>(PortraitResourceFolder + key)
            ?? Resources.Load<Sprite>("Portrait_Enemy_Sprite/" + key)
            ?? Resources.Load<Sprite>("UI_Sprite/UI_Chatting/" + key);
    }

    private void BindPortrait(bool show, string profileKey)
    {
        if (portraitRoot == null) return;

        portraitRoot.SetActive(show);
        if (!show || portraitImage == null) return;

        Sprite sprite = string.IsNullOrWhiteSpace(profileKey)
            ? null
            : LoadPortrait(profileKey.Trim());

        portraitImage.sprite = sprite;
        portraitImage.enabled = sprite != null; // 없으면 프레임 원(placeholder)만
    }
}
