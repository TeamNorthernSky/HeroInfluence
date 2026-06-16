using TMPro;
using UnityEngine;

/// <summary>
/// 부서 레벨을 "{prefix} Lv.{n}" 형식으로 표시하는 라벨. (시설 버튼·팝업 타이틀 공용)
/// HQ.OnStateChanged 구독으로 업그레이드 시 즉시 갱신. 레벨 0(미해금)이면 prefix만 표시.
/// </summary>
[DisallowMultipleComponent]
public class DeptLevelLabel : MonoBehaviour
{
    [SerializeField] private HQDepartment department;
    [SerializeField] private string prefix = "";
    [SerializeField] private TMP_Text label;
    [Tooltip("레벨 0(미해금)일 때도 'Lv.0'을 표시할지. false면 prefix만 표시")]
    [SerializeField] private bool showWhenLocked;

    private HQStateManager subscribedHQ;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
    }

    private void OnEnable() { TrySubscribe(); UpdateLabel(); }

    private void OnDisable()
    {
        if (subscribedHQ != null) { subscribedHQ.OnStateChanged -= UpdateLabel; subscribedHQ = null; }
    }

    private void Update()
    {
        if (subscribedHQ == null) TrySubscribe();
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;
        if (subscribedHQ == null)
        {
            subscribedHQ = gm.HQ;
            subscribedHQ.OnStateChanged += UpdateLabel;
            UpdateLabel();
        }
    }

    private void UpdateLabel()
    {
        if (label == null) return;
        var gm = GameManager.Instance;
        int lv = gm != null && gm.HQ != null ? gm.HQ.GetLevel(department) : 0;
        label.text = (lv >= 1 || showWhenLocked) ? $"{prefix} Lv.{lv}" : prefix;
    }
}
