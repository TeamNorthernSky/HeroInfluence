using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 실제 튜토리얼 홍보 패널 위에 설명 패널을 띄우고, 지정한 대상만 그 위로 끌어올려 강조한다.
/// 단계마다 정한 동작(횟수 설정, 진행, 닫기 등)을 플레이어가 하면 다음 설명으로 넘어간다.
/// </summary>
public sealed class TutorialPublicityExplanationView : MonoBehaviour
{
    public enum AdvanceCondition
    {
        Click,          // 다음 버튼(클릭)으로 넘어간다
        ModalOpened,    // 홍보 패널이 열리면
        CountEquals,    // 진행할 홍보 횟수가 targetCount가 되면
        CountMax,       // 진행할 홍보 횟수가 최대치가 되면
        Confirmed,      // 진행(확정)이 성공하면
        ModalClosed,    // 홍보 패널이 닫히면
        HeroSelected    // 지정 영웅을 직접 선택하면 (기존 직렬화 번호 유지)
    }

    [Serializable]
    public sealed class Step
    {
        [Tooltip("기존 Panels 배열의 설명 패널 번호입니다. 1~10이 홍보 실습이며 0은 도입 안내입니다.")]
        public int panelIndex;
        [Tooltip("클릭 또는 실제 홍보 조작 중 이 단계의 완료 조건입니다. 조작 단계는 조건 충족 전 넘어가지 않습니다.")]
        public AdvanceCondition advance = AdvanceCondition.Click;
        [TextArea, Tooltip("기존 설명 패널에 표시할 문구입니다. 비어 있으면 기존 문구를 유지합니다.")]
        public string message;
        [Tooltip("advance가 CountEquals일 때 맞춰야 할 횟수")]
        public int targetCount;
        [Tooltip("Highlight Pools에서 강조할 대상의 번호(0부터). 여러 개 지정할 수 있습니다.")]
        public List<int> highlightIndices = new List<int>();
        [Tooltip("강조 대상을 올릴 기존 설명 패널입니다. 비어 있으면 현재 Panels 항목을 사용합니다.")]
        public GameObject explanationPanel;
    }

    [SerializeField] private GameObject dimPanel;
    [SerializeField] private TutorialPublicityController publicity;
    [Tooltip("홍보 실습에서 직접 선택할 영웅 키입니다. 시작 시 이 영웅의 IP를 0/50으로 준비하며 자동 선택하지 않습니다.")]
    [SerializeField] private string tutorialHeroKey = "10001";
    [Tooltip("기존 설명 패널별 문구와 완료 조건입니다. 실제 선택·횟수·확정·닫기 조건을 설정합니다.")]
    [SerializeField] private Step[] steps = Array.Empty<Step>();
    [SerializeField] private List<GameObject> highlightPools = new List<GameObject>();
    [SerializeField] private GameObject backgroundSource;
    [SerializeField] private GameObject fakeBackground;
    private readonly List<KeyValuePair<TMPro.TMP_Text, TMPro.TMP_Text>> backgroundTexts =
        new List<KeyValuePair<TMPro.TMP_Text, TMPro.TMP_Text>>();

    private TutorialExploreUIController flow;
    private Step current;
    private int confirmCountAtStart;
    private int maximizeCountAtStart, shownFrame;
    private bool practicePrepared, awaitingRelease;
    private GameObject currentPanel;
    private readonly List<KeyValuePair<GameObject, bool>> hiddenExamples = new List<KeyValuePair<GameObject, bool>>();
    private TMPro.TMP_Text messageText, hintText;
    private string originalMessage, originalHint;
    private readonly List<KeyValuePair<RectTransform, Vector2>> messagePositions = new List<KeyValuePair<RectTransform, Vector2>>();
    private readonly List<KeyValuePair<RectTransform, Vector2>> messageSizes = new List<KeyValuePair<RectTransform, Vector2>>();
    private readonly List<KeyValuePair<TMPro.TMP_Text, Vector2>> messageFonts = new List<KeyValuePair<TMPro.TMP_Text, Vector2>>();
    private readonly List<GameObject> dimRegions = new List<GameObject>();
    private UnityEngine.UI.Image dimImage;
    private Color originalDimColor;
    private RectTransform messageBackground;
    private const float MessageWidth = 460f, MessagePadding = 26f, HeroMessageGap = 60f;
    // 공용 팝업 이미지에 들어 있는 I.P 제목 높이도 정보 강조 범위에 포함한다.
    private const float InformationHeaderHeight = 90f;

