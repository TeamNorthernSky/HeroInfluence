using TMPro;
using UnityEngine;

namespace JC.Tutorial
{
    [DisallowMultipleComponent, AddComponentMenu("JC Tutorial/안내 표현 설정")]
    public sealed class JcTutorialGuideView : MonoBehaviour
    {
        [Header("씬 연결")]
        [Tooltip("기존 또는 신규 안내 패널입니다. 위치·크기는 이 패널의 RectTransform에서 조절합니다.")]
        public CanvasGroup panel;
        [Tooltip("안내 상단의 짧은 분류 문구입니다.")]
        public TMP_Text heading;
        [Tooltip("탐사 안내 본문입니다. 전투는 기존 단계별 텍스트를 사용하므로 비워 둡니다.")]
        public TMP_Text message;
        [Tooltip("클릭할 UI를 감싸는 장식 테두리입니다. 입력을 차단하지 않습니다.")]
        public JcTutorialGraphic focusFrame;
        [Tooltip("클릭할 UI 위에 표시하는 장식 포인터입니다. 입력을 차단하지 않습니다.")]
        public JcTutorialGraphic pointer;
        [Tooltip("수직 또는 수평의 큰 분절 화살표로 가리킬 대상입니다. 탐사 턴 종료 버튼을 연결합니다.")]
        public RectTransform centeredPointerTarget;
        [Tooltip("레거시 분절 화살표입니다. 현재 튜토리얼에서는 사용하지 않습니다.")]
        public bool useLegacySegmentedArrow;
        [Min(20), Tooltip("대상 위아래 또는 좌우에 배치되는 분절 화살표의 최대 너비(Canvas 단위)입니다.")]
        public float centeredPointerWidth=150;
        [Min(6), Tooltip("큰 분절 화살표로 안내하는 대상의 강조 테두리 두께입니다.")]
        public float centeredBorderWidth=12;
        [Tooltip("안내 대상 버튼으로 수렴하는 두 겹의 글로우입니다. 2초 표시 후 1초 쉬며 반복합니다.")]
        public JcTutorialGraphic[] convergingOutlines;
        private Vector2 compactPointerSize;
        private RectTransform animatedPointerTarget;
        private float pointerElapsed, convergenceElapsed;
        private RectTransform convergenceTarget;
        private bool suppressPanel;
        public void SetPanelSuppressed(bool value) { suppressPanel=value;if(value&&panel!=null){panel.alpha=0;panel.blocksRaycasts=false;} }
        public const float ConvergenceExpansion=36;

        private sealed class WordWrap : ITextPreprocessor
        {
            public string PreprocessText(string text) => System.Text.RegularExpressions.Regex.Replace(text,@"(<[^>]+>)|([^\s<>]+)",m=>m.Groups[1].Success?m.Value:"<nobr>"+m.Value+"</nobr>");
        }
        private static readonly WordWrap wordWrap=new WordWrap();
        public static void BindWordWrapping(TMP_Text text) { if(text!=null) {text.textPreprocessor=wordWrap;text.enableWordWrapping=true;} }

        [Header("표현")]
        [Min(0), Tooltip("안내가 나타나고 사라지는 시간(초)입니다. 0이면 즉시 전환하며 게임 시간 배율과 무관합니다.")]
        public float fadeDuration = .2f;
        [Min(.1f), Tooltip("테두리 밝기가 한 번 왕복하는 시간(초)입니다. 게임 시간 배율과 무관합니다.")]
        public float pulsePeriod = 1.6f;
        [Tooltip("클릭 대상 강조색입니다. 실제 적용값은 씬에 저장됩니다.")]
        public Color accent = new Color(25f / 255f, 1f, 84f / 255f, 1);
        [Min(0), Tooltip("강조 테두리와 대상 UI 사이의 여유입니다. Canvas 기준 단위입니다.")]
        public float focusPadding = 9;
        [Min(0), Tooltip("포인터가 대상 UI 위에서 떨어지는 거리입니다. Canvas 기준 단위입니다.")]
        public float pointerGap = 12;
        [Min(0), Tooltip("포인터 상하 움직임의 크기입니다. Canvas 기준 단위이며 0이면 움직이지 않습니다.")]
        public float pointerTravel = 5;

