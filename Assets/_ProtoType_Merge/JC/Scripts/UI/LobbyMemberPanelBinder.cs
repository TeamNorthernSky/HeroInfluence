using TMPro;
using UnityEngine;

/// <summary>
/// 로비 Member1~4 모달 각각에 부착. slotIndex(0~3)에 해당하는 본부 상주 파티 멤버 1명을 표시.
/// 본부 파티 정책: PersistentUnitRepository에서 hqPartyId 조회 → 슬롯이 비면 EmptyIndicator 표시.
/// 데이터 소스: PersistentUnitRepository(영속 상태) + DHCsvTemplateCatalog(이름/클래스).
/// 모달 SetActive(true) 시점에 OnEnable로 자동 갱신.
/// </summary>
[DisallowMultipleComponent]
public class LobbyMemberPanelBinder : MonoBehaviour
{
    [Header("Slot Configuration")]
    [Tooltip("본부 파티 내 슬롯 인덱스 (0~3)")]
    [SerializeField] private int slotIndex = 0;

    [Tooltip("HQVisitState가 미동작 시 사용할 폴백 파티 ID. 빈 문자열이면 폴백 없음(=항상 HQVisitState 따름). 테스트/디버그용")]
    [SerializeField] private string fallbackPartyId = "";

    [Header("UI Bindings (모두 옵션 — 필요한 것만 인스펙터 연결)")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text classText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text atkText;
    [SerializeField] private TMP_Text defText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text favorabilityText;

    [Header("Empty Slot Handling")]
    [Tooltip("슬롯이 비었을 때 활성화할 GameObject (예: '빈 슬롯' 텍스트, placeholder 이미지)")]
    [SerializeField] private GameObject emptySlotIndicator;

    [Tooltip("슬롯이 비었을 때 비활성화할 GameObject (예: 캐릭터 정보 패널 컨테이너)")]
    [SerializeField] private GameObject memberInfoContainer;

    private void OnEnable()
    {
        Refresh();
    }

    [ContextMenu("Refresh")]
    public void Refresh()
    {
        if (TryResolveMember(out UnitPersistentData unit, out UnitData template))
            ApplyMember(unit, template);
        else
            ApplyEmpty();
    }

    private bool TryResolveMember(out UnitPersistentData unit, out UnitData template)
    {
        unit = null;
        template = null;

        string effectivePartyId = ResolveEffectivePartyId();
        if (string.IsNullOrEmpty(effectivePartyId))
            return false;

        // [JC 수정 260512] 머지 사이클: 파티 책임이 PartyPersistentRepository로 이관됨
        PersistentUnitRepository repo = PersistentUnitRepository.Instance;
        PartyPersistentRepository partyRepo = PartyPersistentRepository.Instance;
        if (repo == null || partyRepo == null)
            return false;

        if (!partyRepo.TryGetParty(effectivePartyId, out PartyPersistentData party) || party == null)
            return false;

        if (slotIndex < 0 || slotIndex >= party.UnitIndices.Count)
            return false;

        int unitIndex = party.UnitIndices[slotIndex];
        if (unitIndex <= 0)
            return false;

        if (!repo.TryGetUnit(unitIndex, out unit) || unit == null)
            return false;

        // 템플릿은 옵션. 없어도 PersistentData로 기본 표시 가능
        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey))
            catalog.TryGetPlayerTemplate(unit.UnitTemplateKey, out template);

        return true;
    }

    private void ApplyMember(UnitPersistentData unit, UnitData template)
    {
        if (emptySlotIndicator != null) emptySlotIndicator.SetActive(false);
        if (memberInfoContainer != null) memberInfoContainer.SetActive(true);

        string displayName = template != null && !string.IsNullOrWhiteSpace(template.Name)
            ? template.Name
            : unit.UnitTemplateKey;
        string displayClass = template != null && !string.IsNullOrWhiteSpace(template.UnitType)
            ? template.UnitType
            : "-";

        StatBlock s = unit.BaseStats;

        SetText(nameText, displayName);
        SetText(classText, displayClass);
        SetText(levelText, $"Lv {unit.Level}");
        SetText(hpText, $"HP {s.HP:F0}");
        SetText(atkText, $"ATK {s.Atk:F0}");
        SetText(defText, $"DEF {s.DEF:F0}");
        SetText(speedText, $"SPD {s.Speed:F0}");
        SetText(favorabilityText, $"IP {unit.Favorability}");
    }

    private void ApplyEmpty()
    {
        if (emptySlotIndicator != null) emptySlotIndicator.SetActive(true);
        if (memberInfoContainer != null) memberInfoContainer.SetActive(false);

        SetText(nameText, string.Empty);
        SetText(classText, string.Empty);
        SetText(levelText, string.Empty);
        SetText(hpText, string.Empty);
        SetText(atkText, string.Empty);
        SetText(defText, string.Empty);
        SetText(speedText, string.Empty);
        SetText(favorabilityText, string.Empty);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }

    private string ResolveEffectivePartyId()
    {
        HQVisitState state = HQVisitState.Instance;
        if (state != null && state.HasVisitingParty)
        {
            foreach (var id in state.VisitingPartyIds)
                return id;
        }

        return string.IsNullOrWhiteSpace(fallbackPartyId) ? null : fallbackPartyId;
    }
}
