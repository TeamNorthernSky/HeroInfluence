using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// 공용 히어로 정보 모달. HeroProfileButton 클릭 시 unitIndex만 받아 데이터 교체 후 표시.
/// 모달 GameObject는 인스펙터에서 본 컴포넌트 부착(자기 자신 또는 자식)으로 사용.
/// </summary>
[DisallowMultipleComponent]
public class HeroInfoModal : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("SetActive 토글 대상. 비어두면 본 컴포넌트가 부착된 GameObject 사용")]
    [SerializeField] private GameObject modalRoot;

    [Header("UI Bindings (옵션 — 필요한 것만 연결)")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text classText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text atkText;
    [SerializeField] private TMP_Text defText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text favorabilityText;
    [SerializeField] private TMP_Text partyIdText;

    public void Open(int unitIndex)
    {
        if (!TryResolve(unitIndex, out UnitPersistentData unit, out UnitData template, out string partyId))
        {
            Debug.LogWarning($"[HeroInfoModal] unitIndex {unitIndex} 조회 실패");
            return;
        }

        Apply(unit, template, partyId);
        GetRoot().SetActive(true);
    }

    public void Close()
    {
        GetRoot().SetActive(false);
    }

    private GameObject GetRoot()
    {
        return modalRoot != null ? modalRoot : gameObject;
    }

    private static bool TryResolve(int unitIndex, out UnitPersistentData unit, out UnitData template, out string partyId)
    {
        unit = null;
        template = null;
        partyId = null;

        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return false;

        if (!repo.TryGetUnit(unitIndex, out unit) || unit == null) return false;

        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey))
            catalog.TryGetPlayerTemplate(unit.UnitTemplateKey, out template);

        // unit이 어느 파티에 속하는지 역조회
        for (int i = 0; i < repo.Parties.Count; i++)
        {
            var party = repo.Parties[i];
            if (party == null) continue;
            if (party.UnitIndices.Contains(unitIndex))
            {
                partyId = party.PartyId;
                break;
            }
        }

        return true;
    }

    private void Apply(UnitPersistentData unit, UnitData template, string partyId)
    {
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
        SetText(partyIdText, string.IsNullOrEmpty(partyId) ? "(무소속)" : partyId);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