        [Tooltip("안내 패널이 마우스 입력을 받는지 정합니다. 탐사의 조작 안내 중에는 끄고 확인 설명창에서만 켭니다.")]
        public bool blockPanelRaycasts = true;

        public enum FocusPresentation { PointerAbove, ResourceBlock, Spotlight, BorderOnly, DimmedExplanation }
        [System.Serializable]
        public sealed class ResourceRegion
        {
            public RectTransform target, background;
            [Tooltip("통합 자원 배경 그림의 구획. 좌하단 원점의 0~1 범위입니다.")]
            public Rect normalizedBounds;
        }
        public ResourceRegion[] resourceRegions = System.Array.Empty<ResourceRegion>();
        [Tooltip("씬에 배치한 안내 디밍입니다. 대상과 메시지 영역은 밝게 유지합니다.")]
        public JcTutorialSpotlight spotlight;
        private FocusPresentation focusPresentation;
        private bool visible;
        [System.Serializable]
        public sealed class ContentEdit
        {
            public string sourceTitle, sourceBody, title, body;
        }
        [System.Serializable]
        private sealed class ContentEdits { public System.Collections.Generic.List<ContentEdit> edits; }
        [SerializeField, Tooltip("플레이 UI 편집 도구가 저장한 안내 문구입니다. 진행 조건과 자원 수치는 바꾸지 않습니다.")]
        private System.Collections.Generic.List<ContentEdit> contentEdits = new System.Collections.Generic.List<ContentEdit>();
        private string sourceTitle, sourceBody;
        public bool IsContentText(TMP_Text text) => text != null && (text == heading || text == message);
        public bool HasEditedCurrentContent => contentEdits.Exists(e => e.sourceTitle == sourceTitle && e.sourceBody == sourceBody);
        public string CaptureCurrentContentTexts()
        {
            string title = heading != null ? heading.text : null, body = message != null ? message.text : null;
            if (heading != null) CaptureContentEdit(heading, title);
            return message != null ? CaptureContentEdit(message, body) : JsonUtility.ToJson(new ContentEdits { edits = contentEdits });
        }
        public string CaptureContentEdit(TMP_Text text, string value)
        {
            var edit = contentEdits.Find(e => e.sourceTitle == sourceTitle && e.sourceBody == sourceBody);
            if (edit == null) { edit = new ContentEdit { sourceTitle = sourceTitle, sourceBody = sourceBody, title = heading != null ? heading.text : sourceTitle, body = message != null ? message.text : sourceBody }; contentEdits.Add(edit); }
            if (text == heading) edit.title = value; else edit.body = value;
            SetContent(sourceTitle, sourceBody);
            return JsonUtility.ToJson(new ContentEdits { edits = contentEdits });
        }
        public void SetContentEdits(string json) => contentEdits = JsonUtility.FromJson<ContentEdits>(json).edits ?? new System.Collections.Generic.List<ContentEdit>();
        private RectTransform focus;
        private readonly Vector3[] corners = new Vector3[4];
        public bool IsRequestedVisible => visible;

        private void Awake() { BindWordWrapping(message); if (panel != null) panel.alpha = 0; SetVisible(false); }
        public void SetContent(string title, string body)
        {
            sourceTitle = title; sourceBody = body;
            var edit = contentEdits.Find(e => e.sourceTitle == title && e.sourceBody == body);
            if (edit != null) { title = edit.title; body = edit.body; }
            BindWordWrapping(message); if (heading != null && heading.text != title) heading.text = title; if (message != null && message.text != body) message.text = body;
        }
        public void SetVisible(bool value, RectTransform target = null, FocusPresentation presentation = FocusPresentation.PointerAbove)
        {
            visible = value; focus = value ? target : null; focusPresentation = presentation;
            if (spotlight != null && (!value || (presentation != FocusPresentation.Spotlight && presentation != FocusPresentation.DimmedExplanation))) spotlight.gameObject.SetActive(false);
        }
        [Min(0), Tooltip("탐사 본문이 있는 메시지 박스의 최소 높이(Canvas 단위)입니다. 긴 본문은 필요한 만큼 더 늘어납니다.")]
        public float minimumMessageHeight = 340;
        [Min(0), Tooltip("본문 아래에 확보할 확인 버튼과 여백의 높이(Canvas 단위)입니다.")]
        public float messageBottomSpace = 86;
        public void FitMessageLayout()
        {
            if (panel == null || message == null) return;
            var card = panel.transform as RectTransform;
            var text = message.rectTransform;
            if (card == null || text.parent != card) return;
            float width = Mathf.Max(1, card.rect.width - 64);
            float height = Mathf.Max(120, message.GetPreferredValues(message.text, width, Mathf.Infinity).y + 8);
            const float top = 73;
            card.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(minimumMessageHeight, top + height + messageBottomSpace));
            text.anchorMin = text.anchorMax = new Vector2(.5f, 1);
            text.pivot = new Vector2(.5f, 1);
            text.anchoredPosition = new Vector2(0, -top);
            text.sizeDelta = new Vector2(width, height);
        }
        private void LateUpdate() { if (visible) FitMessageLayout(); RenderPresentation(Time.unscaledTime, Time.unscaledDeltaTime); }

