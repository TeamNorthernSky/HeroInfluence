using UnityEngine;
using UnityEngine.UI;

/// <summary>전투 스킬 버튼의 사용 가능 효과와 선택 테두리만 표시합니다. 스킬 실행 상태는 소유하지 않습니다.</summary>
[DisallowMultipleComponent]
public class BattleSkillButtonVisual : MonoBehaviour
{
    [Tooltip("출전 버튼의 스윕 셰이더를 재사용하는 사용 가능 효과입니다. 클릭을 가로채지 않는 Image를 연결합니다.")]
    [SerializeField] private Image availableEffect;
    [Tooltip("선택 중 유지할 테두리입니다. 호버 여부와 관계없이 선택 상태를 표시합니다.")]
    [SerializeField] private Image selectedFrame;
    [Tooltip("사용 가능한 버튼에 반복 강조 효과를 표시합니다.")]
    [SerializeField] private bool showAvailableEffect = true;
    [Tooltip("사용 가능 효과의 색입니다. 다른 버튼이나 공유 재질에는 영향을 주지 않습니다.")]
    [SerializeField] private Color availableColor = new Color(.2f, .65f, 1f, 1f);
    [Tooltip("사용 가능 외곽 광선 강도입니다. 0이면 광선을 숨깁니다.")]
    [Range(0f, 8f)] [SerializeField] private float glowIntensity = 1.2f;
    [Tooltip("반짝이는 띠의 밝기입니다. 0이면 띠를 숨깁니다.")]
    [Range(0f, 8f)] [SerializeField] private float sweepIntensity = .8f;
    [Tooltip("반복 간격(초)입니다. 전투 배속의 영향을 받지 않습니다.")]
    [Min(.1f)] [SerializeField] private float sweepCycle = 2.5f;
    [Tooltip("띠가 지나가는 시간(초)입니다. 반복 간격보다 길게 설정해도 반복 간격 이내로 제한합니다.")]
    [Min(.05f)] [SerializeField] private float sweepDuration = .6f;
    [Tooltip("선택 테두리의 색입니다. 기존 선택 오버레이는 별도로 유지합니다.")]
    [SerializeField] private Color selectedColor = new Color(1f, .75f, .1f, 1f);
    [Tooltip("선택 테두리의 바깥쪽 폭입니다. 버튼 높이에 대한 UV 비율이며 0이면 외곽 확장을 없앱니다.")]
    [Range(0f, .2f)] [SerializeField] private float selectedWidth = .025f;
    [Tooltip("선택 테두리의 발광 강도입니다. 0이면 발광을 없앱니다.")]
    [Range(0f, 8f)] [SerializeField] private float selectedIntensity = 2f;

    private Material availableInstance;
    private Material selectedInstance;
    private Material availableSource;
    private Material selectedSource;
    private float started;
    private bool wasAvailable;

    private void Awake()
    {
        availableSource = availableEffect != null ? availableEffect.material : null;
        selectedSource = selectedFrame != null ? selectedFrame.material : null;
        availableInstance = CreateInstance(availableEffect, availableSource);
        selectedInstance = CreateInstance(selectedFrame, selectedSource);
        SetState(false, false);
    }

    private Material CreateInstance(Image image, Material source)
    {
        if (image == null || source == null) return null;
        var instance = new Material(source) { name = source.name + " (Battle Instance)", hideFlags = HideFlags.DontSave };
        image.material = instance;
        image.raycastTarget = false;
        return instance;
    }

    /// <summary>SkillButtonControllerから受け取った実際の使用可否と選択状態を表示します。</summary>
    public void SetState(bool available, bool selected)
    {
        if (available && !wasAvailable) started = Time.unscaledTime;
        wasAvailable = available;
        if (availableEffect != null) availableEffect.gameObject.SetActive(available && showAvailableEffect);
        if (selectedFrame != null) selectedFrame.gameObject.SetActive(selected);
        if (availableInstance != null)
        {
            availableInstance.SetColor("_GlowColor", availableColor);
            availableInstance.SetColor("_SweepColor", availableColor);
            availableInstance.SetFloat("_GlowIntensity", glowIntensity);
            availableInstance.SetFloat("_SweepIntensity", sweepIntensity);
            availableInstance.SetFloat("_SweepCycle", Mathf.Max(.1f, sweepCycle));
            availableInstance.SetFloat("_SweepDuration", Mathf.Clamp(sweepDuration, .05f, Mathf.Max(.1f, sweepCycle)));
            availableInstance.SetFloat("_SweepTime", Time.unscaledTime - started);
        }
        if (selectedInstance != null)
        {
            selectedInstance.SetColor("_GlowColor", selectedColor);
            selectedInstance.SetFloat("_GlowWidth", selectedWidth);
            selectedInstance.SetFloat("_GlowIntensity", selectedIntensity);
        }
    }

    private void OnDisable() => SetState(false, false);

    private void OnDestroy()
    {
        if (availableEffect != null) availableEffect.material = availableSource;
        if (selectedFrame != null) selectedFrame.material = selectedSource;
        if (availableInstance != null) Destroy(availableInstance);
        if (selectedInstance != null) Destroy(selectedInstance);
    }
}
