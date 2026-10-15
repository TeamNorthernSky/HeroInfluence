using System;
using System.Collections.Generic;
using ASB.Work.Battle.Core;
using UnityEngine;

/// <summary>
/// 코드 시점 효과음(Event SFX) 진입점. 훅은 "어떤 순간인지"만 알리고, 키는 SkillPresentationData.EventSfx가 정한다.
/// 재생은 PresentationSfxPlayer 경유(테스트 SinkOverride 유지).
/// </summary>
public static class PresentationSfxEvents
{
    // EditMode 테스트에서 프레임 번호를 고정한다.
    internal static Func<int> FrameOverride;

    // Hit 계열 중복 제거: (시전자, 스킬 SO, 해석된 이벤트)를 같은 프레임에 1회만. 프레임이 바뀌면 비운다.
    private static readonly HashSet<(int casterId, int presentationId, PresentationSfxEvent evt)> s_PlayedHits =
        new HashSet<(int, int, PresentationSfxEvent)>();
    private static int s_PlayedHitsFrame = -1;

    private static int CurrentFrame => FrameOverride != null ? FrameOverride() : Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        FrameOverride = null;
        s_PlayedHits.Clear();
        s_PlayedHitsFrame = -1;
    }

    /// <summary>
    /// 피해 확정 순간(대상별 호출). 추가 대상이면 AdditionalHit 줄, 없으면 Hit 줄을 재생한다.
    /// 같은 시전자의 광역·동시 연쇄는 프레임당 1회로 묶고, 다른 시전자의 동시 공격은 각각 재생한다.
    /// 회복·빗나감 판정은 호출자가 한다.
    /// </summary>
    public static void PlayHit(SkillPresentationData presentation, BattleCharactor caster, DamageRole role)
    {
        List<PresentationEventSfx> entries = presentation != null ? presentation.EventSfx : null;
        if (entries == null || entries.Count == 0) return;

        PresentationSfxEvent evt = role == DamageRole.Additional && HasEvent(entries, PresentationSfxEvent.AdditionalHit)
            ? PresentationSfxEvent.AdditionalHit
            : PresentationSfxEvent.Hit;
        if (!HasEvent(entries, evt)) return;

        int frame = CurrentFrame;
        if (frame != s_PlayedHitsFrame)
        {
            s_PlayedHits.Clear();
            s_PlayedHitsFrame = frame;
        }

        int casterId = caster != null ? caster.GetInstanceID() : 0;
        if (!s_PlayedHits.Add((casterId, presentation.GetInstanceID(), evt))) return;

        for (int i = 0; i < entries.Count; i++)
        {
            PresentationEventSfx entry = entries[i];
            if (entry != null && entry.Event == evt) PlayEntry(entry);
        }
    }

    /// <summary>
    /// VFX 착탄·발동 순간. 중복 제거 없이 호출마다 재생하며, 반복 제어는 호출한 VFX 콜백의 순번/가드가 담당한다.
    /// occurrence는 이번 시전에서 몇 번째 착탄인지(0부터).
    /// </summary>
    public static void PlayEffectImpact(SkillPresentationData presentation, int occurrence)
    {
        List<PresentationEventSfx> entries = presentation != null ? presentation.EventSfx : null;
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            PresentationEventSfx entry = entries[i];
            if (entry == null || entry.Event != PresentationSfxEvent.EffectImpact) continue;
            if (entry.Occurrence >= 0 && entry.Occurrence != occurrence) continue;
            PlayEntry(entry);
        }
    }

    /// <summary>실제 회복이 확정된 대상마다 재생한다. 첫 대상과 인접 대상은 별도 키를 사용한다.</summary>
    public static void PlayHeal(SkillPresentationData presentation, bool additional)
    {
        List<PresentationEventSfx> entries = presentation != null ? presentation.EventSfx : null;
        if (entries == null) return;

        PresentationSfxEvent evt = additional && HasEvent(entries, PresentationSfxEvent.AdditionalHeal)
            ? PresentationSfxEvent.AdditionalHeal
            : PresentationSfxEvent.Heal;
        for (int i = 0; i < entries.Count; i++)
        {
            PresentationEventSfx entry = entries[i];
            if (entry != null && entry.Event == evt) PlayEntry(entry);
        }
    }

    /// <summary>조립형 VFX 부품이 실제로 시작한 순서에 맞춰 재생한다.</summary>
    public static void PlayEffectStep(SkillPresentationData presentation, int stepIndex)
    {
        List<PresentationEventSfx> entries = presentation != null ? presentation.EventSfx : null;
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            PresentationEventSfx entry = entries[i];
            if (entry == null || entry.Event != PresentationSfxEvent.EffectStep) continue;
            if (entry.Occurrence >= 0 && entry.Occurrence != stepIndex) continue;
            PlayEntry(entry);
        }
    }

    private static bool HasEvent(List<PresentationEventSfx> entries, PresentationSfxEvent evt)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].Event == evt) return true;
        }

        return false;
    }

    private static void PlayEntry(PresentationEventSfx entry)
    {
        string key = DHAudioClipCatalog.NormalizeKey(entry.Key);
        if (string.IsNullOrEmpty(key)) return;

        if (entry.DurationSeconds > 0f)
            PresentationSfxPlayer.PlayForDuration(key, entry.DurationSeconds);
        else
            PresentationSfxPlayer.Play(key);
    }
}
