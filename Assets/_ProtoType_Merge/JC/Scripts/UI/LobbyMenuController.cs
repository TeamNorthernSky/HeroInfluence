using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyMenuController : MonoBehaviour
{
    private const string Prefix = "BTN_Lobby_";

    // [JC 260514] PlayScene const 폐기 → 동적 property. GameSceneManager.Instance.ExplorationScene 토글 반영.
    private string PlayScene => GameSceneManager.Instance != null
        ? GameSceneManager.Instance.ExplorationScene
        : "DHScene";

    [SerializeField] private GameObject _modalHQ;
    [SerializeField] private GameObject _modalBroadcast;
    [SerializeField] private GameObject _modalRecruitment;
    [SerializeField] private GameObject _modalEnhancement;
    [SerializeField] private GameObject _modalResearch;
    [SerializeField] private GameObject _modalReplace;
    [SerializeField] private GameObject _modalTraining;       // [JC 260515] 트레이닝 임시 placeholder 모달
    [SerializeField] private GameObject _modalEndTurnConfirm; // [JC 260515] 턴 종료 확인 모달 (Yes/No)
    [SerializeField] private GameObject _modalCurrentParty;   // 명단 패널 역할로 재정의 (다키스트 던전 스타일, 항상 활성)
    [SerializeField] private GameObject _modalHeroInfo;       // 공용 히어로 정보 모달 (HeroProfileButton 클릭 시 표시)
    [SerializeField] private GameObject _modalMember1;        // (레거시, 폐기 후보 — HeroInfoModal로 대체)
    [SerializeField] private GameObject _modalMember2;        // (레거시, 폐기 후보)
    [SerializeField] private GameObject _modalMember3;        // (레거시, 폐기 후보)
    [SerializeField] private GameObject _modalMember4;        // (레거시, 폐기 후보)

    [Header("HQ Party Policy")]
    [Tooltip("HQVisitState 미동작 시 폴백 파티 ID. 빈 문자열이면 폴백 없음. 테스트/디버그용")]
    [SerializeField] private string fallbackPartyId = "";

    [Tooltip("true: 본부 슬롯이 비었으면 Member 모달 자체를 열지 않음 / false: 비어도 모달 열되 빈 슬롯 표시")]
    [SerializeField] private bool hideMemberModalWhenSlotEmpty = false;

    private void Awake()
    {
        BindLobbyButtons();
        BindModalCloseButtons();
    }

    private void Start()
    {
        SubscribeHQVisitState();
        ApplyCurrentPartyModalState();
    }

    private void OnDestroy()
    {
        UnsubscribeHQVisitState();
    }

    private void SubscribeHQVisitState()
    {
        var state = HQVisitState.Instance;
        if (state != null)
            state.OnVisitingChanged += OnVisitingChangedHandler;
    }

    private void UnsubscribeHQVisitState()
    {
        var state = HQVisitState.Instance;
        if (state != null)
            state.OnVisitingChanged -= OnVisitingChangedHandler;
    }

    private void OnVisitingChangedHandler()
    {
        ApplyCurrentPartyModalState();
    }

    private void ApplyCurrentPartyModalState()
    {
        // 명단 패널(다키스트 던전 스타일)로 재정의됨. HQVisitState 의존 제거, 로비 진입 시 항상 활성.
        if (_modalCurrentParty == null) return;
        if (!_modalCurrentParty.activeSelf)
            _modalCurrentParty.SetActive(true);
    }

    private bool HasResolvedPartyId()
    {
        return !string.IsNullOrEmpty(ResolveEffectivePartyId());
    }

    private string ResolveEffectivePartyId()
    {
        var state = HQVisitState.Instance;
        if (state != null && state.HasVisitingParty)
        {
            // 다중 방문 중 임의 첫 번째. IsHQMemberSlotEmpty 등 레거시 경로 호환용
            foreach (var id in state.VisitingPartyIds)
                return id;
        }

        return string.IsNullOrWhiteSpace(fallbackPartyId) ? null : fallbackPartyId;
    }

    private void BindLobbyButtons()
    {
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int matched = 0, missed = 0;
        for (int i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            var n = btn.gameObject.name;
            if (!n.StartsWith(Prefix)) continue;
            var key = n.Substring(Prefix.Length);
            if (TryGetHandler(key, out var handler))
            {
                btn.onClick.AddListener(handler);
                matched++;
            }
            else
            {
                Debug.LogWarning($"[Lobby] 매칭 안 된 버튼: {n}");
                missed++;
            }
        }
        Debug.Log($"[Lobby] 버튼 매칭 {matched}건, 미매칭 {missed}건");
    }

    private bool TryGetHandler(string key, out UnityAction handler)
    {
        // [JC 260515] case-insensitive 매칭 — 기존 "replace"/"member1~4" 케이스가 대소문자 불일치로 미작동하던 버그 보정
        string normalized = key != null ? key.ToLowerInvariant() : string.Empty;
        switch (normalized)
        {
            case "hq":           handler = OnClickHQ;          return true;
            case "broadcast":    handler = OnClickBroadcast;   return true;
            case "enhancement":  handler = OnClickEnhancement; return true;
            case "research":     handler = OnClickResearch;    return true;
            case "recruitment":  handler = OnClickRecruitment; return true;
            case "go":           handler = OnClickGo;          return true;
            case "exit":         handler = OnClickExit;        return true;
            case "endturn":      handler = OnClickEndTurn;     return true;
            case "training":     handler = OnClickTraining;    return true;
            case "member1":      handler = OnClickMember1;     return true;
            case "member2":      handler = OnClickMember2;     return true;
            case "member3":      handler = OnClickMember3;     return true;
            case "member4":      handler = OnClickMember4;     return true;
            case "replace":      handler = OnClickReplace;     return true;
            default:             handler = null;               return false;
        }
    }

    private void BindModalCloseButtons()
    {
        BindCloseFor(_modalHQ);
        BindCloseFor(_modalBroadcast);
        BindCloseFor(_modalRecruitment);
        BindCloseFor(_modalEnhancement);
        BindCloseFor(_modalResearch);
        BindCloseFor(_modalReplace);
        BindCloseFor(_modalTraining);
        BindCloseFor(_modalCurrentParty);
        BindCloseFor(_modalHeroInfo);
        BindCloseFor(_modalMember1);
        BindCloseFor(_modalMember2);
        BindCloseFor(_modalMember3);
        BindCloseFor(_modalMember4);
    }

    private void BindCloseFor(GameObject modal)
    {
        if (modal == null) return;
        var target = modal;
        var closes = modal.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < closes.Length; i++)
        {
            if (closes[i].gameObject.name.Contains("CloseButton"))
                closes[i].onClick.AddListener(() => target.SetActive(false));
        }
    }

    private static void OpenModal(GameObject modal)
    {
        if (modal != null) modal.SetActive(true);
    }

    public void OnClickHQ()           { Debug.Log("[Lobby] HQ");          OpenModal(_modalHQ); }
    public void OnClickBroadcast()    { Debug.Log("[Lobby] Broadcast");   OpenModal(_modalBroadcast); }
    public void OnClickEnhancement()  { Debug.Log("[Lobby] Enhancement"); OpenModal(_modalEnhancement); }
    public void OnClickResearch()     { Debug.Log("[Lobby] Research");    OpenModal(_modalResearch); }
    public void OnClickRecruitment()  { Debug.Log("[Lobby] Recruitment"); OpenModal(_modalRecruitment); }
    public void OnClickReplace()      { Debug.Log("[Lobby] Replace");     OpenModal(_modalReplace); }
    public void OnClickTraining()     { Debug.Log("[Lobby] Training");    OpenModal(_modalTraining); }
    public void OnClickMember1()      { Debug.Log("[Lobby] Member1");     OpenMemberModal(_modalMember1, 0); }
    public void OnClickMember2()      { Debug.Log("[Lobby] Member2");     OpenMemberModal(_modalMember2, 1); }
    public void OnClickMember3()      { Debug.Log("[Lobby] Member3");     OpenMemberModal(_modalMember3, 2); }
    public void OnClickMember4()      { Debug.Log("[Lobby] Member4");     OpenMemberModal(_modalMember4, 3); }

    private void OpenMemberModal(GameObject modal, int slotIndex)
    {
        string partyId = ResolveEffectivePartyId();
        if (hideMemberModalWhenSlotEmpty && IsHQMemberSlotEmpty(partyId, slotIndex))
        {
            Debug.Log($"[Lobby] Member{slotIndex + 1} 슬롯 비어있어 모달 미오픈 (partyId='{partyId ?? "(none)"}')");
            return;
        }
        OpenModal(modal);
    }

    private static bool IsHQMemberSlotEmpty(string partyId, int slotIndex)
    {
        if (string.IsNullOrEmpty(partyId)) return true;

        // [JC 수정 260512] 머지 사이클: 파티 책임이 PartyPersistentRepository로 이관됨
        var repo = PersistentUnitRepository.Instance;
        var partyRepo = PartyPersistentRepository.Instance;
        if (repo == null || partyRepo == null) return true;
        if (!partyRepo.TryGetParty(partyId, out var party) || party == null) return true;
        if (slotIndex < 0 || slotIndex >= party.UnitIndices.Count) return true;
        int unitIndex = party.UnitIndices[slotIndex];
        if (unitIndex <= 0) return true;
        return !repo.ContainsUnit(unitIndex);
    }

    public void OnClickGo()
    {
        Debug.Log("[Lobby] Go (보류 — 기능 명시 대기)");
    }

    public void OnClickExit()
    {
        Debug.Log($"[Lobby] Exit → {PlayScene}");
        PersistentStateDebugLogger.Dump("Lobby Exit (before LoadScene DHScene)");
        // [JC 260514] GameSceneManager Instance 경유.
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene(PlayScene);
        else
            SceneManager.LoadScene(PlayScene);
    }

    // [JC 260515] 턴 종료 흐름 변경 — 확인 모달(Yes/No) 거쳐 진행.
    //   Yes  → CurrentDay++ + 로비 아웃(LoadScene PlayScene)
    //   No / ESC → 모달 닫기 (close 버튼 자동 바인딩 + ESC는 SystemMenuController.ModalRegistry 정책)
    public void OnClickEndTurn()
    {
        Debug.Log("[Lobby] EndTurn → 확인 모달 표시");
        if (_modalEndTurnConfirm != null)
            OpenModal(_modalEndTurnConfirm);
        else
            ConfirmEndTurnYes(); // fallback (모달 미연결 시 직접 진행)
    }

    public void OnClickEndTurnConfirmYes() => ConfirmEndTurnYes();
    public void OnClickEndTurnConfirmNo()
    {
        if (_modalEndTurnConfirm != null)
            _modalEndTurnConfirm.SetActive(false);
    }

    private void ConfirmEndTurnYes()
    {
        if (_modalEndTurnConfirm != null)
            _modalEndTurnConfirm.SetActive(false);

        // 턴 진행 — TurnManager는 씬 종속이라 로비에서 직접 호출 불가.
        // GameManager.CurrentDay 영속 데이터를 직접 증가 → 탐사씬 재진입 시 TurnManager.Awake가 복원.
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.CurrentDay = gm.CurrentDay + 1;
            Debug.Log($"[Lobby] EndTurn 확정 → Day={gm.CurrentDay}");
        }

        Debug.Log($"[Lobby] LoadScene → {PlayScene}");
        PersistentStateDebugLogger.Dump("Lobby EndTurn (before LoadScene DHScene)");
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene(PlayScene);
        else
            SceneManager.LoadScene(PlayScene);
    }
}
