using UnityEngine;
using UnityEngine.UI;

namespace JC.Tutorial
{
    // 입력은 기존 전면 차단막이 담당하고, 이 Graphic은 안내 대상만 제외한 디밍을 그린다. 메시지는 별도 Canvas로 그 위에 표시한다.
    [RequireComponent(typeof(CanvasRenderer)), AddComponentMenu("JC Tutorial/안내 영역 디밍")]
    public sealed class JcTutorialSpotlight : MaskableGraphic
    {
        private RectTransform first, second;
        private Canvas messageCanvas;
        private bool originalOverride;
        private int originalLayer, originalOrder;
        private bool messageRaised;

        private void RestoreMessageOrder()
        {
            if (messageRaised && messageCanvas != null)
            {
                messageCanvas.overrideSorting = originalOverride;
                messageCanvas.sortingLayerID = originalLayer;
                messageCanvas.sortingOrder = originalOrder;
            }
            messageRaised = false;
            messageCanvas = null;
        }

        private void RaiseMessage()
        {
            if (first == null || canvas == null) return;
            if (!messageRaised)
            {
                messageCanvas = first.GetComponent<Canvas>();
                if (messageCanvas == null) messageCanvas = first.gameObject.AddComponent<Canvas>();
                originalOverride = messageCanvas.overrideSorting;
                originalLayer = messageCanvas.sortingLayerID;
                originalOrder = messageCanvas.sortingOrder;
                // 중첩 Canvas의 Graphic은 부모 Raycaster에 등록되지 않는다.
                // 기존 버튼/CanvasGroup의 입력 규칙은 그대로 사용한다.
                if (first.GetComponent<GraphicRaycaster>() == null)
                {
                    var source = first.parent != null ? first.parent.GetComponentInParent<GraphicRaycaster>() : null;
                    var raycaster = first.gameObject.AddComponent<GraphicRaycaster>();
                    if (source != null)
                    {
                        raycaster.ignoreReversedGraphics = source.ignoreReversedGraphics;
                        raycaster.blockingObjects = source.blockingObjects;
                        raycaster.blockingMask = source.blockingMask;
                    }
                }
                messageRaised = true;
            }
            var dimCanvas = canvas;
            while (dimCanvas != null && !dimCanvas.overrideSorting && !dimCanvas.isRootCanvas)
                dimCanvas = dimCanvas.transform.parent.GetComponentInParent<Canvas>();
            if (dimCanvas == null) return;
            messageCanvas.overrideSorting = true;
            messageCanvas.sortingLayerID = dimCanvas.sortingLayerID;
            messageCanvas.sortingOrder = Mathf.Max(originalOrder, dimCanvas.sortingOrder + 1);
        }

        protected override void OnEnable() { base.OnEnable(); RaiseMessage(); }
        protected override void OnDisable() { RestoreMessageOrder(); base.OnDisable(); }
        private RectTransform[] extra = System.Array.Empty<RectTransform>();
        public void SetExtraTargets(RectTransform[] targets) { extra = targets ?? System.Array.Empty<RectTransform>(); SetVerticesDirty(); }
        private readonly Vector3[] corners = new Vector3[4];
        public void SetTargets(RectTransform message, RectTransform window = null)
        {
            if (first != message) RestoreMessageOrder();
            first = message; second = window; raycastTarget = false;
            if (isActiveAndEnabled) RaiseMessage();
            SetVerticesDirty();
        }
        private void LateUpdate() { RaiseMessage(); SetVerticesDirty(); }
        private Rect Bounds(RectTransform target)
        {
            if (target == null) return new Rect();
            var source = target.GetComponentInParent<Canvas>();
            var dest = GetComponentInParent<Canvas>();
            Camera a = source != null && source.renderMode != RenderMode.ScreenSpaceOverlay ? source.worldCamera : null;
            Camera b = dest != null && dest.renderMode != RenderMode.ScreenSpaceOverlay ? dest.worldCamera : null;
            target.GetWorldCorners(corners);
            Vector2 lo = Vector2.positiveInfinity, hi = Vector2.negativeInfinity;
            foreach (var corner in corners)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                    RectTransformUtility.WorldToScreenPoint(a, corner), b, out var point);
                lo = Vector2.Min(lo, point); hi = Vector2.Max(hi, point);
            }
            return Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            var holes = new System.Collections.Generic.List<Rect>();
            if (second != null) holes.Add(Bounds(second));
            foreach (var target in extra) if (target != null && target.gameObject.activeInHierarchy) holes.Add(Bounds(target));
            var xList = new System.Collections.Generic.List<float> { r.xMin, r.xMax };
            var yList = new System.Collections.Generic.List<float> { r.yMin, r.yMax };
            foreach (var hole in holes) { xList.Add(Mathf.Clamp(hole.xMin,r.xMin,r.xMax)); xList.Add(Mathf.Clamp(hole.xMax,r.xMin,r.xMax)); yList.Add(Mathf.Clamp(hole.yMin,r.yMin,r.yMax)); yList.Add(Mathf.Clamp(hole.yMax,r.yMin,r.yMax)); }
            var xs = xList.ToArray(); var ys = yList.ToArray();
            System.Array.Sort(xs); System.Array.Sort(ys);
            for(int x=0;x<xs.Length-1;x++) for(int y=0;y<ys.Length-1;y++)
            {
                if(xs[x+1]-xs[x]<.01f || ys[y+1]-ys[y]<.01f) continue;
                var mid = new Vector2((xs[x]+xs[x+1])*.5f,(ys[y]+ys[y+1])*.5f);
                if (holes.Exists(h => h.Contains(mid))) continue;
                int i=vh.currentVertCount;
                vh.AddVert(new Vector3(xs[x],ys[y]), color, Vector2.zero);
                vh.AddVert(new Vector3(xs[x],ys[y+1]), color, Vector2.zero);
                vh.AddVert(new Vector3(xs[x+1],ys[y+1]), color, Vector2.zero);
                vh.AddVert(new Vector3(xs[x+1],ys[y]), color, Vector2.zero);
                vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
            }
        }
    }
}
