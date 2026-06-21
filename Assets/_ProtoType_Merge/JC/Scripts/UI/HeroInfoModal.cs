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

    // [JC 260619] 스킬/무기/무기스킬 버튼 (비우면 이름으로 자동 탐색). 아이콘=각 버튼 자신의 Image.
    // [JC 260619] 물리 위치 좌→우: Btn_ClassSkill(1번) / Btn_WeaponSkill(2번) / Btn_EquipSkill(3번).
    // 이름이 위치와 어긋나 있어, 2번=무기 / 3번=무기스킬로 매핑(아래 ResolveButtons 참조).
    [Header("Skill/Weapon 버튼 (비우면 자동 탐색)")]
    [SerializeField] private Button classSkillButton;   // 1번 = Btn_ClassSkill (클래스스킬)
    [SerializeField] private Button weaponButton;       // 2번 = Btn_WeaponSkill (무기)
    [SerializeField] private Button weaponSkillButton;  // 3번 = Btn_EquipSkill (무기스킬)

    [Header("랭크 알파벳 (비우면 GradeBox/Label 자동 탐색)")]
    [SerializeField] private TMP_Text gradeText;        // GradeBox/Label — 레벨 기반 랭크(N..A,S)

    private int currentUnitIndex = -1;
    private bool buttonsResolved;

    // 호버 툴팁용 슬롯 캐시
    private struct TipSlot { public RectTransform target; public Sprite sprite; public string name; public string desc; public bool has; }
    private TipSlot slotClassSkill, slotWeapon, slotWeaponSkill;

    public void Open(int unitIndex)
    {
        if (!TryResolve(unitIndex, out UnitPersistentData unit, out UnitData template))
        {
            Debug.LogWarning($"[HeroInfoModal] unitIndex {unitIndex} 조회 실패");
            return;
        }

        currentUnitIndex = unitIndex;
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
        // [JC 260616] IP 표기 = 유닛 CurrentInfluence 일원화(PublicityManager 의존 제거)
        float ipValue = unit != null ? unit.CurrentInfluence : 0f;
        // 명칭은 씬의 고정 라벨로 분리됨. 값 텍스트는 값만 출력(값필드 박스 안).
        SetText(ipText, $"{ipValue:F0}");
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

        // [JC 260619] 클래스스킬/무기/무기스킬 아이콘 + 호버 툴팁
        ResolveButtons();
        // [JC 260619] 랭크 알파벳 = 레벨 파생(성장 시스템 V6.0). 매 Open 시 현재 레벨로 계산(시작/레벨업 자동 반영).
        SetText(gradeText, RankUtil.FromLevel(unit != null ? unit.Level : 1));
        ResolveSkillWeaponIcons(unit != null ? unit.UnitIndex : currentUnitIndex);
    }

    // ── 스킬/무기 버튼 결선 (이름 자동 탐색 + 호버 부착) ───────────
    private void ResolveButtons()
    {
        if (buttonsResolved) return;
        Transform root = GetRoot().transform;
        if (classSkillButton == null) classSkillButton = FindButton(root, "Btn_ClassSkill");   // 1번
        if (weaponButton == null) weaponButton = FindButton(root, "Btn_WeaponSkill");          // 2번 = 무기
        if (weaponSkillButton == null) weaponSkillButton = FindButton(root, "Btn_EquipSkill"); // 3번 = 무기스킬
        if (gradeText == null) { var gb = FindDeep(root, "GradeBox"); if (gb != null) gradeText = gb.GetComponentInChildren<TMP_Text>(true); }
        AttachHover(classSkillButton, 0);
        AttachHover(weaponButton, 1);
        AttachHover(weaponSkillButton, 2);
        buttonsResolved = true;
    }

    private static Button FindButton(Transform root, string name)
    {
        var t = FindDeep(root, name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static Transform FindDeep(Transform node, string name)
    {
        if (node.name == name) return node;
        for (int i = 0; i < node.childCount; i++)
        {
            var r = FindDeep(node.GetChild(i), name); // 비활성 자식 포함
            if (r != null) return r;
        }
        return null;
    }

    private void AttachHover(Button btn, int slot)
    {
        if (btn == null) return;
        var h = btn.GetComponent<HeroInfoButtonHover>();
        if (h == null) h = btn.gameObject.AddComponent<HeroInfoButtonHover>();
        h.Bind(this, slot);
    }

    // ── 아이콘 해석 (아이콘 타깃 = 각 버튼 자신의 Image) ───────────
    private void ResolveSkillWeaponIcons(int unitIndex)
    {
        slotClassSkill = default; slotWeapon = default; slotWeaponSkill = default;
        var gm = GameManager.Instance;

        // [JC 260619] 안전장치: 시작 자동장착 타이밍과 무관하게 이 유닛의 기본 스킬/무기 장착을 보장(멱등).
        if (gm != null && unitIndex > 0)
        {
            if (gm.Lab != null) gm.Lab.EnsureDefaultEquipped(unitIndex);
            if (gm.Workshop != null) gm.Workshop.EnsureDefaultEquipped(unitIndex);
        }

        // 클래스스킬: 장착 스킬
        if (gm != null && gm.Lab != null && unitIndex > 0)
        {
            int sk = gm.Lab.GetEquippedSkillIndex(unitIndex);
            if (sk > 0)
            {
                int lv = gm.Lab.GetSkillLevel(unitIndex, sk);
                SkillData sd = FindSkill(gm.Lab.GetClassSkills(unitIndex), sk);
                // [JC 260619] 이름 뒤 Lv.n + 설명은 계수 치환(전투 UI와 동일, ClassSkillTooltipText).
                string csName = (sd != null && !string.IsNullOrWhiteSpace(sd.skillName) ? sd.skillName : "스킬") + $" Lv.{lv}";
                slotClassSkill = MakeSlot(classSkillButton, HeroIcons.GetClassSkillIcon(sk, lv),
                    csName, ClassSkillTooltipText.BuildDesc(sd, lv));
            }
        }

        // 무기 + 무기스킬: 장착 무기
        if (gm != null && gm.Workshop != null && unitIndex > 0)
        {
            int w = gm.Workshop.GetEquippedWeaponIndex(unitIndex);
            if (w > 0)
            {
                int wl = gm.Workshop.GetWeaponLevel(unitIndex, w);
                WeaponData wd = null;
                var cat = DHCsvTemplateCatalog.Instance;
                if (cat != null) cat.TryGetWeapon(w, out wd);
                // 2번째(무기) 툴팁 = 공방 무기 호버와 동일(BuildWeaponLevelDesc). 이름 뒤 Lv.n(무기 레벨).
                string wName = (wd != null && !string.IsNullOrWhiteSpace(wd.WeaponName) ? wd.WeaponName : "무기") + $" Lv.{wl}";
                slotWeapon = MakeSlot(weaponButton, HeroIcons.GetWeaponIcon(w, wl),
                    wName, wd != null ? WeaponTooltipText.BuildWeaponLevelDesc(wd, w, wl) : string.Empty);

                // 3번째(무기스킬) 툴팁 = B타입, 현재 무기스킬 정보만(비교 없음). 이름 뒤 Lv.n(무기 레벨).
                // [JC 260621] 무기스킬 전용 아이콘(무기 레벨 wl 따라감). wl=1도 실사용(전용 1레벨 아이콘).
                if (wd != null && wd.WeaponSkillIndex > 0)
                    slotWeaponSkill = MakeSlot(weaponSkillButton, HeroIcons.GetWeaponSkillIcon(wd.WeaponSkillIndex, wl),
                        wd.WeaponSkillName + $" Lv.{wl}", WeaponTooltipText.BuildWeaponSkillDesc(wd, w, wl));
            }
        }

        ApplySlotIcon(slotClassSkill, classSkillButton);
        ApplySlotIcon(slotWeapon, weaponButton);
        ApplySlotIcon(slotWeaponSkill, weaponSkillButton);
    }

    private static SkillData FindSkill(System.Collections.Generic.List<SkillData> list, int skillIndex)
    {
        if (list == null) return null;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].skillIndex == skillIndex) return list[i];
        return null;
    }

    private static TipSlot MakeSlot(Button btn, Sprite sprite, string name, string desc)
    {
        return new TipSlot
        {
            target = btn != null ? btn.GetComponent<RectTransform>() : null,
            sprite = sprite,
            name = name,
            desc = desc,
            has = sprite != null,
        };
    }

    private static void ApplySlotIcon(TipSlot slot, Button btn)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>(); // 아이콘 = 버튼 자신의 Image
        if (img != null && slot.has && slot.sprite != null) img.sprite = slot.sprite;
    }

    /// <summary>HeroInfoButtonHover 콜백. slot: 0=클래스스킬, 1=무기, 2=무기스킬.</summary>
    public void OnButtonHover(int slot, bool enter)
    {
        var tip = SkillTooltip.Instance;
        if (tip == null) return;
        if (!enter) { tip.Hide(); return; }
        TipSlot s = slot == 0 ? slotClassSkill : slot == 1 ? slotWeapon : slotWeaponSkill;
        if (!s.has || s.target == null) return;
        // [JC 260619] 모달 아이콘 툴팁 Y+250 위로. 2번(무기, slot==1)만 본문 너비 +60 / 아이콘 좌측 -22(=-30+8).
        float extraWidth = slot == 1 ? 60f : 0f;
        float iconDeltaX = slot == 1 ? -22f : 0f;
        tip.ShowInfo(s.sprite, s.name, s.desc, s.target, 250f, extraWidth, iconDeltaX);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
