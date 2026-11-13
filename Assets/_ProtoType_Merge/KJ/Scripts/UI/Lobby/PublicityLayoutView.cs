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
    private Canvas raisedTopBar;
    private bool addedTopBarCanvas;
    private bool originalOverrideSorting;
    private int originalSortingLayer, originalSortingOrder;

    private void OnEnable()
    {
        if (resetButton != null) resetButton.onClick.AddListener(ResetCount);
        RefreshCaption();
    }

    private void OnDisable()
    {
        if (resetButton != null) resetButton.onClick.RemoveListener(ResetCount);
        RestoreExplorationTopBar();
    }

    private void LateUpdate()
    {
        RefreshCaption();
        RefreshExplorationTopBar();
    }

    private void RefreshExplorationTopBar()
    {
        var top = ModalManager.Top;
        if (gameObject.scene.name != "DHScene_3" || top == null || !transform.IsChildOf(top.transform))
        {
            RestoreExplorationTopBar();
            return;
        }
        var modalCanvas = top.GetComponentInParent<Canvas>();
        if (modalCanvas == null) return;
        if (raisedTopBar == null)
        {
            // UI_Explore names its nested TopBar 1 prefab instance "TopBar".
            foreach (var rect in modalCanvas.rootCanvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name != "TopBar" || !rect.gameObject.activeInHierarchy ||
                    rect.GetComponent<UIModulePlacer>() != null) continue;
                raisedTopBar = rect.GetComponent<Canvas>();
                addedTopBarCanvas = raisedTopBar == null;
                if (addedTopBarCanvas) raisedTopBar = rect.gameObject.AddComponent<Canvas>();
                originalOverrideSorting = raisedTopBar.overrideSorting;
                originalSortingLayer = raisedTopBar.sortingLayerID;
                originalSortingOrder = raisedTopBar.sortingOrder;
                break;
            }
        }
        if (raisedTopBar == null) return;
        raisedTopBar.overrideSorting = true;
        raisedTopBar.sortingLayerID = modalCanvas.sortingLayerID;
        raisedTopBar.sortingOrder = modalCanvas.sortingOrder + 1;
    }

    private void RestoreExplorationTopBar()
    {
        if (raisedTopBar == null) return;
        raisedTopBar.sortingLayerID = originalSortingLayer;
        raisedTopBar.sortingOrder = originalSortingOrder;
        raisedTopBar.overrideSorting = originalOverrideSorting;
        if (addedTopBarCanvas) Destroy(raisedTopBar);
        raisedTopBar = null;
    }

    private void RefreshCaption()
    {
        if (countSource == null || countCaption == null) return;
        var tutorial = GetComponentInParent<TutorialPublicityController>();
        string caption = tutorial != null
            ? $"{tutorial.Count} / {tutorial.AvailableCount}"
            : "홍보 횟수 <color=#6A4CC8>" + countSource.text + "</color> 회";
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
