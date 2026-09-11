using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>홍보 모달의 추가 횟수 표시와 초기화 버튼. 홍보 계산은 기존 컨트롤러가 담당한다.</summary>
public sealed class PublicityLayoutView : MonoBehaviour
{
    [SerializeField] private TMP_Text countSource;
    [SerializeField] private TMP_Text countCaption;
    [SerializeField] private Slider countSlider;
    [SerializeField] private Button resetButton;

    private void OnEnable()
    {
        if (resetButton != null) resetButton.onClick.AddListener(ResetCount);
        RefreshCaption();
    }

    private void OnDisable()
    {
        if (resetButton != null) resetButton.onClick.RemoveListener(ResetCount);
    }

    private void LateUpdate() => RefreshCaption();

    private void RefreshCaption()
    {
        if (countSource == null || countCaption == null) return;
        string caption = "홍보 횟수 <color=#6A4CC8>" + countSource.text + "</color> 회";
        if (countCaption.text != caption) countCaption.text = caption;
        if (resetButton != null && countSlider != null)
            resetButton.interactable = countSlider.interactable && countSlider.value > countSlider.minValue;
    }

    private void ResetCount()
    {
        if (countSlider != null && countSlider.interactable)
            countSlider.value = countSlider.minValue;
    }
}
