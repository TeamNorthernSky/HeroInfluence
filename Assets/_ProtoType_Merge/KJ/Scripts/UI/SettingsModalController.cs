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
