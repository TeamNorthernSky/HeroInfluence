using UnityEngine;
using UnityEngine.EventSystems;

// JC | 2026-11-12 | IP 아이콘·숫자 호버를 동일한 칭호 툴팁에 연결하며 기존 숫자 표시는 유지합니다.
public sealed class JcInfluenceBadgeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [HideInInspector] public JcHeroInfluenceBadge badge;
    public void OnPointerEnter(PointerEventData data) { if (badge) badge.OnPointerEnter(data); }
    public void OnPointerExit(PointerEventData data) { if (badge) badge.OnPointerExit(data); }
    void OnDisable() { if (badge) badge.EndHover(); }
}
