using TMPro;
using UnityEngine;

/// <summary>
/// [JC 신설 260512] 전투씬 캐릭터/에네미 머리 위 이름표.
/// WorldSpace Canvas + TMP_Text 자식 GO를 동적 생성. 카메라 방향 1회 회전 셋팅 후 고정(빌보드 아님).
/// 부착 후 SetName(string)으로 텍스트 적용.
/// </summary>
[DisallowMultipleComponent]
public class BattleNameLabel : MonoBehaviour
{
    [SerializeField] private float yOffset = 2.2f;
    [SerializeField] private Vector2 canvasSize = new Vector2(3f, 0.6f);
    [SerializeField] private float worldScale = 0.5f;
    [SerializeField] private float fontSize = 1.2f;

    private TMP_Text labelText;
    private Transform canvasTransform;

    public void SetName(string name)
    {
        EnsureLabel();
        if (labelText != null) labelText.text = name ?? string.Empty;
    }

    private void EnsureLabel()
    {
        if (labelText != null) return;

        var canvasGo = new GameObject("NameLabel_Canvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = new Vector3(0f, yOffset, 0f);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        var canvasRT = canvasGo.GetComponent<RectTransform>();
        canvasRT.sizeDelta = canvasSize;
        canvasRT.localScale = Vector3.one * worldScale;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);
        labelText = textGo.AddComponent<TextMeshProUGUI>();
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = fontSize;
        labelText.color = Color.white;
        labelText.fontStyle = FontStyles.Bold;
        labelText.enableWordWrapping = false;
        labelText.outlineWidth = 0.2f;
        labelText.outlineColor = Color.black;

        var textRT = textGo.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        canvasTransform = canvasGo.transform;

        // 카메라 방향 1회 셋팅 (빌보드 아님 — 매프레임 추적하지 않음)
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 camForward = cam.transform.forward;
            if (camForward.sqrMagnitude > 0.001f)
                canvasTransform.rotation = Quaternion.LookRotation(camForward, Vector3.up);
        }
    }
}