    public void EndPractice()
    {
        Hide();
        practicePrepared = false;
        if (publicity != null) publicity.SetPracticeInput(TutorialPublicityController.PracticeInput.Unrestricted);
    }

    public bool CanAdvance => current != null && Time.frameCount > shownFrame && !awaitingRelease &&
        (current.advance == AdvanceCondition.Click || (!Input.GetMouseButton(0) && IsSatisfied(current)));

    private void Awake()
    {
        if (fakeBackground != null) fakeBackground.SetActive(false);
        HideSharedPreview();
    }

    private void HideSharedPreview()
    {
        var preview = transform.Find("ClickToContinue/SharedPublicityPanel");
        if (preview != null) preview.gameObject.SetActive(false);
    }

    private sealed class SortingState
    {
        public GameObject border;
        public RectTransform rect;
        public Transform parent;
        public int sibling;
        public Vector2 anchorMin, anchorMax, pivot, sizeDelta;
        public Vector3 position, scale;
        public Quaternion rotation;
        public bool keepParent, createdCanvas, createdRaycaster;
        public Canvas canvas;
        public UnityEngine.UI.GraphicRaycaster raycaster;
        public bool canvasEnabled, canvasOverride, raycasterEnabled;
        public int sortingLayer, sortingOrder;
    }
    private readonly List<SortingState> sortingStates = new List<SortingState>();

    private TutorialPublicityController Publicity
    {
        get
        {
            if (publicity == null)
                publicity = FindFirstObjectByType<TutorialPublicityController>(FindObjectsInactive.Include);
            return publicity;
        }
    }

    /// <summary>해당 패널이 다음 버튼 대신 실제 동작으로 넘어가는 단계인지.</summary>
    public bool RequiresAction(int panelIndex)
    {
        var step = FindStep(panelIndex);
        return step != null && step.advance != AdvanceCondition.Click;
    }

    public bool IsPublicityModalOpen => Publicity != null && Publicity.IsModalOpen;

    public void ShowStep(int panelIndex, GameObject panel = null)
    {
        Hide();
        HideSharedPreview();
        current = FindStep(panelIndex);
        if (current == null) return;
        var p = Publicity;
        if (p == null) return;
        if (!practicePrepared)
        {
            p.BeginExplanation(tutorialHeroKey);
            practicePrepared = true;
        }
        currentPanel = current.explanationPanel != null ? current.explanationPanel : panel;
        confirmCountAtStart = p.ConfirmCount;
        maximizeCountAtStart = p.MaximizeCount;
        shownFrame = Time.frameCount;
        awaitingRelease = Input.GetMouseButton(0);
        SetPracticeInput(current.advance);
        PrepareMessage(currentPanel, current);
        PrepareBackground(panelIndex);
        ShowHighlights(current);
    }

