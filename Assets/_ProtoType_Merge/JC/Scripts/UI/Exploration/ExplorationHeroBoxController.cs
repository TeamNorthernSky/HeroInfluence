using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260615] 탐사씬 하단 HeroBox(BTN_Explor_HeroBtn_1~4) 컨트롤러. (탐사 기획 ③-2/③-3)
/// 선택된 파티의 멤버를 버튼에 바인딩: 프로필 / 이름 / 현재HP·최대HP / 현재IP·최대IP.
/// 버튼 클릭 → 영속 HeroInfoModal.Open(추후 B: 스킬 우측 레이아웃 스테이터스 모달로 교체).
/// 파티 선택 탭(③-1)은 SelectParty로 전환(탭 UI는 별도 결선).
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

    [Header("파티 선택 탭 (③-1)")]
    [Tooltip("탭이 생성될 컨테이너(HorizontalLayoutGroup 권장). 파티 수만큼 동적 생성")]
    [SerializeField] private RectTransform tabContainer;
    [SerializeField] private Color tabSelectedColor = new Color(0.30f, 0.65f, 1f, 1f);
    [SerializeField] private Color tabNormalColor = new Color(0.16f, 0.20f, 0.30f, 0.85f);
    private readonly List<GameObject> tabObjects = new List<GameObject>();

    private int selectedPartyIndex;
    private readonly int[] boundUnits = new int[4];
    private EconomyManager subEco; // 갱신 트리거(자원/IP 변동) 용
    private float nextRefresh;     // [JC 260616] 스탯/HP/IP 변동 주기적 재반영 타이머

    private void Awake()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int ci = i;
            if (slots[i]?.button != null) slots[i].button.onClick.AddListener(() => OnSlotClicked(ci));
        }
    }

    private void OnEnable() { RebuildTabs(); Refresh(); }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        // 영속 매니저가 늦게 준비되면 즉시 보강
        if (boundUnits[0] == 0) { Refresh(); return; }
        // [JC 260616] HP/스탯/IP 변동(맵 이벤트·트레이닝 등)은 통지 이벤트가 없어 주기적으로 재반영
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.3f;
        Refresh();
    }

    /// <summary>파티 탭 전환(③-1). index가 파티 레지스트리 순서.</summary>
    public void SelectParty(int index)
    {
        selectedPartyIndex = index;
        Refresh();
        UpdateTabVisual();
    }

    // ─── 파티 선택 탭 ───────────────────────────────────────
    private void RebuildTabs()
    {
        if (tabContainer == null) return;
        for (int i = tabObjects.Count - 1; i >= 0; i--)
            if (tabObjects[i] != null) Destroy(tabObjects[i]);
        tabObjects.Clear();

        int count = PartyCount;
        var font = slots != null && slots.Length > 0 && slots[0] != null && slots[0].nameText != null
            ? slots[0].nameText.font : null;

        for (int i = 0; i < count; i++)
        {
            int ci = i;
            var go = new GameObject("PartyTab" + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(tabContainer, false);
            var le = go.AddComponent<LayoutElement>(); le.preferredWidth = 90; le.preferredHeight = 40;
            go.GetComponent<Image>().color = tabNormalColor;
            go.GetComponent<Button>().onClick.AddListener(() => SelectParty(ci));

            var txtGo = new GameObject("Label", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = $"파티 {i + 1}"; tmp.alignment = TextAlignmentOptions.Center; tmp.fontSize = 18; tmp.color = Color.white; tmp.raycastTarget = false;
            if (font != null) tmp.font = font;

            tabObjects.Add(go);
        }
        UpdateTabVisual();
    }

    private void UpdateTabVisual()
    {
        for (int i = 0; i < tabObjects.Count; i++)
        {
            if (tabObjects[i] == null) continue;
            var img = tabObjects[i].GetComponent<Image>();
            if (img != null) img.color = i == selectedPartyIndex ? tabSelectedColor : tabNormalColor;
        }
    }

    public int PartyCount
    {
        get { var repo = PartyPersistentRepository.Instance; return repo != null ? repo.Parties.Count : 0; }
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
        var gm = GameManager.Instance;

        var members = new List<int>();
        if (party != null)
            for (int u = 0; u < party.UnitIndices.Count; u++)
                if (party.UnitIndices[u] > 0) members.Add(party.UnitIndices[u]);

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
            UnitData template = null;
            if (unitRepo != null) unitRepo.TryGetUnit(unitIndex, out unit);
            if (unit != null && catalog != null && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey))
                catalog.TryGetPlayerTemplate(unit.UnitTemplateKey, out template);

            if (slot.profile != null)
            {
                var sp = HeroProfileCatalog.GetByName(template != null ? template.Name : null);
                slot.profile.enabled = sp != null;
                if (sp != null) slot.profile.sprite = sp;
            }
            if (slot.nameText != null)
                slot.nameText.text = template != null && !string.IsNullOrWhiteSpace(template.Name)
                    ? template.Name : (unit != null ? unit.UnitTemplateKey : "-");
            if (slot.hpText != null)
            {
                float cur = unit != null ? unit.CurrentHp : 0f;
                float max = unit != null ? unit.IngameStats.HP : 0f; // [JC 260616] 표시는 인게임 스탯(베이스는 내부 연산용)
                slot.hpText.text = $"{cur:F0}/{max:F0}";
            }
            if (slot.ipText != null)
            {
                int ip = gm != null && gm.Publicity != null ? gm.Publicity.GetIP(unitIndex) : 0;
                slot.ipText.text = $"{ip}/{PublicityManager.MaxIP}";
            }
        }
    }

    private void OnSlotClicked(int i)
    {
        if (i < 0 || i >= boundUnits.Length || boundUnits[i] <= 0) return;
        var gm = GameManager.Instance;
        if (gm == null) return;
        // 탐사 멤버 클릭 → 스테이터스 모달(스킬 우측). 없으면 일반 HeroInfo로 폴백.
        var modal = gm.HeroStatusModal != null ? gm.HeroStatusModal : gm.HeroInfoModal;
        if (modal != null) modal.Open(boundUnits[i]);
    }
}
