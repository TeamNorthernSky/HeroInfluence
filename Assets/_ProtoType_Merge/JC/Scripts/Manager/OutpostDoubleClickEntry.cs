using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// [JC 260615] 점령(Claimed)한 거점 건물 더블클릭 → 본부(HQLobbyScene) 진입. (탐사 기획: 점령 건물도 본부와 동일 기능)
/// HeroUnionDoubleClickEntry 패턴 복제. 모든 Outpost에 부트스트랩이 자동 부착(씬/프리팹 수정 불필요).
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
        if (ExplorationModalEvents.MapEventModalActive) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance)) return;

        Outpost hitOutpost = hit.collider != null ? hit.collider.GetComponentInParent<Outpost>() : null;
        if (hitOutpost == null || hitOutpost.gameObject != gameObject) return;
        if (!HasPlayerPartyAtInteractionCell(hitOutpost)) return;

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

    private static bool HasPlayerPartyAtInteractionCell(Outpost targetOutpost)
    {
        if (targetOutpost == null)
            return false;

        PartyGridMover party = ResolvePlayerParty();
        if (party == null || !party.gameObject.activeInHierarchy)
            return false;

        if (DefeatedPartyReturnController.IsPartyWaiting(party))
            return false;

        GridManager gridManager = Game.Grid != null ? Game.Grid : Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
            return false;

        Vector2Int partyGrid = party.GetCurrentGrid();
        IReadOnlyList<Vector2Int> interactionCells = targetOutpost.GetAdjacentInteractionCells(gridManager);
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

/// <summary>모든 Outpost에 OutpostDoubleClickEntry + OutpostVisitIndicator를 자동 부착(씬/프리팹 비침습).</summary>
public static class OutpostDoubleClickEntryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        // [JC 260618] 첫 점령 즉시 부착(즉시성). 재진입(이미 점령) 거점은 OutpostClaimed가 발화되지 않으므로
        //   호스트가 씬 로드마다 거점 등장까지 지연·폴링 부착한다(아래 OutpostFeatureAttachHost).
        Outpost.OutpostClaimed += AttachTo;
        EnsureHost();
    }

    /// <summary>거점 등장 타이밍을 놓치지 않도록 씬 로드마다 폴링 부착하는 영속 호스트 보장.</summary>
    private static void EnsureHost()
    {
        if (Object.FindFirstObjectByType<OutpostFeatureAttachHost>() != null) return;
        GameObject go = new GameObject("[OutpostFeatureAttachHost]");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<OutpostFeatureAttachHost>();
    }

    internal static void AttachTo(Outpost outpost)
    {
        if (outpost == null) return;
        if (outpost.GetComponent<OutpostDoubleClickEntry>() == null)
            outpost.gameObject.AddComponent<OutpostDoubleClickEntry>();
        if (outpost.GetComponent<OutpostVisitIndicator>() == null)
            outpost.gameObject.AddComponent<OutpostVisitIndicator>();
    }

    internal static void AttachAll()
    {
        var outposts = Object.FindObjectsByType<Outpost>(FindObjectsSortMode.None);
        for (int i = 0; i < outposts.Length; i++)
            AttachTo(outposts[i]);
    }
}

/// <summary>
/// [JC 260618] 거점은 LevelLoader.Start(SpawnOutposts)에서 동적 생성되므로 sceneLoaded 콜백(=Start 이전) 1회로는
/// 부착 타이밍을 놓친다(재진입 시 더블클릭 진입 불가 버그의 원인). 본 호스트가 씬 로드마다 몇 프레임 폴링하며
/// AttachAll을 재시도해 거점 등장 직후 확실히 부착한다(HeroUnion wiring의 LateUpdate 폴링과 동일 취지). DontDestroyOnLoad.
/// </summary>
public class OutpostFeatureAttachHost : MonoBehaviour
{
    private const int AttachRetryFrames = 10;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(AttachRoutine());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(AttachRoutine());
    }

    private IEnumerator AttachRoutine()
    {
        for (int i = 0; i < AttachRetryFrames; i++)
        {
            OutpostDoubleClickEntryBootstrap.AttachAll();
            yield return null;
        }
    }
}

/// <summary>
/// [JC 260618] 점령(Claimed)한 거점에 파티가 인접 상호작용 셀에 있으면 방문 인디케이터를 표시한다.
/// HeroUnionHQVisitDetector 패턴의 거점 축약판 — 전역 HQVisitState 대신 거점별 인디케이터만 토글.
/// 인디케이터 스프라이트/배치는 런타임에 본부(HeroUnion)의 "VisitorIndicator"에서 복사해 본부와 일관성 유지.
/// 부트스트랩이 자동 부착(씬/프리팹 비침습). 동적 mover/타이밍 회피 위해 폴링 평가(0.25s).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Outpost))]
public class OutpostVisitIndicator : MonoBehaviour
{
    private const float EvalInterval = 0.25f;

