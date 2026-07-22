using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [KJ 260701] 의무실 모달 컨트롤러. Modal_Infirmary 루트에 부착.
/// 협회 방문 파티 유닛을 행(InfirmaryUnitRow)으로 나열 → 개별 회복/부활 + 전체 회복.
/// 자금: 컨트롤러가 Economy.Spend→Infirmary.Try*→실패 시 Add 롤백(3계층 규약).
/// 표시/게이트 판정은 InfirmaryManager(CanHeal/CanRevive)에 위임.
/// </summary>
[DisallowMultipleComponent]
public class InfirmaryModalController : MonoBehaviour
{
    [Header("Modal 본체")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button btnClose;

    [Header("유닛 리스트")]
    [SerializeField] private RectTransform content;
    [SerializeField] private InfirmaryUnitRow rowTemplate;

    [Header("전체 회복 / 안내")]
    [SerializeField] private Button btnHealAll;
    [SerializeField] private GameObject healAllDisabledOverlay;
    [SerializeField] private TextMeshProUGUI totalCostText;
    [SerializeField] private TextMeshProUGUI stateInfoText;
    // [KJ 260707] 상태 메시지 배경 박스(메시지창). 배선되면 박스째로 토글, 없으면 기존처럼 텍스트만.
    [SerializeField] private GameObject stateInfoBox;

    private readonly List<InfirmaryUnitRow> spawnedRows = new List<InfirmaryUnitRow>();
    private InfirmaryManager subscribedInf;
    private EconomyManager subscribedEco;

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnHealAll != null) btnHealAll.onClick.AddListener(OnHealAll);
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
    }

    private void OnEnable() { TrySubscribe(); Refresh(); }
    private void OnDisable() { Unsubscribe(); ClearRows(); }
    private void Update()
    {
        if (subscribedInf == null || subscribedEco == null) TrySubscribe();
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (subscribedInf == null && gm.Infirmary != null)
        {
            subscribedInf = gm.Infirmary;
            subscribedInf.OnStateChanged += Refresh;
        }
        if (subscribedEco == null && gm.Economy != null)
        {
            subscribedEco = gm.Economy;
            subscribedEco.OnResourceChanged += OnResourceChanged;
        }
        Refresh();
    }

    private void Unsubscribe()
    {
        if (subscribedInf != null) { subscribedInf.OnStateChanged -= Refresh; subscribedInf = null; }
        if (subscribedEco != null) { subscribedEco.OnResourceChanged -= OnResourceChanged; subscribedEco = null; }
    }

    private void OnResourceChanged(ResourceType _, int __) => Refresh();

    public void CloseModal()
    {
        if (modalRoot != null) modalRoot.SetActive(false);
        else gameObject.SetActive(false);
    }

    // ─── 개별 회복/부활 ─────────────────────────────────────────
    private void OnRowAction(int unitIndex)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Infirmary == null || gm.Economy == null) return;

        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.TryGetUnit(unitIndex, out UnitPersistentData unit) || unit == null) return;

        bool downed = unit.CurrentHp <= 0f;
        if (downed) TryReviveOne(unitIndex);
        else TryHealOne(unitIndex);
        Refresh();
    }

    private bool TryHealOne(int unitIndex)
    {
        var gm = GameManager.Instance;
        if (!gm.Infirmary.CanHeal(unitIndex)) return false;
        int cost = gm.Infirmary.GetHealCost();
        if (!gm.Economy.Spend(ResourceType.Money, cost)) return false;
        if (!gm.Infirmary.TryHeal(unitIndex))
        {
            gm.Economy.Add(ResourceType.Money, cost); // 자원 유실 방지 롤백
            Debug.LogError($"[Infirmary] TryHeal 실패 — 차감 롤백 (unit {unitIndex})");
            return false;
        }
        return true;
    }

    private bool TryReviveOne(int unitIndex)
    {
        var gm = GameManager.Instance;
        if (!gm.Infirmary.CanRevive(unitIndex)) return false;
        int cost = gm.Infirmary.GetReviveCost();
        if (!gm.Economy.Spend(ResourceType.Money, cost)) return false;
        if (!gm.Infirmary.TryRevive(unitIndex))
        {
            gm.Economy.Add(ResourceType.Money, cost);
            Debug.LogError($"[Infirmary] TryRevive 실패 — 차감 롤백 (unit {unitIndex})");
            return false;
        }
        return true;
    }

    // ─── 전체 회복 ──────────────────────────────────────────────
    // 부활은 제외(비용·의미가 달라 개별 확인). 생존·회복가능 유닛만 순차 회복.
    private void OnHealAll()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Infirmary == null || gm.Economy == null) return;

        List<int> units = ResolveVisitingUnits();
        foreach (int idx in units)
            if (gm.Infirmary.CanHeal(idx)) TryHealOne(idx);
        Refresh();
    }

    // ─── Refresh ────────────────────────────────────────────────
    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Infirmary == null || gm.Economy == null) return;
        if (rowTemplate == null || content == null) return;

        ClearRows();

        var repo = PersistentUnitRepository.Instance;
        List<int> units = ResolveVisitingUnits();
        int healableCount = 0;
        int healAllTotal = 0;
        int healCost = gm.Infirmary.GetHealCost();

        foreach (int idx in units)
        {
            if (repo == null || !repo.TryGetUnit(idx, out UnitPersistentData unit) || unit == null) continue;

            InfirmaryUnitRow row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.Bind(idx, unit, gm.Infirmary, OnRowAction);
            spawnedRows.Add(row);

            if (gm.Infirmary.CanHeal(idx)) { healableCount++; healAllTotal += healCost; }
        }

        bool unlocked = gm.Infirmary.IsUnlocked;
        bool visited = HQVisitState.Instance != null && HQVisitState.Instance.HasVisitingParty;
        bool healAllAffordable = healableCount > 0 && gm.Economy.Has(ResourceType.Money, healAllTotal);
        bool canHealAll = unlocked && visited && healAllAffordable;

        if (btnHealAll != null) btnHealAll.interactable = canHealAll;
        if (healAllDisabledOverlay != null) healAllDisabledOverlay.SetActive(!canHealAll);
        if (totalCostText != null) totalCostText.text = $"{healAllTotal} 골드";

        if (stateInfoText != null)
        {
            string msg = null;
            if (!unlocked) msg = "의무실이 활성화되지 않았습니다.";
            else if (!visited) msg = "협회를 방문한 상태에서만 이용할 수 있습니다.";
            else if (units.Count == 0) msg = "회복할 영웅이 없습니다.";
            else if (healableCount == 0) msg = "회복 가능한 영웅이 없습니다.";
            GameObject toggleTarget = stateInfoBox != null ? stateInfoBox : stateInfoText.gameObject;
            toggleTarget.SetActive(!string.IsNullOrEmpty(msg));
            if (!string.IsNullOrEmpty(msg)) stateInfoText.text = msg;
        }
    }

    // 협회 방문 파티에 편성된 유닛 인덱스(등록 순). 사망 유닛 포함(부활 대상).
    private static List<int> ResolveVisitingUnits()
    {
        var ordered = new List<int>();
        var seen = new HashSet<int>();
        var partyRepo = PartyPersistentRepository.Instance;
        var repo = PersistentUnitRepository.Instance;
        var visit = HQVisitState.Instance;
        if (partyRepo == null || repo == null || visit == null || !visit.HasVisitingParty) return ordered;

        for (int p = 0; p < partyRepo.Parties.Count; p++)
        {
            PartyPersistentData party = partyRepo.Parties[p];
            if (party == null || !visit.IsPartyVisiting(party.PartyId)) continue;
            for (int u = 0; u < party.UnitIndices.Count; u++)
            {
                int idx = party.UnitIndices[u];
                if (idx <= 0 || seen.Contains(idx) || !repo.ContainsUnit(idx)) continue;
                ordered.Add(idx);
                seen.Add(idx);
            }
        }
        return ordered;
    }

    private void ClearRows()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i] != null)
            {
                spawnedRows[i].transform.SetParent(null, false);
                Destroy(spawnedRows[i].gameObject);
            }
        }
        spawnedRows.Clear();
    }
}
