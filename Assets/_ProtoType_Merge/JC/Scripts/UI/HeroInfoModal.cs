using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Portrait")]
    [SerializeField] private Image portraitImage;

    [Header("Hero Info")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text classText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text ipText;

    [Header("Stats")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text atkText;
    [SerializeField] private TMP_Text defText;
    [SerializeField] private TMP_Text critRateText;
    [SerializeField] private TMP_Text counterRateText;
    // [JC 260520] StatBlock에 피해경감률 필드 없음 — 데이터 모델 합의 후 연결. 현재는 "—" 출력
    [SerializeField] private TMP_Text damageReductionText;
    [SerializeField] private TMP_Text speedText;

    [Header("Skill Icons (데이터 원천 미정 — Sprite 미연결)")]
    [SerializeField] private Image classSkillIcon;
    [SerializeField] private Image weaponSkillIcon;

    public void Open(int unitIndex)
    {
        if (!TryResolve(unitIndex, out UnitPersistentData unit, out UnitData template))
        {
            Debug.LogWarning($"[HeroInfoModal] unitIndex {unitIndex} 조회 실패");
            return;
        }

        Apply(unit, template);
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

    private static bool TryResolve(int unitIndex, out UnitPersistentData unit, out UnitData template)
    {
        unit = null;
        template = null;

        // [JC 수정 260512] 머지 사이클: 파티 책임이 PartyPersistentRepository로 이관됨
        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return false;

        if (!repo.TryGetUnit(unitIndex, out unit) || unit == null) return false;

        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey))
            catalog.TryGetPlayerTemplate(unit.UnitTemplateKey, out template);

        return true;
    }

    private void Apply(UnitPersistentData unit, UnitData template)
    {
        string displayName = template != null && !string.IsNullOrWhiteSpace(template.Name)
            ? template.Name : unit.UnitTemplateKey;
        string displayClass = template != null && !string.IsNullOrWhiteSpace(template.UnitType)
            ? template.UnitType : "-";

        StatBlock s = unit.IngameStats; // [JC 260616] 표시는 인게임 스탯(베이스는 내부 연산용·비공개)

        SetText(nameText, displayName);
        SetText(classText, displayClass);
        SetText(rankText, $"RANK : {unit.Level}");
        int ipValue = GameManager.Instance != null && GameManager.Instance.Publicity != null
            ? GameManager.Instance.Publicity.GetIP(unit.UnitIndex)
            : 0;
        // 명칭은 씬의 고정 라벨로 분리됨. 값 텍스트는 값만 출력(값필드 박스 안).
        SetText(ipText, $"{ipValue}");
        SetText(hpText, $"{s.HP:F0}");
        SetText(atkText, $"{s.Atk:F0}");
        SetText(defText, $"{s.DEF:F0}");
        SetText(critRateText, $"{s.CriticalRate:F0}%");
        SetText(counterRateText, $"{s.CounterRate:F0}%");
        SetText(damageReductionText, "—");
        SetText(speedText, $"{s.Speed:F0}");

        if (portraitImage != null)
        {
            var sp = HeroProfileCatalog.GetByName(template != null ? template.Name : null);
            portraitImage.gameObject.SetActive(true);
            portraitImage.enabled = true;
            if (sp != null) portraitImage.sprite = sp;
        }
        // classSkillIcon / weaponSkillIcon — 데이터 원천 미정. Sprite 미할당
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
