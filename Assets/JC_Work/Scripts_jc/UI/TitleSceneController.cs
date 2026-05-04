using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 타이틀씬 컨트롤러. 시작 버튼 하나로 PlayScene 진입.
/// </summary>
public class TitleSceneController : MonoBehaviour
{
    private void Start()
    {
        CreateStartButton();
    }

    private void CreateStartButton()
    {
        // Canvas 생성
        var canvasObj = new GameObject("TitleCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasObj.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // 버튼 (부모: Image + Button)
        var btnObj = new GameObject("StartButton");
        btnObj.transform.SetParent(canvasObj.transform, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = new Vector2(0, -50);
        btnRect.sizeDelta = new Vector2(300, 80);

        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.2f, 0.3f, 0.9f);

        var button = btnObj.AddComponent<Button>();
        button.onClick.AddListener(OnStartClicked);

        // 텍스트 (자식: TextMeshProUGUI)
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "게임 시작";
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // 타이틀 텍스트
        var titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(canvasObj.transform, false);

        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0, 80);
        titleRect.sizeDelta = new Vector2(600, 100);

        var titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "ORORA";
        titleTmp.fontSize = 72;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = Color.white;
    }

    private void OnStartClicked()
    {
        var sceneLoader = GameManager.Instance?.SceneLoader;
        if (sceneLoader != null)
        {
            sceneLoader.LoadScene("PlayScene");
        }
        else
        {
            Debug.LogError("[TitleSceneController] SceneLoader를 찾을 수 없습니다");
        }
    }
}
