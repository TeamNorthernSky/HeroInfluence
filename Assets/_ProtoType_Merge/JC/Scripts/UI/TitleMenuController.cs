using MathNet.Numerics.Optimization;
using NPOI.SS.Formula.Functions;
using Org.BouncyCastle.Asn1.Cmp;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleMenuController : MonoBehaviour
{
    [Header("GameSlot")]
    [SerializeField] private GameObject GameSlotPanel;

    [Header("Exit Popup")]
    [SerializeField] private GameObject ExitPopup;

    private void Awake()
    {
        GameSlotPanel.SetActive(false);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (ModalManager.HasAny)
            ModalManager.CloseTop();
        else
            OnQuitClicked();
    }

    public void OnNewGameClicked()
    {
        Debug.Log("[TitleMenu] 새 게임 → GameLoadScene (게이트씬)");

        GameSlotPanel.SetActive(true);
        //SceneManager.LoadScene("GameLoadScene");
    }

    // [KJ 260708] Settings 모달이 CommonUIManager(DDOL)로 이전됨 — 전역 인스턴스에 위임.
    public void OnSettingsClicked()
    {
        CommonUIManager.Instance.SettingsModal.Open();
    }

    public void OnSettingsExitClicked()
    {
        CommonUIManager.Instance.SettingsModal.Close();
    }

    public void OnQuitClicked()
    {
        ExitPopup.SetActive(true);

    }

    public void OnClickedQuitPopupAccept()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnClickedQuitPopupDeny()
    {
        ExitPopup.SetActive(false);
    }
}
