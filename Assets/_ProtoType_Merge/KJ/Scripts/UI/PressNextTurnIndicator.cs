using System.Collections;
using JC.Tutorial;
using UnityEngine;
using UnityEngine.UI;

/// <summary>턴 종료 안내 패널이 열려 있는 동안 외부 턴 종료 버튼을 강조한다.</summary>
[DisallowMultipleComponent]
public sealed class PressNextTurnIndicator : MonoBehaviour
{
    [SerializeField] private Button nextTurnButton;
    private JcTutorialGuideView view;
    private Coroutine binding;

    private void OnEnable() => binding = StartCoroutine(ShowWhenReady());

    private IEnumerator ShowWhenReady()
    {
        // UIComposer가 버튼을 최종 레이어로 옮긴 뒤 효과를 연결한다.
        yield return null;
        Canvas canvas = null;
        while (canvas == null)
        {
            if (nextTurnButton == null)
            {
                var target = GameObject.Find("BTN_Explor_NextTurn");
                if (target != null) nextTurnButton = target.GetComponentInChildren<Button>(true);
            }
            if (nextTurnButton != null) canvas = nextTurnButton.GetComponentInParent<Canvas>();
            if (canvas == null) yield return new WaitForSecondsRealtime(0.2f);
        }
        if (view == null) CreateView(canvas.rootCanvas.transform);
        view.SetVisible(true, nextTurnButton.transform as RectTransform,
            JcTutorialGuideView.FocusPresentation.BorderOnly);
        binding = null;
    }

    private void CreateView(Transform canvasRoot)
    {
        var overlay = new GameObject("PressNextTurnHighlight", typeof(RectTransform), typeof(CanvasGroup));
        overlay.layer = nextTurnButton.gameObject.layer;
        var rect = (RectTransform)overlay.transform;
        // 버튼의 TurnContent는 크기가 0이므로 화면 크기를 가진 Canvas를 기준으로 그린다.
        rect.SetParent(canvasRoot, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var group = overlay.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        view = overlay.AddComponent<JcTutorialGuideView>();
        view.focusFrame = CreateGraphic("FocusFrame", JcTutorialGraphic.Shape.Frame, 6, 18);
        view.pointer = CreateGraphic("Pointer", JcTutorialGraphic.Shape.Pointer, 0, 0);
        view.convergingOutlines = new[]
        {
            CreateGraphic("ConvergingOutline1", JcTutorialGraphic.Shape.Frame, 3, 14),
            CreateGraphic("ConvergingOutline2", JcTutorialGraphic.Shape.Frame, 3, 14)
        };
    }

    private JcTutorialGraphic CreateGraphic(string name, JcTutorialGraphic.Shape shape, float width, float glow)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.layer = view.gameObject.layer;
        child.transform.SetParent(view.transform, false);
        var graphic = child.AddComponent<JcTutorialGraphic>();
        graphic.Configure(shape, view.accent, 16, width);
        graphic.SetGlowWidth(glow);
        return graphic;
    }

    private void OnDisable()
    {
        if (binding != null) StopCoroutine(binding);
        binding = null;
        if (view != null) view.SetVisible(false);
    }

    private void OnDestroy()
    {
        if (view != null) Destroy(view.gameObject);
    }
}
