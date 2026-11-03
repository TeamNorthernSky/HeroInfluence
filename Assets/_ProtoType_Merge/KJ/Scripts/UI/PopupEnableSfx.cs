using UnityEngine;

/// <summary>Runtime-only enable notification; no authored scene or prefab changes are needed.</summary>
[DisallowMultipleComponent]
public sealed class PopupEnableSfx : MonoBehaviour
{
    private void OnEnable() => PopupSoundService.NotifyEnabled(gameObject);
    private void OnDisable() => PopupSoundService.Forget(gameObject);
    private void OnDestroy() => PopupSoundService.Forget(gameObject);
}
