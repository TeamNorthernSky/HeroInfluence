using UnityEngine;
using UnityEngine.UI;

public class ToggleButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image image;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite activeSprite;

    [SerializeField] private bool isOn;
    public bool IsOn => isOn;

    private void Reset()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
    }

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (image == null) image = GetComponent<Image>();
        if (normalSprite == null) normalSprite = GetComponent<Image>().sprite;
        if (activeSprite == null) activeSprite = GetComponent<Image>().sprite;
    }

    private void OnEnable()
    {
        button.onClick.AddListener(OnClick);
        ApplySprite();
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClick);
    }

    public event System.Action<bool> OnValueChanged;

    protected virtual void OnClick()
    {
       isOn = !isOn;
       ApplySprite();
       OnValueChanged?.Invoke(isOn);
    }

    private void ApplySprite()
    {
        image.sprite = isOn ? activeSprite : normalSprite;
    }

    public void SetState(bool value, bool notify = true)
    {
        isOn = value;
        ApplySprite();
        if (notify) OnValueChanged?.Invoke(isOn);
    }
    
    public void ChangeState()
    {
        isOn = !isOn;
        ApplySprite();
    }

}