    public void Hide()
    {
        current = null;
        RestoreDim();
        RestoreSorting();
        foreach (var size in messageSizes)
            if (size.Key != null) size.Key.sizeDelta = size.Value;
        messageSizes.Clear();
        foreach (var font in messageFonts)
            if (font.Key != null) { font.Key.fontSize = font.Value.x; font.Key.fontSizeMax = font.Value.y; }
        messageFonts.Clear();
        messageBackground = null;
        foreach (var position in messagePositions)
            if (position.Key != null) position.Key.anchoredPosition = position.Value;
        messagePositions.Clear();
        foreach (var example in hiddenExamples)
            if (example.Key != null) example.Key.SetActive(example.Value);
        hiddenExamples.Clear();
        if (messageText != null) messageText.text = originalMessage;
        if (hintText != null) hintText.text = originalHint;
        messageText = hintText = null;
        currentPanel = null;
        if (publicity != null && practicePrepared)
            publicity.SetPracticeInput(TutorialPublicityController.PracticeInput.Blocked);
        backgroundTexts.Clear();
        if (fakeBackground != null) fakeBackground.SetActive(false);
    }

    // 기존 강조 대상의 임시 계층 이동·복원 기능을 도입 안내에도 재사용한다.
    public void ShowEntryTarget(GameObject target, GameObject explanation)
    {
        Hide();
        if (target != null && explanation != null) Promote(target, explanation.transform);
    }

    private void LateUpdate()
    {
        foreach (var pair in backgroundTexts)
            if (pair.Key != null && pair.Value != null) pair.Value.text = pair.Key.text;
    }

    private void PrepareBackground(int panelIndex)
    {
        if (fakeBackground == null || backgroundSource == null || panelIndex == 0) return;
        CopyBackground(backgroundSource.transform, fakeBackground.transform);
        fakeBackground.SetActive(true);
        fakeBackground.transform.SetAsFirstSibling();
    }

    private void CopyBackground(Transform source, Transform destination)
    {
        destination.gameObject.SetActive(source.gameObject.activeSelf);
        var sourceText = source.GetComponent<TMPro.TMP_Text>();
        var targetText = destination.GetComponent<TMPro.TMP_Text>();
        if (sourceText != null && targetText != null)
        {
            targetText.text = sourceText.text;
            backgroundTexts.Add(new KeyValuePair<TMPro.TMP_Text, TMPro.TMP_Text>(sourceText, targetText));
        }
        var sourceImage = source.GetComponent<UnityEngine.UI.Image>();
        var targetImage = destination.GetComponent<UnityEngine.UI.Image>();
        if (sourceImage != null && targetImage != null)
        {
            targetImage.sprite = sourceImage.sprite;
            targetImage.color = sourceImage.color;
            targetImage.enabled = sourceImage.enabled;
        }
        foreach (Transform child in source)
        {
            var target = destination.Find(child.name);
            if (target != null) CopyBackground(child, target);
        }
    }

    private void Update()
    {
        if (current == null) return;
        if (awaitingRelease)
        {
            if (!Input.GetMouseButton(0)) awaitingRelease = false;
            return;
        }
        // 첫 활성 프레임에는 Roster.Start가 아직 카드를 생성하지 않았을 수 있다.
        if (current.advance == AdvanceCondition.HeroSelected &&
            !sortingStates.Exists(s => s.rect != null && s.rect.GetComponent<TutorialPublicityHeroCard>() != null))
            ShowHighlights(current);
        if (current.advance == AdvanceCondition.Click || !CanAdvance) return;
        // 목표값에 닿아도 드래그 중에는 실제 Slider를 다른 부모로 옮기지 않는다.
        if ((current.advance == AdvanceCondition.CountEquals || current.advance == AdvanceCondition.CountMax)
            && Input.GetMouseButton(0)) return;
        if (flow == null) flow = GetComponent<TutorialExploreUIController>();
        // 같은 프레임 중복 전환 방지로 무시되면 다음 프레임에 다시 시도한다.
        if (flow != null) flow.ShowNextPanel();
    }

