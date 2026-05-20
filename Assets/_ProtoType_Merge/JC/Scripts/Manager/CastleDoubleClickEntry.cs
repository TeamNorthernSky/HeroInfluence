using UnityEngine;
using UnityEngine.EventSystems;

// [JC 신설 260514] DHScene_3 본부 더블클릭 로비 진입 트리거.
// 부착 위치: Castle GO (CastleUnit + Collider 보유한 prefab 인스턴스).
// 조건: HQVisitState.HasVisitingParty=true (방문 중인 파티 존재) + ModalRegistry.HasAny=false + MapEventPanel 비활성 + UI 위가 아닐 때.
// 동작: 좌클릭 더블클릭 감지 → GameSceneManager.Instance.LoadLobby() 호출.
[DisallowMultipleComponent]
public class CastleDoubleClickEntry : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private float doubleClickThreshold = 0.3f;
    [SerializeField] private float rayDistance = 1000f;

    private float lastClickTime = -1f;

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        // 입력 차단 가드 (Modal/WorldInputGate)
        if (WorldInputGate.IsBlocked) return;
        if (MapEventPanelUI.IsAnyActive) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // 방문 중인 파티가 있을 때만 입장 허용
        HQVisitState visitState = HQVisitState.Instance;
        if (visitState == null || !visitState.HasVisitingParty) return;

        // 카메라 캐시 + raycast
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance)) return;

        // 자기 본부 GO hit 여부 (자식 콜라이더 hit 포함)
        CastleUnit hitCastle = hit.collider != null ? hit.collider.GetComponentInParent<CastleUnit>() : null;
        if (hitCastle == null || hitCastle.gameObject != gameObject) return;

        // 더블클릭 임계값 검사
        float now = Time.unscaledTime;
        if (lastClickTime > 0f && now - lastClickTime <= doubleClickThreshold)
        {
            EnterLobby();
            lastClickTime = -1f;
        }
        else
        {
            lastClickTime = now;
        }
    }

    private void EnterLobby()
    {
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadLobby();
        }
        else
        {
            Debug.LogWarning("[CastleDoubleClickEntry] GameSceneManager.Instance == null — 폴백 호출", this);
            UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene_New");
        }
    }
}
