using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WorkshopCoreCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image background;
    public Sprite normalSprite;
    public Sprite equippedSprite;
    [SerializeField] private Image equippedFrame;
    [Header("EffectLab button effects")]
    [SerializeField] private WorkshopModalController selectionOwner;
    [SerializeField] private int coreIndex;
    [SerializeField] private Material hoverTemplate;
    [SerializeField] private Material orbitTemplate;
    [SerializeField] private ButtonAmbientLightLab ambientLight;
    [SerializeField, Min(.1f)] private float orbitPeriod = 2.4f;
    [SerializeField, Range(0f, 8f)] private float glowIntensity = 2f;
    private static readonly System.Reflection.FieldInfo SelectedCoreField = typeof(WorkshopModalController)
        .GetField("selectedCore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    private static readonly System.Reflection.FieldInfo CardsField = typeof(WorkshopModalController)
        .GetField("cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    private static readonly System.Reflection.FieldInfo SelectedUnitField = typeof(WorkshopModalController)
        .GetField("selectedUnitIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    private CoreSelectionTooltip coreTooltip;
    private Button button;
    private Image face;
    private Image hoverImage;
    [SerializeField] private Image orbitImage;
    private Material hoverMaterial;
    private Material orbitMaterial;
    private bool equipped;
    private bool hovered;
    private float hoverStarted;
    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform sharedEquipFrame;
    private Transform equipIconPlace;

    private void Awake()
    {
        BindCoreTooltip();
        button = GetComponent<Button>();
        face = button != null ? button.targetGraphic as Image : null;
        if (face == null || face.sprite == null) return;
        if (hoverTemplate != null)
        {
            hoverMaterial = new Material(hoverTemplate);
            hoverImage = CreateOverlay("CoreHoverLight", hoverMaterial);
        }
        if (orbitTemplate != null && orbitImage != null)
        {
            orbitMaterial = new Material(orbitTemplate);
            orbitMaterial.SetFloat("_SelectedMotion", 1f);
            orbitMaterial.SetFloat("_OrbitTrail", .22f);
            orbitMaterial.SetFloat("_OrbitWhiteRim", 1f);
            // The split-sweep material owns its inner sweep settings; legacy orbit stays dots-only.
            if (!orbitMaterial.HasProperty("_SelectedSweepDuration"))
                orbitMaterial.SetFloat("_SelectedSweepIntensity", 0f);
            orbitMaterial.SetFloat("_BloomIntensity", 0f);
            orbitImage.material = orbitMaterial;
            orbitImage.raycastTarget = false;
            orbitImage.gameObject.SetActive(equipped);
        }
    }

    private Image CreateOverlay(string objectName, Material material)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.layer = gameObject.layer;
        go.transform.SetParent(face.transform, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var image = go.GetComponent<Image>();
        image.sprite = face.sprite;
        image.type = face.type;
        image.raycastTarget = false;
        image.material = material;
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        go.SetActive(false);
        return image;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        BindCoreTooltip();
        hovered = true;
        hoverStarted = Time.unscaledTime;
    }

    private void BindCoreTooltip()
    {
        if (selectionOwner == null) selectionOwner = GetComponentInParent<WorkshopModalController>();
        if (selectionOwner == null || CardsField == null) return;
        var cards = CardsField.GetValue(selectionOwner) as System.Collections.Generic.List<WorkshopModalController.CoreCard>;
        if (cards == null || coreIndex < 0 || coreIndex >= cards.Count) return;
        var card = cards[coreIndex];
        if (card == null || card.button == null || card.button.gameObject != gameObject) return;
        if (coreTooltip == null)
            coreTooltip = GetComponent<CoreSelectionTooltip>() ?? gameObject.AddComponent<CoreSelectionTooltip>();
        int hero = SelectedUnitField != null ? (int)SelectedUnitField.GetValue(selectionOwner) : -1;
        coreTooltip.Bind(coreIndex + 1, card.nameText != null ? card.nameText.text : "코어",
            card.icon != null ? card.icon.sprite : null, card.nameText != null ? card.nameText.font : null,
            Mathf.Max(0, hero), omitIPDescription: true);
    }

    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    private void LateUpdate()
    {
        if (equipped) RefreshEquipFrame();
        bool interactive = button != null && button.IsInteractable();
        if (hoverImage != null)
        {
            hoverImage.gameObject.SetActive(hovered && interactive);
            if (hovered && interactive) hoverMaterial.SetFloat("_SweepTime", Time.unscaledTime - hoverStarted);
        }
        if (orbitImage != null)
        {
            orbitImage.gameObject.SetActive(equipped);
            if (equipped && orbitMaterial != null)
            {
                orbitMaterial.SetFloat("_SelectedTime", Time.unscaledTime);
                orbitMaterial.SetFloat("_OrbitPeriod", orbitPeriod);
                orbitMaterial.SetFloat("_GlowIntensity", glowIntensity);
                orbitImage.rectTransform.GetWorldCorners(corners);
                var canvas = orbitImage.canvas;
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                float width = Vector2.Distance(min, RectTransformUtility.WorldToScreenPoint(camera, corners[3]));
                float height = Vector2.Distance(min, RectTransformUtility.WorldToScreenPoint(camera, corners[1]));
                orbitMaterial.SetVector("_OrbitUVPerPixel", new Vector4(1f / Mathf.Max(1f, width), 1f / Mathf.Max(1f, height), 0f, 0f));
            }
        }
        bool selected = selectionOwner != null && SelectedCoreField != null &&
            (int)SelectedCoreField.GetValue(selectionOwner) == coreIndex;
        if (ambientLight != null) ambientLight.SetVisible(selected);
    }

    private void OnDisable()
    {
        hovered = false;
        if (hoverImage != null) hoverImage.gameObject.SetActive(false);
        if (orbitImage != null) orbitImage.gameObject.SetActive(false);
        if (ambientLight != null) ambientLight.SetVisible(false);
    }

    private void OnDestroy()
    {
        if (hoverMaterial != null) Destroy(hoverMaterial);
        if (orbitMaterial != null) Destroy(orbitMaterial);
    }

    public void SetEquipped(bool equipped)
    {
        this.equipped = equipped;
        if (background != null) background.sprite = equipped ? equippedSprite : normalSprite;
        RefreshEquipFrame();
        if (orbitImage != null) orbitImage.gameObject.SetActive(equipped);
    }

    private void RefreshEquipFrame()
    {
        var row = transform.parent;
        if (row == null) return;
        if (sharedEquipFrame == null)
        {
            sharedEquipFrame = (row.Find("frame") ?? row.Find("Frame")) as RectTransform;
            if (sharedEquipFrame != null)
            {
                // The indicator shares the card row but must not occupy a card slot.
                var layout = sharedEquipFrame.GetComponent<LayoutElement>() ??
                    sharedEquipFrame.gameObject.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
            }
        }
        if (sharedEquipFrame == null)
        {
            if (equippedFrame != null) equippedFrame.gameObject.SetActive(equipped);
            return;
        }

        WorkshopCoreCardView equippedCard = null;
        foreach (Transform child in row)
        {
            var card = child.GetComponent<WorkshopCoreCardView>();
            if (card != null && card.equipped)
            {
                equippedCard = card;
                break;
            }
        }
        if (equippedCard != null && equippedCard.equipIconPlace == null)
            equippedCard.equipIconPlace = equippedCard.transform.Find("Icon/EquipIconPlace");
        var place = equippedCard != null ? equippedCard.equipIconPlace : null;
        sharedEquipFrame.gameObject.SetActive(place != null);
        if (place != null) sharedEquipFrame.position = place.position;
    }
}
