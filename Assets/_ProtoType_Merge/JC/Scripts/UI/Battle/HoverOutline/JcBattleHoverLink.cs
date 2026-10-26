using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>툴팁의 호버 결과만 받아 턴 배지와 전투 모델을 연결합니다. 대상 선택 상태는 변경하지 않습니다.</summary>
public sealed class JcBattleHoverLink : MonoBehaviour
{
    [Tooltip("호버 안내선 색입니다. 배지와 모델에 동일하게 적용됩니다.")]
    [SerializeField] private Color outlineColor = new Color(1f, 0.08f, 0.06f, 1f);
    [Tooltip("점멸 한 주기의 초 단위 길이입니다. 값이 클수록 천천히 점멸합니다.")]
    [SerializeField, Min(0.2f)] private float pulseSeconds = 0.8f;
    [Tooltip("모델 외곽선 두께입니다. 최종 화면 픽셀 단위이며 0이면 모델 안내선을 숨깁니다.")]
    [SerializeField, Range(0f, 8f)] private float modelWidth = 5f;
    [Tooltip("배지 안내선 두께입니다. 최종 화면 픽셀 단위이며 Canvas 축소와 무관하게 흰 테두리와 음각 슬롯 경계까지 덮습니다.")]
    [SerializeField, Range(1f, 12f)] private float badgeWidth = 10f;
    [Tooltip("모델 외곽선 바깥에서 수렴하는 범위입니다. 최종 화면 픽셀 단위이며 0이면 수렴 글로우를 끕니다.")]
    [SerializeField, Range(0f, 12f)] private float modelGlowRange = 10f;
    [Tooltip("수렴 글로우 한 주기의 초 단위 길이입니다. 값이 클수록 천천히 모입니다.")]
    [SerializeField, Min(0.2f)] private float convergenceSeconds = 1f;
    [Tooltip("배지 적백 점멸과 화살표 왕복의 공통 주기입니다.")]
    [SerializeField, Min(.2f)] private float badgeCycleSeconds = 1.6f;
    public float ModelClearancePixels => modelWidth + modelGlowRange + 3f;
    public float BadgeClearance => Mathf.Max(badgeWidth + 3f, JcBattleHoverRing.ArrowClearancePixels) / Mathf.Max(.01f, canvas != null ? canvas.scaleFactor : 1f);
    private ASB.Work.BattleGrid.BattleGridManager groundManager;
    private BattleCharactor hoverTarget;
    private bool hoverFromUI;
    private float hoverStart;

    private readonly Dictionary<TurnSlotUI, JcBattleHoverRing> rings = new Dictionary<TurnSlotUI, JcBattleHoverRing>();
    private readonly List<TurnSlotUI> slots = new List<TurnSlotUI>();
    private Canvas canvas;
    private BattleCharactor modelTarget;
    private Renderer[] modelRenderers;
    private int scannedFrame = -1;

    public void Initialize(Canvas owner)
    {
        if (canvas == owner) return;
        Clear();
        DestroyRings();
        canvas = owner;
        ScanSlots();
    }