        // 비플레이 정적 렌더 검증에도 사용한다. 입력·게임 상태를 변경하지 않는다.
        public void RenderPresentation(float time, float delta)
        {
            if (panel != null)
            {
                panel.alpha = Mathf.MoveTowards(panel.alpha, visible && !suppressPanel ? 1 : 0, fadeDuration <= 0 ? 1 : Mathf.Max(0, delta) / fadeDuration);
                panel.blocksRaycasts = visible && !suppressPanel && blockPanelRaycasts;
            }
            bool showFocus = visible && focus != null && focus.gameObject.activeInHierarchy;
            bool dimmed = visible && (focusPresentation == FocusPresentation.DimmedExplanation ||
                (showFocus && focusPresentation == FocusPresentation.Spotlight));
            if (spotlight != null)
            {
                spotlight.SetTargets(!suppressPanel && panel != null ? panel.transform as RectTransform : null, focus);
                spotlight.gameObject.SetActive(dimmed);
            }
            if (focusFrame == null || pointer == null) return;
            focusFrame.gameObject.SetActive(showFocus); pointer.gameObject.SetActive(showFocus);
            if(convergingOutlines!=null)foreach(var outline in convergingOutlines)if(outline!=null)outline.gameObject.SetActive(showFocus);
            if (!showFocus) return;
            var parent = focusFrame.rectTransform.parent as RectTransform;
            if (parent == null) return;
            var targetCanvas = focus.GetComponentInParent<Canvas>();
            var overlayCanvas = parent.GetComponentInParent<Canvas>();
            Camera targetCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
            Camera overlayCamera = overlayCanvas != null && overlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? overlayCanvas.worldCamera : null;
            focus.GetWorldCorners(corners);
            if (focusPresentation == FocusPresentation.ResourceBlock && resourceRegions != null)
                foreach (var region in resourceRegions)
                    if (region != null && region.target == focus && region.background != null)
                    {
                        var background = region.background;
                        var area = region.normalizedBounds;
                        var bounds = background.rect;
                        var lo = bounds.min + Vector2.Scale(bounds.size, area.min);
                        var hi = bounds.min + Vector2.Scale(bounds.size, area.max);
                        corners[0] = background.TransformPoint(new Vector3(lo.x, lo.y));
                        corners[1] = background.TransformPoint(new Vector3(lo.x, hi.y));
                        corners[2] = background.TransformPoint(new Vector3(hi.x, hi.y));
                        corners[3] = background.TransformPoint(new Vector3(hi.x, lo.y));
                        var sourceCanvas = background.GetComponentInParent<Canvas>();
                        targetCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? sourceCanvas.worldCamera : null;
                        break;
                    }
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, RectTransformUtility.WorldToScreenPoint(targetCamera, corners[i]), overlayCamera, out local);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            float padding = focusPresentation == FocusPresentation.ResourceBlock ? 0 : focusPadding;
            min = Vector2.Max(min - Vector2.one * padding, parent.rect.min + Vector2.one * 2);
            max = Vector2.Min(max + Vector2.one * padding, parent.rect.max - Vector2.one * 2);
            if (max.x <= min.x || max.y <= min.y) { focusFrame.gameObject.SetActive(false); pointer.gameObject.SetActive(false); return; }
            float pulse = .5f + .5f * Mathf.Sin(time * Mathf.PI * 2 / Mathf.Max(.1f, pulsePeriod));
            var rect = focusFrame.rectTransform;
            rect.localPosition = (min + max) * .5f;
            rect.sizeDelta = max - min;
            focusFrame.color = new Color(accent.r, accent.g, accent.b, accent.a * Mathf.Lerp(.45f, .9f, pulse));
            focusFrame.SetBorderWidth(focus == centeredPointerTarget ? centeredBorderWidth : 6);
            if(convergenceTarget!=focus){convergenceTarget=focus;convergenceElapsed=0;}
            convergenceElapsed+=Mathf.Max(0,delta);
            if(convergingOutlines!=null)for(int i=0;i<convergingOutlines.Length;i++) {
                    var o=convergingOutlines[i];if(o==null)continue;
                    float cycle=Mathf.Repeat(convergenceElapsed,3);
                    o.gameObject.SetActive(cycle<2);if(cycle>=2)continue;
                    float progress=Mathf.Repeat(cycle+i/(float)convergingOutlines.Length,1);
                    float extra=ConvergenceExpansion*(1-Mathf.SmoothStep(0,1,progress));
                    Vector2 lo=Vector2.Max(min-Vector2.one*extra,parent.rect.min+Vector2.one*(o.GlowWidth+1));
                    Vector2 hi=Vector2.Min(max+Vector2.one*extra,parent.rect.max-Vector2.one*(o.GlowWidth+1));
                    o.rectTransform.localPosition=(lo+hi)*.5f;o.rectTransform.sizeDelta=hi-lo;
                    o.color=new Color(accent.r,accent.g,accent.b,Mathf.Sin(progress*Mathf.PI)*.65f);o.AnimateSweep(time);
                }
            if (focusPresentation == FocusPresentation.Spotlight || focusPresentation == FocusPresentation.BorderOnly) { pointer.gameObject.SetActive(false); return; }
            if(compactPointerSize==Vector2.zero)compactPointerSize=pointer.rectTransform.sizeDelta;
            pointer.color = accent;
            if(useLegacySegmentedArrow && focus == centeredPointerTarget) {
                pointer.AimFromCenter(min,max,centeredPointerWidth,ConvergenceExpansion+8);
                if(animatedPointerTarget!=focus) { animatedPointerTarget=focus;pointerElapsed=0; }
                pointerElapsed+=Mathf.Max(0,delta);
                pointer.color=accent;pointer.AnimateSweep(pointerElapsed);
                pointer.gameObject.SetActive(pointerElapsed < JcTutorialGraphic.ArrowSweepPeriod*JcTutorialGraphic.ArrowSweepCount);
                return;
            }
            pointer.SetShape(JcTutorialGraphic.Shape.Pointer);
            pointer.rectTransform.localRotation = focusPresentation == FocusPresentation.ResourceBlock ? Quaternion.Euler(0, 0, 180) : Quaternion.identity;
            pointer.rectTransform.sizeDelta=compactPointerSize;
            float halfPointer = pointer.rectTransform.rect.height * .5f;
            pointer.rectTransform.localPosition = new Vector3((min.x + max.x) * .5f,
                focusPresentation == FocusPresentation.ResourceBlock
                    ? Mathf.Max(parent.rect.yMin + halfPointer + 2, min.y - pointerGap - pointerTravel * pulse - halfPointer)
                    : Mathf.Min(parent.rect.yMax - halfPointer - 2, max.y + pointerGap + pointerTravel * pulse + halfPointer), 0);
        }

        private void OnDisable()
        {
            if (spotlight != null) spotlight.gameObject.SetActive(false);
            if (focusFrame != null) focusFrame.gameObject.SetActive(false);
            if (pointer != null) pointer.gameObject.SetActive(false);
            if(convergingOutlines!=null)foreach(var o in convergingOutlines)if(o!=null)o.gameObject.SetActive(false);
            // 표시 제어를 해제해도 기존 진행 버튼이 투명하게 남지 않도록 복구한다.
            if (panel != null) { panel.alpha = 1; panel.blocksRaycasts = true; }
        }
    }
}
