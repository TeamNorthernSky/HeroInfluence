// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Unit/MadonnaSkillReceiveTimeline.cs
// 원본 객체: MadonnaSkillReceiveTimeline -> JcMadonnaSkillReceiveTimeline
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
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
public sealed class JcMadonnaSkillReceiveTimeline : MonoBehaviour
{
    [Tooltip("율리아가 예약 스킬을 받았을 때 표시하는 원본 Timeline 연출입니다.")]
    [SerializeField] private TimelineAsset _timeline;
    [Tooltip("JC 전투의 페이즈와 율리아 스킬 예약을 제공하는 상태 객체입니다.")]
    [SerializeField] private JcEncounterBlackboard _blackboard;

    private BattleCharactor _boss;
    private JcBattleFlowManager _flow;
    private PlayableDirector _director;
    private Coroutine _speedRoutine;
    private IDisposable _flowLock;

    // 원본 함수 대응: MadonnaSkillReceiveTimeline.Awake (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MadonnaSkillReceiveTimeline.cs)

    private void Awake()
    {
        _boss = GetComponent<BattleCharactor>();
    }

    // 원본 함수 대응: MadonnaSkillReceiveTimeline.OnEnable (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MadonnaSkillReceiveTimeline.cs)

    private void OnEnable()
    {
        Subscribe();
    }

    // 원본 함수 대응: MadonnaSkillReceiveTimeline.Start (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MadonnaSkillReceiveTimeline.cs)

    private void Start()
    {
        // 씬의 Blackboard/Flow가 프리팹보다 늦게 활성화된 경우 한 번 더 연결한다.
        Subscribe();
    }

    // 원본 함수 대응: MadonnaSkillReceiveTimeline.OnDisable (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MadonnaSkillReceiveTimeline.cs)

    private void OnDisable()
    {
        if (_blackboard != null) _blackboard.YuliaSkillReserved -= PlayReceive;
        if (_flow != null) _flow.OnTurnStarted -= HandleTurnStarted;
        StopReceive();
    }

    // 원본 함수 대응: MadonnaSkillReceiveTimeline.Subscribe (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MadonnaSkillReceiveTimeline.cs)

    private void Subscribe()
    {
        if (_blackboard == null) _blackboard = FindFirstObjectByType<JcEncounterBlackboard>();
        if (_blackboard != null)
        {
            _blackboard.YuliaSkillReserved -= PlayReceive;
            _blackboard.YuliaSkillReserved += PlayReceive;
        }

        if (_flow == null) _flow = FindFirstObjectByType<JcBattleFlowManager>();
        if (_flow != null)
        {
            _flow.OnTurnStarted -= HandleTurnStarted;
            _flow.OnTurnStarted += HandleTurnStarted;
        }
    }

    // 원본 함수 대응: MadonnaSkillReceiveTimeline.PlayReceive (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MadonnaSkillReceiveTimeline.cs)

    private void PlayReceive(int socket, BattleCharactor source)
    {
        if (_timeline == null || _boss == null || _boss.IsDead) return;

        Animator animator = _boss.Anim != null ? _boss.Anim.Animator : GetComponentInChildren<Animator>();
        if (animator == null) return;
        if (_flow == null) _flow = FindFirstObjectByType<JcBattleFlowManager>();

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

    // 원본 함수 대응: MadonnaSkillReceiveTimeline.ApplyTimelineSpeed (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MadonnaSkillReceiveTimeline.cs)

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

    // 원본 함수 대응: MadonnaSkillReceiveTimeline.HandleTurnStarted (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MadonnaSkillReceiveTimeline.cs)

    private void HandleTurnStarted(int roundIndex, BattleCharactor current)
    {
        // 보스의 실제 스킬 연출이 시작되면 흡수 포즈가 Animator를 덮지 않도록 한다.
        if (current == _boss) StopReceive();
    }

    // 원본 함수 대응: MadonnaSkillReceiveTimeline.StopReceive (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MadonnaSkillReceiveTimeline.cs)

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

    // 원본 함수 대응: MadonnaSkillReceiveTimeline.ReleaseFlowLock (Assets/_ProtoType_Merge/ASB/Scripts/Unit/MadonnaSkillReceiveTimeline.cs)

    private void ReleaseFlowLock()
    {
        _flowLock?.Dispose();
        _flowLock = null;
    }
}
