using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 절망의 굴렁쇠(FV40005_1) 광역 커스텀 연출: 충전 → 대상 앞까지 돌진(궤적) → 행 전체 피격 확정.
/// Animator가 없어 기본 광역 시퀀서(Timeline/Cue) 대신 이 레일을 쓴다. 원본: JcYuliaSkillPresentation.Run.
/// <list type="bullet">
/// <item>피해 확정은 applyHits(기존 hit callback 계약) 한 번뿐이다. 연출은 시점만 정한다.</item>
/// <item>피격까지 갔으면 도착 위치를 유지한다 — 이어지는 반격·자해(OnPostExecution) 후 HoopWheelVisual이 생존 시 되돌린다.
///   여기서는 아직 자해 전이므로 생존 여부를 판단하지 않는다.</item>
/// <item>논리 셀(그리드 점유)은 바꾸지 않는다. 루트 transform만 옮긴다.</item>
/// </list>
/// </summary>
public sealed class HoopDashPresentationSequence : IAoESkillPresentationSequence
{
    private const float FallbackWheelHeight = .72f;

    public IEnumerator Run(
        BattleManager battleManager,
        BattleCharactor actor,
        BattleCharactor primaryTarget,
        SkillData skill,
        IReadOnlyList<BattleCharactor> targets,
        Action applyHits)
    {
        HoopWheelVisual visual = actor != null ? actor.GetComponent<HoopWheelVisual>() : null;
        HoopVfxPreset preset = visual != null && visual.isActiveAndEnabled ? visual.Preset : null;
        if (actor == null || preset == null)
        {
            // 외형 컴포넌트가 없으면(다른 씬·미연결) 연출 없이 피해만 확정한다.
            applyHits?.Invoke();
            yield break;
        }

        Vector3 home = actor.transform.position;
        Vector3 end = ResolveDashEnd(home, primaryTarget, preset.stopDistance);
        float trailHeight = visual.WheelCenter != actor.transform
            ? visual.WheelCenter.position.y - home.y
            : FallbackWheelHeight;

        TrailRenderer trail = null;
        JC.VFX.VfxEffect arc = null;
        Transform arcTail = null;
        bool hitDelivered = false;
        try
        {
            HoopVfxEffect.Spawn(preset, HoopEffectKind.Charge, visual.WheelCenter.position);
            yield return Wait(battleManager, preset.chargeSeconds);

            if (preset.dashArcPrefab != null)
            {
                // 트레일 대신 뒤쪽에 전기 아크: 앞 끝점은 바퀴, 뒤 끝점은 진행도를 뒤따르는 지점.
                arcTail = new GameObject("HoopDashArcTail").transform;
                arcTail.position = home + Vector3.up * trailHeight;
                GameObject arcObject = UnityEngine.Object.Instantiate(preset.dashArcPrefab, arcTail.position, Quaternion.identity);
                arc = arcObject.GetComponent<JC.VFX.VfxEffect>();
                if (arc != null) arc.Play(visual.WheelCenter, arcTail);
                else EffectFallbackRelease.Ensure(arcObject);
            }
            else
            {
                trail = CreateTrail(preset, home + Vector3.up * trailHeight, battleManager);
            }

            float elapsed = 0f;
            while (elapsed < preset.dashSeconds && actor != null)
            {
                elapsed += Time.deltaTime * BattleSpeed(battleManager);
                float progress = Mathf.Clamp01(elapsed / preset.dashSeconds);
                actor.transform.position = Vector3.Lerp(home, end, progress);
                if (arcTail != null)
                {
                    arcTail.position = ResolveArcTail(home, end, progress, preset.dashArcLag) + Vector3.up * trailHeight;
                }
                if (trail != null)
                {
                    trail.time = preset.trailSeconds / BattleSpeed(battleManager);
                    trail.transform.position = actor.transform.position + Vector3.up * trailHeight;
                }
                yield return null;
            }

            // 도착하면 이동을 멈추고 뒤쪽 아크를 먼저 소거한다(기획 Y09).
            StopArc(ref arc, ref arcTail);
            yield return Wait(battleManager, preset.impactDelaySeconds);
            applyHits?.Invoke();
            hitDelivered = true;
            yield return Wait(battleManager, preset.afterImpactSeconds);
        }
        finally
        {
            StopArc(ref arc, ref arcTail);
            if (trail != null)
            {
                trail.emitting = false;
                UnityEngine.Object.Destroy(trail.gameObject, trail.time + .05f);
            }

            if (actor != null)
            {
                if (hitDelivered && visual != null)
                {
                    visual.BeginDashHold(home);
                }
                else
                {
                    actor.transform.position = home;
                }
            }
        }
    }

    /// <summary>대상 위치에서 stopDistance만큼 앞(시전자 쪽). 높이는 시전자 기준으로 고정한다.</summary>
    internal static Vector3 ResolveDashEnd(Vector3 home, BattleCharactor primaryTarget, float stopDistance)
    {
        if (primaryTarget == null) return home;
        Vector3 targetPosition = primaryTarget.transform.position;
        targetPosition.y = home.y;
        Vector3 toTarget = targetPosition - home;
        float distance = toTarget.magnitude;
        if (distance <= 1e-4f) return home;
        float travel = Mathf.Max(0f, distance - Mathf.Max(0f, stopDistance));
        return home + toTarget / distance * travel;
    }

    /// <summary>아크 뒤쪽 끝점: 돌진 진행도에서 lag만큼 뒤처진 지점(높이 제외).</summary>
    internal static Vector3 ResolveArcTail(Vector3 home, Vector3 end, float progress, float lag)
    {
        return Vector3.Lerp(home, end, Mathf.Max(0f, progress - Mathf.Clamp01(lag)));
    }

    private static void StopArc(ref JC.VFX.VfxEffect arc, ref Transform arcTail)
    {
        if (arc != null) arc.Stop();
        arc = null;
        // 끝점은 아크가 우아하게 사라질 동안 남겨 둔다(ElectricArcEndpoints는 끝점이 사라지면 정지한다).
        if (arcTail != null) UnityEngine.Object.Destroy(arcTail.gameObject, 1.5f);
        arcTail = null;
    }

    private static TrailRenderer CreateTrail(HoopVfxPreset preset, Vector3 position, BattleManager battleManager)
    {
        var go = new GameObject("HoopDashTrail");
        go.transform.position = position;
        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = preset.energyMaterial;
        trail.time = preset.trailSeconds / BattleSpeed(battleManager);
        trail.minVertexDistance = .03f;
        trail.widthMultiplier = preset.lineWidth * 2f;
        trail.numCapVertices = 4;
        trail.startColor = preset.core;
        Color end = preset.energy;
        end.a = 0f;
        trail.endColor = end;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        return trail;
    }

    private static float BattleSpeed(BattleManager battleManager)
    {
        return battleManager != null ? Mathf.Max(.01f, battleManager.CurrentBattleSpeed) : 1f;
    }

    private static IEnumerator Wait(BattleManager battleManager, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime * BattleSpeed(battleManager))
        {
            yield return null;
        }
    }
}
