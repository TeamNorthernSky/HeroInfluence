using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JC.Tutorial
{
    // JC | 2026-11-13 | 기존 자동/배속 Button의 기능과 눌림은 끄고, 사용 불가 안내용 포인터 입력만 받습니다.
    [DisallowMultipleComponent]
    public sealed class JcTutorialBattleControlLock : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        static readonly HashSet<JcTutorialBattleControlLock> inputs = new HashSet<JcTutorialBattleControlLock>();
        Button button;
        ToggleButton toggle;
        BattleSceneManager manager;
        BattleFlowManager flow;
        bool locked, originalInteractable;
        Selectable.Transition originalTransition;
        int pressFrame = -1;

        public static T FindInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }
        public static bool IsRestricted(Scene scene)
        {
            var manager = FindInScene<BattleSceneManager>(scene);
            return manager != null && manager.LocksTutorialBattleControls;
        }
        public static void Attach(GameObject owner)
        {
            if (!owner.GetComponent<JcTutorialBattleControlLock>()) owner.AddComponent<JcTutorialBattleControlLock>();
        }
        void OnEnable()
        {
            button = GetComponent<Button>(); toggle = GetComponent<ToggleButton>();
            manager = FindInScene<BattleSceneManager>(gameObject.scene);
            flow = FindInScene<BattleFlowManager>(gameObject.scene);
            inputs.Add(this);
            ApplyLock();
        }
        void LateUpdate() => ApplyLock();
        void ApplyLock()
        {
            if (!manager) manager = FindInScene<BattleSceneManager>(gameObject.scene);
            if (!flow) flow = FindInScene<BattleFlowManager>(gameObject.scene);
            bool restrict = manager && manager.LocksTutorialBattleControls;
            if (!button) return;
            if (restrict)
            {
                if (!locked)
                {
                    originalInteractable = button.interactable; originalTransition = button.transition; locked = true;
                    // 이전 전투의 공용 설정을 튜토리얼에 들고 들어오지 않습니다. 일반 전투의 설정 경로는 그대로 사용합니다.
                    var auto = FindInScene<AutoBattleController>(gameObject.scene);
                    if (auto) auto.IsAutoBattle = false;
                    var battle = FindInScene<BattleManager>(gameObject.scene);
                    if (battle) battle.ChangeBattleSpeed(BattleRuntimeSettings.NormalSpeed);
                }
                button.transition = Selectable.Transition.None;
                button.interactable = false;
                if (toggle) toggle.SetState(false, false);
            }
            else Restore();
        }
        void Restore()
        {
            if (!locked || !button) return;
            button.transition = originalTransition;
            // JC | 2026-11-13 | 결과창이 잠근 버튼을 컴포넌트 해제로 다시 켜지 않습니다.
            if (!flow || (!flow.IsEndingBattle && !flow.IsBattleEnded)) button.interactable = originalInteractable;
            locked = false;
        }
        bool CanShow => isActiveAndEnabled && button && button.gameObject.activeInHierarchy &&
            manager && manager.LocksTutorialBattleControls && (!flow || (!flow.IsEndingBattle && !flow.IsBattleEnded));
        public bool RejectClick()
        {
            if (!manager) manager = FindInScene<BattleSceneManager>(gameObject.scene);
            if (!manager || !manager.LocksTutorialBattleControls) return false;
            if (CanShow) JcTutorialBattleControlNotice.Show((RectTransform)transform);
            return true;
        }
        public void OnPointerDown(PointerEventData data)
        {
            pressFrame = data.button == PointerEventData.InputButton.Left && CanShow ? Time.frameCount : -1;
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && pressFrame >= 0) RejectClick();
            pressFrame = -1;
        }
        // JC | 2026-11-13 | 안내의 전체 화면 클릭/딤 차단막도 이 영역을 안내 전용 클릭으로 구분합니다.
        public static bool ContainsPointer(Scene scene, Vector2 point)
        {
            foreach (var input in inputs)
            {
                if (!input || input.gameObject.scene != scene || !input.CanShow) continue;
                var canvas = input.GetComponentInParent<Canvas>();
                var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)input.transform, point, camera)) return true;
            }
            return false;
        }
        void OnDisable() { inputs.Remove(this); pressFrame = -1; Restore(); }
    }
}
