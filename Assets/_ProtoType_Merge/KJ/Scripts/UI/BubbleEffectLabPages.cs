using UnityEngine;

/// <summary>Switches the independent EffectLab demonstration pages.</summary>
public sealed class BubbleEffectLabPages : MonoBehaviour
{
    [SerializeField] private GameObject originalPage;
    [SerializeField] private GameObject sidePopPage;
    [SerializeField] private GameObject cameraShakePage;

    public void Configure(GameObject original, GameObject sidePop)
    {
        originalPage = original;
        sidePopPage = sidePop;
    }

    public void ShowOriginal() => Show(0);
    public void ShowSidePop() => Show(1);
    public void ShowCameraShake() => Show(2);

    private void Show(int page)
    {
        if (originalPage != null) originalPage.SetActive(page == 0);
        if (sidePopPage != null) sidePopPage.SetActive(page == 1);
        if (cameraShakePage != null) cameraShakePage.SetActive(page == 2);
    }
}
