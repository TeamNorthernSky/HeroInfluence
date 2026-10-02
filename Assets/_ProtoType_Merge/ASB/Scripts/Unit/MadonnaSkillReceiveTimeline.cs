using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 증폭기의 능력개방 예약이 성공한 순간 마돈나의 팔 전용 흡수 Timeline을 재생한다.
/// 스킬 시전 Timeline은 보스 루트의 Director를 사용하므로 이 반응용 Director는 자식에 둔다.
/// </summary>
[RequireComponent(typeof(BattleCharactor))]
public sealed class MadonnaSkillReceiveTimeline : MonoBehaviour
{
    [SerializeField] private TimelineAsset _timeline;
    [SerializeField] private EncounterBlackboard _blackboard;

    private BattleCharactor _boss;
    private BattleFlowManager _flow;
    private PlayableDirector _director;
    private Coroutine _speedRoutine;
    private IDisposable _flowLock;

    private void Awake()
    {
        _boss = GetComponent<BattleCharactor>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        // 씬의 Blackboard/Flow가 프리팹보다 늦게 활성화된 경우 한 번 더 연결한다.
        Subscribe();
    }

    private void OnDisable()
    {
        if (_blackboard != null) _blackboard.YuliaSkillReserved -= PlayReceive;
        if (_flow != null) _flow.OnTurnStarted -= HandleTurnStarted;
        StopReceive();
    }

    private void Subscribe()
    {
        if (_blackboard == null) _blackboard = FindFirstObjectByType<EncounterBlackboard>();
        if (_blackboard != null)
        {
            _blackboard.YuliaSkillReserved -= PlayReceive;
            _blackboard.YuliaSkillReserved += PlayReceive;
        }

        if (_flow == null) _flow = FindFirstObjectByType<BattleFlowManager>();
        if (_flow != null)
        {
            _flow.OnTurnStarted -= HandleTurnStarted;
            _flow.OnTurnStarted += HandleTurnStarted;
        }
    }

    private void PlayReceive(int socket, BattleCharactor source)
    {
        if (_timeline == null || _boss == null || _boss.IsDead) return;

        Animator animator = _boss.Anim != null ? _boss.Anim.Animator : GetComponentInChildren<Animator>();
        if (animator == null) return;
        if (_flow == null) _flow = FindFirstObjectByType<BattleFlowManager>();

        StopReceive();
        if (_director == null)
        {
            var player = new GameObject("SkillReceiveTimelinePlayer");
            player.transform.SetParent(transform, false);
            _director = player.AddComponent<PlayableDirector>();
            _director.playOnAwake = false;
        }

        _director.playableAsset = _timeline;
        _director.extrapolationMode = DirectorWrapMode.None;
        foreach (TrackAsset track in _timeline.GetOutputTracks())
        {
            if (track is AnimationTrack) _director.SetGenericBinding(track, animator);
        }

        _director.time = 0d;
        if (_flow != null) _flowLock = _flow.AcquireFlowLock(this, "마돈나 스킬 흡수");
        try
        {
            _director.Play();
            _speedRoutine = StartCoroutine(ApplyTimelineSpeed());
        }
        catch (Exception exception)
        {
            StopReceive();
            Debug.LogException(exception, this);
        }
    }

    private IEnumerator ApplyTimelineSpeed()
    {
        while (_director != null && _director.state == PlayState.Playing)
        {
            PlayableGraph graph = _director.playableGraph;
            if (graph.IsValid() && graph.GetRootPlayableCount() > 0)
            {
                graph.GetRootPlayable(0).SetSpeed(
                    Mathf.Max(0f, PresentationTimelineSpeed.SpeedAt(_timeline, _director.time)));
            }
            yield return null;
        }
        _speedRoutine = null;
        ReleaseFlowLock();
    }

    private void HandleTurnStarted(int roundIndex, BattleCharactor current)
    {
        // 보스의 실제 스킬 연출이 시작되면 흡수 포즈가 Animator를 덮지 않도록 한다.
        if (current == _boss) StopReceive();
    }

    private void StopReceive()
    {
        if (_speedRoutine != null)
        {
            StopCoroutine(_speedRoutine);
            _speedRoutine = null;
        }
        if (_director != null && _director.state == PlayState.Playing) _director.Stop();
        ReleaseFlowLock();
    }

    private void ReleaseFlowLock()
    {
        _flowLock?.Dispose();
        _flowLock = null;
    }
}
