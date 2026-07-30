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
        if (data.hasData)
        {
            SaveSlotRepository.CurrentSlot = pendingSlotIndex; // [KJ 260703] 저장 대상 슬롯 전달
            SaveSlotRepository.IsContinue = true;              // [KJ 260714] 이어하기 — GameLoadGate가 복원 분기로 진입
            SceneManager.LoadScene(GameScene);
        }
        else
        {
            SceneManager.LoadScene(GameScene);

            //튜토리얼 씬이 없으므로 임시 비활성화
            //tutorialPopup?.SetActive(true);
        }
    }

    private void OnTutorialYes()
    {
        SaveSlotRepository.Save(pendingSlotIndex);
        SaveSlotRepository.CurrentSlot = pendingSlotIndex; // [KJ 260703] 저장 대상 슬롯 전달
        SaveSlotRepository.IsContinue = false;             // [KJ 260714] 새 게임 — 이전 이어하기 플래그가 남지 않도록 명시 리셋
        tutorialPopup?.SetActive(false);
        //SceneManager.LoadScene(TutorialScene);
        SceneManager.LoadScene(GameScene);
    }

    private void OnTutorialNo()
    {
        SaveSlotRepository.Save(pendingSlotIndex);
        SaveSlotRepository.CurrentSlot = pendingSlotIndex; // [KJ 260703] 저장 대상 슬롯 전달
        SaveSlotRepository.IsContinue = false;             // [KJ 260714] 새 게임 — 이전 이어하기 플래그가 남지 않도록 명시 리셋
        tutorialPopup?.SetActive(false);
        SceneManager.LoadScene(GameScene);
    }

    private void OnTutorialCancle()
    {
        tutorialPopup.SetActive(false);
    }
}
