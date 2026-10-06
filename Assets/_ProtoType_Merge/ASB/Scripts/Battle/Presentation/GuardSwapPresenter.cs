using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PrimeTween;
using ASB.Work.Battle.Core;

/// <summary>
/// 대신 맞기(FV20003_2) 자리 교환 연출. A = 가디언(재지정된 Target), B = 원래 대상(RedirectedFrom).
///
/// 규칙(재지정·피해 확정)은 BattleManager가 끝낸 뒤 호출되며, transform과 애니 상태만 바꾼다(OccupiedCell 불변).
/// - Enter: A·B 동시 move → 각자 원래 방향으로 회전 → Idle
/// - Exit (A 생존): A의 Hit 종료 대기(필요 시 Idle 전환) → A·B 동시 move → 회전 → Idle
/// - Exit (A 사망): B는 A 자리에서 A 시체가 사라질 때까지 대기 → move → 회전 → Idle
///
/// 캐스트마다 새로 만든다(반격이 같은 루틴을 재진입하므로 공유 상태 금지). Enter가 성공한 경우에만 Exit가 동작하고,
/// Exit에서 A·B의 복귀 여부는 각각 독립적으로 판단한다.
/// 코루틴은 유닛이 아니라 host(BattleManager)에서 돌린다 — 시체 Destroy로 대기 중인 코루틴이 끊기지 않게.
/// </summary>
public sealed class GuardSwapPresenter
{
    // 위치 이동 구간만의 길이. MoveToPosition 내부의 이동 방향 회전(0.1초)과 도착 후 회전(FaceTurnDuration)은 별도라
    // 한 번의 이동 단계는 약 0.45초다.
    private const float GuardSwapDuration = 0.25f;
    private const float GuardSwapBlend = 0.1f;     // MoveForward 블렌드
    private const float FaceTurnDuration = 0.1f;   // 도착 후 원래 방향으로 회전
    private const float HitEndThreshold = 0.9f;
    // 전투 시간(Time.deltaTime × 배속). Hit 클립이 배속으로 재생되므로 같은 시계로 잰다.
    private const float GuardHitExitTimeout = 1f;
    // 스케일드 시간(Time.deltaTime, 배속 미반영). EnemySpawner 시체 제거(WaitForSeconds, 기본 0.8초)와 같은 시계로 재야
    // 고배속에서 상한이 시체 제거보다 먼저 끝나지 않는다. _corpseRemovalDelay보다 충분히 길게 둔다.
    private const float GuardCorpseWaitTimeout = 3f;

    private const string MoveStateName = "MoveForward";
    private const string HitStateName = "Hit";

    private readonly BattleManager _host;
    private BattleCharactor _guardian;
    private BattleCharactor _ward;
    private Vector3 _guardianOrigin;
    private Vector3 _wardOrigin;
    private Quaternion _guardianRotation;
    private Quaternion _wardRotation;
    private bool _entered;

    public GuardSwapPresenter(BattleManager host)
    {
        _host = host;
    }

    /// <summary>재지정된 컨텍스트가 있고 A·B가 모두 살아 있으면 자리를 교환한다. 아니면 아무것도 하지 않는다.</summary>
    public IEnumerator Enter(IReadOnlyList<DamageContext> contexts)
    {
        if (_host == null || contexts == null)
        {
            yield break;
        }

        BattleCharactor guardian = null, ward = null;
        for (int i = 0; i < contexts.Count; i++)
        {
            DamageContext ctx = contexts[i];
            if (ctx != null && ctx.RedirectedFrom != null)
            {
                guardian = ctx.Target;
                ward = ctx.RedirectedFrom;
                break;
            }
        }

        if (!CanMove(guardian) || !CanMove(ward))
        {
            yield break;
        }

        _guardian = guardian;
        _ward = ward;
        _guardianOrigin = guardian.transform.position;
        _guardianRotation = guardian.transform.rotation;
        _wardOrigin = ward.transform.position;
        _wardRotation = ward.transform.rotation;
        _entered = true;

        // 두 이동을 동시 시작 → 순차 대기 = 병렬 실행
        Coroutine guardianIn = _host.StartCoroutine(MoveAndSettle(_guardian, _wardOrigin, _guardianRotation));
        Coroutine wardIn = _host.StartCoroutine(MoveAndSettle(_ward, _guardianOrigin, _wardRotation));
        yield return guardianIn;
        yield return wardIn;
    }

