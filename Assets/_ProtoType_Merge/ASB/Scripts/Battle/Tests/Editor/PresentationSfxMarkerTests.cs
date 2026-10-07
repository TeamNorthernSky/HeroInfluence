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

/// <summary>Assembly-CSharp 런타임 타입은 asmdef 테스트에서 reflection으로 접근한다.</summary>
public class PresentationSfxMarkerTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
    private readonly List<string> _calls = new List<string>();
    private FieldInfo _sinkField;
    private object _previousSink;
    private TimelineAsset _timeline;
    private MonoBehaviour _receiver;

    private static Type MarkerType => FindType("PresentationSfxMarker");
    private static Type ReceiverType => FindType("PresentationSignalReceiver");
    private static Type ValidatorType => FindType("ASB.Work.EditorTools.Jig.SkillTimelineValidator");
    private static Type MessageType => FindType("ASB.Work.EditorTools.Jig.SkillTimelineValidationMessage");
    private static Type CatalogType => FindType("DHAudioClipCatalog");
    private static Type EntryType => FindType("DHAudioClipEntry");

    [SetUp]
    public void SetUp()
    {
        _sinkField = FindType("PresentationSfxPlayer").GetField(
            "SinkOverride", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(_sinkField, Is.Not.Null);
        _previousSink = _sinkField.GetValue(null);
        _sinkField.SetValue(null, new Action<string>(key => _calls.Add(key)));

        _timeline = Track(ScriptableObject.CreateInstance<TimelineAsset>());
        _timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        _timeline.fixedDuration = 1d;
        _timeline.CreateMarkerTrack();
        GameObject actor = Track(new GameObject("SfxReceiverTest"));
        _receiver = (MonoBehaviour)actor.AddComponent(ReceiverType);
    }

    [TearDown]
    public void TearDown()
    {
        if (_sinkField != null) _sinkField.SetValue(null, _previousSink);
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
        _calls.Clear();
    }

    [Test]
    public void Receiver_OnlyConfiguredMarkersInsideRangePlay()
    {
        object inside = AddSfx(0.3d, " Battle_Hit01 ");
        object outside = AddSfx(0.8d, "Battle_Fire01");

        Notify(inside);
        Assert.That(_calls, Is.Empty);
        Configure(0.2d, 0.5d);
        Notify(outside);
        Notify(inside);
        Assert.That(_calls, Has.Count.EqualTo(1));
        Assert.That(_calls[0], Is.EqualTo("Battle_Hit01"));
        ClearConfig();
        Notify(outside);
        Assert.That(_calls, Has.Count.EqualTo(1));
    }

    [Test]
    public void Receiver_DeduplicatesByMarkerReferenceAndResetsOnConfigure()
    {
        object first = AddSfx(0.2d, "Battle_Hit01");
        object second = AddSfx(0.3d, "Battle_Hit01");
        Configure(0d, 1d);

        Notify(first);
        Notify(first);
        Notify(second);
        Assert.That(_calls, Has.Count.EqualTo(2));

        Configure(0d, 1d);
        Notify(first);
        Assert.That(_calls, Has.Count.EqualTo(3));
    }

    [Test]
    public void Receiver_ConfigureForPlanAlsoEnablesSfxAndEmptyKeyDoesNotPlay()
    {
        object valid = AddSfx(0.2d, "Battle_Hit01");
        object empty = AddSfx(0.3d, "  ");
        ReceiverType.GetMethod("ConfigureForPlan").Invoke(_receiver,
            new object[] { null, null, null, 0d, 1d });
        Notify(valid);
        Notify(empty);
        Assert.That(_calls, Has.Count.EqualTo(1));
        Assert.That(_calls[0], Is.EqualTo("Battle_Hit01"));
    }

    [Test]
    public void Marker_ConfigureTrimsKeyAndKeepsNotificationFlags()
    {
        object marker = AddSfx(0.2d, " key ");
        Assert.That(Key(marker), Is.EqualTo("key"));
        MarkerType.GetMethod("Configure").Invoke(marker, new object[] { " Battle_Hit01 " });
        Assert.That(Key(marker), Is.EqualTo("Battle_Hit01"));
        Assert.That(((INotificationOptionProvider)marker).flags,
            Is.EqualTo(NotificationFlags.TriggerOnce | NotificationFlags.Retroactive));
    }

    [Test]
    public void Validator_ReportsEmptyBgmMissingAndCliplessKeys()
    {
        ScriptableObject catalog = NewCatalog();
        AddEntry(catalog, "bgmClips", "Battle_Bgm", Track(AudioClip.Create("bgm", 100, 1, 1000, false)));
        AddEntry(catalog, "sfxClips", "Silent_Sfx", null);
        AddSfx(0.1d, " ");
        AddSfx(0.2d, "Battle_Bgm");
        AddSfx(0.3d, "Missing_Sfx");
        AddSfx(0.4d, "Silent_Sfx");

        object messages = ValidateSfx(catalog);
        Assert.That(HasMessage(messages, "Error", "키가 비어"), Is.True);
        Assert.That(HasMessage(messages, "Warning", "BGM 키"), Is.True);
        Assert.That(HasMessage(messages, "Warning", "SfxClips에 없"), Is.True);
        Assert.That(HasMessage(messages, "Warning", "AudioClip이 없어"), Is.True);
    }

    [Test]
    public void Validator_WarnsOutsideTimelineButNotOutsideUnusedSection()
    {
        ScriptableObject catalog = NewCatalog();
        AddEntry(catalog, "sfxClips", "Battle_Hit01",
            Track(AudioClip.Create("hit", 100, 1, 1000, false)));
        AddSection(0.1d, "Attack", "Start");
        AddSection(0.4d, "Attack", "End");
        AddSfx(0.8d, " battle_hit01 ");
        AddSfx(1.2d, "Battle_Hit01");

        object messages = ValidateSfx(catalog);
        Assert.That(CountMessages(messages, "Warning", "Timeline 길이"), Is.EqualTo(1));
        Assert.That(HasMessage(messages, "Warning", "Section"), Is.False);
        Assert.That(CountMessages(messages, "Warning", "카탈로그"), Is.EqualTo(0));
    }

    [Test]
    public void Validator_MissingCatalogWarnsOnlyOnce()
    {
        AddSfx(0.2d, "Battle_Hit01");
        AddSfx(0.3d, "Battle_Fire01");

        object messages = ValidateSfx(null);
        Assert.That(CountMessages(messages, "Warning", "DHAudioClipCatalog를 찾지 못해"), Is.EqualTo(1));
    }

    [Test]
    public void Player_MissingAudioManagerWarnsOnceAndDoesNotCreateOne()
    {
        Type managerType = FindType("AudioManager");
        Type playerType = FindType("PresentationSfxPlayer");
        PropertyInfo instance = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setInstance = instance.GetSetMethod(true);
        FieldInfo warned = playerType.GetField("s_WarnedMissingManager",
            BindingFlags.NonPublic | BindingFlags.Static);
        object previousManager = instance.GetValue(null);
        object previousWarning = warned.GetValue(null);
        _sinkField.SetValue(null, null);
        try
        {
            setInstance.Invoke(null, new object[] { null });
            warned.SetValue(null, false);
            LogAssert.Expect(LogType.Warning,
                "[PathA-SFX] AudioManager가 없어 Timeline 효과음을 재생하지 못했습니다. BootScene부터 실행하세요.");
            MethodInfo play = playerType.GetMethod("Play");
            play.Invoke(null, new object[] { "Battle_Hit01" });
            play.Invoke(null, new object[] { "Battle_Hit01" });
            Assert.That(instance.GetValue(null), Is.Null);
        }
        finally
        {
            setInstance.Invoke(null, new[] { previousManager });
            warned.SetValue(null, previousWarning);
        }
    }

    private object AddSfx(double time, string key)
    {
        object marker = AddMarker(MarkerType, time);
        MarkerType.GetMethod("Configure").Invoke(marker, new object[] { key });
        return marker;
    }

    private void AddSection(double time, string id, string boundary)
    {
        Type type = FindType("PresentationSectionMarker");
        Type boundaryType = FindType("PresentationSectionBoundary");
        type.GetMethod("Configure").Invoke(AddMarker(type, time),
            new[] { (object)id, Enum.Parse(boundaryType, boundary) });
    }

    private object AddMarker(Type type, double time)
    {
        MethodInfo create = typeof(TrackAsset).GetMethods()
            .Single(method => method.Name == "CreateMarker" && method.IsGenericMethodDefinition);
        return create.MakeGenericMethod(type).Invoke(_timeline.markerTrack, new object[] { time });
    }

    private void Configure(double start, double end)
    {
        ReceiverType.GetMethods().Single(method => method.Name == "Configure" &&
            method.GetParameters().Length == 5).Invoke(_receiver,
            new object[] { null, null, null, start, end });
    }

    private void ClearConfig() => ReceiverType.GetMethod("ClearConfig").Invoke(_receiver, null);

    private void Notify(object marker) =>
        ((INotificationReceiver)_receiver).OnNotify(Playable.Null, (INotification)marker, null);

    private static string Key(object marker) => (string)MarkerType.GetProperty("SfxKey").GetValue(marker);

    private ScriptableObject NewCatalog() => Track(ScriptableObject.CreateInstance(CatalogType));

    private static void AddEntry(ScriptableObject catalog, string fieldName, string key, AudioClip clip)
    {
        FieldInfo field = CatalogType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        var entries = (IList)field.GetValue(catalog);
        object entry = Activator.CreateInstance(EntryType, key, clip, 1f, false, false);
        entries.Add(entry);
    }

    private object ValidateSfx(ScriptableObject catalog)
    {
        Type listType = typeof(List<>).MakeGenericType(MessageType);
        object messages = Activator.CreateInstance(listType);
        ValidatorType.GetMethod("ValidateSfxMarkers", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new[] { (object)_timeline, catalog, messages });
        return messages;
    }

    private static bool HasMessage(object messages, string severity, string text) =>
        ((IEnumerable)messages).Cast<object>().Any(message =>
            message.GetType().GetField("Severity").GetValue(message).ToString() == severity &&
            ((string)message.GetType().GetField("Message").GetValue(message)).Contains(text));

    private static int CountMessages(object messages, string severity, string text) =>
        ((IEnumerable)messages).Cast<object>().Count(message =>
            message.GetType().GetField("Severity").GetValue(message).ToString() == severity &&
            ((string)message.GetType().GetField("Message").GetValue(message)).Contains(text));

    private static Type FindType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"타입 '{name}'을 찾지 못했습니다.");
        return type;
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        _created.Add(value);
        return value;
    }
}
