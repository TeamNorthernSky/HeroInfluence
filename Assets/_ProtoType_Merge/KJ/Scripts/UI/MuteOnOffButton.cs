using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class MuteOnOffButton : MonoBehaviour
{
    public enum SoundChannel { Master, Bgm, Effect }

    [SerializeField] private SoundChannel channel;
    [SerializeField] private Sprite soundOn;
    [SerializeField] private Sprite soundOff;
    [SerializeField] private Image icon;
    private SettingsModalController owner;
    private Button button;

    private void Awake()
    {
        owner = GetComponentInParent<SettingsModalController>(true);
        button = GetComponent<Button>();
        button.onClick.AddListener(ToggleMute);
    }

    private void OnEnable()
    {
        if (owner == null) return;
        owner.SoundSettingsChanged += Refresh;
        Refresh(owner.PendingSound);
    }

    private void OnDisable()
    {
        if (owner != null) owner.SoundSettingsChanged -= Refresh;
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(ToggleMute);
    }

    public void ToggleMute()
    {
        if (owner == null) return;
        switch (channel)
        {
            case SoundChannel.Master: owner.ToggleMasterMute(); break;
            case SoundChannel.Bgm: owner.ToggleBgmMute(); break;
            case SoundChannel.Effect: owner.ToggleEffectMute(); break;
        }
    }

    private void Refresh(SoundSettingsData sound)
    {
        bool muted = channel == SoundChannel.Master ? sound.MasterMuted
            : channel == SoundChannel.Bgm ? sound.BgmMuted : sound.EffectMuted;
        if (icon != null) icon.sprite = muted ? soundOff : soundOn;
    }
}