    /// <summary>자리 원복. Enter가 성공한 경우에만 동작하며 FlushPendingCommits 이후에 호출해야 한다.</summary>
    public IEnumerator Exit()
    {
        if (!_entered)
        {
            yield break;
        }
        _entered = false;

        if (IsAlive(_guardian))
        {
            yield return WaitGuardianHitExit();
        }
        else if (IsAlive(_ward))
        {
            yield return WaitGuardianCorpseGone();
        }

        // 대기 뒤 상태로 각자 독립 판단: 살아 있는 쪽만 자기 원위치로 복귀한다.
        Coroutine guardianBack = CanMove(_guardian)
            ? _host.StartCoroutine(MoveAndSettle(_guardian, _guardianOrigin, _guardianRotation))
            : null;
        Coroutine wardBack = CanMove(_ward)
            ? _host.StartCoroutine(MoveAndSettle(_ward, _wardOrigin, _wardRotation))
            : null;
        if (guardianBack != null) yield return guardianBack;
        if (wardBack != null) yield return wardBack;
    }

    // 목적지로 move → 원래 방향으로 회전 → Idle. 각 단계 전에 Destroy 여부를 확인한다.
    private static IEnumerator MoveAndSettle(BattleCharactor unit, Vector3 destination, Quaternion facing)
    {
        if (!CanMove(unit))
        {
            yield break;
        }

        yield return unit.Anim.MoveToPosition(destination, GuardSwapDuration, MoveStateName, GuardSwapBlend);
        if (unit == null)
        {
            yield break;
        }

        Quaternion yaw = Quaternion.Euler(0f, facing.eulerAngles.y, 0f);
        yield return Tween.Rotation(unit.transform, yaw, FaceTurnDuration).ToYieldInstruction();
        if (unit != null && unit.Anim != null)
        {
            unit.Anim.PlayIdleAnimation();
        }
    }

    // A의 마지막 Hit가 끝날 때까지 대기. 진입 전이 중이면 계속 기다리고, 끝나도 Hit에 머물러 있으면 Idle로 전환한다
    // (방패병 Hit 상태에는 나가는 전이가 없고, 멀티타깃 경로는 Hit 뒤 Idle 복귀를 하지 않는다).
    private IEnumerator WaitGuardianHitExit()
    {
        yield return null; // 직전 프레임의 CrossFade가 Animator 상태 정보에 반영되도록 1프레임 대기

        float elapsed = 0f;
        while (_guardian != null && _guardian.Anim != null
               && !_guardian.Anim.IsStateOverOrAbsent(HitStateName, HitEndThreshold))
        {
            if (elapsed >= GuardHitExitTimeout)
            {
                Debug.LogWarning($"[GuardSwap] {_guardian.UnitName} Hit 종료 대기 상한({GuardHitExitTimeout}초) 초과 — 복귀를 진행합니다.", _guardian);
                break;
            }

            elapsed += Time.deltaTime * _host.CurrentBattleSpeed;
            yield return null;
        }

        if (_guardian != null && _guardian.Anim != null && _guardian.Anim.IsInState(HitStateName))
        {
            _guardian.Anim.PlayIdleAnimation();
        }
    }

    // A 사망: 시체가 사라질 때까지(Destroy 또는 비활성) B를 A 자리에서 대기시킨다.
    // 시체 유지 유닛이거나 상한 초과면 겹침을 허용하고 복귀한다 — B를 A 자리에 남기면 transform과 OccupiedCell이 어긋난다.
    private IEnumerator WaitGuardianCorpseGone()
    {
        if (_guardian == null)
        {
            yield break;
        }

        if (_guardian.TryGetComponent(out EncounterParticipant participant) && participant.KeepCorpseAfterDeath)
        {
            Debug.LogWarning($"[GuardSwap] {_guardian.UnitName} 시체가 유지되는 유닛이라 대기 없이 복귀합니다(시체와 겹침).", _guardian);
            yield break;
        }

        float elapsed = 0f;
        while (_guardian != null && _guardian.gameObject.activeInHierarchy)
        {
            if (elapsed >= GuardCorpseWaitTimeout)
            {
                Debug.LogWarning($"[GuardSwap] {_guardian.UnitName} 시체 제거 대기 상한({GuardCorpseWaitTimeout}초) 초과 — 겹침을 허용하고 복귀합니다.", _guardian);
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static bool IsAlive(BattleCharactor unit) => unit != null && !unit.IsDead;

    private static bool CanMove(BattleCharactor unit) => IsAlive(unit) && unit.Anim != null;
}
