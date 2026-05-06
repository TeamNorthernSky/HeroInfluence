using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class TestCanvasController : MonoBehaviour
{
    [Header("재화 표시")]
    [SerializeField] private TMP_InputField goldDisplay;
    [SerializeField] private TMP_InputField woodDisplay;
    [SerializeField] private TMP_InputField oreDisplay;

    [Header("골드 버튼")]
    [SerializeField] private Button goldAddButton;
    [SerializeField] private Button goldSubtractButton;
    [SerializeField] private Button goldResetButton;

    [Header("목재 입력")]
    [SerializeField] private TMP_InputField woodInput;
    [SerializeField] private Button woodAddButton;
    [SerializeField] private Button woodResetButton;

    [Header("광석 슬라이더")]
    [SerializeField] private Slider oreSlider;
    [SerializeField] private Button oreResetButton;

    [Header("전체")]
    [SerializeField] private Button resetAllButton;

    [Header("CanvasGroup 제어")]
    [SerializeField] private CanvasGroup canvasGroup;

    private Color normalColor = Color.black;
    private Color negativeColor = Color.red;

    private void Start()
    {
        SetupDisplayFields();
        BindButtons();
        BindWoodInput();
        BindOreSlider();
        SubscribeCurrencyEvents();
        RefreshAllDisplays();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null && GameManager.Instance.Economy != null)
        {
            GameManager.Instance.Economy.OnResourceChanged -= HandleResourceChanged;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            ToggleCanvasGroup();
        }
    }

    private void SetupDisplayFields()
    {
        goldDisplay.readOnly = true;
        woodDisplay.readOnly = true;
        oreDisplay.readOnly = true;
    }

    private void BindButtons()
    {
        goldAddButton.onClick.AddListener(() => GameManager.Instance.Economy.Add(ResourceType.Money, 100));
        goldSubtractButton.onClick.AddListener(() => GameManager.Instance.Economy.Add(ResourceType.Money, -100));
        goldResetButton.onClick.AddListener(() => GameManager.Instance.Economy.Reset(ResourceType.Money));

        woodAddButton.onClick.AddListener(OnWoodAddClicked);
        woodResetButton.onClick.AddListener(() => GameManager.Instance.Economy.Reset(ResourceType.Chip));

        oreResetButton.onClick.AddListener(() =>
        {
            GameManager.Instance.Economy.Reset(ResourceType.Crystal);
            oreSlider.SetValueWithoutNotify(0);
        });

        resetAllButton.onClick.AddListener(() =>
        {
            GameManager.Instance.Economy.ResetAll();
            oreSlider.SetValueWithoutNotify(0);
        });
    }

    private void BindWoodInput()
    {
        woodInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        woodInput.text = "";
        woodAddButton.interactable = false;

        woodInput.onValueChanged.AddListener(OnWoodInputChanged);
    }

    private void BindOreSlider()
    {
        oreSlider.wholeNumbers = true;
        oreSlider.minValue = -9999;
        oreSlider.maxValue = 9999;
        oreSlider.value = 0;

        oreSlider.onValueChanged.AddListener(OnOreSliderDragging);

        var trigger = oreSlider.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = oreSlider.gameObject.AddComponent<EventTrigger>();

        var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener(_ => OnOreSliderReleased());
        trigger.triggers.Add(pointerUp);
    }

    private void OnOreSliderDragging(float value)
    {
        oreDisplay.text = ((int)value).ToString();
        oreDisplay.textComponent.color = value < 0 ? negativeColor : normalColor;
    }

    private void OnOreSliderReleased()
    {
        GameManager.Instance.Economy.Set(ResourceType.Crystal, (int)oreSlider.value);
    }

    private void OnWoodInputChanged(string value)
    {
        bool valid = int.TryParse(value, out int amount) && amount != 0;
        woodAddButton.interactable = valid;
    }

    private void OnWoodAddClicked()
    {
        if (int.TryParse(woodInput.text, out int amount) && amount != 0)
        {
            GameManager.Instance.Economy.Add(ResourceType.Chip, amount);
            woodInput.text = "";
        }
        else
        {
            woodInput.text = "0";
        }
    }

    private void SubscribeCurrencyEvents()
    {
        GameManager.Instance.Economy.OnResourceChanged += HandleResourceChanged;
    }

    private void HandleResourceChanged(ResourceType type, int newValue)
    {
        switch (type)
        {
            case ResourceType.Money:
                UpdateDisplay(goldDisplay, newValue, type);
                break;
            case ResourceType.Chip:
                UpdateDisplay(woodDisplay, newValue, type);
                break;
            case ResourceType.Crystal:
                UpdateDisplay(oreDisplay, newValue, type);
                break;
        }
    }

    private void UpdateDisplay(TMP_InputField display, int value, ResourceType type)
    {
        display.text = value.ToString();
        display.textComponent.color = GameManager.Instance.Economy.IsNegative(type) ? negativeColor : normalColor;
    }

    private void RefreshAllDisplays()
    {
        var currency = GameManager.Instance.Economy;
        UpdateDisplay(goldDisplay, currency.Get(ResourceType.Money), ResourceType.Money);
        UpdateDisplay(woodDisplay, currency.Get(ResourceType.Chip), ResourceType.Chip);
        UpdateDisplay(oreDisplay, currency.Get(ResourceType.Crystal), ResourceType.Crystal);
    }

    private void ToggleCanvasGroup()
    {
        bool isActive = canvasGroup.interactable;
        canvasGroup.interactable = !isActive;
        canvasGroup.alpha = isActive ? 0.3f : 1f;
    }
}
