using UnityEngine;

/// <summary>저장값·실제 적용값·설정창 편집값에 사용하는 독립적인 음량 복사본.</summary>
public readonly struct SoundSettingsData
{
    public float Master { get; }
    public float Bgm { get; }
    public float Effect { get; }
    public bool MasterMuted { get; }
    public bool BgmMuted { get; }
    public bool EffectMuted { get; }

    public SoundSettingsData(float master, float bgm, float effect,
        bool masterMuted = false, bool bgmMuted = false, bool effectMuted = false)
    {
        Master = Mathf.Clamp01(master);
        Bgm = Mathf.Clamp01(bgm);
        Effect = Mathf.Clamp01(effect);
        MasterMuted = masterMuted;
        BgmMuted = bgmMuted;
        EffectMuted = effectMuted;
    }

    public SoundSettingsData WithMaster(float value) => new SoundSettingsData(value, Bgm, Effect, MasterMuted, BgmMuted, EffectMuted);
    public SoundSettingsData WithBgm(float value) => new SoundSettingsData(Master, value, Effect, MasterMuted, BgmMuted, EffectMuted);
    public SoundSettingsData WithEffect(float value) => new SoundSettingsData(Master, Bgm, value, MasterMuted, BgmMuted, EffectMuted);
    public SoundSettingsData WithMasterMuted(bool value) => new SoundSettingsData(Master, Bgm, Effect, value, BgmMuted, EffectMuted);
    public SoundSettingsData WithBgmMuted(bool value) => new SoundSettingsData(Master, Bgm, Effect, MasterMuted, value, EffectMuted);
    public SoundSettingsData WithEffectMuted(bool value) => new SoundSettingsData(Master, Bgm, Effect, MasterMuted, BgmMuted, value);
}
