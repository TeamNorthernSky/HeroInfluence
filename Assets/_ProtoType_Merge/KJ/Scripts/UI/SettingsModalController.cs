using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [KJ 260708] TitleScene 전용이던 Settings 모달을 CommonUIManager(DDOL)로 전역화.
// 정적 Instance 싱글톤을 두지 않는다 — 자기 자신이 self-deactivate 모달이라 Awake가
// 최초 오픈 전까지 실행되지 않기 때문(CommonUIManager.cs의 "조각1 교훈" 주석 참고).
// 항상-활성 호스트인 CommonUIManager.Instance.SettingsModal(GetComponentInChildren)을 통해서만 접근한다.
public class SettingsModalController : MonoBehaviour
{
    [SerializeField] private GameObject ControlPanel;
    [SerializeField] private GameObject GraphicPanel;
    [SerializeField] private GameObject SoundPanel;
    [SerializeField] private GameObject CurrentPanel;

    [SerializeField] private Slider MasterVolume;
    [SerializeField] private Slider BackGroundVolume;
    [SerializeField] private Slider EffectVolume;

    [Header("Graphic")]
    [SerializeField] private TMP_Dropdown ResolutionDropdown;
    [SerializeField] private TMP_Dropdown ScreenModeDropdown;

    private static readonly (int width, int height)[] Resolutions =
    {
        (1280, 720),
        (1600, 900),
        (1920, 1080),
        (2560, 1440),
    };

    private void Awake()
    {
        ControlPanel.SetActive(false);
        GraphicPanel.SetActive(false);
        SoundPanel.SetActive(false);

        if (CurrentPanel == null)
            CurrentPanel = ControlPanel;

        InitResolutionDropdown();
        InitScreenModeDropdown();
        InitVolumeSliders();
        CreateAutoTurnEndToggle();
    }

