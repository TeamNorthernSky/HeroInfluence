using UnityEngine;

[DisallowMultipleComponent]
public class Modal : MonoBehaviour
{
    private void OnEnable()
    {
        transform.SetAsLastSibling();
        ModalRegistry.Register(gameObject);
    }

    private void OnDisable()
    {
        ModalRegistry.Unregister(gameObject);
    }
}
