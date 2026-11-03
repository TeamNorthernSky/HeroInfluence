using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JC.VFX;
using UnityEngine;

/// <summary>
/// 애니메이션 테스트 씬에서 네코밍 스킬(4010~4040) 이펙트를 반복 재생마다 보여준다.
/// 재생할 스킬과 프리팹은 씬의 <see cref="AnimTestSkillFxSet"/>(숫자키 1~4)이 정하고, 이 컴포넌트는 모델별 시점·위치를 가진다.
/// 전투 시스템 없이 같은 프리팹에 시작 위치·대상만 넘겨 재생한다.
/// <para>Animator와 같은 오브젝트에 붙인다. 컨트롤러는 같은 클립을 가진 두 상태를 번갈아 오가게 만든다
/// (<see cref="RootMotionLoopReset"/>과 같은 전제 — 상태가 바뀔 때마다 새 반복으로 보고 시점을 다시 센다).</para>
/// <para>스킬마다 모델의 충전·발사 시점에 맞춰 이렇게 재생한다(동작 정지(Hold)와 효과음은 없다).
/// 4010: 충전 구체 → 발사 때 발바닥 투사체 → 힐 피격. 4020: 충전 시점에 Paw For You(비행·적중은 이펙트가 스스로 한다).
/// 4030: 충전 시점에 양손 구체 → 발사 시점에 합체 → 합체가 끝나면 투사체(피격 없음). 4040: 충전 시점에 부활·마법진 → 발사 시점에 연사.
/// None(숫자키 5): 아무것도 재생하지 않는다(4010 충전 구간 배속도 쓰지 않는다).</para>
/// <para>4010 구체는 기본으로 양손 중앙 대신 오른손을 따라간다(한 손 마법 동작은 두 손이 반대쪽에 있어
/// 양손 중앙이 몸통 안에 생긴다). 두 손 동작이면 <see cref="orbAtHandsCenter"/>로 전투와 같게 둔다.</para>
/// </summary>
[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(-10)] // ChargeOrbVfx.LateUpdate(트레일 갱신)보다 먼저 구체를 손 위치로 옮긴다.
public class AnimTestSkillFx : MonoBehaviour
{
    [Header("대상·시점")]
    [Tooltip("아군 대상(발밑 지점). 4010·4030 투사체와 4040 부활이 여기로 간다.")]
    [SerializeField] private Transform target;
    [Tooltip("적 지점 = 대상에서 모델 정면으로 이만큼 더 나간 곳(m). 4020·4040 공격이 여기로 간다. Play 중에만 만든다.")]
    [SerializeField, Min(0f)] private float enemyDistance = 2f;
    [Tooltip("클립 시작부터 충전(시전) 이펙트를 띄울 때까지(초).")]
    [SerializeField, Min(0f)] private float chargeTime = 0.2f;
    [Tooltip("클립 시작부터 발사할 때까지(초). 손을 가장 뻗는 순간에 맞춘다.")]
    [SerializeField, Min(0f)] private float releaseTime = 1f;
    [Tooltip("손 뼈에서 손끝 방향으로 구체를 띄울 거리(m).")]
    [SerializeField, Min(0f)] private float palmOffset = 0.06f;
    [Tooltip("켜면 4010 구체를 오른손 대신 양손 중앙에 두고 거기서 발사한다(전투 4010과 같음). 두 손 동작용.")]
    [SerializeField] private bool orbAtHandsCenter;
    [Tooltip("4010 충전~발사 구간의 재생 배속. Timeline의 Speed 마커(구체 생성~발사 구간만 빠르게)를 따라할 때 쓴다. 1이면 그대로.")]
    [SerializeField, Min(0.01f)] private float chargeSpeedMultiplier = 1f;

    [Header("비행 (SkillPresentation_4010·4030 값)")]
    [SerializeField, Min(0.01f)] private float projectileSpeed = 7f;
    [SerializeField, Min(0f)] private float arcHeight = 1.5f;
    [Tooltip("착탄 후 투사체 잔상을 남겨 두는 시간(초).")]
    [SerializeField, Min(0f)] private float impactVisualLifetime = 0.4f;

    [Tooltip("감시할 Animator 레이어.")]
    [SerializeField] private int layer = 0;

    // 4010 충전 프리팹은 양손 중앙 따라가기가 켜져 있다. 프리팹은 그대로 두고 이 씬에서 만든 인스턴스만 끈다.
    private static readonly FieldInfo FollowHandsCenterField =
        typeof(ChargeOrbVfx).GetField("followHandsCenter", BindingFlags.Instance | BindingFlags.NonPublic);

