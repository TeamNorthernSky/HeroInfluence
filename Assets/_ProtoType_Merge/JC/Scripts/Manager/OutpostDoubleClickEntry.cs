using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// [JC 260615] 점령(Claimed)한 거점 건물 더블클릭 → 본부(HQLobbyScene) 진입. (탐사 기획: 점령 건물도 본부와 동일 기능)
/// CastleDoubleClickEntry 패턴 복제. 모든 Outpost에 부트스트랩이 자동 부착(씬/프리팹 수정 불필요).
/// 조건: outpost.IsPlayerClaimed(점령 완료) + 더블클릭 + 입력 차단 가드 통과.
/// 동일 로비 씬을 본부와 공유한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Outpost))]
public class OutpostDoubleClickEntry : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private float doubleClickThreshold = 0.3f;
    [SerializeField] private float rayDistance = 1000f;

    private Outpost outpost;
    private float lastClickTime = -1f;

    private void Awake()
    {
        outpost = GetComponent<Outpost>();
        if (worldCamera == null) worldCamera = Camera.main;
    }

    private void Update()
    {
        if (DHGameEndState.IsEnding) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (outpost == null || !outpost.IsPlayerClaimed) return;

        if (WorldInputGate.IsBlocked) return;
        if (MapEventPanelUI.IsAnyActive) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance)) return;

        Outpost hitOutpost = hit.collider != null ? hit.collider.GetComponentInParent<Outpost>() : null;
        if (hitOutpost == null || hitOutpost.gameObject != gameObject) return;

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
        if (DHGameEndState.IsEnding) return;
        if (GameSceneManager.Instance != null) GameSceneManager.Instance.LoadLobby();
        else SceneManager.LoadScene("HQLobbyScene");
    }
}

/// <summary>모든 Outpost에 OutpostDoubleClickEntry를 자동 부착(씬/프리팹 비침습).</summary>
public static class OutpostDoubleClickEntryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += (_, __) => AttachAll();
        // [JC 260617] 거점은 런타임 생성(Clone)이라 sceneLoaded 시점엔 없을 수 있음 →
        //   점령 시점(더블클릭이 유효해지는 바로 그 때)에 확실히 부착. 중복 부착 가드 포함.
        Outpost.OutpostClaimed += AttachTo;
        AttachAll();
    }

    private static void AttachTo(Outpost outpost)
    {
        if (outpost != null && outpost.GetComponent<OutpostDoubleClickEntry>() == null)
            outpost.gameObject.AddComponent<OutpostDoubleClickEntry>();
    }

    private static void AttachAll()
    {
        var outposts = Object.FindObjectsByType<Outpost>(FindObjectsSortMode.None);
        for (int i = 0; i < outposts.Length; i++)
            AttachTo(outposts[i]);
    }
}
