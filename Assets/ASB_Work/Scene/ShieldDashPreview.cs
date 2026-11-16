using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// 테스트 씬 전용 방패 돌진 미리보기.
/// 스킬 Timeline의 Move/Return 마커 시간·곡선과 돌진 Cue 시간을 그대로 읽어,
/// 전투(SkillPresentationDirector)와 같은 계산으로 대상 앞까지 미끄러져 갔다가 돌아오기를 반복한다.
/// 전투 코드(BattleCharactor 등)는 쓰지 않는다.
/// </summary>
public sealed class ShieldDashPreview : MonoBehaviour
{
    [SerializeField] private TimelineAsset skillTimeline;
    [SerializeField] private Transform target;
    [SerializeField] private ParticleSystem dashEffect;
    [SerializeField] private string dashCue = "shield_bash_dash";
    [SerializeField] private string dashStopCue = "shield_bash_dash_stop";
    [Tooltip("대상 중심에서 멈추는 거리. 전투 프리팹 UnitMovementProfile.ApproachDistance 값.")]
    [SerializeField, Min(0f)] private float approachDistance = 0.95f;
    [Tooltip("1 = 전투 1배속.")]
    [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
    [Tooltip("한 번 끝나고 다음 돌진까지 기다리는 실제 시간(초).")]
    [SerializeField, Min(0f)] private float loopInterval = 1f;
    [SerializeField] private bool loop = true;

    private Vector3 originPosition;
    private Quaternion originRotation;
    private bool dashPlaying;

    private void OnEnable()
    {
        originPosition = transform.position;
        originRotation = transform.rotation;
        StopDashEffect();
        StartCoroutine(Run());
    }

    private void OnDisable()
    {
        StopDashEffect();
        transform.SetPositionAndRotation(originPosition, originRotation);
    }

    private IEnumerator Run()
    {
        if (!TryReadTimeline(out Plan plan))
        {
            Debug.LogWarning("[ShieldDashPreview] Timeline에서 Move/Return 마커를 찾지 못했습니다.", this);
            yield break;
        }
        if (target == null)
        {
            Debug.LogWarning("[ShieldDashPreview] 대상(target)이 비어 있습니다.", this);
            yield break;
        }

        do
        {
            transform.SetPositionAndRotation(originPosition, originRotation);
            bool dashStarted = false, dashStopped = false, moveStarted = false, returnStarted = false;
            Vector3 moveFrom = originPosition, moveTo = originPosition, returnFrom = originPosition;
            Quaternion moveFromRot = originRotation, moveToRot = originRotation, returnFromRot = originRotation;
            float time = 0f;

            while (true)
            {
                if (!dashStarted && plan.DashOn >= 0f && time >= plan.DashOn)
                {
                    dashStarted = true;
                    PlayDashEffect();
                }
                if (dashStarted && !dashStopped && plan.DashOff >= 0f && time >= plan.DashOff)
                {
                    dashStopped = true;
                    StopDashEffect();
                }

                if (time >= plan.MoveStart.time && time < plan.ReturnStart.time)
                {
                    if (!moveStarted)
                    {
                        moveStarted = true;
                        moveFrom = transform.position;
                        moveFromRot = transform.rotation;
                        Vector3 away = moveFrom - target.position;
                        away.y = 0f;
                        if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
                        away.Normalize();
                        moveTo = target.position + away * approachDistance;
                        moveTo.y = moveFrom.y;
                        moveToRot = Quaternion.LookRotation(-away, Vector3.up);
                    }
                    float t = Progress(time, plan.MoveStart.time, plan.MoveEnd.time);
                    transform.SetPositionAndRotation(
                        Vector3.Lerp(moveFrom, moveTo, plan.MoveStart.EvaluatePosition(t)),
                        Quaternion.Slerp(moveFromRot, moveToRot, plan.MoveStart.EvaluateRotation(t)));
                }
                else if (time >= plan.ReturnStart.time)
                {
                    if (!returnStarted)
                    {
                        returnStarted = true;
                        returnFrom = transform.position;
                        returnFromRot = transform.rotation;
                    }
                    float t = Progress(time, plan.ReturnStart.time, plan.ReturnEnd.time);
                    transform.SetPositionAndRotation(
                        Vector3.Lerp(returnFrom, originPosition, plan.ReturnStart.EvaluatePosition(t)),
                        Quaternion.Slerp(returnFromRot, originRotation, t));
                }

                if (time >= plan.Duration) break;
                yield return null;
                time += Time.deltaTime * playbackSpeed;
            }

            StopDashEffect();
            transform.SetPositionAndRotation(originPosition, originRotation);
            if (loopInterval > 0f) yield return new WaitForSeconds(loopInterval);
            else yield return null;
        } while (loop);
    }

    private static float Progress(float time, double start, double end)
    {
        double length = end - start;
        return length <= 0d ? 1f : Mathf.Clamp01((float)((time - start) / length));
    }

    private void PlayDashEffect()
    {
        if (dashEffect == null) return;
        foreach (ParticleSystem particle in dashEffect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particle.main;
            main.simulationSpeed = playbackSpeed;
        }
        dashEffect.Clear(true);
        dashEffect.Play(true);
        dashPlaying = true;
    }

    private void StopDashEffect()
    {
        if (dashEffect == null) return;
        if (!dashPlaying && !dashEffect.isPlaying) return;
        // 전투의 ShieldBashParticleEffect.Stop과 같이 즉시 지운다.
        dashEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        dashPlaying = false;
    }

    private struct Plan
    {
        public PresentationMoveMarker MoveStart, MoveEnd;
        public PresentationReturnMarker ReturnStart, ReturnEnd;
        public float DashOn, DashOff, Duration;
    }

    private bool TryReadTimeline(out Plan plan)
    {
        plan = new Plan { DashOn = -1f, DashOff = -1f };
        if (skillTimeline == null) return false;

        var tracks = new HashSet<TrackAsset>(skillTimeline.GetOutputTracks());
        if (skillTimeline.markerTrack != null) tracks.Add(skillTimeline.markerTrack);
        foreach (TrackAsset track in tracks)
        {
            foreach (IMarker marker in track.GetMarkers())
            {
                switch (marker)
                {
                    case PresentationMoveMarker move:
                        if (move.Boundary == PresentationSectionBoundary.Start) plan.MoveStart = move;
                        else plan.MoveEnd = move;
                        break;
                    case PresentationReturnMarker ret:
                        if (ret.Boundary == PresentationSectionBoundary.Start) plan.ReturnStart = ret;
                        else plan.ReturnEnd = ret;
                        break;
                    case PresentationSignalMarker signal when signal.Kind == PresentationSignalKind.Cue:
                        if (signal.CueName == dashCue) plan.DashOn = (float)signal.time;
                        else if (signal.CueName == dashStopCue) plan.DashOff = (float)signal.time;
                        break;
                }
            }
        }
        plan.Duration = (float)skillTimeline.duration;
        return plan.MoveStart != null && plan.MoveEnd != null && plan.ReturnStart != null && plan.ReturnEnd != null;
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Vector3 from = Application.isPlaying ? originPosition : transform.position;
        Vector3 away = from - target.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) return;
        Vector3 stop = target.position + away.normalized * approachDistance;
        stop.y = from.y;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(from, stop);
        Gizmos.DrawWireSphere(stop, 0.2f);
    }
}
