using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JC.BattleTesting
{
    // 씬 단위 진입 연출. 행동 이벤트 전에 UI만 바인딩하고, 클릭은 단계별로 소비합니다.
    public sealed class JcBattleHudEntryGate : MonoBehaviour
    {
        sealed class State
        {
            public CanvasGroup group; public float alpha; public bool interactable, raycasts, owned;
        }
        readonly List<State> groups = new List<State>();
        readonly HashSet<Transform> tracked = new HashSet<Transform>();
        BattleFlowManager flow;
        Canvas overlay;
        Image banner, blur;
        Material blurMaterial;
        RectTransform left, right;
        Vector2 leftHome, rightHome;
        bool revealing, complete;
        const float EnterSeconds = .3f, HoldSeconds = .5f, ExitSeconds = .3f, HudSeconds = .5f;
        static readonly Dictionary<int, JcBattleHudEntryGate> active = new Dictionary<int, JcBattleHudEntryGate>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => active.Clear();
        public static JcBattleHudEntryGate Find(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded && active.TryGetValue(scene.handle, out var gate) && gate != null ? gate : null;
        }
        public static bool IsPending(Scene scene) { var g = Find(scene); return g != null && !g.complete; }
        public static void Install(Scene scene, BattleFlowManager flow)
        {
            if (flow == null || Find(scene) != null) return;
            JC.Environment.JcDayNightController.EnsureBattle(scene,JC.Environment.JcDayNightController.CurrentHours,
                JC.Environment.JcDayNightController.CurrentProfile,JcBankBattleBackground.Load()?.Matches(CombatContext.Instance)==true);
            var owner = new GameObject("JC_BattleHudEntryGate");
            SceneManager.MoveGameObjectToScene(owner, scene);
            var gate = owner.AddComponent<JcBattleHudEntryGate>(); gate.flow = flow;
            active[scene.handle] = gate;
            gate.CreateOverlay(); gate.CaptureHud();
            flow.OnBattleEnded += gate.Ended;
        }
        public static IEnumerator ShowStart(BattleFlowManager flow)
        {
            var gate = Find(flow.gameObject.scene);
            if (gate != null) yield return gate.PlayBanner();
        }
        public static IEnumerator RevealForFirstUnit(BattleFlowManager flow)
        {
            var gate = Find(flow.gameObject.scene);
            if (gate != null) yield return gate.Reveal();
        }
        void CaptureHud()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var panel in root.GetComponentsInChildren<HeroInfoPanel>(true))
                {
                    Transform target = panel.transform;
                    for (var p = target; p != null; p = p.parent) if (p.name == "BottomPanel") { target = p; break; }
                    left = target as RectTransform; Track(target);
                }
                foreach (var panel in root.GetComponentsInChildren<TurnOrderUI>(true)) { right = panel.transform as RectTransform; Track(panel.transform); }
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                    if (rect.name == "TopRightButtons") Track(rect);
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas != overlay && (canvas.renderMode == RenderMode.WorldSpace || canvas.name == "HPCanvas" || canvas.name == "IPCanvas")) Track(canvas.transform);
            }
        }
        void Track(Transform target)
        {
            if (!tracked.Add(target)) return;
            var group = target.GetComponent<CanvasGroup>(); bool owned = group == null;
            if (owned) group = target.gameObject.AddComponent<CanvasGroup>();
            groups.Add(new State { group = group, alpha = group.alpha, interactable = group.interactable, raycasts = group.blocksRaycasts, owned = owned });
            group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
        }
        void LateUpdate()
        {
            if (complete) return;
            if (!revealing) CaptureHud(); // 초기 스폰 중 만들어지는 머리 위 게이지도 첫 렌더 전에 숨깁니다.
            foreach (var s in groups)
                if (s.group != null) { if (!revealing) s.group.alpha = 0; s.group.interactable = false; s.group.blocksRaycasts = false; }
        }
        void CreateOverlay()
        {
            var go = new GameObject("BattleEntryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false); overlay = go.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.sortingOrder = 1000;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var blocker = NewImage("EntryInputBlocker"); blocker.color = Color.clear; blocker.raycastTarget = true;
            var rect = blocker.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            blur = NewImage("StartMotionBlur"); banner = NewImage("BattleStart");
            var sprite = Resources.Load<Sprite>("BattleEntry/BattleStart");
            banner.sprite = blur.sprite = sprite;
            banner.preserveAspect = blur.preserveAspect = true;
            banner.rectTransform.sizeDelta = blur.rectTransform.sizeDelta = new Vector2(720, 270);
            var source = Resources.Load<Material>("BattleEntry/StartBlur");
            if (source != null) { blurMaterial = new Material(source); blur.material = blurMaterial; }
            banner.gameObject.SetActive(false); blur.gameObject.SetActive(false);
        }
        Image NewImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(overlay.transform, false); var image = go.GetComponent<Image>(); image.raycastTarget = false; return image;
        }
        bool Alive => !complete && flow != null && !flow.IsEndingBattle;
        bool Paused => Time.timeScale <= 0 || ModalManager.HasAny || (flow != null && flow.IsFlowBlocked);
        bool Clicked(int opened) => Time.frameCount > opened && JcPointerInput.CanControl && JcPointerInput.Inside &&
            (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1));
        IEnumerator DrainClick()
        {
            yield return null;
            while (Alive && (Input.GetMouseButton(0) || Input.GetMouseButton(1))) yield return null;
            yield return null;
        }
        IEnumerator PlayBanner()
        {
            // 로딩 중에는 투명하게 대기하고, 페이드 종료 후 좌측에서 진입합니다.
            banner.gameObject.SetActive(true);
            ApplyBannerTime(0);
            while (Alive && SceneFadeController.Instance != null && SceneFadeController.Instance.IsTransitioning) yield return null;
            int opened = Time.frameCount; float elapsed = 0; bool skipped = false;
            while (Alive && elapsed < EnterSeconds + HoldSeconds + ExitSeconds)
            {
                yield return null;
                if (Paused) { opened = Time.frameCount; continue; }
                if (Clicked(opened)) { skipped = true; break; }
                elapsed += Time.unscaledDeltaTime;
                ApplyBannerTime(elapsed);
            }
            banner.gameObject.SetActive(false); blur.gameObject.SetActive(false);
            if (skipped) yield return DrainClick();
        }
        void ApplyBannerTime(float elapsed)
        {
            float distance = ((RectTransform)overlay.transform).rect.width * .5f + 650;
            float x, alpha, motion, direction;
            if (elapsed < EnterSeconds)
            {
                float t = Mathf.Clamp01(elapsed / EnterSeconds);
                x = -distance * (1 - t) * (1 - t) * (1 - t);
                alpha = t;
                motion = (1 - t) * (1 - t);
                direction = 1; // 우측으로 이동하므로 잔상은 좌측으로 남깁니다.
            }
            else
            {
                float t = Mathf.Clamp01((elapsed - EnterSeconds - HoldSeconds) / ExitSeconds);
                x = distance * t * t;
                alpha = 1 - t;
                motion = t;
                direction = 1;
            }
            banner.rectTransform.anchoredPosition = new Vector2(x, 0);
            banner.color = new Color(1, 1, 1, alpha);
            bool showBlur = blurMaterial != null && motion > .001f && alpha > 0;
            blur.gameObject.SetActive(showBlur);
            if (showBlur)
            {
                blur.rectTransform.anchoredPosition = banner.rectTransform.anchoredPosition;
                blurMaterial.SetFloat("_BlurLength", direction * .18f * motion);
                blurMaterial.SetFloat("_Strength", 1.4f * motion);
                blur.color = new Color(1, 1, 1, alpha);
            }
        }
        IEnumerator Reveal()
        {
            yield return null; // UI 전용 이벤트의 모든 갱신/레이아웃 적용 후 위치 확정
            if (!Alive) yield break;
            CaptureHud(); Canvas.ForceUpdateCanvases();
            var presentation = BattlePresentationController.FindInScene(gameObject.scene);
            presentation?.PrepareEntryHud();
            if (left != null) leftHome = left.anchoredPosition;
            if (right != null) rightHome = right.anchoredPosition;
            revealing = true;
            // 체력바와 메뉴는 진입 종료 후 공개합니다.
            foreach (var state in groups) if (state.group != null && (state.group.transform == left || state.group.transform == right)) state.group.alpha = state.alpha;
            float elapsed = 0; int opened = Time.frameCount;
            ApplyHudProgress(0);
            while (Alive && elapsed < HudSeconds)
            {
                yield return null;
                if (Paused) { opened = Time.frameCount; continue; }
                if (Clicked(opened)) break;
                elapsed += Time.unscaledDeltaTime; ApplyHudProgress(Mathf.Clamp01(elapsed / HudSeconds));
            }
            ApplyHudProgress(1);
            yield return DrainClick();
            Finish();
        }
        void ApplyHudProgress(float progress)
        {
            float eased = 1 - (1 - progress) * (1 - progress);
            if (left != null) left.anchoredPosition = leftHome - Vector2.right * (Mathf.Max(left.rect.width, 960) + 64) * (1 - eased);
            if (right != null) right.anchoredPosition = rightHome + Vector2.right * (right.rect.width + 400) * (1 - eased);
        }
        void Ended(BattleResult result) => Finish();
        void Finish()
        {
            if (complete) return; complete = true;
            if (active.TryGetValue(gameObject.scene.handle, out var registered) && registered == this) active.Remove(gameObject.scene.handle);
            if (revealing) ApplyHudProgress(1);
            foreach (var s in groups)
            {
                if (s.group == null) continue;
                s.group.alpha = s.alpha; s.group.interactable = s.interactable; s.group.blocksRaycasts = s.raycasts;
                if (s.owned) Destroy(s.group);
            }
            groups.Clear(); if (overlay != null) overlay.gameObject.SetActive(false);
            Destroy(gameObject);
        }
        void OnDisable() { Finish(); }
        void OnDestroy()
        {
            if (flow != null) flow.OnBattleEnded -= Ended;
            if (!complete) Finish();
            if (blurMaterial != null) Destroy(blurMaterial);
        }
    }
}
