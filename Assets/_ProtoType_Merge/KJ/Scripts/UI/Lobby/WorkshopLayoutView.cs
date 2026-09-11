using TMPro;
using UnityEngine;

/// <summary>공방 모달의 안내 문구. 주 버튼 라벨(제작/강화/사유)을 읽어 문구 칸에 옮긴다. 공방 로직은 기존 컨트롤러가 담당한다.</summary>
public sealed class WorkshopLayoutView : MonoBehaviour
{
    [SerializeField] private TMP_Text actionSource;
    [SerializeField] private TMP_Text prompt;

    private void OnEnable() => RefreshPrompt();

    private void LateUpdate() => RefreshPrompt();

    private void RefreshPrompt()
    {
        if (actionSource == null || prompt == null) return;
        string action = actionSource.text;
        string text = action == "제작" || action == "강화"
            ? "선택한 코어를\n" + action + "하시겠습니까?"
            : action;
        if (prompt.text != text) prompt.text = text;
    }
}
