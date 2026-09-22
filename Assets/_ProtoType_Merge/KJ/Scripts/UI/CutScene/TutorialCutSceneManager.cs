using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TutorialCutSceneManager : MonoBehaviour
{
    public List<GameObject> CutScenes;
    [SerializeField] private GameObject skipPopup;
    private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
    private int popupClosedFrame = -1;
    private int currentIndex = 0;
    private GameObject currentImage = null;
    // Start is called before the first frame update
    void Start()
    {
        if (skipPopup != null) skipPopup.SetActive(false);
        foreach (var item in CutScenes)
        {
            item.gameObject.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (skipPopup != null && skipPopup.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseSkipPopup();
            return;
        }
        if (Time.frameCount == popupClosedFrame) return;
        // Button clicks belong to the UI, not to the cutscene's advance input.
        if (Input.GetMouseButtonDown(0) && IsPointerOverButton()) return;

        if(Input.GetKeyDown(KeyCode.LeftArrow))
        {
            GoPreviousCutScene();
        }

        if(Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (currentIndex < CutScenes.Count)
            {
                GoNextCutScene();
                return;
            }
            GoTutorialExploreScene();
        }
    }

    private bool IsPointerOverButton()
    {
        if (EventSystem.current == null) return false;
        pointerHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        }, pointerHits);
        foreach (var hit in pointerHits)
            if (hit.gameObject.GetComponentInParent<UnityEngine.UI.Button>() != null)
                return true;
        return false;
    }

    public void ShowSkipPopup()
    {
        if (skipPopup == null) return;
        skipPopup.transform.SetAsLastSibling();
        skipPopup.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void CloseSkipPopup()
    {
        if (skipPopup == null) return;
        skipPopup.SetActive(false);
        popupClosedFrame = Time.frameCount;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void ConfirmSkip()
    {
        if (skipPopup == null || !skipPopup.activeSelf) return;
        CloseSkipPopup();
        GoTutorialExploreScene();
    }

    void GoNextCutScene()
    {
        currentImage = CutScenes[currentIndex];
        ++currentIndex;

        currentImage.gameObject.SetActive(true);
    }
    
    public void GoTutorialExploreScene()
    {
        SceneManager.LoadScene("TutorialExploreScene");
    }

    void GoPreviousCutScene()
    {
        if (currentIndex < 1) return;
        currentImage.gameObject.SetActive(false);
        --currentIndex;
        currentImage = CutScenes[currentIndex];

    }
}
