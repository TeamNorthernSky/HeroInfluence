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
        ModalClosed     // 홍보 패널이 닫히면
    }

    [Serializable]
    public sealed class Step
    {
        public int panelIndex;
        public AdvanceCondition advance = AdvanceCondition.Click;
        [Tooltip("advance가 CountEquals일 때 맞춰야 할 횟수")]
        public int targetCount;
        [Tooltip("Highlight Pools에서 강조할 대상의 번호(0부터). 여러 개 지정할 수 있습니다.")]
        public List<int> highlightIndices = new List<int>();
        public GameObject explanationPanel;
    }

    [SerializeField] private GameObject dimPanel;
    [SerializeField] private TutorialPublicityController publicity;
    [Tooltip("홍보 패널이 열렸는데 선택된 영웅이 없으면 자동으로 고를 영웅 키")]
    [SerializeField] private string tutorialHeroKey = "10001";
    [SerializeField] private Step[] steps = Array.Empty<Step>();
    [SerializeField] private List<GameObject> highlightPools = new List<GameObject>();
    [SerializeField] private GameObject backgroundSource;
    [SerializeField] private GameObject fakeBackground;
    private readonly List<KeyValuePair<TMPro.TMP_Text, TMPro.TMP_Text>> backgroundTexts =
        new List<KeyValuePair<TMPro.TMP_Text, TMPro.TMP_Text>>();

    private TutorialExploreUIController flow;
    private Step current;
    private int confirmCountAtStart;

    private void Awake()
    {
        if (fakeBackground != null) fakeBackground.SetActive(false);
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

    public void ShowStep(int panelIndex)
    {
        Hide();
        current = FindStep(panelIndex);
        if (current == null) return;
        if (panelIndex == 0 && Publicity != null) Publicity.BeginExplanation(tutorialHeroKey);
        if (Publicity != null) confirmCountAtStart = Publicity.ConfirmCount;
        EnsureHeroSelected();
        PrepareBackground(panelIndex);
        ShowHighlights(current);
    }

    public void Hide()
    {
        current = null;
        RestoreSorting();
        backgroundTexts.Clear();
        if (fakeBackground != null) fakeBackground.SetActive(false);
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
        if (current == null || current.advance == AdvanceCondition.Click) return;
        EnsureHeroSelected();
        if (!IsSatisfied(current)) return;
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
            case AdvanceCondition.CountEquals: return p.Count == step.targetCount;
            case AdvanceCondition.CountMax: return p.Count > 0 && p.Count == p.MaxCount;
            case AdvanceCondition.Confirmed: return p.ConfirmCount > confirmCountAtStart;
            default: return false;
        }
    }

    // 실제 패널은 영웅이 선택돼야 게이지·진행이 동작하므로, 튜토리얼에서는 미리 골라 둔다.
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

    private void ShowHighlights(Step step)
    {
        var dim = step.explanationPanel != null ? step.explanationPanel : dimPanel;
        if (dim == null) return;
        // 실제 입력 대상은 현재 Explanation의 자식, 배경용 UI는 그 아래에 둔다.
        transform.SetAsLastSibling();
        if (step.highlightIndices == null || highlightPools == null) return;
        foreach (int index in step.highlightIndices)
        {
            if (index < 0 || index >= highlightPools.Count) continue;
            var target = highlightPools[index];
            if (target == null || target == dim ||
                transform.IsChildOf(target.transform) ||
                (dim != null && dim.transform.IsChildOf(target.transform))) continue;
            HideBackgroundCopy(target.transform);
            Promote(target, dim.transform);
        }
        Canvas.ForceUpdateCanvases();
        var gaugeGroup = sortingStates.FindAll(s => s.rect != null &&
            (s.rect.GetComponent<UnityEngine.UI.Slider>() != null || s.rect.GetComponent<UnityEngine.UI.Button>() != null));
        bool hasGauge = gaugeGroup.Exists(s => s.rect.GetComponent<UnityEngine.UI.Slider>() != null);
        if (hasGauge && gaugeGroup.Count > 0)
            gaugeGroup[0].border = CreateBoundsBorder((RectTransform)dim.transform, gaugeGroup);
        foreach (var state in sortingStates)
        {
            if (hasGauge && gaugeGroup.Contains(state)) continue;
            state.border = state.rect.name == "Rest Count"
                ? CreateBoundsBorder((RectTransform)dim.transform, new List<SortingState> { state })
                : CreateHighlightBorder(state.rect);
        }
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
        rect.SetParent(explanation, true);
        rect.SetAsLastSibling();
    }

    private static GameObject CreateBoundsBorder(RectTransform parent, List<SortingState> targets)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var corners = new Vector3[4];
        foreach (var state in targets)
        {
            var rect = state.rect;
            // 횟수 컨테이너 자체보다 실제 라벨/숫자 영역을 기준으로 맞춘다.
            var texts = rect.name == "Rest Count" ? rect.GetComponentsInChildren<TMPro.TMP_Text>(true) : null;
            if (texts != null && texts.Length > 0)
            {
                foreach (var text in texts)
                {
                    text.ForceMeshUpdate(true);
                    var bounds = text.textBounds;
                    for (int i = 0; i < 4; i++)
                    {
                        var point = text.transform.TransformPoint(new Vector3(
                            (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                            (i & 2) == 0 ? bounds.min.y : bounds.max.y, 0));
                        Vector2 local = parent.InverseTransformPoint(point);
                        min = Vector2.Min(min, local); max = Vector2.Max(max, local);
                    }
                }
            }
            else
            {
                rect.GetWorldCorners(corners);
                foreach (var point in corners)
                {
                    Vector2 local = parent.InverseTransformPoint(point);
                    min = Vector2.Min(min, local); max = Vector2.Max(max, local);
                }
            }
        }
        var border = CreateHighlightBorder(parent);
        var borderRect = (RectTransform)border.transform;
        borderRect.anchorMin = borderRect.anchorMax = parent.pivot;
        borderRect.pivot = new Vector2(.5f, .5f);
        borderRect.anchoredPosition = (min + max) * .5f;
        borderRect.sizeDelta = max - min + new Vector2(16, 16);
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

    private void OnDisable() => Hide();

    private void OnDestroy() => RestoreSorting();
}
