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
        public static bool BlocksOutsideScreen(Vector2 point, Vector2 screenSize)
        {
            if (screenSize.x <= 0 || screenSize.y <= 0) return false;
            // 포인터가 지나간 화면 경계의 UI를 검사한다. 내부 클릭 통과 여부와 스크롤 차단은 분리한다.
            var edgePoint = new Vector2(Mathf.Clamp(point.x, 0, screenSize.x - .01f),
                Mathf.Clamp(point.y, 0, screenSize.y - .01f));
            foreach (var guard in FindObjectsByType<JcTutorialScrollGuard>(FindObjectsSortMode.None))
            {
                if (!guard.isActiveAndEnabled || guard.gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) continue;
                var canvas = guard.GetComponentInParent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)guard.transform, edgePoint, camera)) return true;
            }
            return false;
        }
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
