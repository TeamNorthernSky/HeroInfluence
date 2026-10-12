using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class SaveSlotPanel : MonoBehaviour
{
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private Button[] deleteButtons;

    [Header("슬롯 이미지 (데이터 유무별) [KJ 260716]")]
    [Tooltip("세이브 데이터가 있는 슬롯의 배경 스프라이트.")]
    [SerializeField] private Sprite slotFilledSprite;
    [Tooltip("비어있는 슬롯의 배경 스프라이트.")]
    [SerializeField] private Sprite slotEmptySprite;

    [Header("삭제 확인 팝업")]
    [SerializeField] private GameObject deleteConfirmPopup;
    [SerializeField] private Button deleteConfirmButton;
    [SerializeField] private Button deleteCancelButton;

    [SerializeField] private Button backButton;

    
    [Header("튜토리얼 팝업")]
    [SerializeField] private GameObject tutorialPopup;
    [SerializeField] private Button tutorialYesButton;
    [SerializeField] private Button tutorialNoButton;
    [SerializeField] private Button tutorialCancleButton;

    private const string TutorialScene = "TutorialExploreScene";
    private const string GameScene = "GameLoadScene";
    private const string OpeningCutScene = "Opening CutScene";
    private int pendingSlotIndex = -1;
    private bool tutorialSelectionPending;

    private void Awake()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int index = i;
            slotButtons[i].onClick.AddListener(() => OnSlotClicked(index));
        }

        for (int i = 0; i < deleteButtons.Length; i++)
        {
            int index = i;
            deleteButtons[i]?.onClick.AddListener(() => OnDeleteClicked(index));
        }

        deleteConfirmButton?.onClick.AddListener(OnDeleteConfirm);
        deleteCancelButton?.onClick.AddListener(() => deleteConfirmPopup?.SetActive(false));

        deleteConfirmPopup?.SetActive(false);

        backButton?.onClick.AddListener(() => gameObject.SetActive(false));

        tutorialYesButton?.onClick.AddListener(OnTutorialYes);
        tutorialNoButton?.onClick.AddListener(OnTutorialNo);
        tutorialCancleButton?.onClick.AddListener(OnTutorialCancle);

        tutorialPopup?.SetActive(false);
    }

    private void OnEnable()
    {
        ModalManager.OnStackChanged += HandleModalStackChanged;
        RefreshSlots();
    }

    private void OnDisable()
    {
        ModalManager.OnStackChanged -= HandleModalStackChanged;
        if (tutorialSelectionPending)
        {
            tutorialSelectionPending = false;
            pendingSlotIndex = -1;
            tutorialPopup?.SetActive(false);
        }
    }

    private void HandleModalStackChanged()
    {
        // ESC도 ModalManager를 통해 팝업을 숨기므로 같은 선택 취소를 적용한다.
        if (tutorialSelectionPending && tutorialPopup != null && !tutorialPopup.activeInHierarchy)
        {
            tutorialSelectionPending = false;
            pendingSlotIndex = -1;
        }
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            SaveSlotData data = SaveSlotRepository.Load(i);
            TMP_Text label = slotButtons[i].GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = data.hasData
                    ? $"<b>슬롯 {i + 1}</b>\n<size=32>{data.savedAt}\n저장됨</size>"
                    : $"<b>슬롯 {i + 1}</b>\n<size=32>저장 가능</size>";

            // [KJ 260716] 데이터 유무별 슬롯 배경 교체 (스프라이트 미할당 시 기존 이미지 유지)
            Sprite slotSprite = data.hasData ? slotFilledSprite : slotEmptySprite;
            if (slotSprite != null && slotButtons[i].image != null)
                slotButtons[i].image.sprite = slotSprite;

            if (i < deleteButtons.Length && deleteButtons[i] != null)
                deleteButtons[i].gameObject.SetActive(data.hasData);
        }
    }

    private void OnDeleteClicked(int slotIndex)
    {
        pendingSlotIndex = slotIndex;
        deleteConfirmPopup?.SetActive(true);
    }

    private void OnDeleteConfirm()
    {
        SaveSlotRepository.Delete(pendingSlotIndex);
        deleteConfirmPopup?.SetActive(false);
        pendingSlotIndex = -1;
        RefreshSlots();
    }

    private void OnSlotClicked(int slotIndex)
    {
        pendingSlotIndex = slotIndex;
        if (pendingSlotIndex < 0) return;

        SaveSlotData data = SaveSlotRepository.Load(pendingSlotIndex);
        if (data.hasData) // 저장할 데이터가 있으면
        {
            SaveSlotRepository.CurrentSlot = pendingSlotIndex; // [KJ 260703] 저장 대상 슬롯 전달
            SaveSlotRepository.IsContinue = true;              // [KJ 260714] 이어하기 — GameLoadGate가 복원 분기로 진입
            SceneFadeController.LoadSceneWithFadeIfNeeded(GameScene);
        }
        else
        {
            // 시작을 확정하기 전에는 슬롯 파일과 전역 슬롯 선택을 변경하지 않는다.
            tutorialSelectionPending = true;
            tutorialPopup?.SetActive(true);
        }
    }

    private void OnTutorialYes()
    {
        if (!tutorialSelectionPending || pendingSlotIndex < 0) return;
        SaveSlotRepository.Save(pendingSlotIndex);
        SaveSlotRepository.CurrentSlot = pendingSlotIndex; // [KJ 260703] 저장 대상 슬롯 전달
        SaveSlotRepository.IsContinue = false;             // [KJ 260714] 새 게임 — 이전 이어하기 플래그가 남지 않도록 명시 리셋
        tutorialPopup?.SetActive(false);
        //튜토리얼 하면 튜토리얼씬
        SceneFadeController.LoadSceneWithFadeIfNeeded(TutorialScene);
    }

    private void OnTutorialNo()
    {
        if (!tutorialSelectionPending || pendingSlotIndex < 0) return;
        SaveSlotRepository.Save(pendingSlotIndex);
        SaveSlotRepository.CurrentSlot = pendingSlotIndex; // [KJ 260703] 저장 대상 슬롯 전달
        SaveSlotRepository.IsContinue = false;             // [KJ 260714] 새 게임 — 이전 이어하기 플래그가 남지 않도록 명시 리셋
        tutorialPopup?.SetActive(false);

        //튜토리얼 안하면 오프닝 컷씬
        SceneFadeController.LoadSceneWithFadeIfNeeded(OpeningCutScene);
    }

    private void OnTutorialCancle()
    {
        tutorialSelectionPending = false;
        pendingSlotIndex = -1;
        tutorialPopup?.SetActive(false);
    }
}
