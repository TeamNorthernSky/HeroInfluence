// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/KJ/Scripts/UI/SkillButtonTooltip.cs
// 원본 객체: SkillButtonTooltip -> JcSkillButtonTooltip
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 고정 설명 컨트롤러가 연결된 전투 버튼은 호버 상태만 전달합니다.
/// 연결하지 않은 기존 씬은 공용 DDOL SkillTooltip(아이콘·이름·설명)을 계속 사용합니다.
/// </summary>
public class JcSkillButtonTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("클래스 스킬 또는 무기 스킬의 실제 시전 설명을 표시할지 선택합니다.")]
    [SerializeField] private SkillButtonType skillType = SkillButtonType.ClassSkill;
    [Tooltip("JC 전투의 현재 턴과 행동 권한을 읽는 흐름입니다.")]
    [SerializeField] private JcBattleFlowManager battleFlowManager;
    [Tooltip("전투씬 하단 우측 고정 설명을 관리합니다. 연결하면 공용 DDOL 툴팁을 호출하지 않습니다. 기존 씬은 비워 둡니다.")]
    [SerializeField] private JcSkillButtonController fixedDescription;

    [Tooltip("[JC 260622] 툴팁 표시 위치 오프셋(버튼 중앙 기준, X 우+/Y 상+). 전투 스킬버튼이 화면 우하단이라 위로 띄우려면 X 음수·Y 양수. 에디터에서 직접 조정.")]
    [SerializeField] private Vector2 tooltipOffset = new Vector2(-250f, 350f);

    private BattleCharactor currentUnit;
    private bool isHovering;

    // 원본 함수 대응: SkillButtonTooltip.Awake (Assets/_ProtoType_Merge/KJ/Scripts/UI/SkillButtonTooltip.cs)

    private void Awake()
    {
        if (battleFlowManager == null)
            battleFlowManager = FindFirstObjectByType<JcBattleFlowManager>();

        // [JC 260622] 옛 KJ HoverTooltip이 같은 버튼에 남아 자체 호버로 빈 툴팁을 띄우는 것 방지(비활성화).
        var legacy = GetComponent<HoverTooltip>();
        if (legacy != null) legacy.enabled = false;
    }

    // 원본 함수 대응: SkillButtonTooltip.OnEnable (Assets/_ProtoType_Merge/KJ/Scripts/UI/SkillButtonTooltip.cs)

    private void OnEnable()
    {
        if (fixedDescription != null) return;
        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted += OnTurnStarted;
    }

    // 원본 함수 대응: SkillButtonTooltip.OnDisable (Assets/_ProtoType_Merge/KJ/Scripts/UI/SkillButtonTooltip.cs)

    private void OnDisable()
    {
        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted -= OnTurnStarted;
        isHovering = false;
        HideTip();
    }

    // 원본 함수 대응: SkillButtonTooltip.OnTurnStarted (Assets/_ProtoType_Merge/KJ/Scripts/UI/SkillButtonTooltip.cs)

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

    // 원본 함수 대응: SkillButtonTooltip.ShowTip (Assets/_ProtoType_Merge/KJ/Scripts/UI/SkillButtonTooltip.cs)

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
        // 원본 ShowTip 대응: 테스트에서 적용한 스킬/무기 계수를 그대로 표시하고 협회 영속 값을 조회하지 않습니다.
        currentUnit.ResolveSelectedSkill();
        SkillData skill = skillType == SkillButtonType.ClassSkill ? currentUnit.SelectedSkillData : currentUnit.EquippedWeaponData?.ToSkillData();
        if (skill == null) return;
        string body = JC.BattleTesting.JcTestDataAssembler.DescribeAppliedSkill(skill, skillType != SkillButtonType.ClassSkill);
        var icon = skillType == SkillButtonType.ClassSkill ? Sprites.Icon.ClassSkill(skill.skillIndex, 1) : Sprites.Icon.WeaponSkill(skill.skillIndex, 1);
        tip.ShowInfo(icon, skill.skillName, body, target, extraY: tooltipOffset.y, extraX: tooltipOffset.x);
    }

    // 원본 함수 대응: SkillButtonTooltip.HideTip (Assets/_ProtoType_Merge/KJ/Scripts/UI/SkillButtonTooltip.cs)

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
