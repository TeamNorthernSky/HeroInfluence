using JC.Environment;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class DayNightLightController : MonoBehaviour
{
    [SerializeField] private Light targetLight;
    [Tooltip("Game hour to switch on. 18.5 means 18:30. Equal on/off hours keep the light off.")]
    [SerializeField, Range(0f, 24f)] private float onHour = 18.5f;
    [Tooltip("Game hour to switch off. 6.5 means 06:30.")]
    [SerializeField, Range(0f, 24f)] private float offHour = 6.5f;

    private void OnEnable()
    {
        ResolveLight();
        if (Application.isPlaying)
            RefreshLight();
    }

    private void LateUpdate()
    {
        RefreshLight();
    }

    private void OnDisable()
    {
        if (Application.isPlaying && targetLight != null)
            targetLight.enabled = false;
    }

    public void RefreshLight()
    {
        if (targetLight == null)
            return;

        bool shouldEnable = IsOnAtHour(JcDayNightController.CurrentHours);
        if (targetLight.enabled != shouldEnable)
            targetLight.enabled = shouldEnable;
    }

    private bool IsOnAtHour(double totalHours)
    {
        float hour = Mathf.Repeat((float)(totalHours % 24d), 24f);
        float on = Mathf.Repeat(onHour, 24f);
        float off = Mathf.Repeat(offHour, 24f);
        return on < off ? hour >= on && hour < off : on > off && (hour >= on || hour < off);
    }

    private void ResolveLight()
    {
        if (targetLight == null)
            targetLight = GetComponentInChildren<Light>(true);
    }

    private void Reset()
    {
        ResolveLight();
    }
}
