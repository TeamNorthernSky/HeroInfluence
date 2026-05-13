using UnityEngine;
using UnityEngine.UI;

// [JC 신설 260513] 탐사씬 우클릭으로 띄우는 파티 정보 모달.
// 800×680, 4 슬롯 + 닫기 버튼. PartyComposition.UnitIndices를 슬롯 4개에 바인딩.
[DisallowMultipleComponent]
[RequireComponent(typeof(Modal))]
public class PartyInfoModal : MonoBehaviour
{
    [Header("Slot Views (1~N, 인스펙터 크기 = 모달이 표현 가능한 최대 슬롯 수)")]
    [SerializeField] private PartyMemberSlotView[] slotViews = new PartyMemberSlotView[4];

    [Header("Close")]
    [SerializeField] private Button closeButton;

    [Header("Detail Modal")]
    [SerializeField] private CharacterDetailModal detailModal;

    [Header("Layout (dynamic resize)")]
    [SerializeField] private RectTransform modalRect;
    [SerializeField] private RectTransform groupRect;
    [SerializeField] private float slotHeight = 130f;
    [SerializeField] private float slotSpacing = 20f;
    [SerializeField] private float headerHeight = 50f;
    [SerializeField] private float footerHeight = 50f;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        for (int i = 0; i < slotViews.Length; i++)
            slotViews[i]?.SetDetailModal(detailModal);
    }

    public void Open(PartyGridMover party)
    {
        if (party == null) return;
        PartyComposition comp = party.GetComponent<PartyComposition>();
        int[] indices = comp != null ? comp.UnitIndices : System.Array.Empty<int>();
        BindSlots(indices, isEnemy: false);
    }

    // [JC 260513] 적 파티 우클릭 시 동일 양식 모달 표시.
    public void Open(EnemyGridMover enemy)
    {
        if (enemy == null) return;
        EnemyComposition comp = enemy.GetComponent<EnemyComposition>();
        int[] indices = comp != null ? comp.UnitIndices : System.Array.Empty<int>();
        BindSlots(indices, isEnemy: true);
    }

    private void BindSlots(int[] indices, bool isEnemy)
    {
        // [JC 260513] 활성 슬롯 수에 따라 빈 칸 제외 + 그룹·모달 사이즈·슬롯 위치 동적 재계산.
        int activeCount = CountValidIndices(indices);
        float groupH = activeCount > 0
            ? activeCount * slotHeight + (activeCount - 1) * slotSpacing
            : slotHeight; // 0명일 경우 최소 슬롯 1칸 높이 유지 (시각적 비어있음 표시)
        float modalH = headerHeight + groupH + footerHeight;

        if (groupRect != null) groupRect.sizeDelta = new Vector2(groupRect.sizeDelta.x, groupH);
        if (modalRect != null) modalRect.sizeDelta = new Vector2(modalRect.sizeDelta.x, modalH);

        int activeIdx = 0;
        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotViews[i] == null) continue;
            int unitIndex = i < indices.Length ? indices[i] : 0;
            bool active = unitIndex > 0;
            slotViews[i].gameObject.SetActive(active);
            if (!active) continue;

            slotViews[i].Bind(unitIndex, isEnemy);
            RectTransform rt = slotViews[i].GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                float y = (groupH - slotHeight) * 0.5f - activeIdx * (slotHeight + slotSpacing);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
            }
            activeIdx++;
        }

        gameObject.SetActive(true);
    }

    private static int CountValidIndices(int[] indices)
    {
        int count = 0;
        if (indices == null) return 0;
        for (int i = 0; i < indices.Length; i++)
            if (indices[i] > 0) count++;
        return count;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