    private bool IsSatisfied(Step step)
    {
        var p = Publicity;
        if (p == null) return false;
        switch (step.advance)
        {
            case AdvanceCondition.ModalOpened: return p.IsModalOpen;
            case AdvanceCondition.ModalClosed: return !p.IsModalOpen;
            case AdvanceCondition.HeroSelected: return p.SelectedKey == tutorialHeroKey;
            case AdvanceCondition.CountEquals: return p.SelectedKey == tutorialHeroKey && p.Count == step.targetCount;
            case AdvanceCondition.CountMax: return p.SelectedKey == tutorialHeroKey &&
                p.MaximizeCount > maximizeCountAtStart && p.Count > 0 && p.Count == p.MaxCount;
            case AdvanceCondition.Confirmed: return p.SelectedKey == tutorialHeroKey && p.ConfirmCount > confirmCountAtStart;
            default: return false;
        }
    }

    // 기존 자동 선택 기능은 보존하되 수동 선택 실습에서는 호출하지 않는다.
    private void EnsureHeroSelected()
    {
        var p = Publicity;
        if (p == null || !p.IsModalOpen || p.SelectedKey == tutorialHeroKey) return;
        p.SelectHero(tutorialHeroKey);
    }

    private Step FindStep(int panelIndex)
    {
        foreach (var step in steps)
            if (step != null && step.panelIndex == panelIndex) return step;
        return null;
    }

    private void SetPracticeInput(AdvanceCondition advance)
    {
        var input = TutorialPublicityController.PracticeInput.Blocked;
        switch (advance)
        {
            case AdvanceCondition.HeroSelected: input = TutorialPublicityController.PracticeInput.HeroSelection; break;
            case AdvanceCondition.CountEquals: input = TutorialPublicityController.PracticeInput.Count; break;
            case AdvanceCondition.CountMax: input = TutorialPublicityController.PracticeInput.Max; break;
            case AdvanceCondition.Confirmed: input = TutorialPublicityController.PracticeInput.Confirm; break;
            case AdvanceCondition.ModalClosed: input = TutorialPublicityController.PracticeInput.Close; break;
        }
        Publicity.SetPracticeInput(input);
    }

    private List<GameObject> PracticeTargets(AdvanceCondition advance)
    {
        var targets = new List<GameObject>();
        var p = Publicity;
        if (p == null) return targets;
        switch (advance)
        {
            case AdvanceCondition.HeroSelected:
                foreach (var card in FindObjectsByType<TutorialPublicityHeroCard>(FindObjectsSortMode.None))
                    if (card.Owner == p && card.HeroKey == tutorialHeroKey) targets.Add(card.gameObject);
                break;
            case AdvanceCondition.CountEquals:
            case AdvanceCondition.CountMax:
            case AdvanceCondition.Confirmed:
                targets.AddRange(InformationTargets());
                if (advance == AdvanceCondition.CountMax && p.MaxButton != null) targets.Add(p.MaxButton.gameObject);
                if (advance == AdvanceCondition.Confirmed && p.ConfirmButton != null) targets.Add(p.ConfirmButton.gameObject);
                break;
            case AdvanceCondition.ModalClosed:
                if (p.CloseButton != null) targets.Add(p.CloseButton.gameObject);
                break;
        }
        return targets;
    }

    private List<GameObject> InformationTargets()
    {
        var targets = new List<GameObject>();
        var p = Publicity;
        if (p.ProgressSlider != null) targets.Add(p.ProgressSlider.gameObject);
        if (p.PreviousButton != null) targets.Add(p.PreviousButton.gameObject);
        if (p.NextButton != null) targets.Add(p.NextButton.gameObject);
        if (p.CountCaption != null) targets.Add(p.CountCaption.gameObject);
        foreach (var target in p.InformationTargets) if (target != null) targets.Add(target);
        return targets;
    }

