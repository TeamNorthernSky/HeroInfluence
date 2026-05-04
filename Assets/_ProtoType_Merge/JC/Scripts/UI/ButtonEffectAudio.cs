using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class ButtonEffectAudio : ButtonEffectModule
{
    [Header("Triggers")]
    [SerializeField] private bool playOnHover = true;
    [SerializeField] private bool playOnClick = true;

    [Header("Clips")]
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;

    [Header("Settings")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private bool muted = false;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    public override void OnHoverEnter()
    {
        if (!playOnHover || muted || hoverClip == null) return;
        audioSource.PlayOneShot(hoverClip, volume);
    }

    public override void OnClick()
    {
        if (!playOnClick || muted || clickClip == null) return;
        audioSource.PlayOneShot(clickClip, volume);
    }
}