    private Outpost outpost;
    private GameObject indicator;
    private float nextEvalTime;

    private void Awake()
    {
        outpost = GetComponent<Outpost>();
    }

    private void OnDisable()
    {
        if (indicator != null) indicator.SetActive(false);
    }

    private static readonly List<string> visitBuffer = new List<string>();

    private void Update()
    {
        if (Time.unscaledTime < nextEvalTime) return;
        nextEvalTime = Time.unscaledTime + EvalInterval;

        if (DHGameEndState.IsEnding)
        {
            if (indicator != null && indicator.activeSelf) indicator.SetActive(false);
            RegisterVisit(null); // 엔딩 중에는 방문 등록 해제
            return;
        }
        Evaluate();
    }

    private void Evaluate()
    {
        if (outpost == null) return;
        EnsureIndicator(); // 스프라이트 미확보 시 indicator==null일 수 있으나 방문 등록은 진행

        // 점령 거점에 한해 인접 상호작용 셀의 방문 파티를 수집.
        List<string> visitingParties = outpost.IsPlayerClaimed ? CollectVisitingParties() : null;

        // [JC 260618] HQVisitState에 거점별 source로 등록 → 시설(visitingOnly)·출전이 본부 방문과 동일하게 다룬다.
        RegisterVisit(visitingParties);

        bool visiting = visitingParties != null && visitingParties.Count > 0;
        if (indicator != null && indicator.activeSelf != visiting) indicator.SetActive(visiting);
    }

    /// <summary>거점별 안정적 source 키(progressKey)로 방문 파티 집합을 HQVisitState에 등록/해제.</summary>
    private void RegisterVisit(List<string> partyIds)
    {
        HQVisitState state = HQVisitState.Instance;
        if (state == null) return;
        string sourceKey = ResolveSourceKey();
        if (string.IsNullOrEmpty(sourceKey)) return;

        if (partyIds == null || partyIds.Count == 0) state.ClearSource(sourceKey);
        else state.SetVisitingParties(sourceKey, partyIds);
    }

    private string ResolveSourceKey()
    {
        if (outpost == null) return null;
        GridManager grid = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        return grid != null ? outpost.GetProgressKey(grid) : null;
    }

    /// <summary>인접 상호작용 셀에 위치한 파티들의 PartyId 목록(공유 버퍼 반환 — 호출 즉시 소비할 것).</summary>
    private List<string> CollectVisitingParties()
    {
        visitBuffer.Clear();
        GridManager grid = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        if (grid == null) return visitBuffer;

        IReadOnlyList<Vector2Int> cells = outpost.GetAdjacentInteractionCells(grid);
        if (cells == null || cells.Count == 0) return visitBuffer;

        var movers = FindObjectsByType<PartyGridMover>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < movers.Length; i++)
        {
            PartyGridMover mover = movers[i];
            if (mover == null || !mover.gameObject.activeInHierarchy) continue;
            if (DefeatedPartyReturnController.IsPartyWaiting(mover)) continue;

            Vector2Int g = mover.GetCurrentGrid();
            bool atCell = false;
            for (int c = 0; c < cells.Count; c++)
                if (cells[c] == g) { atCell = true; break; }
            if (!atCell) continue;

            PartyIdentity identity = mover.GetComponent<PartyIdentity>();
            string partyId = identity != null ? identity.PartyId : mover.gameObject.name;
            if (!string.IsNullOrWhiteSpace(partyId)) visitBuffer.Add(partyId);
        }
        return visitBuffer;
    }

    /// <summary>인디케이터 GO를 1회 생성. 스프라이트·배치는 본부 와이어링(HeroUnionVisitWiringForDHScene3)에서 읽어 본부와 일관.
    /// 컴포넌트는 씬에 항상 존재하므로 wire 타이밍과 무관(없으면 보류 후 다음 평가 재시도).</summary>
    private void EnsureIndicator()
    {
        if (indicator != null) return;

        HeroUnionVisitWiringForDHScene3 wiring = FindFirstObjectByType<HeroUnionVisitWiringForDHScene3>();
        if (wiring == null || wiring.VisitorIndicatorSprite == null) return;

        indicator = new GameObject("OutpostVisitorIndicator");
        indicator.transform.SetParent(transform, false);
        indicator.transform.localPosition = wiring.VisitorIndicatorLocalPosition;
        indicator.transform.localEulerAngles = wiring.VisitorIndicatorLocalEulerAngles;
        indicator.transform.localScale = wiring.VisitorIndicatorLocalScale;

        SpriteRenderer sr = indicator.AddComponent<SpriteRenderer>();
        sr.sprite = wiring.VisitorIndicatorSprite;
        sr.sortingOrder = wiring.VisitorIndicatorSortingOrder;

        indicator.SetActive(false);
    }
}
