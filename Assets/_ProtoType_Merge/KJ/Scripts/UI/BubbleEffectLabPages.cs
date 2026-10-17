using UnityEngine;

/// <summary>Switches the independent EffectLab demonstration pages.</summary>
public sealed class BubbleEffectLabPages : MonoBehaviour
{
    [SerializeField] private GameObject originalPage;
    [SerializeField] private GameObject sidePopPage;

    public void Configure(GameObject original, GameObject sidePop)
    {
        originalPage = original;
        sidePopPage = sidePop;
    }

    public void ShowOriginal() => Show(false);
    public void ShowSidePop() => Show(true);

    private void Show(bool sidePop)
    {
        if (originalPage != null) originalPage.SetActive(!sidePop);
        if (sidePopPage != null) sidePopPage.SetActive(sidePop);
    }
}
