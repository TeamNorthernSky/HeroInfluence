using UnityEngine;

namespace JC.Tutorial
{
    [DisallowMultipleComponent, AddComponentMenu("JC Tutorial/이동력 수렴 강조")]
    public sealed class JcTutorialFocusPulse : MonoBehaviour
    {
        [Tooltip("이동력 UI 바로 바깥의 글로우 테두리입니다. 클릭 판정은 없습니다.")]
        public JcTutorialGraphic rim;
        [Tooltip("바깥에서 이동력 UI를 향해 수렴하는 글로우 테두리입니다. 클릭 판정은 없습니다.")]
        public JcTutorialGraphic converging;
        [Min(.1f), Tooltip("수렴 강조 1회의 시간(초)입니다. 게임 시간 배율과 무관하며 반복 횟수 후 자동으로 사라집니다.")]
        public float period = .9f;
        [Min(1), Tooltip("두 번째 이동 안내에서 반복하는 횟수입니다. 기본값은 3회이며 재클릭해도 다시 시작하지 않습니다.")]
        public int repetitions = 3;
        [Min(0), Tooltip("이동력 UI와 고정 테두리 사이의 여유(Canvas 단위)입니다.")]
        public float padding = 8;
        [Min(0), Tooltip("수렴 테두리가 바깥에서 시작하는 추가 거리(Canvas 단위)입니다.")]
        public float expansion = 24;
        [Tooltip("수렴 테두리와 글로우 색상입니다.")]
        public Color accent = new Color(.5f, 1f, .86f, 1);
        [Tooltip("이동력 박스를 가리키며 함께 반짝이는 큰 화살표입니다. 클릭 판정은 없습니다.")]
        public JcTutorialGraphic pointer;
        [Tooltip("큰 안내 화살표의 너비(Canvas 단위)입니다. 세로 길이는 화면 중앙부터 대상까지 자동 계산합니다.")]
        public Vector2 pointerSize = new Vector2(76,96);
        private RectTransform target;
        private readonly Vector3[] corners = new Vector3[4];
        private float elapsed;
        private bool running, suspended;
        public bool IsRunning => running;

        public void Begin(RectTransform value) { target = value; elapsed = 0; running = value != null; }
        public void Suspend(bool value) { suspended = value; if (value) Show(false); }
        public void Stop() { running = false; Show(false); }
        private void LateUpdate() => Render(Time.unscaledDeltaTime);
        public void Render(float delta)
        {
            if (!running || suspended || target == null || !target.gameObject.activeInHierarchy) { Show(false); return; }
            elapsed += Mathf.Max(0, delta);
            if (elapsed >= Mathf.Max(.1f, period) * Mathf.Max(1, repetitions)) { Stop(); return; }
            if (rim == null || converging == null) return;
            var parent = rim.rectTransform.parent as RectTransform;
            if (parent == null) return;
            var from = target.GetComponentInParent<Canvas>(); var to = parent.GetComponentInParent<Canvas>();
            Camera fromCamera = from != null && from.renderMode != RenderMode.ScreenSpaceOverlay ? from.worldCamera : null;
            Camera toCamera = to != null && to.renderMode != RenderMode.ScreenSpaceOverlay ? to.worldCamera : null;
            target.GetWorldCorners(corners);
            Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
            foreach (var corner in corners)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, RectTransformUtility.WorldToScreenPoint(fromCamera, corner), toCamera, out var local);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            float phase = Mathf.Repeat(elapsed / Mathf.Max(.1f, period), 1);
            float intensity = Mathf.Sin(phase * Mathf.PI);
            Vector2 center = (min + max) * .5f, size = max - min + Vector2.one * (padding * 2);
            Show(true);
            if(pointer != null) {
                pointer.AimFromCenter(min,max,pointerSize.x,padding+expansion+Mathf.Max(rim.GlowWidth,converging.GlowWidth));
                pointer.color=accent;
                pointer.AnimateSweep(elapsed * JcTutorialGraphic.ArrowSweepPeriod / Mathf.Max(.1f,period));
                pointer.gameObject.SetActive(elapsed < Mathf.Max(.1f,period)*JcTutorialGraphic.ArrowSweepCount);
            }
            rim.AnimateSweep(elapsed); converging.AnimateSweep(elapsed);
            Draw(rim, center, size, .15f + .7f * intensity);
            float cycle=Mathf.Repeat(elapsed,3);
            converging.gameObject.SetActive(cycle<2);
            float convergencePhase=Mathf.Repeat(cycle,1);
            Draw(converging, center, size + Vector2.one * (expansion * 2 * (1 - Mathf.SmoothStep(0, 1, convergencePhase))), Mathf.Sin(convergencePhase*Mathf.PI));
        }
        private void Draw(JcTutorialGraphic graphic, Vector2 center, Vector2 size, float alpha)
        {
            // 하단 HUD는 화면 끝에 가까우므로 글로우까지 화면 안에서 수렴시킨다.
            var parent = graphic.rectTransform.parent as RectTransform;
            if (parent != null)
            {
                Vector2 margin = Vector2.one * (graphic.GlowWidth + 1);
                Vector2 min = Vector2.Max(center - size * .5f, parent.rect.min + margin);
                Vector2 max = Vector2.Min(center + size * .5f, parent.rect.max - margin);
                size = Vector2.Max(Vector2.zero, max - min); center = (min + max) * .5f;
            }
            graphic.rectTransform.localPosition = center; graphic.rectTransform.sizeDelta = size;
            var color = accent; color.a *= alpha; graphic.color = color;
        }
        private void Show(bool visible)
        { if(pointer != null)pointer.gameObject.SetActive(visible); if (rim != null) rim.gameObject.SetActive(visible); if (converging != null) converging.gameObject.SetActive(visible); }
        private void OnDisable() => Stop();
    }
}
