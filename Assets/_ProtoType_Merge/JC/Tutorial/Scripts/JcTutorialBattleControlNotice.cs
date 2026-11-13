using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.Tutorial
{
    // JC | 2026-11-13 | 자동/배속이 공유하는 안내 한 개만 표시합니다. 입력을 가로채지 않고 실제 시간으로 수명을 관리합니다.
    public sealed class JcTutorialBattleControlNotice : MonoBehaviour
    {
        static readonly Dictionary<int, JcTutorialBattleControlNotice> notices = new Dictionary<int, JcTutorialBattleControlNotice>();
        RectTransform target, rect, parentRect;
        CanvasGroup group;
        Canvas parentCanvas;
        float hold, fadeElapsed, fadeDuration, fadeFrom;
        bool showing;
        int sceneHandle;
        readonly Vector3[] corners = new Vector3[4];

        public static void Show(RectTransform button)
        {
            int key = button.gameObject.scene.handle;
            if (!notices.TryGetValue(key, out var notice) || !notice)
            {
                var canvas = button.GetComponentInParent<Canvas>();
                if (!canvas) return;
                var root = canvas.rootCanvas;
                var go = new GameObject("JC_TutorialControlNotice", typeof(RectTransform), typeof(CanvasGroup));
                go.layer = button.gameObject.layer; go.transform.SetParent(root.transform, false);
                notice = go.AddComponent<JcTutorialBattleControlNotice>();
                notice.sceneHandle = key; notice.parentCanvas = root; notice.parentRect = (RectTransform)root.transform;
                notice.rect = (RectTransform)go.transform; notice.rect.anchorMin = notice.rect.anchorMax = notice.rect.pivot = Vector2.one * .5f;
                notice.group = go.GetComponent<CanvasGroup>(); notice.group.blocksRaycasts = false; notice.group.interactable = false;
                var overlay = go.AddComponent<Canvas>(); overlay.overrideSorting = true;
                overlay.sortingLayerID = root.sortingLayerID;
                int order = root.sortingOrder;
                foreach (var sceneRoot in button.gameObject.scene.GetRootGameObjects())
                    foreach (var candidate in sceneRoot.GetComponentsInChildren<Canvas>(true))
                        if (candidate != overlay && candidate.sortingLayerID == root.sortingLayerID) order = Mathf.Max(order, candidate.sortingOrder);
                overlay.sortingOrder = order + 2;
                var bg = go.AddComponent<JcTutorialGraphic>();
                bg.Configure(JcTutorialGraphic.Shape.Panel, new Color(.035f, .09f, .12f, 1), 8, 2.5f); bg.SetOpaqueFill(true);
                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.layer = go.layer; labelGo.transform.SetParent(go.transform, false);
                var label = labelGo.GetComponent<TextMeshProUGUI>();
                var guide = JcTutorialBattleControlLock.FindInScene<JcTutorialBattleGuide>(button.gameObject.scene);
                var source = guide && guide.view && guide.view.heading ? guide.view.heading : button.GetComponentInChildren<TMP_Text>(true);
                if (source) label.font = source.font;
                label.fontSize = 22; label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
                label.text = "튜토리얼 중 사용 불가"; label.enableWordWrapping = false; label.raycastTarget = false;
                var tr = label.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(18, 12); tr.offsetMax = new Vector2(-18, -12);
                notice.rect.sizeDelta = new Vector2(Mathf.Ceil(label.GetPreferredValues(label.text).x) + 36, 70);
                notices[key] = notice;
            }
            if (notice.showing) return; // 페이드 중에도 중복 생성/위치 이동/유지 시간 갱신을 하지 않습니다.
            notice.target = button; notice.showing = true; notice.hold = 2;
            notice.fadeElapsed = 0; notice.fadeDuration = .5f; notice.fadeFrom = 1;
            notice.group.alpha = 1; notice.gameObject.SetActive(true); notice.Position();
        }
        public static void OnGuideTransition(Scene scene)
        {
            if (notices.TryGetValue(scene.handle, out var notice) && notice) notice.FadeForTransition();
        }
        void FadeForTransition()
        {
            if (!showing) return;
            // 이미 진행 중인 페이드보다 시간을 늘리거나 알파를 되돌리지 않습니다.
            float remaining = hold > 0 ? .3f : Mathf.Min(.3f, fadeDuration - fadeElapsed);
            hold = 0; fadeFrom = group.alpha; fadeElapsed = 0; fadeDuration = Mathf.Max(0, remaining);
        }
        void LateUpdate()
        {
            if (!showing) return;
            if (!target || !target.gameObject.activeInHierarchy) { Hide(); return; }
            var flow = JcTutorialBattleControlLock.FindInScene<BattleFlowManager>(gameObject.scene);
            if (flow && (flow.IsEndingBattle || flow.IsBattleEnded)) FadeForTransition();
            Advance(Time.unscaledDeltaTime); Position();
        }
        // JC | 2026-11-13 | 유지 시간을 넘긴 프레임의 나머지도 페이드에 반영해 낮은 FPS에서 수명이 길어지지 않습니다.
        void Advance(float delta)
        {
            if (!showing) return;
            delta = Mathf.Max(0, delta);
            float wait = Mathf.Min(hold, delta); hold -= wait; delta -= wait;
            if (hold > 0) return;
            fadeElapsed += delta;
            group.alpha = fadeDuration > 0 ? fadeFrom * (1 - Mathf.Clamp01(fadeElapsed / fadeDuration)) : 0;
            if (fadeElapsed >= fadeDuration) Hide();
        }
        void Hide() { showing = false; if (group) group.alpha = 0; gameObject.SetActive(false); }
        void Position()
        {
            if (!target || !parentRect) return;
            var source = target.GetComponentInParent<Canvas>();
            Camera from = source && source.renderMode != RenderMode.ScreenSpaceOverlay ? source.worldCamera : null;
            Camera to = parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? parentCanvas.worldCamera : null;
            target.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in corners)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, RectTransformUtility.WorldToScreenPoint(from, corner), to, out var p);
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            Vector2 half = rect.sizeDelta * .5f;
            Rect area = parentRect.rect;
            rect.localPosition = new Vector3(
                Mathf.Clamp((min.x + max.x) * .5f, area.xMin + half.x + 8, Mathf.Max(area.xMin + half.x + 8, area.xMax - half.x - 8)),
                Mathf.Clamp(max.y + half.y + 8, area.yMin + half.y + 8, Mathf.Max(area.yMin + half.y + 8, area.yMax - half.y - 8)), 0);
        }
        void OnDisable() { showing = false; if (group) group.alpha = 0; }
        void OnDestroy() { if (notices.TryGetValue(sceneHandle, out var value) && value == this) notices.Remove(sceneHandle); }
    }
}
