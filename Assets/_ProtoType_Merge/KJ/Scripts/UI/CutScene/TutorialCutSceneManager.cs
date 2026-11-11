using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TutorialCutSceneManager : MonoBehaviour
{
    public enum CutScene
    {
        None,
        Opening,
        Ending
    }

    public CutScene currentCutSceneState = CutScene.None;

    public List<GameObject> CutScenes;
    [SerializeField] private GameObject skipPopup;
    [SerializeField, Min(0f)] private float cutFadeInDuration = 0.35f;
    private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
    private int popupClosedFrame = -1;
    // 다음에 표시할 컷의 인덱스. 표시 중인 마지막 컷은 currentIndex - 1이다.
    private int currentIndex = 0;
    private GameObject currentImage = null;
    private bool transitioning;
    // JC | 2026-11-10 | KJ 컷 진행 입력은 로컬 중복 방지와 공통 페이드 잠금을 함께 따릅니다.
    private bool IsInputLocked => transitioning || SceneFadeController.IsInputBlocked;

    //public string nextScene = "DHScene_3";
    // Start is called before the first frame update
    void Start()
    {
        if (skipPopup != null) skipPopup.SetActive(false);
        foreach (var item in CutScenes)
        {
            item.gameObject.SetActive(false);
        }
        PrepareCutFadeIns();
        currentIndex = 0;
        currentImage = null;
        if (CutScenes.Count > 0) GoNextCutScene();
    }

    private void PrepareCutFadeIns()
    {
        foreach (var item in CutScenes)
        {
            if (item == null) continue;
            foreach (var image in item.GetComponentsInChildren<Image>(true))
            {
                // Sprite.name에는 확장자가 없다. 말풍선과 설명은 대상에서 제외한다.
                if (!image.name.StartsWith("CutScene", System.StringComparison.Ordinal) ||
                    image.sprite == null || !image.sprite.name.EndsWith("cut", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var fade = image.GetComponent<CutSceneFadeIn>();
                if (fade == null) fade = image.gameObject.AddComponent<CutSceneFadeIn>();
                fade.SetDuration(cutFadeInDuration);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (IsInputLocked || Input.GetKeyDown(KeyCode.Escape)) return;
        if (skipPopup != null && skipPopup.activeSelf) return;
        if (Time.frameCount == popupClosedFrame) return;
        // Button clicks belong to the UI, not to the cutscene's advance input.
        if (Input.GetMouseButtonDown(0) && IsPointerOverButton()) return;

#if UNITY_EDITOR
        if(Input.GetKeyDown(KeyCode.LeftArrow))
        {
            GoPreviousCutScene();
        }
#endif

        if(Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (currentIndex < CutScenes.Count)
            {
                GoNextCutScene();
                return;
            }

            switch (currentCutSceneState)
            {
                case CutScene.Opening:
                    GoMainExploreScene();
                    break;

                case CutScene.Ending:
                    AfterEndCutScene();
                    break;

                default:
                    Debug.Log($"currentCutSceneState가 {currentCutSceneState.ToString()} 입니다!");
                    break;
            }
        }
    }

    /// <summary>공통 메뉴보다 먼저 컷신의 Esc 입력을 처리한다.</summary>
    public static bool HandleEscape()
    {
        var cutscene = FindFirstObjectByType<TutorialCutSceneManager>();
        if (cutscene == null || !cutscene.isActiveAndEnabled ||
            cutscene.gameObject.scene != SceneManager.GetActiveScene() ||
            (cutscene.currentCutSceneState != CutScene.Opening && cutscene.currentCutSceneState != CutScene.Ending))
            return false;

        // JC | 2026-11-10 | 전환 중 Esc로 스킵 창을 다시 여는 경로도 차단합니다.
        if (!cutscene.IsInputLocked)
        {
            if (cutscene.skipPopup != null && cutscene.skipPopup.activeSelf)
                cutscene.CloseSkipPopup();
            else
                cutscene.ShowSkipPopup();
        }
        return true;
    }

    /// <summary>The visible skip popup owns Space/Enter, regardless of the selected button.</summary>
    public static bool TryGetSkipConfirmButton(out Button confirmButton)
    {
        confirmButton = null;
        foreach (var cutscene in FindObjectsByType<TutorialCutSceneManager>(FindObjectsSortMode.None))
        {
            if (!cutscene.isActiveAndEnabled || cutscene.gameObject.scene != SceneManager.GetActiveScene() ||
                (cutscene.currentCutSceneState != CutScene.Opening && cutscene.currentCutSceneState != CutScene.Ending) ||
                cutscene.skipPopup == null || !cutscene.skipPopup.activeInHierarchy) continue;

            // JC | 2026-11-10 | 페이드 중 Space/Enter 확정 대상은 제공하지 않습니다.
            if (!cutscene.IsInputLocked)
                foreach (var button in cutscene.skipPopup.GetComponentsInChildren<Button>(true))
                    if (button.name == "Accept Button")
                    {
                        confirmButton = button;
                        break;
                    }
            return true;
        }
        return false;
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
        // JC | 2026-11-10 | 버튼 이벤트가 직접 호출해도 전환 잠금을 우회하지 않습니다.
        if (IsInputLocked) return;
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
        // JC | 2026-11-10 | 스킵 확정 이후 연속 입력은 새 씬/실패 복구가 끝날 때까지 무시합니다.
        if (IsInputLocked) return;
        if (skipPopup == null || !skipPopup.activeSelf) return;
        CloseSkipPopup();

        switch (currentCutSceneState)
        {
            case CutScene.Opening:
                GoMainExploreScene();
                break;

            case CutScene.Ending:
                AfterEndCutScene();
                break;
            
            default:
                Debug.Log($"currentCutSceneState가 {currentCutSceneState.ToString()} 입니다!");
                break;
        }

    }

    void GoNextCutScene()
    {
        currentImage = CutScenes[currentIndex];
        ++currentIndex;

        currentImage.gameObject.SetActive(true);
    }
    
    private void GoTutorialExploreScene()
    {
        // JC | 2026-11-10 | 튜토리얼 분기도 동일한 요청/실패 복구 경로를 사용합니다.
        BeginCutSceneTransition("TutorialExploreScene");
    }
    private void GoMainExploreScene()
    {
        // JC | 2026-11-10 | 인트로 스킵과 마지막 컷 클릭의 중복 전환을 막습니다.
        if (IsInputLocked) return;
        // 튜토리얼 시설 상태를 넘기지 않고 새 게임 초기화를 거친다.
        SaveSlotRepository.IsContinue = false;
        BeginCutSceneTransition("GameLoadScene");
    }

    // JC | 2026-11-10 | 요청 시 즉시 잠그고, 거절/로드 실패 때 로컬 잠금도 해제해 재시도를 허용합니다.
    private void BeginCutSceneTransition(string sceneName)
    {
        if (IsInputLocked) return;
        transitioning = true;
        var request = SceneFadeController.LoadSceneAsyncWithFade(sceneName,
            onFailed: () => { if (this != null) transitioning = false; });
        if (request == null) transitioning = false;
    }

    void GoPreviousCutScene()
    {
        if (currentIndex <= 1) return;
        currentImage.gameObject.SetActive(false);
        --currentIndex;
        currentImage = CutScenes[currentIndex - 1];

    }

    private void AfterEndCutScene()
    {
        // JC | 2026-11-10 | 엔딩의 기존 새 게임 초기화는 유지하고 전환 잠금/실패 처리는 공통화합니다.
        if (IsInputLocked) return;
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.ResetForNewGame();
        else DHGameProgressResetService.ResetDHProgress();
        BeginCutSceneTransition("TitleScene");
    }
}