    private enum HoldAnchor { Orb, RightWeapon, LeftWeapon }

    private readonly List<(GameObject effect, HoldAnchor anchor)> _held = new List<(GameObject, HoldAnchor)>();
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Animator _animator;
    private Transform _rightHand;
    private Transform _rightLowerArm;
    private Transform _leftHand;
    private Transform _enemyPoint;
    private int _lastStateHash;
    private bool _charged;
    private bool _released;
    private int _epoch;

    private static AnimTestSkillFxSet Set => AnimTestSkillFxSet.Instance;
    private bool UsesChargeSpeed =>
        Set != null && Set.Current == AnimTestSkill.Heal4010 && !Mathf.Approximately(chargeSpeedMultiplier, 1f);

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
    }

    private void OnEnable() => AnimTestSkillFxSet.SkillChanged += OnSkillChanged;

    private void OnDisable()
    {
        AnimTestSkillFxSet.SkillChanged -= OnSkillChanged;
        ClearAll();
        _lastStateHash = 0;
        _animator.speed = 1f;
    }

    private void OnDestroy()
    {
        if (_enemyPoint != null) Destroy(_enemyPoint.gameObject);
    }

    private void OnSkillChanged(AnimTestSkill skill)
    {
        ClearAll();
        _animator.speed = 1f;
        // 지금 반복은 건너뛰고 다음 반복부터 새 스킬을 재생한다(동작 중간에 시작하지 않게).
        _charged = true;
        _released = true;
    }

    private void LateUpdate()
    {
        // 전환 중이면 도착할 상태 기준으로 판단한다(그 상태의 시간이 전환 시작부터 0으로 흐른다).
        AnimatorStateInfo state = _animator.IsInTransition(layer)
            ? _animator.GetNextAnimatorStateInfo(layer)
            : _animator.GetCurrentAnimatorStateInfo(layer);

        if (state.fullPathHash != _lastStateHash)
        {
            _lastStateHash = state.fullPathHash;
            _charged = false;
            _released = false;
            StopHeld();
        }

        float time = state.normalizedTime * state.length;
        if (!_charged && time >= chargeTime)
        {
            _charged = true;
            OnCharge();
        }
        if (!_released && time >= releaseTime)
        {
            _released = true;
            OnRelease();
        }

        if (UsesChargeSpeed) _animator.speed = _charged && !_released ? chargeSpeedMultiplier : 1f;
        foreach ((GameObject effect, HoldAnchor anchor) in _held)
        {
            if (effect != null) effect.transform.position = HoldPoint(anchor);
        }
    }

    private void OnCharge()
    {
        AnimTestSkillFxSet set = Set;
        if (set == null) return;
        switch (set.Current)
        {
            case AnimTestSkill.Heal4010:
                Hold(set.HealCharge, HoldAnchor.Orb);
                break;
            case AnimTestSkill.PawForYou4020:
                // charge Cue(Anchor=Caster): 생성부터 비행·적중까지 이펙트가 스스로 진행한다.
                PlayEffect(set.PawForYouCast, transform.position, EnemyPoint());
                break;
            case AnimTestSkill.FightingLove4030:
                // charge_r·charge_l Cue(Socket=WeaponVfxRight·WeaponVfx): 양손 무기 소켓에 구체.
                Hold(set.FightingLoveCharge, HoldAnchor.RightWeapon);
                Hold(set.FightingLoveCharge, HoldAnchor.LeftWeapon);
                break;
            case AnimTestSkill.Taosenaiyo4040:
                // revive Cue(Anchor=ReviveTarget)와 tao_circle Cue(Anchor=Caster).
                if (target != null) PlayEffect(set.TaoRevive, target.position, target);
                PlayEffect(set.TaoCircle, transform.position, EnemyPoint());
                break;
        }
    }

    private void OnRelease()
    {
        AnimTestSkillFxSet set = Set;
        if (set == null) return;
        switch (set.Current)
        {
            case AnimTestSkill.Heal4010:
            {
                Vector3 start = _held.Count > 0 && _held[0].effect != null ? _held[0].effect.transform.position : HoldPoint(HoldAnchor.Orb);
                StopHeld();
                Launch(set.HealProjectile, start, set.HealHit);
                break;
            }
            case AnimTestSkill.FightingLove4030:
            {
                // parts_lfl_charge_off_0·1 → parts_merge(Socket=RightWeapon): 손 구체를 끄고 오른손 무기 소켓에서 합체한다.
                // 합체가 끝나면 투사체를 쏜다.
                StopHeld();
                Vector3 mergePoint = HoldPoint(HoldAnchor.RightWeapon);
                VfxEffect merge = PlayEffect(set.FightingLoveMerge, mergePoint, target);
                GameObject projectile = set.FightingLoveProjectile;
                if (merge == null)
                {
                    Launch(projectile, mergePoint, null);
                    break;
                }
                int epoch = _epoch;
                merge.OnFinished += _ =>
                {
                    if (epoch == _epoch && isActiveAndEnabled) Launch(projectile, mergePoint, null);
                };
                break;
            }
            case AnimTestSkill.Taosenaiyo4040:
                // fire Cue(Anchor=Caster): 적 목록을 받아 연사한다.
                PlayEffect(set.TaoBarrage, transform.position, EnemyPoint(), true);
                break;
        }
    }

    private void Hold(GameObject prefab, HoldAnchor anchor)
    {
        if (prefab == null) return;
        GameObject effect = Instantiate(prefab, HoldPoint(anchor), Quaternion.identity);
        ChargeOrbVfx orb = effect.GetComponent<ChargeOrbVfx>();
        if (orb != null)
        {
            FollowHandsCenterField?.SetValue(orb, false);
            orb.Play();
        }
        _held.Add((effect, anchor));
    }

    // 전투에서 충전 핸들을 정리하는 것과 같다(JcVfxPresentationAdapter.Stop: 이펙트 정지 후 파괴).
    private void StopHeld()
    {
        foreach ((GameObject effect, HoldAnchor anchor) held in _held) StopAndDestroy(held.effect);
        _held.Clear();
    }

    private static void StopAndDestroy(GameObject effect)
    {
        if (effect == null) return;
        JcVfxPresentationAdapter adapter = effect.GetComponent<JcVfxPresentationAdapter>();
        if (adapter != null)
        {
            adapter.Stop();
            return;
        }
        effect.GetComponent<VfxEffect>()?.Stop();
        Destroy(effect);
    }

    // 시전자(이 모델)를 origin으로 넘겨 재생한다. 정리는 프리팹의 JcVfxPresentationAdapter가 재생 종료 시 한다.
    private VfxEffect PlayEffect(GameObject prefab, Vector3 position, Transform aim, bool aimAsTargets = false)
    {
        if (prefab == null || aim == null) return null;
        GameObject instance = Instantiate(prefab, position, transform.rotation);
        _spawned.Add(instance);
        VfxEffect effect = instance.GetComponent<VfxEffect>();
        if (effect == null) return null;
        if (aimAsTargets) effect.SetTargets(new[] { VfxTarget.Of(aim) });
        effect.Play(transform, aim);
        return effect;
    }

    private void Launch(GameObject prefab, Vector3 start, GameObject hitPrefab)
    {
        if (prefab == null || target == null) return;
        GameObject projectile = Instantiate(prefab, start, Quaternion.identity);
        _spawned.Add(projectile);
        projectile.GetComponent<ProjectileVfx>()?.Show(start);
        StartCoroutine(Fly(projectile, start, hitPrefab));
    }

    // ProjectileImpactAction + ArcTrajectory와 같은 공식: 직선 보간 + arcHeight·4t(1−t),
    // 비행 시간 = 발사 시점 거리 / 속도, 끝점은 대상 현재 위치를 따라간다(TrackTarget).
    private IEnumerator Fly(GameObject projectile, Vector3 start, GameObject hitPrefab)
    {
        Transform tr = projectile.transform;
        float duration = Mathf.Max(0.0001f, Vector3.Distance(start, target.position) / projectileSpeed);
        Vector3 last = start;
        float t = 0f;
        while (t < 1f)
        {
            yield return null;
            if (projectile == null) yield break;
            t = Mathf.Min(1f, t + Time.deltaTime / duration);
            Vector3 end = target != null ? target.position : last;
            Vector3 position = Vector3.Lerp(start, end, t);
            position.y += arcHeight * 4f * t * (1f - t);
            if ((position - last).sqrMagnitude > 1e-6f) tr.rotation = Quaternion.LookRotation(position - last);
            tr.position = position;
            last = position;
        }

        if (hitPrefab != null) SpawnHit(hitPrefab, last);
        projectile.GetComponent<ProjectileVfx>()?.BurstAt(last);
        yield return FadeOutPaw(projectile);
        _spawned.Remove(projectile);
        if (projectile != null) Destroy(projectile);
    }

    // 전투에서는 ProjectileImpactAction.FlightFinished를 받아 NekoHealPawProjectile이 하는 착탄 페이드(0.2초, 1.15배)를 대신한다.
    private IEnumerator FadeOutPaw(GameObject projectile)
    {
        NekoHealPawProjectile flight = projectile.GetComponent<NekoHealPawProjectile>();
        PawSprite paw = flight != null ? flight.paw : null;
        if (flight != null) flight.enabled = false;
        for (float elapsed = 0f; elapsed < impactVisualLifetime; elapsed += Time.deltaTime)
        {
            if (projectile == null) yield break;
            if (paw != null)
            {
                float k = Mathf.Clamp01(elapsed / 0.2f);
                paw.SetBasePosition(projectile.transform.position);
                paw.SetEnvelope(1f - Mathf.SmoothStep(0f, 1f, k));
                paw.SetScaleMul(Mathf.Lerp(1f, 1.15f, k));
            }
            yield return null;
        }
    }

    // BattleVisualDirector.PlayHitEffect와 같다: 대상 위치에 만들고 HealOrbitVfx면 대상 발밑 좌표로 재생.
    // 정리는 프리팹의 JcVfxPresentationAdapter가 재생 종료 시 한다.
    private void SpawnHit(GameObject hitPrefab, Vector3 point)
    {
        Vector3 at = target != null ? target.position : point;
        GameObject hit = Instantiate(hitPrefab, at, target != null ? target.rotation : Quaternion.identity);
        _spawned.Add(hit);
        HealOrbitVfx heal = hit.GetComponent<HealOrbitVfx>();
        if (heal != null) heal.Play(at);
        else hit.GetComponent<VfxEffect>()?.Play();
    }

    private void ClearAll()
    {
        _epoch++;
        StopAllCoroutines();
        StopHeld();
        foreach (GameObject effect in _spawned)
        {
            if (effect != null) Destroy(effect);
        }
        _spawned.Clear();
    }

    // 적 지점은 Play 중에만 만드는 임시 오브젝트다(씬에 저장되지 않는다).
    private Transform EnemyPoint()
    {
        if (_enemyPoint == null) _enemyPoint = new GameObject("AnimTestEnemyPoint").transform;
        Vector3 forward = ModelForward();
        _enemyPoint.SetPositionAndRotation(EnemyPosition(forward), Quaternion.LookRotation(-forward));
        return _enemyPoint;
    }

    private Vector3 ModelForward()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
    }

    private Vector3 EnemyPosition(Vector3 forward) =>
        (target != null ? target.position : transform.position) + forward * enemyDistance;

    private Vector3 HoldPoint(HoldAnchor anchor)
    {
        switch (anchor)
        {
            case HoldAnchor.RightWeapon: return WeaponPoint(_rightHand, Set != null ? Set.RightWeaponLocal : Vector3.zero);
            case HoldAnchor.LeftWeapon: return WeaponPoint(_leftHand, Set != null ? Set.LeftWeaponLocal : Vector3.zero);
            default: return orbAtHandsCenter ? HandsCenter() : Palm(_rightHand, _rightLowerArm);
        }
    }

    // 전투 프리팹에서 손 뼈 아래에 붙은 무기 소켓과 같은 로컬 위치(같은 FBX라 손 뼈 배율도 같다).
    private Vector3 WeaponPoint(Transform hand, Vector3 local) => hand != null ? hand.TransformPoint(local) : transform.position;

    private Vector3 HandsCenter() =>
        ChargeOrbVfx.TryGetHandsCenter(_animator, out Vector3 center) ? center : Palm(_rightHand, _rightLowerArm);

    private Vector3 Palm(Transform hand, Transform lowerArm)
    {
        if (hand == null) return transform.position;
        Vector3 forearm = lowerArm != null ? (hand.position - lowerArm.position).normalized : Vector3.zero;
        return hand.position + forearm * palmOffset;
    }

    private void OnDrawGizmos()
    {
        if (target == null) return;
        Gizmos.color = new Color(0.4f, 1f, 0.6f);
        Gizmos.DrawWireSphere(target.position, 0.25f);
        Gizmos.color = new Color(1f, 0.5f, 0.4f);
        Gizmos.DrawWireCube(EnemyPosition(ModelForward()), Vector3.one * 0.3f);
    }
}
