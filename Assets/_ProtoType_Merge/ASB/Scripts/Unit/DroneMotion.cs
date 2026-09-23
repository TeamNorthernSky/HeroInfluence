using UnityEngine;

/// <summary>
/// 드론 비주얼 피벗(안쪽 Model)에 붙는 상시·반응 모션 컨트롤러.
/// <list type="bullet">
/// <item>상시: 공중 hover(+bob).</item>
/// <item>이동 추종 lean: <see cref="movementRoot"/>가 실제로 움직일 때 진행 방향으로 pitch(로컬 X). (필드 이동/피격 변위 등)</item>
/// <item>전투 피격: <c>owner.OnHpChanged</c> 감소 시 회전 recoil(+옵션 위치 변위). 코스메틱만, 그리드 셀 불변.</item>
/// <item>사망: <c>owner.OnDied</c> 시 lean/recoil/변위를 정착시키고(옵션 하강) 정지. 부활하면 재개.</item>
/// </list>
/// transform·연출만 만지고 게임 상태는 절대 바꾸지 않는다. root/셀은 외부(무버/규칙)가 소유하며 읽기만 한다.
/// owner가 없으면(필드) 이벤트 미구독 → hover+lean만 동작.
/// </summary>
[DisallowMultipleComponent]
public class DroneMotion : MonoBehaviour
{
    private enum MotionState { Alive, DeathSettling, Stopped }

    [Header("Hover (상시)")]
    [Tooltip("basePos(=authored 로컬 위치, 드론 내부 Model은 Y=0.5) 위에 더하는 추가 상승. authored 0.5를 유지하려면 0.")]
    [SerializeField] private float extraRise = 0f;
    [Min(0f)][SerializeField] private float bobAmplitude = 0f;
    [Tooltip("bob 각속도 (radians/sec).")]
    [Min(0f)][SerializeField] private float bobSpeed = 2f;

