using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private SoundRegistry _registry;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void PlayById(int id, Vector3 position, float volume = 1f)
    {
        if (_registry == null)
        {
            return;
        }

        AudioClip clip = _registry.Get(id);
        if (clip == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    public void PlayClip(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}
