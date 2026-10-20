using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>생성 순간의 머리 위 월드 좌표에 고정하고 실제 시간 기준으로 읽을 시간을 보장합니다.</summary>
[RequireComponent(typeof(CanvasGroup))]
public class DamagePopupEffect : MonoBehaviour
{
    [Tooltip("등장 시 위로 이동할 월드 거리입니다. 0이면 이동하지 않습니다. 프리팹에 저장됩니다.")]
    [SerializeField, Min(0)] private float _riseHeight = .3f;
    [Tooltip("등장 시간(실제 초)입니다. 배속과 무관합니다. 프리팹에 저장됩니다.")]
    [SerializeField, Min(0)] private float _riseDuration = .22f;
    [Tooltip("등장 후 완전 불투명하게 유지할 실제 초입니다. 프리팹에 저장됩니다.")]
    [SerializeField, Min(0)] private float _holdDuration = 1f;
    [Tooltip("퇴장 페이드의 실제 초입니다. 0이면 즉시 사라집니다. 프리팹에 저장됩니다.")]
    [SerializeField, Min(0)] private float _fadeDuration = .18f;
    [Tooltip("치명타 등장 확대 배율입니다. 1이면 확대하지 않습니다. 프리팹에 저장됩니다.")]
    [SerializeField, Min(1)] private float _punchScale = 1.15f;
    [Tooltip("생성 시 대상 머리 위에 더할 월드 높이입니다. 생성 후에는 대상을 따라가지 않습니다. 프리팹에 저장됩니다.")]
    [SerializeField, Min(0)] private float _headPadding = .55f;
    [Tooltip("같은 대상의 동시 수치 사이 월드 높이 간격입니다. 프리팹에 저장됩니다.")]
    [SerializeField, Min(0)] private float _stackSpacing = 0f;
    [Tooltip("깊이 검사를 하지 않는 아이콘 전용 머티리얼입니다. 프리팹에 저장됩니다.")]
    [SerializeField] private Material _iconMaterial;
    private static readonly List<DamagePopupEffect> active = new List<DamagePopupEffect>();
    private CanvasGroup group;
    private Transform target, head, socket;
    private SkinnedMeshRenderer[] bodies;
    private Camera viewCamera;
    private Vector3 lastAnchor, initialScale, riseDirection;
    private float elapsed;
    private int lane;
    private bool playing, critical;

    // 대상 정보는 생성 위치를 한 번 계산할 때만 사용합니다.
    public void Follow(Transform actor)
    {
        target = actor;
        var profile = actor.GetComponent<UnitVisualProfile>();
        socket = profile != null ? profile.DamagePopupSocket : null;
        var animator = actor.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
        bodies = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
        // 가장 낮은 빈 줄을 배정하므로 앞 수치가 사라져도 기존 수치는 튀지 않습니다.
        for (lane = 0; ; lane++)
        {
            bool occupied = false;
            foreach (var popup in active)
                if (popup != null && popup.target == target && popup.lane == lane) { occupied = true; break; }
            if (!occupied) break;
        }
    }

    public void Play(bool isCritical = false)
    {
        if (playing) return;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false;
        initialScale = transform.localScale;
        lastAnchor = transform.position;
        viewCamera = Camera.main;
        lastAnchor = Anchor();
        riseDirection = viewCamera != null ? viewCamera.transform.up : Vector3.up;
        transform.position = lastAnchor;
        critical = isCritical; elapsed = 0; playing = true;
        active.Add(this);
        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null) { canvas.overrideSorting = true; canvas.sortingOrder = 500; }
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            image.raycastTarget = false;
            if (_iconMaterial != null) image.material = _iconMaterial;
            // 스프라이트는 셰이더의 알파 경계 확장으로 처리합니다. 단색 십자가만 UI 메시 효과를 사용합니다.
            if (image.sprite != null) continue;
            var outline = image.GetComponent<Outline>();
            if (outline == null) outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white; outline.effectDistance = new Vector2(.15f, -.15f);
            // Outline도 Shadow를 상속하므로 독립 Shadow만 찾습니다.
            Shadow shadow = null;
            foreach (var candidate in image.GetComponents<Shadow>())
                if (!(candidate is Outline)) { shadow = candidate; break; }
            if (shadow == null) shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.2f, .2f, .2f, .8f);
            shadow.effectDistance = new Vector2(.3f, -.3f);
        }
    }

    private Vector3 Anchor()
    {
        if (target == null) return lastAnchor;
        if (head != null) return head.position + Vector3.up * _headPadding;
        // 소켓이 없는 비휴머노이드도 실제 표시 메시 상단을 기준으로 합니다.
        bool found = false; Bounds bounds = default;
        if (bodies != null) foreach (var body in bodies)
        {
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = body.bounds; found = true; } else bounds.Encapsulate(body.bounds);
        }
        if (found) return new Vector3(bounds.center.x, bounds.max.y + _headPadding, bounds.center.z);
        return (socket != null ? socket.position : target.position + Vector3.up * 1.5f) + Vector3.up * _headPadding;
    }

    private void LateUpdate()
    {
        if (!playing) return;
        elapsed += Time.unscaledDeltaTime;
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null) transform.rotation = viewCamera.transform.rotation;
        // 피격·사망 애니메이션에 따라 앵커를 다시 계산하지 않습니다.
        float enter = _riseDuration > 0 ? Mathf.Clamp01(elapsed / _riseDuration) : 1;
        float ease = 1 - Mathf.Pow(1 - enter, 3);
        transform.position = lastAnchor + riseDirection * (_riseHeight * ease + lane * _stackSpacing);
        transform.localScale = initialScale * (critical ? Mathf.Lerp(_punchScale, 1, ease) : 1);
        float fadeTime = elapsed - _riseDuration - _holdDuration;
        group.alpha = fadeTime <= 0 ? 1 : (_fadeDuration > 0 ? 1 - Mathf.Clamp01(fadeTime / _fadeDuration) : 0);
        if (elapsed >= _riseDuration + _holdDuration + _fadeDuration) Destroy(gameObject);
    }
    private void OnDestroy() { active.Remove(this); }
}