    private void CreateAutoTurnEndToggle()
    {
        if (ControlPanel == null || ControlPanel.GetComponentInChildren<AutoTurnEndSettingsToggle>(true) != null) return;
        var parent = ControlPanel.transform;
        var fontSource = ControlPanel.GetComponentInChildren<TMP_Text>(true);
        // Control rows keep their authored dimensions and placement.
        for (int i = 0; i < parent.childCount; i++)
        {
            var row = parent.GetChild(i) as RectTransform;
            if (row == null) continue;
            if (row.name.StartsWith("ControlRow_")) continue;
            row.anchoredPosition = new Vector2(row.anchoredPosition.x, 120f - i * 80f);
            row.localScale = new Vector3(1f, 0.7f, 1f);
        }
        var root = new GameObject("AutoTurnEndToggle", typeof(RectTransform), typeof(Toggle));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(1030f, 44f);
        rect.anchoredPosition = new Vector2(0f, -220f);
        var toggle = root.GetComponent<Toggle>();
        var box = new GameObject("Checkbox", typeof(RectTransform), typeof(Image));
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.SetParent(rect, false);
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, 0.5f);
        boxRect.anchoredPosition = new Vector2(40f, 0f);
        boxRect.sizeDelta = new Vector2(36f, 36f);
        var background = box.GetComponent<Image>();
        background.color = new Color(0.15f, 0.3f, 0.5f);
        var mark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        var markRect = mark.GetComponent<RectTransform>();
        markRect.SetParent(boxRect, false);
        markRect.sizeDelta = new Vector2(24f, 24f);
        var markImage = mark.GetComponent<Image>();
        markImage.color = Color.cyan;
        markImage.raycastTarget = false;
        toggle.targetGraphic = background;
        toggle.graphic = markImage;
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(80f, 0f);
        labelRect.offsetMax = Vector2.zero;
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        if (fontSource != null) label.font = fontSource.font;
        label.text = "자동 턴 넘김 (이동력 소진 시)";
        label.fontSize = 28f;
        label.color = fontSource != null ? fontSource.color : Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        root.AddComponent<AutoTurnEndSettingsToggle>();
    }

    private void InitResolutionDropdown()
    {
        if (ResolutionDropdown == null) return;

        ResolutionDropdown.ClearOptions();
        var options = new List<string>();
        for (int i = 0; i < Resolutions.Length; i++)
        {
            var r = Resolutions[i];
            options.Add($"{r.width} x {r.height}");
        }
        ResolutionDropdown.AddOptions(options);
        ResolutionDropdown.value = GameSettings.ResolutionIndex;
        ResolutionDropdown.RefreshShownValue();
        ResolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void InitScreenModeDropdown()
    {
        if (ScreenModeDropdown == null) return;

        ScreenModeDropdown.ClearOptions();
        ScreenModeDropdown.AddOptions(new List<string> { "창 모드", "전체 화면" });
        ScreenModeDropdown.value = GameSettings.IsFullScreen ? 1 : 0;
        ScreenModeDropdown.RefreshShownValue();
        ScreenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);
    }

    private void InitVolumeSliders()
    {
        InitSlider(MasterVolume,     GameSettings.MasterVolume,  GameSettings.ApplyMasterVolume);
        InitSlider(BackGroundVolume, GameSettings.BGMVolume,     GameSettings.ApplyBGMVolume);
        InitSlider(EffectVolume,     GameSettings.EffectVolume,  GameSettings.ApplyEffectVolume);
    }

    private static void InitSlider(Slider slider, float savedValue, System.Action<float> onChanged)
    {
        if (slider == null) return;

        // Keep the whole track clickable, including the unfilled portion.
        if (slider.fillRect != null && slider.fillRect.parent is RectTransform track)
        {
            var trackGraphic = track.GetComponent<Graphic>();
            if (trackGraphic == null)
            {
                var hitArea = track.gameObject.AddComponent<Image>();
                hitArea.color = Color.clear;
                trackGraphic = hitArea;
            }
            trackGraphic.raycastTarget = true;
        }

        slider.value = savedValue;

        TMP_Text valueText = slider.transform.parent?.Find("Value")?.GetComponent<TMP_Text>();
        if (valueText != null)
            valueText.text = Mathf.RoundToInt(savedValue * 100f).ToString();

        slider.onValueChanged.AddListener(v =>
        {
            if (valueText != null)
                valueText.text = Mathf.RoundToInt(v * 100f).ToString();
            onChanged(v);
        });
    }

    private void OnResolutionChanged(int index)
    {
        GameSettings.ApplyResolution(index, Resolutions);
    }

    private void OnScreenModeChanged(int index)
    {
        GameSettings.ApplyFullScreen(index == 1);
    }

    // [KJ 260708] TitleScene "설정" 버튼 + 인게임 SystemMenu "Options" 버튼이 공용 호출.
    // gameObject.SetActive(true)를 가장 먼저 호출 — 최초 오픈 시 Awake가 동기 실행된 뒤
    // 아래 CurrentPanel 참조가 유효해짐을 보장한다.
    public void Open()
    {
        gameObject.SetActive(true);
        CurrentPanel.SetActive(true);
    }

    public void Close()
    {
        CurrentPanel.SetActive(false);
        gameObject.SetActive(false);
    }

    public void OnDefaultButtonClicked()
    {
        const float defaultVolume = 1.0f;
        GameSettings.ApplyMasterVolume(defaultVolume);
        GameSettings.ApplyBGMVolume(defaultVolume);
        GameSettings.ApplyEffectVolume(defaultVolume);
        SetVolumeSlider(MasterVolume, defaultVolume);
        SetVolumeSlider(BackGroundVolume, defaultVolume);
        SetVolumeSlider(EffectVolume, defaultVolume);
    }

    private static void SetVolumeSlider(Slider slider, float value)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(value);
        TMP_Text valueText = slider.transform.parent?.Find("Value")?.GetComponent<TMP_Text>();
        if (valueText != null)
            valueText.text = Mathf.RoundToInt(value * 100f).ToString();
    }

    public void OnControlButtonClicked()
    {
        if (CurrentPanel == ControlPanel) return;
        CurrentPanel.SetActive(false);
        CurrentPanel = ControlPanel;
        CurrentPanel.SetActive(true);
    }

    public void OnGraphicButtonClicked()
    {
        if (CurrentPanel == GraphicPanel) return;
        CurrentPanel.SetActive(false);
        CurrentPanel = GraphicPanel;
        CurrentPanel.SetActive(true);
    }

    public void OnSoundButtonClicked()
    {
        if (CurrentPanel == SoundPanel) return;
        CurrentPanel.SetActive(false);
        CurrentPanel = SoundPanel;
        CurrentPanel.SetActive(true);
    }
}
