using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [KJ 260714] 선택지 버튼 뷰 (ChoiceButton.prefab 루트). ChoiceArea(VerticalLayoutGroup) 아래 런타임 Instantiate.
/// 문구 표시 + 클릭 콜백 바인딩만 담당. 분기 평가/점프는 러너 소관.
/// </summary>
[DisallowMultipleComponent]
public class ChoiceButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    /// <summary>버튼 문구와 클릭 동작 설정. 재사용 대비 기존 리스너 제거 후 등록.</summary>
    public void Bind(string text, Action onClick)
    {
        if (label != null) label.text = text;
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }
    }
}
