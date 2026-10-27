using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 총기 발사 반동 연출. Timeline의 <see cref="PresentationRecoilMarker"/>를 받으면 상체(가슴 뼈)를 뒤로 젖혔다가 되돌리고,
/// 외형(Model)을 살짝 뒤로 민다.
/// <list type="bullet">
/// <item>LateUpdate에서 Animator·Timeline·Animation Rigging이 이번 프레임에 쓴 포즈 위에 회전을 곱한다 → additive 레이어와 같은 효과.
///   스킬은 Timeline으로 재생돼 컨트롤러 레이어가 덮이고 Timeline에는 additive 트랙이 없어서 이 방식을 쓴다.</item>
/// <item>총과 양손이 모두 가슴 아래에 있어 통째로 움직인다 → 왼손 IK가 총에서 떨어지지 않는다.
///   오른팔·오른손·무기 소켓을 여기서 돌리면 IK가 이미 풀린 뒤라 왼손만 남으므로 공통 부모 뼈만 돌린다.</item>
/// <item>애니가 그 프레임에 뼈를 다시 쓰지 않으면(일시정지·컬링) 지난 프레임 기준 포즈에 다시 얹어 반동이 쌓이지 않게 한다.</item>
/// <item>시간은 Hit 넉백과 같은 시계(Time.deltaTime × 전투 배속 × 히트스톱 배율)로 잰다.</item>
/// </list>
/// 마커 알림은 PlayableDirector와 같은 GameObject(유닛 루트)에 있어야 받는다. 현재 소총병 Unit_VillanGun_20002.
/// <see cref="HitKnockbackMotion"/>과 같은 외형 루트를 밀기 때문에 한 유닛에 둘을 같이 붙이지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GunRecoilMotion : MonoBehaviour, INotificationReceiver
{
    [Tooltip("배속·히트스톱 기준. 비면 같은 오브젝트의 BattleCharactor.")]
    [SerializeField] private BattleCharactor owner;
    [Tooltip("젖힐 뼈. 총과 양손의 공통 부모여야 한다. 비면 양손의 가장 가까운 공통 부모(소총병=Spine02), 없으면 휴머노이드 UpperChest → Chest → Spine.")]
    [SerializeField] private Transform recoilBone;
    [Tooltip("뒤로 밀 외형 루트. 비면 자식 'Model'. 루트 자신을 넣으면 칸 위치가 흔들리므로 넣지 않는다.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("최대 젖힘 각도(도). 캐릭터 오른쪽 축을 기준으로 뒤로 젖힌다(총구도 같이 들린다).")]
    [SerializeField, Range(0f, 30f)] private float kickAngle = 6f;
    [Tooltip("최대 밀림 거리(월드 m). 0이면 밀지 않는다.")]
    [SerializeField, Min(0f)] private float pushDistance = .03f;
    [Tooltip("최대치까지 가는 시간(전투 초).")]
    [SerializeField, Min(.01f)] private float riseSeconds = .04f;
    [Tooltip("제자리로 돌아오는 시간(전투 초).")]
    [SerializeField, Min(.01f)] private float returnSeconds = .22f;

    private CharactorAnimationController _anim;
    private Transform _facing;
    private bool _boneResolved;

    private float _amount;
    private float _fromAmount;
    private float _peakAmount;
    private float _elapsed;
    private bool _active;

    private Quaternion _baseLocalRotation;
    private Quaternion _writtenLocalRotation;
    private bool _boneWritten;
    private Vector3 _restLocalPosition;
    private bool _pushApplied;

    internal bool IsActive => _active;

    private void Awake()
    {
        if (owner == null) owner = GetComponent<BattleCharactor>();
        if (visualRoot == null) visualRoot = transform.Find("Model");
        if (visualRoot == transform) visualRoot = null;
    }

    private void OnDisable()
    {
        Finish();
    }

    void INotificationReceiver.OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is PresentationRecoilMarker marker) Kick(marker.Strength);
    }

    /// <summary>반동 1회. 이전 반동이 남아 있으면 현재 값에서 다시 차오른다(연사).</summary>
    public void Kick(float strength = 1f)
    {
        if (strength <= 0f || (owner != null && owner.IsDead)) return;
        ResolveReferences();
        if (recoilBone == null && visualRoot == null) return;

        if (!_active && visualRoot != null)
        {
            _restLocalPosition = visualRoot.localPosition;
            _pushApplied = false;
        }

        _fromAmount = _amount;
        _peakAmount = strength;
        _elapsed = 0f;
        _active = true;
    }

    private void LateUpdate()
    {
        if (!_active) return;
        float speed = _anim != null ? _anim.CurrentAnimSpeed * _anim.HitStopSpeedMultiplier : 1f;
        bool finished = Tick(Time.deltaTime * speed);
        ApplyBone();
        ApplyPush();
        if (finished) Finish();
    }

    /// <summary>전투 시간 dt만큼 반동량을 진행한다. 빠르게 차오른 뒤 부드럽게 0으로 돌아온다. 다 돌아오면 true.</summary>
    internal bool Tick(float battleDeltaTime)
    {
        _elapsed += Mathf.Max(0f, battleDeltaTime);
        if (_elapsed < riseSeconds)
        {
            float t = _elapsed / riseSeconds;
            _amount = Mathf.LerpUnclamped(_fromAmount, _peakAmount, 1f - (1f - t) * (1f - t));
            return false;
        }

        float u = (_elapsed - riseSeconds) / returnSeconds;
        _amount = u >= 1f ? 0f : Mathf.LerpUnclamped(_peakAmount, 0f, Mathf.SmoothStep(0f, 1f, u));
        return u >= 1f;
    }

    private void ApplyBone()
    {
        if (recoilBone == null) return;

        // 애니가 이번 프레임에 뼈를 다시 썼으면 그 값이 기준 포즈, 안 썼으면(우리가 쓴 값 그대로) 지난 기준 포즈.
        Quaternion current = recoilBone.localRotation;
        Quaternion baseLocal = _boneWritten && current == _writtenLocalRotation ? _baseLocalRotation : current;
        _baseLocalRotation = baseLocal;

        if (_amount <= 0f || kickAngle <= 0f)
        {
            recoilBone.localRotation = baseLocal;
            _boneWritten = false;
            return;
        }

        Transform parent = recoilBone.parent;
        Quaternion parentRotation = parent != null ? parent.rotation : Quaternion.identity;
        Vector3 rightAxis = _facing != null ? _facing.right : transform.right;
        Quaternion kick = Quaternion.AngleAxis(-kickAngle * _amount, rightAxis);
        recoilBone.localRotation = Quaternion.Inverse(parentRotation) * (kick * (parentRotation * baseLocal));
        _writtenLocalRotation = recoilBone.localRotation;
        _boneWritten = true;
    }

    private void ApplyPush()
    {
        if (visualRoot == null || pushDistance <= 0f) return;

        Vector3 back = -(_facing != null ? _facing.forward : transform.forward);
        back.y = 0f;
        if (back.sqrMagnitude < .0001f) return;

        // 월드 방향을 매 프레임 부모 공간으로 바꾼다 — 반동 중 루트가 회전해도 밀리는 방향은 캐릭터 뒤쪽.
        Vector3 offset = back.normalized * (pushDistance * Mathf.Max(0f, _amount));
        Transform parent = visualRoot.parent;
        visualRoot.localPosition = _restLocalPosition + (parent != null ? parent.InverseTransformVector(offset) : offset);
        _pushApplied = true;
    }

    private void Finish()
    {
        if (_boneWritten && recoilBone != null) recoilBone.localRotation = _baseLocalRotation;
        if (_pushApplied && visualRoot != null) visualRoot.localPosition = _restLocalPosition;
        _boneWritten = false;
        _pushApplied = false;
        _amount = 0f;
        _active = false;
    }

    private void ResolveReferences()
    {
        if (_anim == null && owner != null)
        {
            owner.EnsureAnimationController();
            _anim = owner.Anim;
        }

        Animator animator = _anim != null ? _anim.Animator : null;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (_facing == null && animator != null) _facing = animator.transform;

        if (_boneResolved || recoilBone != null) return;
        _boneResolved = true;
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning($"[GunRecoilMotion] 휴머노이드 Animator 없음 → recoilBone을 직접 지정해야 상체 반동이 나온다. ({name})", this);
            return;
        }

        // 휴머노이드 Chest가 매핑 안 된 모델이 있어(소총병은 Spine만) 양손의 가장 가까운 공통 부모를 먼저 쓴다.
        recoilBone = CommonParent(animator.GetBoneTransform(HumanBodyBones.LeftHand),
            animator.GetBoneTransform(HumanBodyBones.RightHand), animator.transform);
        if (recoilBone == null) recoilBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (recoilBone == null) recoilBone = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (recoilBone == null) recoilBone = animator.GetBoneTransform(HumanBodyBones.Spine);
    }

    /// <summary>a와 b를 모두 자식으로 가진 가장 가까운 뼈. <paramref name="stopAt"/>(Animator 루트)까지 오르면 null.</summary>
    private static Transform CommonParent(Transform a, Transform b, Transform stopAt)
    {
        if (a == null || b == null) return null;
        for (Transform t = a.parent; t != null && t != stopAt; t = t.parent)
            if (b.IsChildOf(t)) return t;
        return null;
    }
}
