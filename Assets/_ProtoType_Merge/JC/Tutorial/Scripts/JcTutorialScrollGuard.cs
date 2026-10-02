using UnityEngine;
using UnityEngine.UI;

namespace JC.Tutorial
{
    // 미니맵 내부 클릭은 통과시키고, 버튼 영역은 자식 버튼 뒤에서 빈틈과 바깥 24 Canvas 단위를 보호한다.
    [RequireComponent(typeof(Image), typeof(CameraEdgeScrollBlocker))]
    public sealed class JcTutorialScrollGuard : MonoBehaviour, ICanvasRaycastFilter
    {
        public RectTransform content;
        private bool passThroughContent;
        public bool IsRaycastLocationValid(Vector2 point, Camera camera) => content != null && (!passThroughContent || !RectTransformUtility.RectangleContainsScreenPoint(content, point, camera));
        public static void Install(RectTransform target)
        {
            if (target == null || target.GetComponentInChildren<JcTutorialScrollGuard>(true) != null) return;
            if (target.GetComponent<CameraEdgeScrollBlocker>() == null) target.gameObject.AddComponent<CameraEdgeScrollBlocker>();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(target);
            var go = new GameObject("Tutorial Edge Scroll Guard", typeof(RectTransform), typeof(JcTutorialScrollGuard));
            go.layer = target.gameObject.layer; go.transform.SetParent(target, false); go.transform.SetAsFirstSibling();
            var guard = go.GetComponent<JcTutorialScrollGuard>(); guard.content = target; guard.passThroughContent = target.GetComponent<RawImage>() != null;
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * -24; rect.offsetMax = Vector2.one * 24;
            if (target.rect.width < 1 || target.rect.height < 1)
            {
                rect.anchorMin = rect.anchorMax = target.pivot;
                rect.sizeDelta = (Vector2)bounds.size + Vector2.one * 48;
                rect.localPosition = bounds.center;
            }
            var image = go.GetComponent<Image>(); image.color = Color.clear; image.raycastTarget = true; image.canvasRenderer.cullTransparentMesh = false;
        }
    }
}
