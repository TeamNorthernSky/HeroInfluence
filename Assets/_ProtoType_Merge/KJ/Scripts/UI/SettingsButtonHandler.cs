using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SettingsButtonHandler : MonoBehaviour
{
    [SerializeField] private GameObject toBeContinueObject;

    private void Awake()
    {
        GetComponent<Button>()?.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        ShowAndHide();
    }

    private /*IEnumerator */ void ShowAndHide()
    {
        toBeContinueObject.SetActive(true);
    }
}