    [Header("Lean (이동 추종 pitch)")]
    [Tooltip("실제로 움직이는 상위 transform. 비우면 owner.transform로 폴백, 그래도 없으면 lean 비활성. ★transform.root 자동폴백 안 함.")]
    [SerializeField] private Transform movementRoot;
    [Tooltip("movementRoot 로컬 공간 기준 전진축. 메시의 -Y가 아니라 실제 이동 방향 기준으로 맞춘다.")]
    [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
    [Tooltip("pitch 회전축(보통 로컬 X).")]
    [SerializeField] private Vector3 tiltAxis = Vector3.right;
    [SerializeField] private float maxLeanAngle = 15f;
    [Min(0.01f)][SerializeField] private float speedForMaxLean = 3f;
    [Min(0f)][SerializeField] private float leanSmooth = 8f;
    [Min(0f)][Tooltip("한 프레임 이동량이 이 값을 넘으면 텔레포트로 보고 lean에 반영하지 않는다.")]
    [SerializeField] private float teleportDistanceThreshold = 3f;

    [Header("Hit (전투 피격 — owner 필요)")]
    [SerializeField] private float recoilAngle = 12f;
    [Tooltip("recoil 방향 부호. 정지 시 lean이 0이라 고정값을 쓴다.")]
    [SerializeField] private float recoilSign = 1f;
    [Min(0f)][SerializeField] private float recoilDecay = 120f;
    [Tooltip("피격 시 피벗 로컬 위치 변위 방향.")]
    [SerializeField] private Vector3 hitDisplaceDir = Vector3.back;
    [Tooltip("피격 위치 변위 거리. 0이면 회전 recoil만(권장 초기값).")]
    [SerializeField] private float hitDisplaceDist = 0f;
    [Min(0f)][SerializeField] private float hitDecay = 6f;

    [Header("Death")]
    [Tooltip("사망 시 하강 거리. 0이면 제자리 정착.")]
    [SerializeField] private float deathDropDistance = 0f;
    [Min(0f)][SerializeField] private float deathSettleSpeed = 4f;

    [Header("Refs")]
    [Tooltip("피격/사망/부활 이벤트원. 비우면 필드 모드(hover+lean만). ★GetComponentInParent 금지 — 반드시 Inspector로 전투 루트 연결.")]
    [SerializeField] private BattleCharactor owner;

    private MotionState _state;
    private Vector3 _basePos;
    private Quaternion _baseRot;
    private Vector3 _prevRootPos;
    private float _prevHp;
    private float _bobPhase;
    private float _lean;
    private float _recoil;
    private Vector3 _hitOffset;
    private float _dropY;
    private bool _leanEnabled;
    private bool _firstFrame;
    private bool _subscribed;

    private void Awake()
    {
        _basePos = transform.localPosition;   // authored(0.5) 보존 — 덮어쓰지 말 것
        _baseRot = transform.localRotation;

        if (movementRoot == null && owner != null) movementRoot = owner.transform;
        _leanEnabled = movementRoot != null;
        if (!_leanEnabled)
        {
            Debug.LogWarning($"[DroneMotion] movementRoot 미지정 & owner 없음 → lean 비활성. ({name})", this);
        }
    }

    private void OnEnable()
    {
        // 재활성화(풀링) 시 잘못된 lean/recoil 튐 방지 — 상태 재동기.
        _state = MotionState.Alive;
        _lean = 0f;
        _recoil = 0f;
        _hitOffset = Vector3.zero;
        _dropY = 0f;
        _bobPhase = 0f;
        _firstFrame = true;
        _prevRootPos = movementRoot != null ? movementRoot.position : transform.position;
        _prevHp = owner != null ? owner.CurrentHp : 0f;

        if (owner != null && !_subscribed)
        {
            owner.OnHpChanged += HandleHpChanged;
            owner.OnDied += HandleDied;
            _subscribed = true;
        }
    }

    private void OnDisable()
    {
        if (owner != null && _subscribed)
        {
            owner.OnHpChanged -= HandleHpChanged;
            owner.OnDied -= HandleDied;
            _subscribed = false;
        }
    }

    private void HandleHpChanged(float cur, float max)
    {
        // 부활 재개: 죽은 상태에서 HP가 되살아나면 Alive로 복귀 (Revive가 OnHpChanged를 호출한다).
        if (_state != MotionState.Alive && !owner.IsDead && cur > 0f)
        {
            _state = MotionState.Alive;
            _firstFrame = true;
        }
        // 피격 recoil + 코스메틱 변위 — 치명타 제외.
        // (TakeDamage는 OnHpChanged를 Die()보다 먼저 호출하므로 이 시점 IsDead는 아직 false → cur<=0로 치명타 판정)
        else if (cur < _prevHp && cur > 0f && !owner.IsDead)
        {
            _recoil = recoilSign * recoilAngle;
            if (hitDisplaceDist > 0f)
            {
                Vector3 dir = hitDisplaceDir.sqrMagnitude > 1e-6f ? hitDisplaceDir.normalized : Vector3.back;
                _hitOffset = dir * hitDisplaceDist;
            }
        }

        _prevHp = cur;   // 힐(증가)·초기 세팅은 무시된다.
    }

    private void HandleDied(BattleCharactor _)
    {
        if (_state != MotionState.Stopped)
        {
            _state = MotionState.DeathSettling;
        }
    }

    private void LateUpdate()
    {
        if (_state == MotionState.Stopped) return;

        float battleSpeed = (owner != null && BattleManager.Instance != null)
            ? Mathf.Max(0f, BattleManager.Instance.CurrentBattleSpeed)
            : 1f;
        float visualDt = Time.deltaTime * battleSpeed;

        // --- 이동 속도 + 텔레포트 가드 ---
        Vector3 worldVel = Vector3.zero;
        if (_leanEnabled && movementRoot != null)
        {
            Vector3 delta = movementRoot.position - _prevRootPos;
            bool teleport = _firstFrame || visualDt < 1e-5f
                || delta.sqrMagnitude > teleportDistanceThreshold * teleportDistanceThreshold;
            worldVel = teleport ? Vector3.zero : delta / visualDt;
            _prevRootPos = movementRoot.position;
            _firstFrame = false;
        }

        // --- lean (Alive만; 그 외 0으로 수렴) ---
        float targetLean = 0f;
        if (_state == MotionState.Alive && _leanEnabled)
        {
            Vector3 fwdAxis = localForwardAxis.sqrMagnitude > 1e-6f ? localForwardAxis.normalized : Vector3.forward;
            float fwd = Vector3.Dot(movementRoot.InverseTransformDirection(worldVel), fwdAxis);
            targetLean = Mathf.Clamp(fwd / speedForMaxLean, -1f, 1f) * maxLeanAngle;
        }
        _lean = Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-leanSmooth * visualDt));

        // --- 피격 임펄스 감쇠 ---
        _recoil = Mathf.MoveTowards(_recoil, 0f, recoilDecay * visualDt);
        _hitOffset = Vector3.MoveTowards(_hitOffset, Vector3.zero, hitDecay * visualDt);

        // --- hover(basePos 보존) + 사망 정착/하강 ---
        float bob = (_state == MotionState.Alive && bobAmplitude > 0f) ? Mathf.Sin(_bobPhase) * bobAmplitude : 0f;
        if (_state == MotionState.Alive) _bobPhase += bobSpeed * visualDt;
        float targetDrop = (_state == MotionState.DeathSettling) ? deathDropDistance : 0f;
        _dropY = Mathf.MoveTowards(_dropY, targetDrop, deathSettleSpeed * visualDt);

        Vector3 tilt = tiltAxis.sqrMagnitude > 1e-6f ? tiltAxis.normalized : Vector3.right;
        transform.localPosition = _basePos + Vector3.up * (extraRise + bob - _dropY) + _hitOffset;
        transform.localRotation = _baseRot * Quaternion.AngleAxis(_lean + _recoil, tilt);

        // --- 정착 완료 → 종료 (DeathSettling에선 bob이 항상 0이라 bob 검사는 불필요) ---
        if (_state == MotionState.DeathSettling
            && Mathf.Abs(_lean) < 0.05f && _recoil == 0f && _hitOffset == Vector3.zero
            && Mathf.Abs(_dropY - deathDropDistance) < 1e-3f)
        {
            _state = MotionState.Stopped;
        }
    }
}
