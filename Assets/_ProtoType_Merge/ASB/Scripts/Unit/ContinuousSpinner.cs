using UnityEngine;

/// <summary>
/// 드론 프로펠러 등 "살아있는 동안 항상" 도는 상시 회전 연출.
/// Timeline/Animator/프레젠테이션과 무관하게 자기(또는 지정) transform만 로컬축으로 돌린다.
/// <para>owner가 있으면 사망(IsDead) 중에는 정지하고, 부활하면 자동 재개한다(영구 latch가 아니라 매 프레임 IsDead 검사).</para>
/// <para>owner가 없으면(필드 표현 등) 항상 회전한다. 게임 상태는 건드리지 않는다.</para>
/// </summary>
[DisallowMultipleComponent]
public class ContinuousSpinner : MonoBehaviour
{
    [Tooltip("회전 대상. 비우면 자기 자신.")]
    [SerializeField] private Transform target;

    [Tooltip("로컬 회전축. 프로펠러면 보통 up 또는 forward. 좌/우를 반대로 돌리려면 부호를 반전.")]
    [SerializeField] private Vector3 localAxis = Vector3.up;

    [Tooltip("초당 회전 각도(도).")]
    [SerializeField] private float degreesPerSecond = 720f;

    [Tooltip("전투 배속(BattleManager.CurrentBattleSpeed)에 회전 속도를 동기화. owner가 있을 때만 적용된다.")]
    [SerializeField] private bool syncBattleSpeed = true;

    [Tooltip("사망 정지 판정에 쓰는 전투 유닛. 비우면 항상 회전(필드 표현 등).")]
    [SerializeField] private BattleCharactor owner;

    private void Reset()
    {
        target = transform;
    }

    private void Awake()
    {
        if (target == null) target = transform;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 사망 중 정지 — 영구 latch가 아니라 매 프레임 검사이므로 부활(IsDead=false) 시 자동 재개된다.
        if (owner != null && owner.IsDead) return;

        float battleSpeed = (owner != null && syncBattleSpeed && BattleManager.Instance != null)
            ? Mathf.Max(0f, BattleManager.Instance.CurrentBattleSpeed)
            : 1f;

        Vector3 axis = localAxis.sqrMagnitude > 1e-6f ? localAxis.normalized : Vector3.up;
        target.Rotate(axis, degreesPerSecond * battleSpeed * Time.deltaTime, Space.Self);
    }
}
