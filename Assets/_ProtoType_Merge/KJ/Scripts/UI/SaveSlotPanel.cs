using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class SaveSlotPanel : MonoBehaviour
{
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private Button[] deleteButtons;

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

    private const string TutorialScene = "TutorialScene";
    private const string GameScene = "GameLoadScene";

    private int pendingSlotIndex = -1;

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
        RefreshSlots();
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            SaveSlotData data = SaveSlotRepository.Load(i);
            TMP_Text label = slotButtons[i].GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = data.hasData ? $"Slot {i + 1}\n{data.savedAt}" : $"Slot {i + 1}\n- 비어있음 -";

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
        if (data.hasData)
        {
            SaveSlotRepository.CurrentSlot = pendingSlotIndex; // [KJ 260703] 저장 대상 슬롯 전달
            SceneManager.LoadScene(GameScene);
        }
        else
        {
            tutorialPopup?.SetActive(true);
        }
    }

    private void OnTutorialYes()
    {
        SaveSlotRepository.Save(pendingSlotIndex);
        SaveSlotRepository.CurrentSlot = pendingSlotIndex; // [KJ 260703] 저장 대상 슬롯 전달
        tutorialPopup?.SetActive(false);
        //SceneManager.LoadScene(TutorialScene);
        SceneManager.LoadScene(GameScene);
    }

    private void OnTutorialNo()
    {
        SaveSlotRepository.Save(pendingSlotIndex);
        SaveSlotRepository.CurrentSlot = pendingSlotIndex; // [KJ 260703] 저장 대상 슬롯 전달
        tutorialPopup?.SetActive(false);
        SceneManager.LoadScene(GameScene);
    }

    private void OnTutorialCancle()
    {
        tutorialPopup.SetActive(false);
    }
}