    private void PrepareMessage(GameObject panel, Step step)
    {
        if (panel == null || string.IsNullOrEmpty(step.message)) return;
        var background = panel.transform.Find("TutorialGuideBackground");
        if (background == null)
        {
            // 배경이 없는 기존 패널도 다른 설명 패널에 배치된 배경을 빌려 사용한다.
            foreach (var candidate in GetComponentsInChildren<Transform>(true))
                if (candidate.name == "TutorialGuideBackground") { background = candidate; break; }
            if (background != null) Promote(background.gameObject, panel.transform);
        }
        if (background != null) background.SetAsFirstSibling();
        foreach (Transform child in panel.transform)
        {
            // 이전의 예시 슬라이더·카드가 실제 조작 대상 위에 겹치지 않게 임시로 숨긴다.
            if (child.name == "Text (TMP)")
            {
                messageText = child.GetComponent<TMPro.TMP_Text>();
                if (messageText != null) { originalMessage = messageText.text; messageText.text = step.message; }
            }
            else if (child.name == "TutorialContinueHint")
            {
                hintText = child.GetComponent<TMPro.TMP_Text>();
                if (hintText != null)
                {
                    originalHint = hintText.text;
                    hintText.text = step.advance == AdvanceCondition.Click ? "아무 곳이나 클릭하면 계속합니다." : "안내한 조작을 완료하면 계속합니다.";
                }
            }
            else if (child.name != "TutorialGuideBackground")
            {
                hiddenExamples.Add(new KeyValuePair<GameObject, bool>(child.gameObject, child.gameObject.activeSelf));
                child.gameObject.SetActive(false);
            }
        }
        // 실제 영웅 목록은 우측에 있으므로 기존 안내 상자를 좌측으로 옮겨 가림을 피한다.
        messageBackground = background as RectTransform;
        SetMessageWidth(messageBackground, MessageWidth + MessagePadding * 2);
        MoveMessageLeft(messageBackground);
        if (messageText != null)
        {
            ReduceMessageFont(messageText, 30f);
            SetMessageWidth(messageText.rectTransform, MessageWidth);
            MoveMessageLeft(messageText.rectTransform);
            messageText.transform.SetAsLastSibling();
        }
        if (hintText != null)
        {
            ReduceMessageFont(hintText, 22f);
            SetMessageWidth(hintText.rectTransform, MessageWidth);
            MoveMessageLeft(hintText.rectTransform);
            hintText.transform.SetAsLastSibling();
        }
    }

    private void ReduceMessageFont(TMPro.TMP_Text text, float size)
    {
        messageFonts.Add(new KeyValuePair<TMPro.TMP_Text, Vector2>(text, new Vector2(text.fontSize, text.fontSizeMax)));
        text.fontSize = Mathf.Min(text.fontSize, size);
        text.fontSizeMax = Mathf.Min(text.fontSizeMax, size);
    }

