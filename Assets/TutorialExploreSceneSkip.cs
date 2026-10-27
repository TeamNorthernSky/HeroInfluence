using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TutorialExploreSceneSkip : MonoBehaviour
{
    [SerializeField] private Button skipButton;
    [SerializeField] private GameObject skipPopup;
    [SerializeField] private Button skipAcceptButton;
    [SerializeField] private Button skipDenyButton;
    private bool isLoading;
    private static TutorialExploreSceneSkip current;
    private bool popupPaused;
    private float previousTimeScale;
    public static bool IsConfirmationOpen => current != null && current.popupPaused;

    public static bool PointerOverSkipButton
    {
        get
        {
            if (current == null || !current.isActiveAndEnabled || current.skipButton == null) return false;
            var rect = current.skipButton.transform as RectTransform;
            var canvas = current.skipButton.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera);
        }
    }

    private bool CanSkip => !isLoading && !JC.Tutorial.JcTutorialExitTransition.IsActive &&
        (SceneFadeController.Instance == null || !SceneFadeController.Instance.IsTransitioning);

    private static void RaiseAboveGuide(GameObject target, int order)
    {
        var canvas = target.GetComponent<Canvas>();
        if (canvas == null) canvas = target.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
        if (target.GetComponent<GraphicRaycaster>() == null) target.AddComponent<GraphicRaycaster>();
        var group = target.GetComponent<CanvasGroup>();
        if (group == null) group = target.AddComponent<CanvasGroup>();
        group.ignoreParentGroups = true;
        group.alpha = 1; group.interactable = true; group.blocksRaycasts = true;
    }

    private void Awake()
    {
        current = this;
        if(skipButton == null)
            skipButton = GetComponent<Button>();
        
        if (skipButton != null)
        {
            RaiseAboveGuide(skipButton.gameObject, 1050);
            skipButton.onClick.AddListener(OpenPopup);
        }
        else
            Debug.LogWarning("SkipAccept 버튼을 연결해주세요.", this);

        if (skipAcceptButton != null)
            skipAcceptButton.onClick.AddListener(goNextScene);
        else
            Debug.LogWarning("SkipAccept 버튼을 연결해주세요.", this);

        if (skipDenyButton != null)
            skipDenyButton.onClick.AddListener(ClosePopup);
        else
            Debug.LogWarning("SkipDeny 버튼을 연결해주세요", this);
    }

    private void OnDestroy()
    {
        ClosePopup();
        if (current == this) current = null;
        if (skipAcceptButton != null)
            skipAcceptButton.onClick.RemoveListener(goNextScene);
        if (skipDenyButton != null)
            skipDenyButton.onClick.RemoveListener(ClosePopup);
        if (skipButton != null)
            skipButton.onClick.RemoveListener(OpenPopup);
    }

    public void goNextScene()
    {
        if (!CanSkip || skipPopup == null || !skipPopup.activeInHierarchy ||
            !Application.CanStreamedLevelBeLoaded("Opening CutScene")) return;
        isLoading = true;
        JC.Tutorial.JcTutorialExitTransition.Begin("Opening CutScene");
        ClosePopup();
    }

    private void OpenPopup()
    {
        if (skipPopup == null || !CanSkip || popupPaused) return;
        previousTimeScale = Time.timeScale;
        popupPaused = true;
        Time.timeScale = 0;
        RaiseAboveGuide(skipPopup, 1100);
        skipPopup.transform.SetAsLastSibling();
        skipPopup.SetActive(true);
        ModalManager.Register(skipPopup);
    }

    private void ClosePopup()
    {
        if (skipPopup != null) { ModalManager.Unregister(skipPopup); skipPopup.SetActive(false); }
        if (popupPaused)
        {
            popupPaused = false;
            if (Mathf.Approximately(Time.timeScale, 0)) Time.timeScale = previousTimeScale;
        }
    }
    private void OnDisable() => ClosePopup();
    private void Update()
    {
        if (popupPaused && (skipPopup == null || !skipPopup.activeInHierarchy)) ClosePopup();
    }
}
