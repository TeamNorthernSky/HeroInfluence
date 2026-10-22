using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// SkillPresentationData.OnValidate의 Cue 검증이 실행 계약과 맞는지.
/// state 없는 MovePrepare Cue는 이동 직전에 즉시 발화하므로(상태 게이트 우회) 경고 대상이 아니고,
/// AttackPrepare/Post는 즉시 발화 경로가 없어 경고가 유지돼야 한다. Assembly-CSharp 타입은 reflection으로 접근한다.
/// </summary>
public class PresentationCueValidationTests
{
    private static Type DataType => FindType("SkillPresentationData");
    private static Type CueType => FindType("CueBinding");
    private static Type TimingEnum => FindType("CueTimingSource");

    private ScriptableObject _data;

    [SetUp]
    public void SetUp()
    {
        _data = ScriptableObject.CreateInstance(DataType);
        _data.name = "CueValidationProbe";
        DataType.GetField("PresentationSchemaVersion").SetValue(_data, 1);
    }

    [TearDown]
    public void TearDown()
    {
        if (_data != null) UnityEngine.Object.DestroyImmediate(_data);
    }

    [Test]
    public void MovePrepare_CueWithoutState_FiresImmediately_NoWarning()
    {
        AddCue("MovePrepare", "fist_trail_on", timing: "ClipEvent");

        Validate();

        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void MovePrepare_DataTimedCueWithoutState_WarnsTimingIgnored()
    {
        AddCue("MovePrepare", "fist_trail_on", timing: "NormalizedTime");

        Validate();

        LogAssert.Expect(LogType.Warning, new Regex(@"MovePrepare\.Cues\[0\].*즉시 발화하므로 Timing=NormalizedTime"));
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase("AttackPrepare")]
    [TestCase("Post")]
    public void OtherPhases_CueWithoutState_StillWarn(string phase)
    {
        AddCue(phase, "charge_on", timing: "ClipEvent");

        Validate();

        LogAssert.Expect(LogType.Warning, new Regex(phase + "에 Cue가 있는데 AnimationStateName이 비어"));
        LogAssert.NoUnexpectedReceived();
    }

    private void AddCue(string phaseField, string cueName, string timing)
    {
        object phase = DataType.GetField(phaseField).GetValue(_data);
        phase.GetType().GetField("Enabled").SetValue(phase, true);
        phase.GetType().GetField("AnimationStateName").SetValue(phase, string.Empty);

        object cue = Activator.CreateInstance(CueType);
        CueType.GetField("CueName").SetValue(cue, cueName);
        CueType.GetField("Timing").SetValue(cue, Enum.Parse(TimingEnum, timing));
        CueType.GetField("Time").SetValue(cue, 0.3f);
        ((IList)phase.GetType().GetField("Cues").GetValue(phase)).Add(cue);
    }

    private void Validate()
    {
        DataType.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_data, null);
    }

    private static Type FindType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"타입 '{name}'을 찾지 못했습니다.");
        return type;
    }
}
