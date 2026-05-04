using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SceneNavigationController : MonoBehaviour
{
    private const string FontPath = "Assets/JC_Work/Assets_jc/Maplestory Light SDF.asset";

    private SceneLoader sceneLoader;
    private TMP_FontAsset koreanFont;

    public void Setup(SceneLoader loader)
    {
        sceneLoader = loader;
        LoadFont();
        ConfigureCanvas();
        CreateSceneLabel();
        CreateDirectJumpButtons();
        CreateNavigationBar();
        LoadHistoryPanel();
    }

    private void LoadFont()
    {
#if UNITY_EDITOR
        koreanFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
#endif
    }

    private void ApplyFont(TextMeshProUGUI tmp)
    {
        if (koreanFont != null)
            tmp.font = koreanFont;
    }

    private void ConfigureCanvas()
    {
        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private void CreateSceneLabel()
    {
        var obj = new GameObject("SceneLabel");
        obj.transform.SetParent(transform, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(600, 80);

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = sceneLoader.GetDisplayName(sceneLoader.CurrentSceneName);
        tmp.fontSize = 50;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
        ApplyFont(tmp);
    }

    private void CreateDirectJumpButtons()
    {
        string[] targets = sceneLoader.GetDirectJumpTargets(sceneLoader.CurrentSceneName);
        float totalWidth = targets.Length * 220f - 20f;
        float startX = -totalWidth / 2f + 100f;

        for (int i = 0; i < targets.Length; i++)
        {
            string target = targets[i];
            float x = startX + i * 220f;
            string displayName = sceneLoader.GetDisplayName(target);

            CreateButton(
                displayName,
                new Vector2(0.5f, 0.5f),
                new Vector2(x, 120f),
                new Vector2(200, 50),
                () => sceneLoader.LoadScene(target)
            );
        }
    }

    private void CreateNavigationBar()
    {
        CreateButton(
            "<< 이전 씬",
            new Vector2(0.5f, 0f),
            new Vector2(-200f, 60f),
            new Vector2(180, 50),
            () => sceneLoader.LoadPreviousScene()
        );

        CreateButton(
            "다음 씬 >>",
            new Vector2(0.5f, 0f),
            new Vector2(200f, 60f),
            new Vector2(180, 50),
            () => sceneLoader.LoadNextScene()
        );
    }

    // UI 규칙: 부모에 Image + Button, 자식에 TextMeshProUGUI
    private Button CreateButton(string label, Vector2 anchor, Vector2 position,
        Vector2 size, System.Action onClick, bool interactable = true)
    {
        var btnObj = new GameObject(label);
        btnObj.transform.SetParent(transform, false);

        var rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var image = btnObj.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        var button = btnObj.AddComponent<Button>();
        button.interactable = interactable;
        button.onClick.AddListener(() => onClick());

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        ApplyFont(tmp);

        return button;
    }

    // --- 씬 이력 패널 (프리팹 로드) ---

    private void LoadHistoryPanel()
    {
        var prefab = GameManager.Instance != null && GameManager.Instance.UIPrefabRegistry != null
            ? GameManager.Instance.UIPrefabRegistry.SceneHistoryPanel
            : null;
        if (prefab == null)
        {
            Debug.LogError("[SceneNavigationController] SceneHistoryPanel prefab이 UIPrefabRegistry에 등록되지 않았습니다");
            return;
        }

        var panel = Instantiate(prefab, transform);
        panel.name = "SceneHistoryPanel";

        var controller = panel.GetComponent<SceneHistoryPanelController>();
        if (controller != null)
        {
            controller.Setup(sceneLoader);
        }
        else
        {
            Debug.LogError("[SceneNavigationController] SceneHistoryPanelController가 프리팹에 없습니다");
        }
    }

}
