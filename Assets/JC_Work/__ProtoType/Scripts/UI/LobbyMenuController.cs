using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyMenuController : MonoBehaviour
{
    private const string Prefix = "BTN_Lobby_";
    private const string PlayScene = "TEST_PlayScene";

    [SerializeField] private GameObject _modalHQ;
    [SerializeField] private GameObject _modalBroadcast;
    [SerializeField] private GameObject _modalRecruitment;
    [SerializeField] private GameObject _modalEnhancement;
    [SerializeField] private GameObject _modalResearch;
    [SerializeField] private GameObject _modalReplace;
    [SerializeField] private GameObject _modalMember1;
    [SerializeField] private GameObject _modalMember2;
    [SerializeField] private GameObject _modalMember3;
    [SerializeField] private GameObject _modalMember4;

    private void Awake()
    {
        BindLobbyButtons();
        BindModalCloseButtons();
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
    public void OnClickMember1()      { Debug.Log("[Lobby] Member1");     OpenModal(_modalMember1); }
    public void OnClickMember2()      { Debug.Log("[Lobby] Member2");     OpenModal(_modalMember2); }
    public void OnClickMember3()      { Debug.Log("[Lobby] Member3");     OpenModal(_modalMember3); }
    public void OnClickMember4()      { Debug.Log("[Lobby] Member4");     OpenModal(_modalMember4); }

    public void OnClickGo()
    {
        Debug.Log("[Lobby] Go (보류 — 기능 명시 대기)");
    }

    public void OnClickExit()
    {
        Debug.Log($"[Lobby] Exit → {PlayScene}");
        SceneManager.LoadScene(PlayScene);
    }

    public void OnClickEndTurn()
    {
        Debug.Log($"[Lobby] EndTurn → {PlayScene}");
        SceneManager.LoadScene(PlayScene);
    }
}
