using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260615] 탐사씬 하단 HeroBox(BTN_Explor_HeroBtn_1~4) 컨트롤러. (탐사 기획 ③-2/③-3)
/// 선택된 파티의 멤버를 버튼에 바인딩: 프로필 / 이름 / 현재HP·최대HP / 현재IP·최대IP.
/// 버튼 클릭 → 영속 HeroInfoModal.Open.
/// [JC 260630] 파티 선택 탭(③-1) 코드 전면 제거 — PartyTabBar GO 삭제에 대응.
/// </summary>
[DisallowMultipleComponent]
public class ExplorationHeroBoxController : MonoBehaviour
{
    [Serializable]
    public class HeroSlot
    {
        public GameObject root;      // 버튼 GO (BTN_Explor_HeroBtn_n)
        public Button button;
        public Image profile;
        public TMP_Text nameText;
        public TMP_Text hpText;
        public TMP_Text ipText;
    }

    [Header("멤버 슬롯 (BTN_Explor_HeroBtn 1~4)")]
    [SerializeField] private HeroSlot[] slots = new HeroSlot[4];

    [Header("파티")]
    [Tooltip("빈칸이면 selectedPartyIndex로 선택. 보통 빈칸")]
    [SerializeField] private string targetPartyId = "";

    private int selectedPartyIndex;
    // [JC 260625] slots 길이에 맞춰 Awake에서 할당(고정 4 → IndexOutOfRange 방지). slots 5+ 설정해도 안전.
    private int[] boundUnits = System.Array.Empty<int>();
    private EconomyManager subEco; // 갱신 트리거(자원/IP 변동) 용
    private float nextRefresh;     // [JC 260616] 스탯/HP/IP 변동 주기적 재반영 타이머

    private void Awake()
    {
        boundUnits = new int[slots != null ? slots.Length : 0];
        for (int i = 0; i < slots.Length; i++)
        {
            int ci = i;
            if (slots[i]?.button != null) slots[i].button.onClick.AddListener(() => OnSlotClicked(ci));
        }
    }

    private void OnEnable() { Refresh(); }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (boundUnits.Length == 0) return;
        // 영속 매니저가 늦게 준비되면 즉시 보강
        if (boundUnits[0] == 0) { Refresh(); return; }
        // [JC 260616] HP/스탯/IP 변동(맵 이벤트·트레이닝 등)은 통지 이벤트가 없어 주기적으로 재반영
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.3f;
        Refresh();
    }

    private PartyPersistentData ResolveParty()
    {
        var repo = PartyPersistentRepository.Instance;
        if (repo == null || repo.Parties.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(targetPartyId))
            for (int p = 0; p < repo.Parties.Count; p++)
                if (repo.Parties[p] != null && repo.Parties[p].PartyId == targetPartyId) return repo.Parties[p];
        int idx = Mathf.Clamp(selectedPartyIndex, 0, repo.Parties.Count - 1);
        return repo.Parties[idx];
    }

    public void Refresh()
    {
        var party = ResolveParty();
        var unitRepo = PersistentUnitRepository.Instance;
        var catalog = DHCsvTemplateCatalog.Instance;

        // [JC 260628] 전열→후열 순(PartyFormation 공용 규약)으로 표시 — 전력평가·전투결과와 일치
        var members = party != null
            ? PartyFormation.OrderFrontFirst(party.UnitIndices, party.UnitSlots)
            : new List<int>();

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;
            bool active = i < members.Count;
            if (slot.root != null) slot.root.SetActive(active);
            boundUnits[i] = active ? members[i] : 0;
            if (!active) continue;

            int unitIndex = members[i];
            UnitPersistentData unit = null;
            DHPlayerUnitTemplate template = null;
            if (unitRepo != null) unitRepo.TryGetUnit(unitIndex, out unit);
            if (unit != null && catalog != null && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey))
                catalog.TryGetPlayerUnitTemplate(unit.UnitTemplateKey, out template);

            if (slot.profile != null)
            {
                // [JC 260621] 포트레이트 = unitIndex(=HeroIndex) 기준 PortraitLibrary 해석.
                var sp = HeroProfileCatalog.GetByUnitIndex(unitIndex);
                slot.profile.enabled = sp != null;
                if (sp != null) slot.profile.sprite = sp;
            }
            if (slot.nameText != null)
                slot.nameText.text = template != null && !string.IsNullOrWhiteSpace(template.UnitName)
                    ? template.UnitName : (unit != null ? unit.UnitTemplateKey : "-");
            if (slot.hpText != null)
            {
                float cur = unit != null ? unit.CurrentHp : 0f;
                float max = unit != null ? unit.IngameStats.HP : 0f; // [JC 260616] 표시는 인게임 스탯(베이스는 내부 연산용)
                slot.hpText.text = $"{cur:F0}/{max:F0}";
            }
            if (slot.ipText != null)
            {
                // [JC 260616] IP 표기 = 유닛 CurrentInfluence/IngameStats.Influence 일원화(PublicityManager 의존 제거)
                float ip = unit != null ? unit.CurrentInfluence : 0f;
                float maxIp = unit != null ? unit.IngameStats.Influence : 0f;
                slot.ipText.text = $"{ip:F0}/{maxIp:F0}";
            }
        }
    }

    private void OnSlotClicked(int i)
    {
        if (i < 0 || i >= boundUnits.Length || boundUnits[i] <= 0) return;
        var cui = CommonUIManager.Instance;
        if (cui == null) return;
        // [JC 260619] 탐사 멤버 클릭 → 로비와 동일 레이아웃(Modal_HeroInfo)으로 통일. HeroStatusModal 우선선택 폐기.
        var modal = cui.HeroInfoModal;
        if (modal != null) modal.Open(boundUnits[i]);
    }
}
