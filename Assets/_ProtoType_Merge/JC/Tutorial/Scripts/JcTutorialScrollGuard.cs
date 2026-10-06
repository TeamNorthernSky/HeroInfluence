using UnityEngine;
using UnityEngine.UI;

namespace JC.Tutorial
{
    // 미니맵 보호와 다음 턴 버튼에서 이어지는 외부 스크롤 구간을 구분한다.
    [RequireComponent(typeof(Image), typeof(CameraEdgeScrollBlocker))]
    public sealed class JcTutorialScrollGuard : MonoBehaviour, ICanvasRaycastFilter
    {
        public RectTransform content;
        private bool outsideButtonBands;
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
                if (guard.outsideButtonBands)
                {
                    var rect = guard.content.rect;
                    // 그림의 우측 띠는 버튼 높이, 아래 띠는 버튼 너비를 기준으로 한다.
                    // 두 축을 모서리로 투영하지 않아 우하단 바깥 모서리까지 차단이 퍼지지 않는다.
                    var right = ScreenBounds(guard.content, Rect.MinMaxRect(rect.xMin, rect.yMin - 40, rect.xMax, rect.yMax + 8), camera);
                    var bottom = ScreenBounds(guard.content, Rect.MinMaxRect(rect.xMin + 16, rect.yMin, rect.xMax, rect.yMax), camera);
                    if (ContainsOutsideButtonBands(point, screenSize, right, bottom)) return true;
                    continue;
                }
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)guard.transform, edgePoint, camera)) return true;
            }
            return false;
        }
        public static bool ContainsOutsideButtonBands(Vector2 point, Vector2 screenSize, Rect right, Rect bottom)
            => screenSize.x > 0 && screenSize.y > 0 &&
               ((point.x >= screenSize.x && point.y >= 0 && point.y < screenSize.y && point.y >= right.yMin && point.y <= right.yMax) ||
                (point.y < 0 && point.x >= 0 && point.x < screenSize.x && point.x >= bottom.xMin && point.x <= bottom.xMax));

        private static Rect ScreenBounds(RectTransform target, Rect local, Camera camera)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3(i >= 2 ? local.xMax : local.xMin, i == 1 || i == 2 ? local.yMax : local.yMin, 0);
                var point = RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(corner));
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static void Install(RectTransform target, bool buttonBands = false)
        {
            if (target == null || target.GetComponentInChildren<JcTutorialScrollGuard>(true) != null) return;
            if (target.GetComponent<CameraEdgeScrollBlocker>() == null) target.gameObject.AddComponent<CameraEdgeScrollBlocker>();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(target);
            var go = new GameObject("Tutorial Edge Scroll Guard", typeof(RectTransform), typeof(JcTutorialScrollGuard));
            go.layer = target.gameObject.layer; go.transform.SetParent(target, false); go.transform.SetAsFirstSibling();
            var guard = go.GetComponent<JcTutorialScrollGuard>(); guard.content = target; guard.passThroughContent = target.GetComponent<RawImage>() != null;
            guard.outsideButtonBands = buttonBands;
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            float padding = buttonBands ? 0 : 24;
            rect.offsetMin = Vector2.one * -padding; rect.offsetMax = Vector2.one * padding;
            if (target.rect.width < 1 || target.rect.height < 1)
            {
                rect.anchorMin = rect.anchorMax = target.pivot;
                rect.sizeDelta = (Vector2)bounds.size + Vector2.one * (padding * 2);
                rect.localPosition = bounds.center;
            }
            var image = go.GetComponent<Image>(); image.color = Color.clear; image.raycastTarget = true; image.canvasRenderer.cullTransparentMesh = false;
        }
    }
}
