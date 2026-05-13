using UnityEngine;

[DisallowMultipleComponent]
public class Modal : MonoBehaviour
{
    // [JC 260513] 활성 중 Time.timeScale=0 적용 여부. 다른 pausesGame 모달이 스택에 남아 있으면 유지.
    [SerializeField] private bool pausesGame;

    public bool PausesGame => pausesGame;

    private void OnEnable()
    {
        transform.SetAsLastSibling();
        ModalRegistry.Register(gameObject);
        if (pausesGame)
            ModalPauseGate.Refresh();
    }

    private void OnDisable()
    {
        ModalRegistry.Unregister(gameObject);
        if (pausesGame)
            ModalPauseGate.Refresh();
    }
}
