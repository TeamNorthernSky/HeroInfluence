using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Debug = UnityEngine.Debug;

/// <summary>
/// 전투 시작 직후(로딩 화면이 덮여 있는 동안) 이번 전투 유닛들의 스킬 Timeline 재생 그래프를
/// 숨긴 PlayableDirector에서 한 번씩 만들었다 지운다.
///
/// 이유: Mono는 System.Object를 바로 부모로 둔 제네릭 클래스(Timeline 패키지의 IntervalTree&lt;T&gt; 등)를
/// 처음 쓸 때 타입 정보 템플릿을 만드느라 에디터에서 약 0.15초 멈춘다. 그 멈춤이 첫 시전 중에 생기면
/// 그 프레임 시간만큼 Timeline이 건너뛴다. 그래프를 미리 한 번 만들어 두면 그 비용이 로딩 중에 끝난다.
///
/// 캐릭터에 연결하지 않고 재생·평가도 하지 않으므로 소리·이펙트·마커는 발화하지 않는다.
/// 실패해도 전투 진입을 막지 않는다(경고만 남긴다).
/// </summary>
public static class PresentationTimelineWarmup
{
    public static void Run(SkillPresentationCatalog catalog, IReadOnlyList<BattleCharactor> units)
    {
        if (catalog == null || units == null) return;

        var stopwatch = Stopwatch.StartNew();
        var timelines = new List<TimelineAsset>();
        var seen = new HashSet<TimelineAsset>();
        try
        {
            foreach (BattleCharactor unit in units)
            {
                if (unit == null) continue;
                AddSkillTimeline(catalog, unit, unit.SelectedSkillData, timelines, seen);
                foreach (SkillData skill in unit.availableSkills)
                    AddSkillTimeline(catalog, unit, skill, timelines, seen);
                foreach (SkillData skill in unit.ClassSkillSlots)
                    AddSkillTimeline(catalog, unit, skill, timelines, seen);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[TimelineWarmup] 스킬 Timeline 수집 실패: " + exception.Message);
        }
        if (timelines.Count == 0) return;

        int built = 0;
        GameObject host = null;
        try
        {
            host = new GameObject("PresentationTimelineWarmup") { hideFlags = HideFlags.HideAndDontSave };
            var director = host.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            foreach (TimelineAsset timeline in timelines)
            {
                try
                {
                    director.playableAsset = timeline;
                    director.RebuildGraph();
                    built++;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[TimelineWarmup] " + timeline.name + " 그래프 생성 실패: " + exception.Message);
                }
            }
            director.playableAsset = null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[TimelineWarmup] 예열 실패: " + exception.Message);
        }
        finally
        {
            if (host != null) UnityEngine.Object.Destroy(host);
        }

        Debug.Log("[TimelineWarmup] 스킬 Timeline " + built + "/" + timelines.Count + "개 예열, " +
                  stopwatch.Elapsed.TotalMilliseconds.ToString("0") + "ms");
    }

    private static void AddSkillTimeline(SkillPresentationCatalog catalog, BattleCharactor unit, SkillData skill,
        List<TimelineAsset> timelines, HashSet<TimelineAsset> seen)
    {
        if (skill == null) return;
        SkillPresentationData presentation = catalog.Get(skill.skillIndex);
        if (presentation == null || presentation.AnimationRail != AnimationRail.Timeline) return;
        TimelineAsset timeline = presentation.ResolveTimeline(unit.PresentationIndex, unit.UnitName);
        if (timeline != null && seen.Add(timeline)) timelines.Add(timeline);
    }
}