    private void SetMessageWidth(RectTransform rect, float width)
    {
        if (rect == null) return;
        messageSizes.Add(new KeyValuePair<RectTransform, Vector2>(rect, rect.sizeDelta));
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private void PositionHeroMessage(RectTransform hero, RectTransform panel)
    {
        if (messageText == null || hero == null || panel == null) return;
        var corners = new Vector3[4];
        hero.GetWorldCorners(corners);
        Vector2 left = panel.InverseTransformPoint(corners[0]);
        Vector2 top = panel.InverseTransformPoint(corners[1]);
        float x = left.x - HeroMessageGap - (MessageWidth + MessagePadding * 2) * .5f;
        float y = (left.y + top.y) * .5f;
        if (messageBackground != null)
        {
            messageBackground.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 220f);
            messageBackground.anchoredPosition = new Vector2(x, y);
        }
        messageText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 160f);
        messageText.rectTransform.anchoredPosition = new Vector2(x, y);
        if (hintText != null) hintText.rectTransform.anchoredPosition = new Vector2(x, y - 80f);
    }

    private void MoveMessageLeft(RectTransform rect)
    {
        if (rect == null) return;
        messagePositions.Add(new KeyValuePair<RectTransform, Vector2>(rect, rect.anchoredPosition));
        rect.anchoredPosition = new Vector2(-690f, rect.anchoredPosition.y);
    }

    private void ShowHighlights(Step step)
    {
        var dim = currentPanel != null ? currentPanel : dimPanel;
        if (dim == null) return;
        // 실제 입력 대상은 현재 Explanation의 자식, 배경용 UI는 그 아래에 둔다.
        transform.SetAsLastSibling();
        var targets = PracticeTargets(step.advance);
        if (step.highlightIndices != null && highlightPools != null)
            foreach (int index in step.highlightIndices)
                if (index >= 0 && index < highlightPools.Count) targets.Add(highlightPools[index]);
        Canvas.ForceUpdateCanvases();
        foreach (var target in targets)
        {
            if (target == null || target == dim ||
                transform.IsChildOf(target.transform) ||
                (dim != null && dim.transform.IsChildOf(target.transform))) continue;
            HideBackgroundCopy(target.transform);
            Promote(target, dim.transform);
        }
        Canvas.ForceUpdateCanvases();
        var informationTargets = InformationTargets();
        var gaugeGroup = sortingStates.FindAll(s => s.rect != null && informationTargets.Contains(s.rect.gameObject));
        bool hasGauge = (step.advance == AdvanceCondition.CountEquals ||
            step.advance == AdvanceCondition.CountMax || step.advance == AdvanceCondition.Confirmed) &&
            gaugeGroup.Exists(s => s.rect.GetComponent<UnityEngine.UI.Slider>() != null);
        if (hasGauge)
        {
            Rect bounds = TargetBounds((RectTransform)dim.transform, gaugeGroup);
            bounds.yMax += InformationHeaderHeight;
            KeepMessageClear((RectTransform)dim.transform, bounds);
            gaugeGroup[0].border = CreateBoundsBorder((RectTransform)dim.transform, bounds);
            OpenDimWindow((RectTransform)dim.transform, bounds);
        }
        foreach (var state in sortingStates)
        {
            if (state.rect.name == "TutorialGuideBackground") continue;
            if (hasGauge && gaugeGroup.Contains(state)) continue;
            state.border = state.rect.name == "Rest Count"
                ? CreateBoundsBorder((RectTransform)dim.transform, new List<SortingState> { state })
                : CreateHighlightBorder(state.rect);
            if (state.rect.GetComponent<TutorialPublicityHeroCard>() != null)
                PositionHeroMessage(state.rect, (RectTransform)dim.transform);
        }
    }

    private void KeepMessageClear(RectTransform panel, Rect information)
    {
        if (messageBackground == null) return;
        var corners = new Vector3[4];
        messageBackground.GetWorldCorners(corners);
        float right = panel.InverseTransformPoint(corners[2]).x;
        float shift = Mathf.Min(0, information.xMin - 32f - right);
        if (shift == 0) return;
        messageBackground.anchoredPosition += new Vector2(shift, 0);
        if (messageText != null) messageText.rectTransform.anchoredPosition += new Vector2(shift, 0);
        if (hintText != null) hintText.rectTransform.anchoredPosition += new Vector2(shift, 0);
    }

    private void OpenDimWindow(RectTransform panel, Rect window)
    {
        dimImage = panel.GetComponent<UnityEngine.UI.Image>();
        if (dimImage == null) return;
        originalDimColor = dimImage.color;
        var transparent = originalDimColor; transparent.a = 0;
        dimImage.color = transparent;
        // 원래 전체 화면 Graphic은 클릭을 차단한다. 실제 조작 대상은 그 위에 둔다.
        Rect full = panel.rect;
        window.xMin = Mathf.Clamp(window.xMin - 8, full.xMin, full.xMax);
        window.xMax = Mathf.Clamp(window.xMax + 8, full.xMin, full.xMax);
        window.yMin = Mathf.Clamp(window.yMin - 8, full.yMin, full.yMax);
        window.yMax = Mathf.Clamp(window.yMax + 8, full.yMin, full.yMax);
        AddDimRegion(panel, Rect.MinMaxRect(full.xMin, window.yMax, full.xMax, full.yMax));
        AddDimRegion(panel, Rect.MinMaxRect(full.xMin, full.yMin, full.xMax, window.yMin));
        AddDimRegion(panel, Rect.MinMaxRect(full.xMin, window.yMin, window.xMin, window.yMax));
        AddDimRegion(panel, Rect.MinMaxRect(window.xMax, window.yMin, full.xMax, window.yMax));
    }

    private void AddDimRegion(RectTransform panel, Rect bounds)
    {
        if (bounds.width <= 0 || bounds.height <= 0) return;
        var region = new GameObject("TutorialDimRegion", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = (RectTransform)region.transform;
        rect.SetParent(panel, false);
        rect.anchorMin = rect.anchorMax = panel.pivot;
        rect.anchoredPosition = bounds.center;
        rect.sizeDelta = bounds.size;
        rect.SetAsFirstSibling();
        var image = region.GetComponent<UnityEngine.UI.Image>();
        image.color = originalDimColor;
        image.raycastTarget = false;
        dimRegions.Add(region);
    }

    private void RestoreDim()
    {
        if (dimImage != null) dimImage.color = originalDimColor;
        dimImage = null;
        foreach (var region in dimRegions)
            if (region != null) { region.SetActive(false); if (Application.isPlaying) Destroy(region); else DestroyImmediate(region); }
        dimRegions.Clear();
    }

    private void HideBackgroundCopy(Transform target)
    {
        if (backgroundSource == null || fakeBackground == null || !target.IsChildOf(backgroundSource.transform)) return;
        string path = target.name;
        for (var parent = target.parent; parent != null && parent != backgroundSource.transform; parent = parent.parent)
            path = parent.name + "/" + path;
        var copy = fakeBackground.transform.Find(path);
        if (copy != null) copy.gameObject.SetActive(false);
    }

    private void Promote(GameObject target, Transform explanation)
    {
        var rect = target.transform as RectTransform;
        if (rect == null || sortingStates.Exists(s => s.rect == rect)) return;
        var state = new SortingState { rect = rect, parent = rect.parent,
            sibling = rect.GetSiblingIndex(), anchorMin = rect.anchorMin, anchorMax = rect.anchorMax,
            pivot = rect.pivot, sizeDelta = rect.sizeDelta, position = rect.anchoredPosition3D,
            scale = rect.localScale, rotation = rect.localRotation };
        sortingStates.Add(state);
        if (rect.GetComponent<TutorialPublicityHeroCard>() != null)
        {
            // 목록에서 제거하지 않고 렌더 순서만 올려 네 슬롯의 자동 배치를 유지한다.
            state.keepParent = true;
            state.canvas = rect.GetComponent<Canvas>();
            state.createdCanvas = state.canvas == null;
            if (state.createdCanvas) state.canvas = rect.gameObject.AddComponent<Canvas>();
            state.canvasEnabled = state.canvas.enabled;
            state.canvasOverride = state.canvas.overrideSorting;
            state.sortingLayer = state.canvas.sortingLayerID;
            state.sortingOrder = state.canvas.sortingOrder;
            var rootCanvas = explanation.GetComponentInParent<Canvas>();
            state.canvas.enabled = true;
            state.canvas.overrideSorting = true;
            if (rootCanvas != null)
            {
                state.canvas.sortingLayerID = rootCanvas.sortingLayerID;
                state.canvas.sortingOrder = rootCanvas.sortingOrder + 1;
            }
            state.raycaster = rect.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            state.createdRaycaster = state.raycaster == null;
            if (state.createdRaycaster) state.raycaster = rect.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            state.raycasterEnabled = state.raycaster.enabled;
            state.raycaster.enabled = true;
            return;
        }
        Vector3 worldPosition = rect.position;
        Vector2 size = rect.rect.size;
        rect.SetParent(explanation, true);
        // 원래 부모가 LayoutGroup이어도 전체 화면 설명 패널로 옮긴 뒤 크기가 늘어나지 않게 고정한다.
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.position = worldPosition;
        rect.SetAsLastSibling();
    }

    private static GameObject CreateBoundsBorder(RectTransform parent, List<SortingState> targets)
        => CreateBoundsBorder(parent, TargetBounds(parent, targets));

    private static Rect TargetBounds(RectTransform parent, List<SortingState> targets)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var corners = new Vector3[4];
        foreach (var state in targets)
        {
            var rect = state.rect;
            // 문자열의 textBounds는 갱신 시점에 따라 달라질 수 있으므로 실제 UI 사각형을 사용한다.
            rect.GetWorldCorners(corners);
            foreach (var point in corners)
            {
                Vector2 local = parent.InverseTransformPoint(point);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static GameObject CreateBoundsBorder(RectTransform parent, Rect bounds)
    {
        var border = CreateHighlightBorder(parent);
        var borderRect = (RectTransform)border.transform;
        borderRect.anchorMin = borderRect.anchorMax = parent.pivot;
        borderRect.pivot = new Vector2(.5f, .5f);
        borderRect.anchoredPosition = bounds.center;
        borderRect.sizeDelta = bounds.size + new Vector2(16, 16);
        return border;
    }

    private static GameObject CreateHighlightBorder(RectTransform target)
    {
        var root = new GameObject("TutorialHighlightBorder", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
        root.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)root.transform;
        rect.SetParent(target, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-4, -4);
        rect.offsetMax = new Vector2(4, 4);
        AddBorderEdge(rect, "Top", new Vector2(0, 1), Vector2.one, new Vector2(0, -3), Vector2.zero);
        AddBorderEdge(rect, "Bottom", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 3));
        AddBorderEdge(rect, "Left", Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
        AddBorderEdge(rect, "Right", new Vector2(1, 0), Vector2.one, new Vector2(-3, 0), Vector2.zero);
        return root;
    }

    private static void AddBorderEdge(RectTransform parent, string name, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var edge = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = (RectTransform)edge.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var image = edge.GetComponent<UnityEngine.UI.Image>();
        image.color = new Color(1f, .82f, .15f, 1f);
        image.raycastTarget = false;
    }

    private void RestoreSorting()
    {
        for (int i = sortingStates.Count - 1; i >= 0; i--)
        {
            var s = sortingStates[i];
            if (s.border != null)
            {
                s.border.SetActive(false);
                if (Application.isPlaying) Destroy(s.border);
                else DestroyImmediate(s.border);
            }
            if (s.keepParent)
            {
                // GraphicRaycaster는 Canvas에 의존하므로 먼저 해제한다.
                if (s.raycaster != null)
                {
                    s.raycaster.enabled = s.raycasterEnabled;
                    if (s.createdRaycaster) { s.raycaster.enabled = false; if (Application.isPlaying) Destroy(s.raycaster); else DestroyImmediate(s.raycaster); }
                }
                if (s.canvas != null)
                {
                    s.canvas.overrideSorting = s.canvasOverride;
                    s.canvas.sortingLayerID = s.sortingLayer;
                    s.canvas.sortingOrder = s.sortingOrder;
                    s.canvas.enabled = s.canvasEnabled;
                    if (s.createdCanvas) { if (Application.isPlaying) Destroy(s.canvas); else DestroyImmediate(s.canvas); }
                }
                continue;
            }
            if (s.rect != null && s.parent != null)
            {
                s.rect.SetParent(s.parent, false);
                s.rect.SetSiblingIndex(s.sibling);
                s.rect.anchorMin = s.anchorMin;
                s.rect.anchorMax = s.anchorMax;
                s.rect.pivot = s.pivot;
                s.rect.sizeDelta = s.sizeDelta;
                s.rect.anchoredPosition3D = s.position;
                s.rect.localScale = s.scale;
                s.rect.localRotation = s.rotation;
            }
        }
        sortingStates.Clear();
    }

    private void OnDisable() => EndPractice();

    private void OnDestroy() => RestoreSorting();
}
