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
        HeroSelected,   // 지정 영웅을 직접 선택하면 (기존 직렬화 번호 유지)
        CountAndConfirmed // 횟수를 조정한 뒤 실제 진행이 성공하면 (기존 직렬화 번호 유지)
    }

    [Serializable]
    public sealed class Step
    {
        [Tooltip("기존 Panels 배열의 설명 패널 번호입니다. 1~10이 홍보 실습이며 0은 도입 안내입니다.")]
        public int panelIndex;
        [Tooltip("클릭 또는 실제 홍보 조작 중 이 단계의 완료 조건입니다. 조작 단계는 조건 충족 전 넘어가지 않습니다.")]
        public AdvanceCondition advance = AdvanceCondition.Click;
        [HideInInspector] // 이전 편집 저장본 호환용. 표시 문구는 sceneMessage에서 편집합니다.
        public string message;
        [Tooltip("CountEquals에서 맞출 횟수 또는 Confirmed·CountAndConfirmed에서 허용할 확정 횟수입니다. 확정 단계의 0은 기본 실습 20회를 사용합니다.")]
        public int targetCount;
        [Tooltip("Highlight Pools에서 강조할 대상의 번호(0부터). 여러 개 지정할 수 있습니다.")]
        public List<int> highlightIndices = new List<int>();
        [Tooltip("강조 대상을 올릴 기존 설명 패널입니다. 비어 있으면 현재 Panels 항목을 사용합니다.")]
        public GameObject explanationPanel;
        [HideInInspector]
        public bool useAuthoredPresentation;
        [HideInInspector]
        public MessagePresentation authoredPresentation;
        [HideInInspector]
        public List<OverlayPresentation> authoredOverlays = new List<OverlayPresentation>();
        [Header("씬에서 편집하는 안내 배치")]
        public RectTransform messageBox;
        public TMPro.TMP_Text sceneMessage, sceneHint;
        public RectTransform practiceLayer;
        public List<SceneOverlay> sceneOverlays = new List<SceneOverlay>();
    }

    [Serializable]
    public sealed class SceneOverlay
    {
        public string key;
        public RectTransform rect;
    }

    [Serializable]
    public sealed class ElementPresentation
    {
        public Vector2 anchorMin, anchorMax, pivot, position, size;
        public Vector3 scale;
        public Quaternion rotation;
        public float fontSize, fontSizeMin, fontSizeMax, characterSpacing, lineSpacing, wordSpacing, paragraphSpacing;
        public bool autoSize, wordWrapping, hasImage, hasImageEnabled, imageEnabled, active;
        public Color color, imageColor;
        public Vector4 margin;
        public TMPro.FontStyles fontStyle;
        public TMPro.TextAlignmentOptions alignment;
        public static ElementPresentation Capture(RectTransform rect, TMPro.TMP_Text text = null)
        {
            if (rect == null) return null;
            var result = new ElementPresentation { anchorMin = rect.anchorMin, anchorMax = rect.anchorMax,
                pivot = rect.pivot, position = rect.anchoredPosition, size = rect.sizeDelta,
                scale = rect.localScale, rotation = rect.localRotation, active = rect.gameObject.activeSelf };
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image != null) { result.hasImage = true; result.imageColor = image.color; result.hasImageEnabled = true; result.imageEnabled = image.enabled; }
            if (text != null)
            {
                result.fontSize = text.fontSize; result.fontSizeMin = text.fontSizeMin; result.fontSizeMax = text.fontSizeMax;
                result.autoSize = text.enableAutoSizing; result.wordWrapping = text.enableWordWrapping;
                result.characterSpacing = text.characterSpacing; result.lineSpacing = text.lineSpacing;
                result.wordSpacing = text.wordSpacing; result.paragraphSpacing = text.paragraphSpacing;
                result.margin = text.margin; result.fontStyle = text.fontStyle;
                result.color = text.color; result.alignment = text.alignment;
            }
            return result;
        }
        public void Apply(RectTransform rect, TMPro.TMP_Text text = null)
        {
            if (rect == null) return;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = scale; rect.localRotation = rotation;
            rect.gameObject.SetActive(active);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (hasImage && image != null) { image.color = imageColor; if (hasImageEnabled) image.enabled = imageEnabled; }
            if (text == null) return;
            text.fontSize = fontSize; text.fontSizeMin = fontSizeMin; text.fontSizeMax = fontSizeMax;
            text.enableAutoSizing = autoSize; text.enableWordWrapping = wordWrapping;
            text.characterSpacing = characterSpacing; text.lineSpacing = lineSpacing; text.color = color; text.alignment = alignment;
            text.wordSpacing = wordSpacing; text.paragraphSpacing = paragraphSpacing; text.margin = margin; text.fontStyle = fontStyle;
        }
    }
    [Serializable]
    public sealed class MessagePresentation
    {
        public ElementPresentation message, hint, background;
        public string hintText;
        public bool textInBackground, hintInBackground;
    }
    [Serializable]
    public sealed class OverlayPresentation
    {
        public string key, childPath;
        public ElementPresentation element;
    }
    [Serializable]
    private sealed class OverlayEdits
    {
        public List<OverlayPresentation> elements = new List<OverlayPresentation>();
    }
    // 씬에 배치된 영역을 이전 편집 저장본의 키로 연결한다.
    private readonly Dictionary<string, RectTransform> generatedOverlays = new Dictionary<string, RectTransform>();
    private static Transform TargetTransform(UnityEngine.Object target)
        => target is Component component ? component.transform : target is GameObject go ? go.transform : null;
    public bool IsOverlayElement(UnityEngine.Object target)
    {
        var rect = TargetTransform(target);
        if (current == null || rect == null || current.messageBox != null) return false;
        foreach (var root in generatedOverlays.Values)
            if (root != null && (rect == root || rect.IsChildOf(root))) return true;
        return false;
    }
    public string CaptureOverlayPresentation(UnityEngine.Object target = null)
    {
        if (current == null) return null;
        if (current.authoredOverlays == null) current.authoredOverlays = new List<OverlayPresentation>();
        var selected = TargetTransform(target);
        foreach (var pair in generatedOverlays)
        {
            var root = pair.Value;
            if (root == null || (target != null && (selected == null || (selected != root && !selected.IsChildOf(root))))) continue;
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                string path = rect == root ? "" : rect.name;
                for (var parent = rect.parent; rect != root && parent != null && parent != root; parent = parent.parent) path = parent.name + "/" + path;
                var saved = current.authoredOverlays.Find(e => e.key == pair.Key && e.childPath == path);
                // Undo는 이미 편집한 요소만 재보관한다. 다른 영역의 기본 배치를 고정하지 않는다.
                if (target == null && saved == null) continue;
                if (saved == null) { saved = new OverlayPresentation { key = pair.Key, childPath = path }; current.authoredOverlays.Add(saved); }
                saved.element = ElementPresentation.Capture(rect);
            }
        }
        return JsonUtility.ToJson(new OverlayEdits { elements = current.authoredOverlays });
    }
    public void SetOverlayPresentation(int panelIndex, string json)
    {
        var step = FindStep(panelIndex);
        if (step == null) throw new ArgumentException("홍보 안내 단계가 없습니다: " + panelIndex);
        step.authoredOverlays = JsonUtility.FromJson<OverlayEdits>(json)?.elements ?? new List<OverlayPresentation>();
        generatedOverlays.Clear();
        if (step.sceneOverlays != null) foreach (var overlay in step.sceneOverlays)
            if (overlay != null && overlay.rect != null) generatedOverlays[overlay.key] = overlay.rect;
        ApplyOverlayPresentation(step);
        generatedOverlays.Clear();
    }
    private void ApplyOverlayPresentation(Step step)
    {
        if (step.authoredOverlays == null) return;
        foreach (var edit in step.authoredOverlays)
            if (edit != null && generatedOverlays.TryGetValue(edit.key, out var root) && root != null)
                edit.element?.Apply(string.IsNullOrEmpty(edit.childPath) ? root : root.Find(edit.childPath) as RectTransform);
    }
    public int CurrentPanelIndex => current != null ? current.panelIndex : -1;
    public TMPro.TMP_Text CurrentMessage => messageText;
    public bool IsMessageElement(UnityEngine.Object target)
    {
        var component = target as Component;
        var go = target as GameObject;
        var transform = component != null ? component.transform : go != null ? go.transform : null;
        return current != null && current.messageBox == null && transform != null && (transform == messageBackground ||
            (messageText != null && transform == messageText.transform) || (hintText != null && transform == hintText.transform));
    }
    public string CaptureMessagePresentation()
    {
        if (current == null || messageText == null) return null;
        current.message = messageText.text;
        current.useAuthoredPresentation = true;
        current.authoredPresentation = new MessagePresentation {
            message = ElementPresentation.Capture(messageText.rectTransform, messageText),
            hint = hintText != null ? ElementPresentation.Capture(hintText.rectTransform, hintText) : null,
            background = ElementPresentation.Capture(messageBackground), hintText = hintText != null ? hintText.text : null,
            textInBackground = messageBackground != null && messageText.transform.IsChildOf(messageBackground),
            hintInBackground = messageBackground != null && hintText != null && hintText.transform.IsChildOf(messageBackground) };
        return JsonUtility.ToJson(current.authoredPresentation);
    }
    public void SetMessagePresentation(int panelIndex, string message, string json)
    {
        var step = FindStep(panelIndex);
        if (step == null) throw new ArgumentException("홍보 안내 단계가 없습니다: " + panelIndex);
        step.message = message;
        step.useAuthoredPresentation = true;
        step.authoredPresentation = JsonUtility.FromJson<MessagePresentation>(json);
        if (step.messageBox == null || step.sceneMessage == null) return;
        messageBackground = step.messageBox;
        step.authoredPresentation.background?.Apply(step.messageBox);
        ApplyMessageText(step.authoredPresentation.message, step.sceneMessage, step.authoredPresentation.textInBackground);
        ApplyMessageText(step.authoredPresentation.hint, step.sceneHint, step.authoredPresentation.hintInBackground);
        step.sceneMessage.text = message;
        if (step.sceneHint != null && step.authoredPresentation.hintText != null) step.sceneHint.text = step.authoredPresentation.hintText;
        messageBackground = null;
    }

    [SerializeField] private GameObject dimPanel;
    [SerializeField] private TutorialPublicityController publicity;
    [Tooltip("홍보 실습에서 직접 선택할 영웅 키입니다. 시작 시 이 영웅의 IP를 0/40으로 준비하며 자동 선택하지 않습니다.")]
    [SerializeField] private string tutorialHeroKey = "10001";
    [Tooltip("기존 설명 패널별 완료 조건과 씬 UI 참조입니다. 문구와 배치는 해당 씬 오브젝트에서 편집합니다.")]
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
    [SerializeField] private GameObject legacyPreview;
    private TMPro.TMP_Text messageText, hintText;
    private RectTransform messageBackground;

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
        if (legacyPreview != null) legacyPreview.SetActive(false);
    }

    private sealed class SortingState
    {
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
    public bool IsTemporarilyPromotedTarget(UnityEngine.Object target)
    {
        var component = target as Component;
        var go = target as GameObject;
        var rect = component != null ? component.transform : go != null ? go.transform : null;
        return rect != null && sortingStates.Exists(s => !s.keepParent && s.rect != null &&
            (rect == s.rect || rect.IsChildOf(s.rect)));
    }

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
        RestoreSorting();
        generatedOverlays.Clear();
        messageBackground = null;
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
            case AdvanceCondition.Confirmed:
            case AdvanceCondition.CountAndConfirmed:
                return p.SelectedKey == tutorialHeroKey && p.ConfirmCount > confirmCountAtStart;
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
            case AdvanceCondition.CountAndConfirmed: input = TutorialPublicityController.PracticeInput.CountAndConfirm; break;
            case AdvanceCondition.CountMax: input = TutorialPublicityController.PracticeInput.Max; break;
            case AdvanceCondition.Confirmed: input = TutorialPublicityController.PracticeInput.Confirm; break;
            case AdvanceCondition.ModalClosed: input = TutorialPublicityController.PracticeInput.Close; break;
        }
        Publicity.SetPracticeInput(input, current.targetCount);
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
            case AdvanceCondition.CountAndConfirmed:
                targets.AddRange(InformationTargets());
                if (advance == AdvanceCondition.CountMax && p.MaxButton != null) targets.Add(p.MaxButton.gameObject);
                if ((advance == AdvanceCondition.Confirmed || advance == AdvanceCondition.CountAndConfirmed) &&
                    p.ConfirmButton != null) targets.Add(p.ConfirmButton.gameObject);
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

    // 씬 오브젝트를 그대로 표시하며 위치·크기·문자 설정을 덮어쓰지 않는다.
    private void PrepareMessage(GameObject panel, Step step)
    {
        messageBackground = step.messageBox;
        messageText = step.sceneMessage;
        hintText = step.sceneHint;
    }

    private void ShowHighlights(Step step)
    {
        var dim = currentPanel != null ? currentPanel : dimPanel;
        if (dim == null) return;
        var targets = PracticeTargets(step.advance);
        if (step.highlightIndices != null && highlightPools != null)
            foreach (int index in step.highlightIndices)
                if (index >= 0 && index < highlightPools.Count) targets.Add(highlightPools[index]);
        foreach (var target in targets)
        {
            if (target == null || target == dim || transform.IsChildOf(target.transform) ||
                dim.transform.IsChildOf(target.transform)) continue;
            HideBackgroundCopy(target.transform);
            Promote(target, step.practiceLayer != null ? step.practiceLayer : dim.transform);
        }
        // 디밍·강조는 씬에 저장된 사각형이다. 실습 대상의 표시 순서만 올린다.
        generatedOverlays.Clear();
        if (step.sceneOverlays != null)
            foreach (var overlay in step.sceneOverlays)
                if (overlay != null && overlay.rect != null) generatedOverlays[overlay.key] = overlay.rect;
    }

    private bool IsNestedMessage(RectTransform rect) => rect != null && messageBackground != null &&
        rect != messageBackground && rect.IsChildOf(messageBackground);

    private void ApplyMessageText(ElementPresentation saved, TMPro.TMP_Text text, bool nestedCoordinates)
    {
        if (saved == null || text == null) return;
        var rect = text.rectTransform;
        if (nestedCoordinates || !IsNestedMessage(rect)) { saved.Apply(rect, text); return; }
        // 이전 저장본은 설명 패널 좌표다. 그 좌표로 적용한 뒤 배경 자식 좌표로 변환한다.
        var parent = rect.parent;
        rect.SetParent(messageBackground.parent, false);
        saved.Apply(rect, text);
        var size = rect.rect.size; var position = rect.position;
        rect.SetParent(parent, true);
        rect.anchorMin = rect.anchorMax = ((RectTransform)parent).pivot;
        rect.sizeDelta = size; rect.position = position;
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

    private void RestoreSorting()
    {
        for (int i = sortingStates.Count - 1; i >= 0; i--)
        {
            var s = sortingStates[i];
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
