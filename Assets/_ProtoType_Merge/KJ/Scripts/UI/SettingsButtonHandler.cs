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
        if (toBeContinueObject != null)
            StartCoroutine(ShowAndHide());
    }

    private IEnumerator ShowAndHide()
    {
        toBeContinueObject.SetActive(true);
        yield return new WaitForSeconds(3f);
        toBeContinueObject.SetActive(false);
    }
}
