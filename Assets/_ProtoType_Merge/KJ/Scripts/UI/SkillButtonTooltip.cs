using UnityEngine;
using UnityEngine.EventSystems;

public enum SkillButtonType { ClassSkill, WeaponSkill }

/// <summary>
/// [JC 260622] 전투 스킬버튼(클래스/무기스킬) 호버 툴팁.
/// 기존 KJ HoverTooltip(단순 title+desc) 대신, HeroInfoModal과 "동일한" 리치 SkillTooltip
/// (아이콘 + 이름 Lv.n + 계수 치환 desc)을 표시한다.
/// SkillTooltip은 영속 싱글턴 — 배틀 씬에 SkillTooltip.prefab을 배치해 두어야 동작(없으면 no-op).
/// </summary>
public class SkillButtonTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private SkillButtonType skillType = SkillButtonType.ClassSkill;
    [SerializeField] private BattleFlowManager battleFlowManager;

    [Tooltip("[JC 260622] 툴팁 표시 위치 오프셋(버튼 중앙 기준, X 우+/Y 상+). 전투 스킬버튼이 화면 우하단이라 위로 띄우려면 X 음수·Y 양수. 에디터에서 직접 조정.")]
    [SerializeField] private Vector2 tooltipOffset = new Vector2(-250f, 350f);

    private BattleCharactor currentUnit;
    private bool isHovering;

    private void Awake()
    {
        if (battleFlowManager == null)
            battleFlowManager = FindFirstObjectByType<BattleFlowManager>();

        // [JC 260622] 옛 KJ HoverTooltip이 같은 버튼에 남아 자체 호버로 빈 툴팁을 띄우는 것 방지(비활성화).
        var legacy = GetComponent<HoverTooltip>();
        if (legacy != null) legacy.enabled = false;
    }

    private void OnEnable()
    {
        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted += OnTurnStarted;
    }

    private void OnDisable()
    {
        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted -= OnTurnStarted;
        isHovering = false;
        HideTip();
    }

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        currentUnit = (unit != null && unit.IsPlayer) ? unit : null;
        // [JC 260703] 호버 유지 중 턴 전환 시 이전 유닛 툴팁 잔존 방지 — 숨긴 뒤 새 유닛으로 재표시(적 턴이면 숨김 유지).
        if (isHovering)
        {
            HideTip();
            if (currentUnit != null) ShowTip();
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { isHovering = true; ShowTip(); }
    public void OnPointerExit(PointerEventData eventData) { isHovering = false; HideTip(); }

    private void ShowTip()
    {
        var tip = SkillTooltip.Instance;
        if (tip == null || currentUnit == null) return;

        RectTransform target = transform as RectTransform;
        var gm = GameManager.Instance;
        int uIdx = currentUnit.SourceData != null ? currentUnit.SourceData.UnitIndex : 0;

        if (skillType == SkillButtonType.ClassSkill)
        {
            // HeroInfoModal slot0과 동일: 장착 클래스 스킬 아이콘 + Lv + 계수치환 desc.
            currentUnit.ResolveSelectedSkill();
            SkillData sd = currentUnit.SelectedSkillData;
            if (sd == null) return;
            int lv = (gm != null && gm.Lab != null && uIdx > 0)
                ? gm.Lab.GetSkillLevel(uIdx, sd.skillIndex)
                : (currentUnit.SourceData != null ? currentUnit.SourceData.SkillLevel : 1);
            tip.ShowInfo(
                Sprites.Icon.ClassSkill(sd.skillIndex, lv),
                $"{sd.skillName} Lv.{lv}",
                ClassSkillTooltipText.BuildDesc(sd, lv),
                target, extraY: tooltipOffset.y, extraX: tooltipOffset.x);
        }
        else
        {
            // HeroInfoModal slot2와 동일: 무기스킬 아이콘 + 무기 레벨 Lv + BuildWeaponSkillDesc.
            WeaponData wd = currentUnit.EquippedWeaponData;
            if (wd == null || wd.WeaponSkillIndex <= 0) return;
            int wIdx = currentUnit.EquippedWeaponIndex > 0
                ? currentUnit.EquippedWeaponIndex
                : (currentUnit.SourceData != null ? currentUnit.SourceData.CurrentWeaponIndex : 0);
            int wl = (gm != null && gm.Workshop != null && uIdx > 0)
                ? gm.Workshop.GetWeaponLevel(uIdx, wIdx)
                : 1;
            tip.ShowInfo(
                Sprites.Icon.WeaponSkill(wd.WeaponSkillIndex, wl),
                $"{wd.WeaponSkillName} Lv.{wl}",
                WeaponTooltipText.BuildWeaponSkillDesc(wd, wIdx, wl),
                target, extraY: tooltipOffset.y, extraX: tooltipOffset.x);
        }
    }

    private void HideTip()
    {
        var tip = SkillTooltip.Instance;
        if (tip != null) tip.Hide();
    }
}
