using MathNet.Numerics.Optimization;
using NPOI.SS.Formula.Functions;
using Org.BouncyCastle.Asn1.Cmp;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleMenuController : MonoBehaviour
{
    [SerializeField] private GameObject SettingModal;
    [SerializeField] private GameObject ControlPanel;
    [SerializeField] private GameObject GraphicPanel;
    [SerializeField] private GameObject SoundPanel;
    [SerializeField] private GameObject CurrentPanel;

    [Header("GameSlot")]
    [SerializeField] private GameObject GameSlotPanel;

    [SerializeField] private Slider MasterVolume;
    [SerializeField] private Slider BackGroundVolume;
    [SerializeField] private Slider EffectVolume;

    [Header("Graphic")]
    [SerializeField] private TMP_Dropdown ResolutionDropdown;
    [SerializeField] private TMP_Dropdown ScreenModeDropdown;

    [Header("Exit Popup")]
    [SerializeField] private GameObject ExitPopup;

    private static readonly (int width, int height)[] Resolutions =
    {
        (1280, 720),
        (1600, 900),
        (1920, 1080),
        (2560, 1440),
    };

    private void Awake()
    {
        SettingModal.SetActive(false);
        ControlPanel.SetActive(false);
        GraphicPanel.SetActive(false);
        SoundPanel.SetActive(false);
        GameSlotPanel.SetActive(false);

        if (CurrentPanel == null)
        {
            Debug.Log("Current == null");
            CurrentPanel = ControlPanel;
        }

        GameSettings.LoadAndApplyAll();

        InitResolutionDropdown();
        InitScreenModeDropdown();
        InitVolumeSliders();
    }

    private void InitResolutionDropdown()
    {
        if (ResolutionDropdown == null) return;

        ResolutionDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>();
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
        ScreenModeDropdown.AddOptions(new System.Collections.Generic.List<string> { "창 모드", "전체 화면" });
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
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (ModalRegistry.HasAny)
            ModalRegistry.CloseTop();
        else
            OnQuitClicked();
    }

    public void OnNewGameClicked()
    {
        Debug.Log("[TitleMenu] 새 게임 → GameLoadScene (게이트씬)");
        
        GameSlotPanel.SetActive(true);
        //SceneManager.LoadScene("GameLoadScene");
    }

    public void OnLoadClicked()
    {
        //Debug.Log("[TitleMenu] 불러오기 클릭 (미구현)");
    }

    public void OnSettingsClicked()
    {
        SettingModal.SetActive(true);
        CurrentPanel.SetActive(true);
        Debug.Log(CurrentPanel.gameObject.name);
    }

    public void OnSettingsExitClicked()
    {
        CurrentPanel.SetActive(false);
        SettingModal.SetActive(false);
    }

    public void OnSettingsControlButtonClicked()
    {
        if (CurrentPanel == ControlPanel) return;
        CurrentPanel.SetActive(false);
        CurrentPanel = ControlPanel;
        CurrentPanel.SetActive(true);
    }

    public void OnSettingsGraphicButtonClicked()
    {
        if (CurrentPanel == GraphicPanel) return;
        CurrentPanel.SetActive(false);
        CurrentPanel = GraphicPanel;
        CurrentPanel.SetActive(true);
    }

    public void OnSettingsSoundbuttonClicked()
    {
        if(CurrentPanel == SoundPanel) return;
        CurrentPanel.SetActive(false);
        CurrentPanel = SoundPanel;
        CurrentPanel.SetActive(true);
    }

    public void OnQuitClicked()
    {
        ExitPopup.SetActive(true);

    }

    public void OnClickedQuitPopupAccept()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnClickedQuitPopupDeny()
    {
        ExitPopup.SetActive(false);
    }
}
