using UnityEngine;
using UnityEngine.UI;

namespace JC.Tutorial
{
    // 입력은 기존 전면 차단막이 담당하고, 이 Graphic은 두 안내 영역을 제외한 디밍만 그린다.
    [RequireComponent(typeof(CanvasRenderer)), AddComponentMenu("JC Tutorial/안내 영역 디밍")]
    public sealed class JcTutorialSpotlight : MaskableGraphic
    {
        private RectTransform first, second;
        private readonly Vector3[] corners = new Vector3[4];
        public void SetTargets(RectTransform message, RectTransform window = null)
        { first = message; second = window; raycastTarget = false; SetVerticesDirty(); }
        private void LateUpdate() => SetVerticesDirty();
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
            Rect r = rectTransform.rect, a = Bounds(first), b = Bounds(second);
            float[] xs = { r.xMin, r.xMax, Mathf.Clamp(a.xMin,r.xMin,r.xMax), Mathf.Clamp(a.xMax,r.xMin,r.xMax), Mathf.Clamp(b.xMin,r.xMin,r.xMax), Mathf.Clamp(b.xMax,r.xMin,r.xMax) };
            float[] ys = { r.yMin, r.yMax, Mathf.Clamp(a.yMin,r.yMin,r.yMax), Mathf.Clamp(a.yMax,r.yMin,r.yMax), Mathf.Clamp(b.yMin,r.yMin,r.yMax), Mathf.Clamp(b.yMax,r.yMin,r.yMax) };
            System.Array.Sort(xs); System.Array.Sort(ys);
            for(int x=0;x<xs.Length-1;x++) for(int y=0;y<ys.Length-1;y++)
            {
                if(xs[x+1]-xs[x]<.01f || ys[y+1]-ys[y]<.01f) continue;
                var mid = new Vector2((xs[x]+xs[x+1])*.5f,(ys[y]+ys[y+1])*.5f);
                if((first != null && a.Contains(mid)) || (second != null && b.Contains(mid))) continue;
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
