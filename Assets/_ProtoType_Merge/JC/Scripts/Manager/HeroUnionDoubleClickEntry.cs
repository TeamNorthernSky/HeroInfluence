using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// [JC 신설 260514] DHScene_3 본부 더블클릭 로비 진입 트리거.
// 부착 위치: HeroUnion GO (HeroUnionUnit + Collider 보유한 prefab 인스턴스).
// 조건: HQVisitState.HasVisitingParty=true (방문 중인 파티 존재) + ModalRegistry.HasAny=false + MapEventPanel 비활성 + UI 위가 아닐 때.
// 동작: 좌클릭 더블클릭 감지 → GameSceneManager.Instance.LoadLobby() 호출.
[DisallowMultipleComponent]
public class HeroUnionDoubleClickEntry : MonoBehaviour
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
        JcBuildingSilhouetteFeature.ClearHoverHighlight(this);
        if (gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) return;
        if (DHGameEndState.IsEnding) return;

        // 입력 차단 가드 (Modal/WorldInputGate)
        if (WorldInputGate.IsBlocked) return;
        if (ExplorationModalEvents.MapEventModalActive) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // [JC 260615] 파티 상주 여부와 무관하게 본부 진입 허용(기획 변경).
        // HQVisitState(방문 파티)는 본부 내 연구/홍보/공방의 영웅 선택 제한에서만 사용.

        // 카메라 캐시 + raycast
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        if (!BuildingEntryPointer.TryHit(worldCamera, rayDistance, out RaycastHit hit)) return;

        // 자기 본부 GO hit 여부 (자식 콜라이더 hit 포함)
        HeroUnionUnit hitHeroUnion = hit.collider != null ? hit.collider.GetComponentInParent<HeroUnionUnit>() : null;
        if (hitHeroUnion == null || hitHeroUnion.gameObject != gameObject) return;
        if (!HasPlayerPartyAtInteractionCell(hitHeroUnion)) return;

        JcBuildingSilhouetteFeature.SetHoverHighlight(this, transform);
        if (!Input.GetMouseButtonDown(0)) return;

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

    private void OnDisable()
    {
        JcBuildingSilhouetteFeature.ClearHoverHighlight(this);
        lastClickTime = -1f;
    }

    private void EnterLobby()
    {
        if (DHGameEndState.IsEnding)
            return;

        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadLobby();
        }
        else
        {
            Debug.LogWarning("[HeroUnionDoubleClickEntry] GameSceneManager.Instance == null — 폴백 호출", this);
            SceneFadeController.LoadSceneWithFadeIfNeeded("HQLobbyScene");
        }
    }

    private static bool HasPlayerPartyAtInteractionCell(HeroUnionUnit heroUnion)
    {
        if (heroUnion == null)
            return false;

        PartyGridMover party = ResolvePlayerParty();
        if (party == null || !party.gameObject.activeInHierarchy)
            return false;

        if (DefeatedPartyReturnController.IsPartyWaiting(party))
            return false;

        Vector2Int partyGrid = party.GetCurrentGrid();
        IReadOnlyList<Vector2Int> interactionCells = heroUnion.GetInteractionCells();
        if (interactionCells == null)
            return false;

        for (int i = 0; i < interactionCells.Count; i++)
        {
            if (interactionCells[i] == partyGrid)
                return true;
        }

        return false;
    }

    private static PartyGridMover ResolvePlayerParty()
    {
        PartyRegistry registry = Object.FindFirstObjectByType<PartyRegistry>();
        if (registry != null && registry.PlayerParty != null)
            return registry.PlayerParty;

        return Object.FindFirstObjectByType<PartyGridMover>();
    }
}

// 동일 카메라의 건물들이 프레임마다 같은 포인터 Raycast를 반복하지 않도록 공유한다.
internal static class BuildingEntryPointer
{
    private static int frame = -1;
    private static Camera camera;
    private static float distance;
    private static bool hasHit;
    private static RaycastHit hit;

    public static bool TryHit(Camera worldCamera, float rayDistance, out RaycastHit result)
    {
        if (frame != Time.frameCount || camera != worldCamera || distance != rayDistance)
        {
            frame = Time.frameCount; camera = worldCamera; distance = rayDistance;
            hasHit = Physics.Raycast(worldCamera.ScreenPointToRay(Input.mousePosition), out hit, rayDistance);
        }
        result = hit;
        return hasHit;
    }
}
