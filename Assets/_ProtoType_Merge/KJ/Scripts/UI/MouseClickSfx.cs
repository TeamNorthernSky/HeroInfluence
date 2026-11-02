using UnityEngine;

/// <summary>Plays the shared button click sound for each left mouse press in every scene.</summary>
[DefaultExecutionOrder(-31000)]
public sealed class MouseClickSfx : MonoBehaviour
{
    private const string ClickSfxKey = "Button_Click";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var host = new GameObject("[KJ_MouseClickSfx]");
        DontDestroyOnLoad(host);
        host.AddComponent<MouseClickSfx>();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        var audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfx(ClickSfxKey);
    }
}
