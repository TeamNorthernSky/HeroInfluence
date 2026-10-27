using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(TutorialOutpostObject))]
public sealed class TutorialOutpostClickEntry : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField, Tooltip("점령 건물은 연속 두 번 클릭했을 때만 협회에 진입합니다.")] private bool requireDoubleClick = true;
    [SerializeField, Min(.1f), Tooltip("같은 건물의 두 클릭을 더블클릭으로 인정할 최대 간격(초)입니다.")] private float doubleClickThreshold = 0.3f;
    [SerializeField] private float rayDistance = 1000f;

    private TutorialOutpostObject outpost;
    private float lastClickTime = -1f;
    private bool entering;

    private void Awake()
    {
        outpost = GetComponent<TutorialOutpostObject>();
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void Update()
    {
        UpdateHoverHighlight();
        if (DHGameEndState.IsEnding || entering)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (WorldInputGate.IsBlocked)
        { lastClickTime = -1f; return; }

        if (ExplorationModalEvents.MapEventModalActive)
        { lastClickTime = -1f; return; }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        { lastClickTime = -1f; return; }

        if (!TryHitThisOutpost())
        { lastClickTime = -1f; return; }

        if (outpost == null)
            return;

        outpost.RefreshStateVisuals();
        if (outpost.CurrentClaimState != TutorialOutpostClaimState.HeroClaimed)
            return;

        if (requireDoubleClick && !ConsumeDoubleClick())
            return;

        entering = outpost.TryEnterTutorialLobbyScene();
    }

    private void UpdateHoverHighlight()
    {
        JcBuildingSilhouetteFeature.ClearHoverHighlight(this);
        if (entering || DHGameEndState.IsEnding || WorldInputGate.IsBlocked ||
            ExplorationModalEvents.MapEventModalActive ||
            gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (outpost == null || outpost.CurrentClaimState != TutorialOutpostClaimState.HeroClaimed) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null || !BuildingEntryPointer.TryHit(worldCamera, rayDistance, out var hit)) return;
        if (hit.collider == null || hit.collider.GetComponentInParent<TutorialOutpostObject>() != outpost) return;
        var registry = Object.FindFirstObjectByType<PartyRegistry>();
        var party = registry != null && registry.PlayerParty != null
            ? registry.PlayerParty : Object.FindFirstObjectByType<PartyGridMover>();
        if (party == null || !party.gameObject.activeInHierarchy || DefeatedPartyReturnController.IsPartyWaiting(party)) return;
        if (!outpost.IsInteractionCell(party.GetCurrentGrid())) return;
        JcBuildingSilhouetteFeature.SetHoverHighlight(this, transform);
    }

    private bool TryHitThisOutpost()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null)
            return false;

        Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance))
            return false;

        TutorialOutpostObject hitOutpost = hit.collider != null
            ? hit.collider.GetComponentInParent<TutorialOutpostObject>()
            : null;

        return hitOutpost != null && hitOutpost == outpost;
    }

    private bool ConsumeDoubleClick()
    {
        float now = Time.unscaledTime;
        if (lastClickTime >= 0f && now - lastClickTime <= doubleClickThreshold)
        {
            lastClickTime = -1f;
            return true;
        }

        lastClickTime = now;
        return false;
    }
    private void OnDisable() { JcBuildingSilhouetteFeature.ClearHoverHighlight(this); lastClickTime = -1f; entering = false; }
}
