using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 마우스 호버 시 자식 툴팁 GO를 표시하는 단순 컴포넌트(클릭 동작 없음).
/// HeroInfo/HeroStatus 모달의 정보 버튼(캐릭터 스킬/장비/장비 스킬) 등에 부착.
/// (KJ의 HoverTooltip과 클래스명 충돌을 피하기 위해 HeroInfoTooltip으로 분리)
/// </summary>
[DisallowMultipleComponent]
public class HeroInfoTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("호버 시 켤 툴팁 GO(평소 비활성). 보통 버튼 자식 패널")]
    [SerializeField] private GameObject tooltip;
    [Tooltip("툴팁 본문 TMP(선택). SetBody로 갱신")]
    [SerializeField] private TMP_Text bodyText;

    public void SetBody(string text)
    {
        if (bodyText != null) bodyText.text = text;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.SetActive(false);
    }

    private void OnDisable()
    {
        if (tooltip != null) tooltip.SetActive(false);
    }
}
