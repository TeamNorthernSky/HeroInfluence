using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [KJ 260714] 채팅 말풍선 뷰 (Bubble.prefab 루트). Content(VerticalLayoutGroup) 아래 런타임 Instantiate.
/// 데이터 바인딩만 담당 — Chat_Type으로 좌/우 정렬(0=좌, 1=우). 진행 로직은 컨트롤러/러너 소관.
/// [KJ 260715] 좌/우 말풍선 색 구분 + 상대방(좌) 원형 초상화(카카오톡 스타일). 초상화는 HLG 첫 자식이라
/// 상대방일 때만 활성화하면 말풍선 왼쪽에 붙는다(플레이어는 비활성 → 레이아웃에서 제외).
/// </summary>
[DisallowMultipleComponent]
public class ChatBubbleView : MonoBehaviour
{
    private const string PortraitResourceFolder = "Portrait_Hero_Sprite/"; // Assets/Resources/Portrait_Hero_Sprite

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

    [Header("초상화 (상대방 전용, 원형 마스크)")]
    [Tooltip("초상화 루트(원형 마스크 프레임). 상대방 말풍선에서만 활성화.")]
    [SerializeField] private GameObject portraitRoot;
    [Tooltip("실제 초상화 스프라이트가 들어가는 이미지. 리소스 없으면 비활성(프레임 원만 표시).")]
    [SerializeField] private Image portraitImage;

    /// <summary>말풍선 채우기. type 0=좌(상대, 초상화 표시), 1=우(플레이어). profileKey=Char_Profile 리소스 키.</summary>
    public void Bind(string charName, string message, int type, string profileKey = null)
    {
        bool isPlayer = type == 1;

        if (nameText != null) nameText.text = charName;
        if (messageText != null) messageText.text = message;
        if (rowLayout != null)
            rowLayout.childAlignment = isPlayer ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
        if (bubbleImage != null)
            bubbleImage.color = isPlayer ? playerBubbleColor : otherBubbleColor;

        BindPortrait(isPlayer, profileKey);
    }

    private void BindPortrait(bool isPlayer, string profileKey)
    {
        if (portraitRoot == null) return;

        portraitRoot.SetActive(!isPlayer); // 플레이어는 초상화 없음 (레이아웃에서 제외)
        if (isPlayer || portraitImage == null) return;

        Sprite sprite = string.IsNullOrWhiteSpace(profileKey)
            ? null
            : Resources.Load<Sprite>(PortraitResourceFolder + profileKey.Trim());

        portraitImage.sprite = sprite;
        portraitImage.enabled = sprite != null; // 없으면 프레임 원(placeholder)만
    }
}
