using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

/// <summary>실제 PlayableDirector 그래프에서 SFX 마커의 시작·일시정지·정지를 확인한다.</summary>
public class PresentationSfxMarkerPlayModeTests
{
    [UnityTest]
    public IEnumerator Director_StartPauseStopAndReplay_RoutesSfxAtMostOncePerPlayback()
    {
        Type markerType = FindType("PresentationSfxMarker");
        Type receiverType = FindType("PresentationSignalReceiver");
        FieldInfo sinkField = FindType("PresentationSfxPlayer").GetField(
            "SinkOverride", BindingFlags.NonPublic | BindingFlags.Static);
        object previousSink = sinkField.GetValue(null);
        var heard = new List<string>();
        var actor = new GameObject("SfxTimelinePlayModeTest");
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        try
        {
            sinkField.SetValue(null, new Action<string>(key => heard.Add(key)));
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = 0.7d;
            timeline.CreateMarkerTrack();
            AddMarker(timeline, markerType, 0d, "Start");
            AddMarker(timeline, markerType, 0.15d, "Middle");
            AddMarker(timeline, markerType, 0.55d, "AfterStop");

            var receiver = (MonoBehaviour)actor.AddComponent(receiverType);
            var director = actor.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            Configure(receiverType, receiver);
            director.Play();
            director.time = 0d;
            director.Evaluate();
            yield return null;
            Assert.That(heard.Count(key => key == "Start"), Is.EqualTo(1));

            director.Pause();
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(heard, Is.EquivalentTo(new[] { "Start" }));

            director.Resume();
            float deadline = Time.realtimeSinceStartup + 0.4f;
            while (!heard.Contains("Middle") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(heard.Count(key => key == "Middle"), Is.EqualTo(1));

            director.Stop();
            receiverType.GetMethod("ClearConfig").Invoke(receiver, null);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(heard, Does.Not.Contain("AfterStop"));

            Configure(receiverType, receiver);
            director.Play();
            director.time = 0d;
            director.Evaluate();
            yield return null;
            Assert.That(heard.Count(key => key == "Start"), Is.EqualTo(2));

            // 광역 경로는 Evaluate()를 Play()보다 먼저 호출한다.
            director.Stop();
            receiverType.GetMethod("ClearConfig").Invoke(receiver, null);
            Configure(receiverType, receiver);
            director.time = 0d;
            director.Evaluate();
            director.Play();
            yield return null;
            Assert.That(heard.Count(key => key == "Start"), Is.EqualTo(3));
        }
        finally
        {
            sinkField.SetValue(null, previousSink);
            UnityEngine.Object.Destroy(actor);
            UnityEngine.Object.Destroy(timeline);
        }
    }

    private static void AddMarker(TimelineAsset timeline, Type markerType, double time, string key)
    {
        MethodInfo create = typeof(TrackAsset).GetMethods()
            .Single(method => method.Name == "CreateMarker" && method.IsGenericMethodDefinition);
        object marker = create.MakeGenericMethod(markerType).Invoke(timeline.markerTrack, new object[] { time });
        markerType.GetMethod("Configure").Invoke(marker, new object[] { key });
    }

    private static void Configure(Type receiverType, MonoBehaviour receiver)
    {
        receiverType.GetMethods().Single(method => method.Name == "Configure" &&
            method.GetParameters().Length == 5).Invoke(receiver,
            new object[] { null, null, null, 0d, 0.7d });
    }

    private static Type FindType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"타입 '{name}'을 찾지 못했습니다.");
        return type;
    }
}
