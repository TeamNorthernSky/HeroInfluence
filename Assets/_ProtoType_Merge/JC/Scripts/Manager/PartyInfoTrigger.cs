using UnityEngine;
using UnityEngine.EventSystems;

// [JC 신설 260513] 탐사씬 우클릭 단일 중앙 디스패처.
// 우클릭 → raycast → PartyGridMover hit + IsMoving==false → PartyInfoModal.Open(party).
// World 입력 차단(WorldInputGate)과 UI 우선(EventSystem.IsPointerOverGameObject) 동시 가드.
[DisallowMultipleComponent]
public class PartyInfoTrigger : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private PartyInfoModal partyInfoModal;
    [SerializeField] private PartyInfoModal enemyInfoModal;  // 동일 PartyInfoModal 컴포넌트, GO 이름만 EnemyInfoModal
    [SerializeField] private float rayDistance = 1000f;

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(1)) return;
        if (WorldInputGate.IsBlocked) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (worldCamera == null) return;

        Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance)) return;
        if (hit.collider == null) return;

        // 아군 파티 hit (이동 중 가드)
        PartyGridMover party = hit.collider.GetComponentInParent<PartyGridMover>();
        if (party != null)
        {
            if (party.IsMoving) return;
            partyInfoModal?.Open(party);
            return;
        }

        // 적 파티 hit (이동 중 가드 없음 — 사용자 정책)
        EnemyGridMover enemy = hit.collider.GetComponentInParent<EnemyGridMover>();
        if (enemy != null)
        {
            enemyInfoModal?.Open(enemy);
        }
    }
}
