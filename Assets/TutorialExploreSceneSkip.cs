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

    private void Awake()
    {
        if(skipButton == null)
            skipButton = GetComponent<Button>();
        
        if (skipButton != null)
            skipButton.onClick.AddListener(OpenPopup);
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
        if (skipAcceptButton != null)
            skipAcceptButton.onClick.RemoveListener(goNextScene);
        if (skipDenyButton != null)
            skipDenyButton.onClick.RemoveListener(ClosePopup);
        if (skipButton != null)
            skipButton.onClick.RemoveListener(OpenPopup);
    }

    public void goNextScene()
    {
        if (isLoading) return;
        isLoading = true;
        TutorialProgressRepository.ClearProgress();
        TutorialCatalog.DestroyAllTutorialCatalogs();
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadSceneAsync("DHScene_3");
        else
            SceneManager.LoadSceneAsync("DHScene_3");
    }

    private void OpenPopup()
    {
        skipPopup.SetActive(true);
    }

    private void ClosePopup()
    {
        skipPopup.SetActive(false);
    }
}
