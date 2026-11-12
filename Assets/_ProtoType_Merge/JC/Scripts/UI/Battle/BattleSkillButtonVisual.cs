using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>전투 스킬 버튼의 사용 가능 효과와 선택 테두리만 표시합니다. 스킬 실행 상태는 소유하지 않습니다.</summary>
[DisallowMultipleComponent]
public class BattleSkillButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("마우스 오버 시 확대 배율입니다. 1이면 기존 크기를 유지합니다.")]
    [Range(1f, 1.12f)] [SerializeField] private float hoverScale = 1f;
    [Min(.01f)] [SerializeField] private float hoverScaleSeconds = .12f;
    private Vector3 restingScale;
    private Selectable selectable;
    private bool hovered;
    private float hoverBlend;
    [Tooltip("JC_BattleUI_VFX/SkillBTN_SweepGlow의 공통 설정입니다. 연결하면 아래 개별 시각 설정보다 우선합니다.")]
    [SerializeField] private BattleSkillSweepSettings sharedSettings;
    [Tooltip("출전 버튼의 스윕 셰이더를 재사용하는 사용 가능 효과입니다. 클릭을 가로채지 않는 Image를 연결합니다.")]
    [SerializeField] private Image availableEffect;
    [Tooltip("선택 중 유지할 테두리입니다. 호버 여부와 관계없이 선택 상태를 표시합니다.")]
    [SerializeField] private Image selectedFrame;
    [Tooltip("사용 가능한 버튼에 반복 강조 효과를 표시합니다.")]
    [SerializeField] private bool showAvailableEffect = true;
    [Tooltip("사용 가능 효과의 색입니다. 다른 버튼이나 공유 재질에는 영향을 주지 않습니다.")]
    [SerializeField] private Color availableColor = new Color(.2f, .65f, 1f, 1f);
    [Tooltip("사용 가능 외곽 광선 강도입니다. 0이면 광선을 숨깁니다.")]
    [Range(0f, 8f)] [SerializeField] private float glowIntensity = 1.2f;
    [Tooltip("반짝이는 띠의 밝기입니다. 0이면 띠를 숨깁니다.")]
    [Range(0f, 8f)] [SerializeField] private float sweepIntensity = .8f;
    [Tooltip("반복 간격(초)입니다. 전투 배속의 영향을 받지 않습니다.")]
    [Min(.1f)] [SerializeField] private float sweepCycle = 2.5f;
    [Tooltip("띠가 지나가는 시간(초)입니다. 반복 간격보다 길게 설정해도 반복 간격 이내로 제한합니다.")]
    [Min(.05f)] [SerializeField] private float sweepDuration = .6f;
    [Tooltip("선택 테두리의 색입니다. 기존 선택 오버레이는 별도로 유지합니다.")]
    [SerializeField] private Color selectedColor = new Color(1f, .75f, .1f, 1f);
    [Tooltip("선택 테두리의 바깥쪽 폭입니다. 버튼 높이에 대한 UV 비율이며 0이면 외곽 확장을 없앱니다.")]
    [Range(0f, .2f)] [SerializeField] private float selectedWidth = .025f;
    [Tooltip("선택 테두리의 발광 강도입니다. 0이면 발광을 없앱니다.")]
    [Range(0f, 8f)] [SerializeField] private float selectedIntensity = 2f;

    private Image inset;

    private void UpdateInset(bool selected)
    {
        bool show = selected && sharedSettings != null && sharedSettings.IsSkillButtonStyle &&
            sharedSettings.isActiveAndEnabled && sharedSettings.selectedInset && sharedSettings.insetDepth > 0f &&
            sharedSettings.InsetSprite != null;
        if (show && inset == null)
        {
            var go = new GameObject("SelectedInset", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            inset = go.GetComponent<Image>();
            var r = inset.rectTransform;
            r.SetParent(transform, false);
            r.SetAsFirstSibling(); // 버튼 면 위, 아이콘·스킬명·선택 테두리 아래.
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(3, 3); r.offsetMax = new Vector2(-3, -3);
            inset.sprite = sharedSettings.InsetSprite; inset.type = Image.Type.Sliced;
            inset.raycastTarget = false;
        }
        if (inset == null) return;
        inset.enabled = show;
        if (show) inset.color = new Color(1, 1, 1, sharedSettings.insetDepth);
    }

    private Material availableInstance;
    private Material selectedInstance;
    private Material availableSource;
    private Material selectedSource;
    private float started;
    private bool wasAvailable;
    private bool wasSelected;
    private float selectedStarted;
    // JC | 2026-11-04 | 버튼 면과 강조 효과에 같은 종횡비와 윤곽을 적용해 확대하거나 선택해도 테두리가 어긋나지 않게 합니다.
    private bool systemButtonStyle;
    private readonly Vector3[] selectedCorners = new Vector3[4];

    private void Awake()
    {
        restingScale = transform.localScale;
        // JC | 2026-11-12 | 테스트씬 직접 시작은 공통 HUD 재배치 없이 실행되므로 저장된 코어 텍스트도 여기서 연결합니다.
        // 공통 재배치가 실행되는 경로는 해당 코드에서 새 기본 위치를 다시 연결하며, 기존 형제 계층은 그대로 유지합니다.
        if (sharedSettings != null && sharedSettings.SupportsMotionControls && name == "Button_Skill_2" &&
            transform.parent != null && transform.parent.name == "CoreSkillPanel")
            BindScaleCompanion(transform.parent.Find("Title") as RectTransform);
        selectable = GetComponent<Selectable>();
        var face = GetComponent<Image>();
        systemButtonStyle = face != null && face.material != null &&
            face.material.shader.name == "UI/JC/SystemButtonFit";
        availableSource = availableEffect != null ? availableEffect.material : null;
        selectedSource = selectedFrame != null ? selectedFrame.material : null;
        availableInstance = CreateInstance(availableEffect, availableSource);
        selectedInstance = CreateInstance(selectedFrame, selectedSource);
        if (systemButtonStyle && selectedFrame != null)
        {
            selectedFrame.type = face.type;
            selectedFrame.preserveAspect = face.preserveAspect;
            foreach (var edge in selectedFrame.GetComponentsInChildren<Image>(true))
                if (edge != selectedFrame && (edge.name == "Top" || edge.name == "Bottom" || edge.name == "Left" || edge.name == "Right"))
                    edge.enabled = false; // 구형 사각 보조선 대신 버튼 실루엣을 따라 순환합니다.
        }
        if (selectedFrame != null &&
            ((sharedSettings != null && sharedSettings.IsSkillButtonStyle) || gameObject.scene.name == "TmpBattleScene" || gameObject.scene.name == "TutorialBattleScene"))
        {
            // Carry image-relative coordinates in the mesh, before Canvas batching transforms POSITION.
            if (selectedFrame.GetComponent<BattleSkillRimCoordinates>() == null)
                selectedFrame.gameObject.AddComponent<BattleSkillRimCoordinates>();
        }
        SetState(false, false);
    }

    private Material CreateInstance(Image image, Material source)
    {
        if (image == null || source == null) return null;
        var instance = new Material(source) { name = source.name + " (Battle Instance)", hideFlags = HideFlags.DontSave };
        image.material = instance;
        image.raycastTarget = false;
        return instance;
    }

    /// <summary>SkillButtonControllerから受け取った実際の使用可否と選択状態を表示します。</summary>
    public void SetState(bool available, bool selected)
    {
        bool skillStyle = sharedSettings != null && sharedSettings.IsSkillButtonStyle;
        UpdateInset(selected);
        if (skillStyle && !sharedSettings.isActiveAndEnabled)
        {
            available = false;
            selected = false;
        }
        if (selected && !wasSelected) selectedStarted = Time.unscaledTime;
        wasSelected = selected;
        if (selectedInstance != null) selectedInstance.SetFloat("_SelectedTime", Time.unscaledTime - selectedStarted);
        if (available && !wasAvailable) started = Time.unscaledTime;
        wasAvailable = available;
        bool useShared = sharedSettings != null && sharedSettings.isActiveAndEnabled;
        if (availableEffect != null) availableEffect.gameObject.SetActive(available && (useShared ? sharedSettings.showAvailableEffect : showAvailableEffect));
        if (selectedFrame != null) selectedFrame.gameObject.SetActive(selected);
        if (useShared)
        {
            sharedSettings.Apply(availableInstance, selectedInstance);
            if (systemButtonStyle && selectedInstance != null)
            {
                selectedInstance.SetFloat("_SystemButtonFit", 1f);
                selectedInstance.SetFloat("_ConfigurableOrbitRim", 0f);
            }
            UpdateOrbitPixelSize();
            if (availableInstance != null) availableInstance.SetFloat("_SweepTime", Time.unscaledTime - started);
            return;
        }
        if (skillStyle) return; // 공통 설정을 끈 경우 개별 버튼의 구형 광선으로 되돌아가지 않습니다.
        if (availableInstance != null)
        {
            availableInstance.SetColor("_GlowColor", availableColor);
            availableInstance.SetColor("_SweepColor", availableColor);
            availableInstance.SetFloat("_GlowIntensity", glowIntensity);
            availableInstance.SetFloat("_SweepIntensity", sweepIntensity);
            availableInstance.SetFloat("_SweepCycle", Mathf.Max(.1f, sweepCycle));
            availableInstance.SetFloat("_SweepDuration", Mathf.Clamp(sweepDuration, .05f, Mathf.Max(.1f, sweepCycle)));
            availableInstance.SetFloat("_SweepTime", Time.unscaledTime - started);
        }
        if (selectedInstance != null)
        {
            selectedInstance.SetColor("_GlowColor", selectedColor);
            selectedInstance.SetFloat("_GlowWidth", selectedWidth);
            selectedInstance.SetFloat("_GlowIntensity", selectedIntensity);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    private void Update()
    {
        // JC | 2026-11-12 | 6씬의 공통 초안을 매 프레임 읽어 기본 너비·배율·두 축 피봇을 즉시 반영합니다.
        float scale = hoverScale;
        bool useMotion = false;
        if (sharedSettings != null && sharedSettings.isActiveAndEnabled &&
            sharedSettings.TryGetMotionDraft(out var sharedScale, out var pivotX, out var pivotY, out var widthRatio))
        {
            useMotion = true;
            scale = sharedScale;
            ApplyMotionWidth(widthRatio);
            ApplyMotionPivot(new Vector2(pivotX, pivotY));
        }
        if (!useMotion && scale <= 1f) return;
        bool canHover = (hovered || wasSelected) && (selectable == null || selectable.IsInteractable());
        hoverBlend = Mathf.MoveTowards(hoverBlend, canHover ? 1f : 0f,
            Time.unscaledDeltaTime / Mathf.Max(.01f, hoverScaleSeconds));
        // JC | 2026-11-12 | 확대 중 1배로 낮춰도 반드시 기본 크기로 돌아오며, 씬의 98% 기본 배율은 유지합니다.
        transform.localScale = restingScale * Mathf.Lerp(1f, scale, Mathf.SmoothStep(0f, 1f, hoverBlend));
    }

    // JC | 2026-11-12 | 원래 스트레치 배치를 보관해 축소를 반복하거나 부모 너비가 바뀌어도 원래 좌측 경계에서 계산합니다.
    private bool motionLayoutCaptured;
    private Vector2 originalHorizontalAnchors;
    private float originalWidthDelta, originalPositionX, originalPivotX;
    private float companionWidthReduction;

    public void CaptureMotionLayout()
    {
        var rect = transform as RectTransform;
        if (rect == null) return;
        originalHorizontalAnchors = new Vector2(rect.anchorMin.x, rect.anchorMax.x);
        originalWidthDelta = rect.sizeDelta.x;
        originalPositionX = rect.anchoredPosition.x;
        originalPivotX = rect.pivot.x;
        companionWidthReduction = 0;
        motionLayoutCaptured = true;
    }

    private void ApplyMotionWidth(float ratio)
    {
        var rect = transform as RectTransform;
        var parent = rect != null ? rect.parent as RectTransform : null;
        if (parent == null) return;
        if (!motionLayoutCaptured) CaptureMotionLayout();
        float originalWidth = parent.rect.width * (originalHorizontalAnchors.y - originalHorizontalAnchors.x) + originalWidthDelta;
        if (originalWidth <= 0) return;
        float width = originalWidth * Mathf.Clamp(ratio, .6f, 1f);
        float originalLocalX = parent.rect.xMin + parent.rect.width * Mathf.Lerp(originalHorizontalAnchors.x, originalHorizontalAnchors.y, originalPivotX) + originalPositionX;
        float left = originalLocalX - originalWidth * originalPivotX * restingScale.x;
        // JC | 2026-11-12 | 배율로 찌그러뜨리지 않고 Rect 너비만 변경합니다. 원래 비확대 왼쪽 경계에서 현재 피봇 위치를 다시 구합니다.
        rect.sizeDelta = new Vector2(originalWidthDelta + width - originalWidth, rect.sizeDelta.y);
        var position = rect.localPosition;
        position.x = left + width * rect.pivot.x * restingScale.x;
        rect.localPosition = position;
        companionWidthReduction = (originalWidth - width) * restingScale.x;
    }

    private void ApplyMotionPivot(Vector2 pivot)
    {
        var rect = transform as RectTransform;
        if (rect == null || rect.pivot == pivot) return;
        // JC | 2026-11-12 | 두 축 모두 비확대 상태의 중심 기준으로 보정하여 피봇 변경 중 행 정렬·간격을 보존합니다.
        Vector2 delta = pivot - rect.pivot;
        Vector3 offset = new Vector3(delta.x * rect.rect.width * restingScale.x, delta.y * rect.rect.height * restingScale.y, 0);
        Vector3 position = rect.localPosition + rect.localRotation * offset;
        rect.pivot = pivot;
        rect.localPosition = position;
    }

    // JC | 2026-11-12 | 코어스킬명은 버튼의 형제이므로 기존 계층·참조를 유지하면서 같은 피봇/배율로 동반 확대합니다.
    private RectTransform scaleCompanion;
    private Vector2 companionRestPosition;
    private Vector3 companionRestScale;
    private float companionRestWidthDelta;

    public void BindScaleCompanion(RectTransform label)
    {
        scaleCompanion = label;
        if (label == null) return;
        companionRestPosition = label.anchoredPosition;
        companionRestScale = label.localScale;
        companionRestWidthDelta = label.sizeDelta.x;
    }

    private void LateUpdate()
    {
        if (scaleCompanion == null || scaleCompanion.parent != transform.parent || restingScale.x == 0 || restingScale.y == 0) return;
        // JC | 2026-11-12 | 매번 원래 앵커 위치에서 계산하므로 해상도 변경과 반복 호버에도 텍스트 위치가 누적 이동하지 않습니다.
        scaleCompanion.anchoredPosition = companionRestPosition;
        // JC | 2026-11-12 | 형제인 코어스킬명의 오른쪽 텍스트 영역도 줄어든 버튼 너비를 따라가게 합니다.
        scaleCompanion.sizeDelta = new Vector2(companionRestWidthDelta - companionWidthReduction, scaleCompanion.sizeDelta.y);
        Vector3 multiplier = new Vector3(transform.localScale.x / restingScale.x, transform.localScale.y / restingScale.y, 1);
        scaleCompanion.localPosition = transform.localPosition + Vector3.Scale(scaleCompanion.localPosition - transform.localPosition, multiplier);
        scaleCompanion.localScale = Vector3.Scale(companionRestScale, multiplier);
    }

    private void RestoreScaleCompanion()
    {
        if (scaleCompanion == null) return;
        scaleCompanion.anchoredPosition = companionRestPosition;
        scaleCompanion.localScale = companionRestScale;
    }

    private void OnDisable()
    {
        SetState(false, false);
        hovered = false;
        hoverBlend = 0f;
        // JC | 2026-11-12 | 공통 확대 설정을 사용하는 스킬도 비활성화 시 기본 크기로 복원합니다.
        if (hoverScale > 1f || (sharedSettings != null && sharedSettings.SupportsMotionControls)) transform.localScale = restingScale;
        // JC | 2026-11-12 | 버튼 비활성화 시 별도 텍스트도 원래 배치로 복원합니다.
        RestoreScaleCompanion();
    }

    private void UpdateOrbitPixelSize()
    {
        if (selectedInstance == null || selectedFrame == null) return;
        var rect = selectedFrame.rectTransform.rect;
        selectedInstance.SetVector("_BattleRimRect", new Vector4(rect.xMin, rect.yMin, rect.width, rect.height));
        var canvas = selectedFrame.canvas;
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        selectedFrame.rectTransform.GetWorldCorners(selectedCorners);
        var bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, selectedCorners[0]);
        float width = Vector2.Distance(bottomLeft, RectTransformUtility.WorldToScreenPoint(camera, selectedCorners[3]));
        float height = Vector2.Distance(bottomLeft, RectTransformUtility.WorldToScreenPoint(camera, selectedCorners[1]));
        if (selectedFrame.preserveAspect && selectedFrame.sprite != null)
        {
            float aspect = selectedFrame.sprite.rect.width / Mathf.Max(1, selectedFrame.sprite.rect.height);
            if (width / Mathf.Max(1, height) > aspect) width = height * aspect;
            else height = width / aspect;
        }
        selectedInstance.SetVector("_OrbitUVPerPixel", new Vector4(1 / Mathf.Max(1, width), 1 / Mathf.Max(1, height), 0, 0));
    }

    private void OnDestroy()
    {
        // JC | 2026-11-12 | 시각 컴포넌트만 제거되는 경우에도 형제 텍스트에 확대값을 남기지 않습니다.
        RestoreScaleCompanion();
        if (inset != null)
        {
            if (Application.isPlaying) Destroy(inset.gameObject);
            else DestroyImmediate(inset.gameObject);
        }
        if (availableEffect != null) availableEffect.material = availableSource;
        if (selectedFrame != null) selectedFrame.material = selectedSource;
        if (availableInstance != null) Destroy(availableInstance);
        if (selectedInstance != null) Destroy(selectedInstance);
    }
}
