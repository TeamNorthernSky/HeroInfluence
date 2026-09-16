using UnityEngine;
using UnityEngine.EventSystems;

public enum SkillButtonType { ClassSkill, WeaponSkill }

/// <summary>
/// 고정 설명 컨트롤러가 연결된 전투 버튼은 호버 상태만 전달합니다.
/// 연결하지 않은 기존 씬은 공용 DDOL SkillTooltip(아이콘·이름·설명)을 계속 사용합니다.
/// </summary>
public class SkillButtonTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private SkillButtonType skillType = SkillButtonType.ClassSkill;
    [SerializeField] private BattleFlowManager battleFlowManager;
    [Tooltip("전투씬 하단 우측 고정 설명을 관리합니다. 연결하면 공용 DDOL 툴팁을 호출하지 않습니다. 기존 씬은 비워 둡니다.")]
    [SerializeField] private SkillButtonController fixedDescription;

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
        if (fixedDescription != null) return;
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
        var button = GetComponent<UnityEngine.UI.Button>();
        if (fixedDescription != null)
        {
            fixedDescription.SetDescriptionHover(button, true);
            return;
        }
        if (button != null && !button.interactable) return;
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
                ? Mathf.Max(1, gm.Workshop.GetWeaponLevel(wIdx))
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
        if (fixedDescription != null)
        {
            fixedDescription.SetDescriptionHover(GetComponent<UnityEngine.UI.Button>(), false);
            return;
        }
        var tip = SkillTooltip.Instance;
        if (tip != null) tip.Hide();
    }
}
