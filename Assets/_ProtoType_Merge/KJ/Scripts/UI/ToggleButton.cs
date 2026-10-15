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
    private bool pressedAppearance;
    private Color restingColor;
    private GameObject insetShading;

    // 전투 자동/배속 버튼만 사용합니다. 다른 ToggleButton의 기존 외형은 유지합니다.
    public void EnablePressedAppearance()
    {
        if (image == null) image = GetComponent<Image>();
        if (!pressedAppearance)
        {
            restingColor = image.color;
            pressedAppearance = true;
            insetShading = new GameObject("PressedInset", typeof(RectTransform));
            insetShading.layer = gameObject.layer;
            var rect = (RectTransform)insetShading.transform;
            rect.SetParent(image.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            AddInsetEdge("UpperShadow", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(22, -16), new Vector2(-22, -11), new Color(0, 0, 0, .36f));
            AddInsetEdge("LowerLight", Vector2.zero, Vector2.right,
                new Vector2(22, 11), new Vector2(-22, 13), new Color(1, 1, 1, .23f));
        }
        ApplySprite();
    }

    private void AddInsetEdge(string name, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        var edge = go.GetComponent<Image>();
        edge.raycastTarget = false; edge.color = color;
        var rect = edge.rectTransform;
        rect.SetParent(insetShading.transform, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }
    private void Reset()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
    }

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (image == null) image = GetComponent<Image>();
        if (normalSprite == null) normalSprite = image != null ? image.sprite : null;
        if (activeSprite == null) activeSprite = image != null ? image.sprite : null;
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
        if (pressedAppearance)
        {
            image.color = isOn
                ? new Color(restingColor.r * .72f, restingColor.g * .72f, restingColor.b * .72f, restingColor.a)
                : restingColor;
            insetShading.SetActive(isOn);
        }
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
