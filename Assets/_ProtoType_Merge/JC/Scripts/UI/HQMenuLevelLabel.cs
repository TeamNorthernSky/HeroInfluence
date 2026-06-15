using TMPro;
using UnityEngine;

/// <summary>
/// HQLobbyScene의 본부 메뉴 버튼(BTN_HQLobby_Frame_HQMenu) 내부 레벨 라벨 갱신.
/// "본부 Lv.{N}" 형식으로 표시하고, HQ 업그레이드(HQStateManager.OnStateChanged) 시 자동 갱신.
/// </summary>
[DisallowMultipleComponent]
public class HQMenuLevelLabel : MonoBehaviour
{
    [Tooltip("버튼 내부 Text (TMP). 비우면 자식에서 자동 탐색")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private string format = "본부 Lv.{0}";

    private HQStateManager subscribedHQ;

    private void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
    }

    private void OnEnable() { TrySubscribe(); Refresh(); }
    private void OnDisable()
    {
        if (subscribedHQ != null) { subscribedHQ.OnStateChanged -= Refresh; subscribedHQ = null; }
    }
    private void Update() { if (subscribedHQ == null) TrySubscribe(); }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null || subscribedHQ != null) return;
        subscribedHQ = gm.HQ;
        subscribedHQ.OnStateChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        if (label == null) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;
        int lv = gm.HQ.GetLevel(HQDepartment.Headquarters);
        label.text = string.Format(format, lv);
    }
}
