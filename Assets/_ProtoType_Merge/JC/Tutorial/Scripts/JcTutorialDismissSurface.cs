using UnityEngine;
using UnityEngine.EventSystems;

namespace JC.Tutorial
{
    [DisallowMultipleComponent, AddComponentMenu("JC Tutorial/안내 닫기 입력")]
    public sealed class JcTutorialDismissSurface : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, ICanvasRaycastFilter
    {
        [Tooltip("이 표면을 새로 클릭했을 때 닫을 탐사 안내입니다. 창을 열었던 클릭은 넘기지 않습니다.")]
        public JcTutorialExploreGuide guide;
        [Tooltip("화면 클릭으로 닫을 전투 결과 안내입니다. 탐사 안내와 동시에 연결하지 않습니다.")]
        public JcTutorialBattleGuide battleGuide;
        private int pressFrame = -1;
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            var target = guide != null ? guide.AllowedInputTarget : null;
            if (target == null || !target.gameObject.activeInHierarchy) return true;
            var canvas = target.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return !RectTransformUtility.RectangleContainsScreenPoint(target, screenPoint, camera);
        }
        public void OnPointerDown(PointerEventData data)
        {
            pressFrame = data.button == PointerEventData.InputButton.Left ? Time.frameCount : -1;
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && guide != null && pressFrame > guide.PresentationOpenedFrame)
                guide.ContinueExplanation();
            if(data.button==PointerEventData.InputButton.Left&&battleGuide!=null&&pressFrame>battleGuide.ResultOpenedFrame)battleGuide.DismissResult();
            pressFrame = -1;
        }
        private void OnDisable() { pressFrame = -1; }
    }
}
