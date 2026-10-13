using UnityEngine;
using TMPro;

/// <summary>카탈로그 전체의 예제 상태를 제어하며 실제 게임 데이터에 접근하지 않습니다.</summary>
public sealed class CanvasButtonCatalog : MonoBehaviour
{
    [Tooltip("씬에 배치된 버튼 효과 예제입니다. 원본 효과 코드는 각 예제의 References에서 확인합니다.")]
    public CanvasButtonCatalogSample[] samples;
    [Tooltip("입력 가능 상태를 보여주는 카탈로그 텍스트입니다.")]
    public TMP_Text status;
    private bool inputsEnabled = true;

    public void ToggleInputs()
    {
        inputsEnabled = !inputsEnabled;
        foreach (var sample in samples) if (sample != null) sample.SetInteractable(inputsEnabled);
        RefreshStatus();
    }

    public void ResetAll()
    {
        inputsEnabled = true;
        foreach (var sample in samples) if (sample != null) sample.ResetPreview();
        RefreshStatus();
    }

    private void Start() => RefreshStatus();
    private void RefreshStatus()
    {
        if (status != null) status.text = inputsEnabled ? "입력 가능 · 버튼에 마우스를 올리거나 클릭하세요" : "입력 불가 · 호버 / 클릭 / 강조 해제 상태";
    }
}
