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
    private Transform target;
    private Camera viewCamera;
    private Vector3 lastAnchor, initialScale, riseDirection;
    private float elapsed;
    private int lane;
    private bool playing, critical;

    // 대상 정보는 생성 위치를 한 번 계산할 때만 사용합니다.
    public void Follow(Transform actor)
    {
        target = actor;
        cameraTargetId = actor ? actor.GetInstanceID() : 0;
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

    // JC | 2026-11-10 | 카메라 예약 영역도 실제 숫자와 같은 머리/메시/소켓 앵커를 사용합니다. 표시 동작은 유지합니다.
    public Vector3 PredictAnchor(Transform actor) => ResolveAnchor(actor, _headPadding, lastAnchor);
    private Vector3 Anchor() => PredictAnchor(target);
    private static Vector3 ResolveAnchor(Transform actor, float padding, Vector3 previous)
    {
        if (!actor) return previous;
        var animator = actor.GetComponentInChildren<Animator>();
        var bone = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (bone) return bone.position + Vector3.up * padding;
        bool found = false; Bounds bounds = default;
        foreach (var body in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!body.enabled || !body.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = body.bounds; found = true; } else bounds.Encapsulate(body.bounds);
        }
        if (found) return new Vector3(bounds.center.x, bounds.max.y + padding, bounds.center.z);
        var profile = actor.GetComponent<UnitVisualProfile>();
        var anchor = profile && profile.DamagePopupSocket ? profile.DamagePopupSocket.position : actor.position + Vector3.up * 1.5f;
        return anchor + Vector3.up * padding;
    }

    // JC | 2026-11-10 | 빌보드 회전/상승/치명타 확대를 포함하는 보수적 월드 영역입니다. 피해 계산이나 파티클 재생은 하지 않습니다.
    public Bounds CameraEnvelope(Transform actor, Vector2 halfSize, float motionMargin, Quaternion rotation)
    {
        // JC | 2026-11-10 | 빈 구체 대신 실제 빌보드 사각형과 상승 구간의 합집합을 사용해 세로 화면의 과도한 축소를 방지합니다.
        var right = rotation * Vector3.right * (halfSize.x * Mathf.Max(1, _punchScale));
        var up = rotation * Vector3.up * (halfSize.y * Mathf.Max(1, _punchScale) + Mathf.Max(0, _riseHeight) * .5f);
        var extents = new Vector3(Mathf.Abs(right.x) + Mathf.Abs(up.x), Mathf.Abs(right.y) + Mathf.Abs(up.y), Mathf.Abs(right.z) + Mathf.Abs(up.z));
        extents += Vector3.one * Mathf.Max(.02f, motionMargin);
        return new Bounds(PredictAnchor(actor) + rotation * Vector3.up * (Mathf.Max(0, _riseHeight) * .5f), extents * 2);
    }

    // JC | 2026-11-10 | 실제 생성된 숫자의 자릿수·아이콘도 진입/복귀에서 보호합니다. targetId는 사망 후에도 생성 시 식별자를 유지합니다.
    private int cameraTargetId;
    public static void AppendActiveCameraPoints(int targetId, List<Vector3> points, Quaternion? desiredRotation = null)
    {
        foreach (var popup in active)
        {
            if (!popup || popup.cameraTargetId != targetId || !popup.playing) continue;
            void Add(Vector3 world)
            {
                // 카메라가 복귀 회전한 뒤 빌보드가 갱신될 자세를 미리 평가합니다. 상승 잔량도 포함합니다.
                var rotation = desiredRotation ?? popup.transform.rotation;
                float scaleRatio = Mathf.Abs(popup.initialScale.x) > .00001f ? Mathf.Abs(popup.transform.localScale.x / popup.initialScale.x) : 1;
                float expansion = popup.critical ? Mathf.Max(1, popup._punchScale / Mathf.Max(.00001f, scaleRatio)) : 1;
                var point = popup.transform.position + rotation * (Quaternion.Inverse(popup.transform.rotation) * (world - popup.transform.position)) * expansion;
                var remaining = popup.lastAnchor + popup.riseDirection * (popup._riseHeight + popup.lane * popup._stackSpacing) - popup.transform.position;
                points.Add(point); points.Add(point + remaining);
            }
            foreach (var label in popup.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                label.ForceMeshUpdate();
                var bounds = label.textBounds;
                for (int i = 0; i < 8; i++) Add(label.transform.TransformPoint(JC.BattleTesting.JcBlackBulletCameraFraming.Corner(bounds, i)));
            }
            var corners = new Vector3[4];
            foreach (var image in popup.GetComponentsInChildren<Image>())
            {
                if (!image.enabled) continue;
                image.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners) Add(corner);
            }
        }
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
