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
            // JC | 2026-11-13 | 전투 자동/배속의 사용 불가 안내만 딤 아래로 통과시키며 안내 닫기 입력으로 취급하지 않습니다.
            if (battleGuide != null && JcTutorialBattleControlLock.ContainsPointer(gameObject.scene, screenPoint)) return false;
            if (battleGuide != null && battleGuide.GuidedActive)
                return !battleGuide.IsAllowedPointer(screenPoint);
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
            // JC | 2026-11-13 | 안내 도중 눌림/해제 위치가 바뀌어도 제한 버튼 클릭으로 메시지를 넘기지 않습니다.
            if (battleGuide != null && JcTutorialBattleControlLock.ContainsPointer(gameObject.scene, data.position)) { pressFrame = -1; return; }
            if (data.button == PointerEventData.InputButton.Left && guide != null && pressFrame > guide.PresentationOpenedFrame)
                guide.ContinueExplanation();
            if(data.button==PointerEventData.InputButton.Left&&battleGuide!=null&&pressFrame>battleGuide.ResultOpenedFrame)battleGuide.DismissResult();
            pressFrame = -1;
        }
        private void OnDisable() { pressFrame = -1; }
    }
}
