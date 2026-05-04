using UnityEngine;

[DisallowMultipleComponent]
public class KJ_JCStyleFogBridge : MonoBehaviour
{
    [SerializeField] private KJ_PlayFogManager fogManager;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PartyGridMover[] partyMovers;
    [SerializeField] private int sightRadiusCells = 3;
    [SerializeField] private int decayTurns = 3; // 인스펙터 노출: 소멸 턴 수
    [SerializeField] private bool revealOnStart = true;
    [SerializeField] private bool followFocusedParty = true;

    private PartyGridMover activeMover;
    [SerializeField] private KJ_FogToggleRevealer[] staticRevealers;

    private int lastTrackedDay = -1;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveActiveMover();
        
        // 초기 턴 동기화
        if (turnManager != null)
        {
            lastTrackedDay = turnManager.GetDay();
            if (fogManager != null)
            {
                Debug.Log($"[KJ_FogBridge] 초기 턴 동기화: {lastTrackedDay}");
                fogManager.AdvanceTurn(lastTrackedDay, decayTurns);
            }
        }

        RefreshFog();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void Update()
    {
        // 1. TurnManager 자동 갱신 및 날짜 변화 폴링
        var currentTM = FindFirstObjectByType<TurnManager>();
        if (currentTM != null)
        {
            turnManager = currentTM;
            int currentDay = turnManager.GetDay();
            if (currentDay != lastTrackedDay)
            {
                Debug.Log($"[KJ_FogBridge] 날짜 변화 감지: {lastTrackedDay} -> {currentDay}");
                OnDayAdvanced(currentDay);
                lastTrackedDay = currentDay;
            }
        }

        // 2. 가시성 소스 매 프레임 초기화 및 재등록 (rtCurrent 렌더링용)
        if (fogManager != null)
        {
            fogManager.ResetVisibilitySources();
            
            // 파티 이동자 등록 (이력 기록 포함 가능)
            if (partyMovers != null)
            {
                foreach (var mover in partyMovers)
                {
                    if (mover != null)
                        fogManager.AddVisibilitySource(mover.GetCurrentGrid(), sightRadiusCells, true);
                }
            }

            // 토글형 제거기 등록
            if (staticRevealers != null)
            {
                foreach (var rev in staticRevealers)
                {
                    if (rev != null && rev.isOpen)
                        fogManager.AddVisibilitySource(rev.GetGridPos(), rev.revealRadius, rev.recordHistory);
                }
            }
        }

        ResolveActiveMover();
    }

    public void ResolveReferences()
    {
        if (fogManager == null)
            fogManager = GetComponent<KJ_PlayFogManager>();

        if (fogManager == null)
            fogManager = FindFirstObjectByType<KJ_PlayFogManager>(FindObjectsInactive.Include);

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        if ((partyMovers == null || partyMovers.Length == 0) && partyRegistry != null)
            partyMovers = partyRegistry.PartyMovers;

        if (partyMovers == null || partyMovers.Length == 0)
            partyMovers = FindObjectsByType<PartyGridMover>(FindObjectsSortMode.None);
            
        // 씬 내 토글 오브젝트 수집
        staticRevealers = FindObjectsByType<KJ_FogToggleRevealer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    public void RefreshFog()
    {
        ResolveReferences();
    }

    public void RefreshAllPartiesFog()
    {
        RefreshFog();
    }

    private void OnDayAdvanced(int day)
    {
        Debug.Log($"[KJ_FogBridge] OnDayAdvanced 발생! Day: {day}, 소멸턴: {decayTurns}");
        
        if (fogManager != null)
        {
            // 턴이 바뀔 때 모든 파티의 현재 위치를 '방문함'으로 다시 남김
            if (partyMovers != null)
            {
                foreach (var mover in partyMovers)
                {
                    if (mover != null)
                        fogManager.AddVisibilitySource(mover.GetCurrentGrid(), sightRadiusCells, true);
                }
            }
            
            // 토글형 중 이력 기록 설정된 것들도 다시 남김
            if (staticRevealers != null)
            {
                foreach (var rev in staticRevealers)
                {
                    if (rev != null && rev.isOpen && rev.recordHistory)
                        fogManager.AddVisibilitySource(rev.GetGridPos(), rev.revealRadius, true);
                }
            }

            fogManager.AdvanceTurn(day, decayTurns);
        }
    }

    private PartyGridMover ResolveActiveMover()
    {
        if (partyMovers == null || partyMovers.Length == 0)
        {
            activeMover = null;
            return null;
        }

        if (!followFocusedParty || Camera.main == null)
        {
            activeMover = partyMovers[0];
            return activeMover;
        }

        float bestDistance = float.PositiveInfinity;
        PartyGridMover bestMover = null;
        Camera mainCamera = Camera.main;

        for (int i = 0; i < partyMovers.Length; i++)
        {
            PartyGridMover mover = partyMovers[i];
            if (mover == null)
                continue;

            Vector3 delta = mover.transform.position - mainCamera.transform.position;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                bestMover = mover;
            }
        }

        activeMover = bestMover;
        return activeMover;
    }
}
