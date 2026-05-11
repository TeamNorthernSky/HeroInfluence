using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyMenuController : MonoBehaviour
{
    private const string Prefix = "BTN_Lobby_";
    private const string PlayScene = "DHScene";

    [SerializeField] private GameObject _modalHQ;
    [SerializeField] private GameObject _modalBroadcast;
    [SerializeField] private GameObject _modalRecruitment;
    [SerializeField] private GameObject _modalEnhancement;
    [SerializeField] private GameObject _modalResearch;
    [SerializeField] private GameObject _modalReplace;
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
        switch (key)
        {
            case "HQ":           handler = OnClickHQ;          return true;
            case "Broadcast":    handler = OnClickBroadcast;   return true;
            case "Enhancement":  handler = OnClickEnhancement; return true;
            case "Research":     handler = OnClickResearch;    return true;
            case "Recruitment":  handler = OnClickRecruitment; return true;
            case "Go":           handler = OnClickGo;          return true;
            case "Exit":         handler = OnClickExit;        return true;
            case "EndTurn":      handler = OnClickEndTurn;     return true;
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

        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return true;
        if (!repo.TryGetParty(partyId, out var party) || party == null) return true;
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
        SceneManager.LoadScene(PlayScene);
    }

    public void OnClickEndTurn()
    {
        Debug.Log($"[Lobby] EndTurn → {PlayScene}");
        PersistentStateDebugLogger.Dump("Lobby EndTurn (before LoadScene DHScene)");
        SceneManager.LoadScene(PlayScene);
    }
}