    private void ScanSlots()
    {
        if (canvas == null) return;
        slots.Clear();
        canvas.GetComponentsInChildren(true, slots);
        scannedFrame = Time.frameCount;
        foreach (var slot in slots)
        {
            if (slot == null || rings.ContainsKey(slot)) continue;
            var anchor = FindBadge(slot);
            if (anchor == null) continue;
            var go = new GameObject("JC_HoverOutline", typeof(RectTransform), typeof(CanvasRenderer), typeof(JcBattleHoverRing));
            go.layer = anchor.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(anchor, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var ring = go.GetComponent<JcBattleHoverRing>();
            foreach (var image in slot.GetComponentsInChildren<Image>(true))
                if (image.name == "PortraitCircle") { ring.AlignToBadge(image); break; }
            ring.raycastTarget = false;
            ring.enabled = false;
            rings.Add(slot, ring);
        }
    }

    private static RectTransform FindBadge(TurnSlotUI slot)
    {
        // 기존 흰색 링은 초상화 마스크 밖에 있으므로 빨간 링도 잘리지 않습니다.
        foreach (var image in slot.GetComponentsInChildren<Image>(true))
            if (image.name == "PortraitWhiteOutline") return image.rectTransform;
        return null;
    }

    public bool IsBadgeHit(TurnSlotUI slot, Vector2 pointer, Camera eventCamera)
    {
        if (!rings.TryGetValue(slot, out var ring) || ring == null)
        {
            ScanSlots();
            if (!rings.TryGetValue(slot, out ring) || ring == null) return false;
        }
        var badge = ring.transform.parent as RectTransform;
        if (badge == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(badge, pointer, eventCamera, out var local)) return false;
        var rect = badge.rect;
        if (rect.width <= 0 || rect.height <= 0) return false;
        var relative = local - rect.center;
        relative.x /= rect.width * 0.5f;
        relative.y /= rect.height * 0.5f;
        return relative.sqrMagnitude <= 1f;
    }

    public void Show(BattleCharactor target, bool overUI, TurnSlotUI sourceSlot, Camera camera)
    {
        JcBattleHoverOutlineFeature.Clear(this);
        if (target == null || target.IsPlayer || target.IsDead || !target.gameObject.activeInHierarchy) { Clear(); return; }
        // 초기 비활성/동적 생성 슬롯도 지원합니다. 전체 씬 검색은 하지 않습니다.
        if (scannedFrame < 0 || Time.frameCount - scannedFrame > 60) ScanSlots();
        if (hoverTarget != target || hoverFromUI != overUI)
        { hoverTarget = target; hoverFromUI = overUI; hoverStart = Time.unscaledTime; }
        float progress = Mathf.Repeat((Time.unscaledTime - hoverStart) / Mathf.Max(.2f, convergenceSeconds), 1f);
        float convergence = 1f - Mathf.SmoothStep(0, 1, progress);
        float glowAlpha = Mathf.Sin(progress * Mathf.PI);
        bool highlightModel = !overUI || (sourceSlot != null && sourceSlot.DisplayedUnit == target);
        SetGround(highlightModel ? target : null);
        float pulse = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.2f, pulseSeconds));
        var color = outlineColor;
        color.a *= Mathf.Lerp(0.9f, 1f, pulse);
        float badgePhase = Mathf.Repeat((Time.unscaledTime - hoverStart) / Mathf.Max(.2f, badgeCycleSeconds), 1f);
        float badgeBlend = JcBattleHoverArrow.Blend(badgePhase);
        var badgeColor = Color.Lerp(outlineColor, Color.white, badgeBlend);
        badgeColor.a = 1f;
        foreach (var pair in rings)
        {
            if (pair.Value == null) continue;
            if (!overUI && pair.Key != null && pair.Key.isActiveAndEnabled && pair.Key.DisplayedUnit == target)
                pair.Value.Show(badgeColor, badgeWidth, badgePhase);
            else pair.Value.enabled = false;
        }
        if (!highlightModel) return;
        if (camera == null || modelWidth <= 0) return;
        if (modelTarget != target || modelRenderers == null)
        {
            modelTarget = target;
            var renderers = new List<Renderer>();
            foreach (var item in target.GetComponentsInChildren<Renderer>(true))
            {
                if (!(item is MeshRenderer) && !(item is SkinnedMeshRenderer)) continue;
                if (item.GetComponentInParent<Canvas>() != null || item.GetComponentInParent<ParticleSystem>() != null) continue;
                renderers.Add(item);
            }
            modelRenderers = renderers.ToArray();
        }
        JcBattleHoverOutlineFeature.Publish(this, camera, target, modelRenderers, color, modelWidth, modelGlowRange * convergence, modelGlowRange > 0 ? glowAlpha : 0);
    }

    private void SetGround(BattleCharactor target)
    {
        var manager = ASB.Work.BattleGrid.BattleGridManager.Instance;
        if (groundManager != null && groundManager != manager) groundManager.SetInfoHover(this, null);
        groundManager = manager != null && manager.gameObject.scene == gameObject.scene ? manager : null;
        if (groundManager != null) groundManager.SetInfoHover(this, target);
    }

    private void ClearVisuals()
    {
        JcBattleHoverOutlineFeature.Clear(this);
        foreach (var ring in rings.Values) if (ring != null) ring.enabled = false;
    }

    public void Clear()
    {
        ClearVisuals();
        SetGround(null);
        hoverTarget = null;
        modelTarget = null;
        modelRenderers = null;
    }

    private void OnDisable() => Clear();
    private void OnDestroy() { Clear(); DestroyRings(); }
    private void DestroyRings()
    {
        foreach (var ring in rings.Values) if (ring != null) Destroy(ring.gameObject);
        rings.Clear();
        slots.Clear();
        scannedFrame = -1;
    }
}
