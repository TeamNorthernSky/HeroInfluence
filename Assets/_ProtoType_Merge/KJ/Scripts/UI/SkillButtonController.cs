using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>전투 스킬 버튼의 표시와 입력 선택 상태를 연결합니다. 스킬 장착·실행 데이터는 변경하지 않습니다.</summary>
public class SkillButtonController : MonoBehaviour
{
    [Tooltip("기존 단일 히어로 스킬 버튼입니다. 4슬롯이 없는 씬에서 사용합니다.")]
    [SerializeField] private ToggleButton toggle1;
    [Tooltip("기존 무기스킬 실행 경로를 사용하는 코어스킬 버튼입니다.")]
    [SerializeField] private ToggleButton toggle2;
    [Tooltip("실제 대상 선택 상태를 소유하는 전투 입력입니다.")]
    [SerializeField] private InputHandler inputHandler;
    [Tooltip("현재 행동 유닛과 턴 시작 이벤트를 제공하는 전투 흐름입니다.")]
    [SerializeField] private BattleFlowManager battleFlowManager;

    [Serializable]
    private class HeroSkillSlot
    {
        [Tooltip("씬에 배치한 스킬 선택 토글입니다.")] public ToggleButton toggle;
        [Tooltip("습득 및 기존 실행 연결 여부에 따라 활성화하는 버튼입니다.")] public Button button;
        [Tooltip("표시할 스킬의 아이콘입니다.")] public Image icon;
        [Tooltip("표시할 스킬 이름입니다.")] public TMP_Text name;
        [Tooltip("요구 레벨 미달 시 표시하는 음영과 자물쇠 묶음입니다.")] public GameObject lockedOverlay;
        [Tooltip("습득했지만 실행 연결이 없는 슬롯의 임시 안내입니다.")] public GameObject unconnectedLabel;
        [NonSerialized] public SkillData skill;
        [NonSerialized] public Action<bool> click;
    }

    [Tooltip("기본 스킬 습득 순서의 4슬롯입니다. 비어 있으면 기존 단일 버튼을 사용합니다. 위치는 씬에서 직접 조절합니다.")]
    [SerializeField] private HeroSkillSlot[] heroSlots = Array.Empty<HeroSkillSlot>();

    [Tooltip("전투씬 하단 우측에 선배치한 설명 제목입니다. 비어 있는 기존 씬은 공용 툴팁을 유지합니다.")]
    [SerializeField] private TMP_Text descriptionTitle;
    [Tooltip("호버·선택 스킬의 설명 또는 스킬 선택 안내를 표시합니다.")]
    [SerializeField] private TMP_Text descriptionBody;
    [Tooltip("효과·거리·대상 순서의 태그 글자입니다. 각 글자의 부모는 개별 태그 배경이며 안내 상태에서는 숨깁니다.")]
    [SerializeField] private TMP_Text[] descriptionTags = Array.Empty<TMP_Text>();

    private Button hoveredSkillButton;
    private bool descriptionBattleEnded;

    private Action<bool> onToggle1;
    private Action<bool> onToggle2;
    private bool HasHeroSlots => heroSlots != null && heroSlots.Length > 0;
    public SkillData CurrentSkillData { get; private set; }

    private void Awake()
    {
        if (inputHandler == null) inputHandler = FindFirstObjectByType<InputHandler>();
        if (battleFlowManager == null) battleFlowManager = FindFirstObjectByType<BattleFlowManager>();
    }

    private void OnEnable()
    {
        descriptionBattleEnded = false;
        onToggle1 = _ => Select(PendingActionType.ClassSkill);
        onToggle2 = _ => Select(PendingActionType.WeaponSkill);
        if (!HasHeroSlots && toggle1 != null) toggle1.OnValueChanged += onToggle1;
        if (toggle2 != null) toggle2.OnValueChanged += onToggle2;
        if (HasHeroSlots)
            foreach (var slot in heroSlots)
            {
                if (slot == null || slot.toggle == null) continue;
                slot.click = _ => { if (slot.button != null && slot.button.interactable) Select(PendingActionType.ClassSkill); };
                slot.toggle.OnValueChanged += slot.click;
            }
        if (battleFlowManager != null)
        {
            battleFlowManager.OnTurnStarted += OnTurnStarted;
            battleFlowManager.OnBattleEnded += OnBattleEnded;
        }
        if (inputHandler != null) inputHandler.SelectionChanged += SyncSelection;
        RefreshSkills();
    }

    // 모든 오브젝트의 Awake가 끝난 뒤 현재 유닛과 카탈로그를 다시 확인합니다.
    private void Start() => RefreshSkills();

    private void OnDisable()
    {
        if (!HasHeroSlots && toggle1 != null) toggle1.OnValueChanged -= onToggle1;
        if (toggle2 != null) toggle2.OnValueChanged -= onToggle2;
        if (HasHeroSlots)
            foreach (var slot in heroSlots)
                if (slot != null && slot.toggle != null) slot.toggle.OnValueChanged -= slot.click;
        if (battleFlowManager != null)
        {
            battleFlowManager.OnTurnStarted -= OnTurnStarted;
            battleFlowManager.OnBattleEnded -= OnBattleEnded;
        }
        if (inputHandler != null) inputHandler.SelectionChanged -= SyncSelection;
        hoveredSkillButton = null;
        SetDescription(string.Empty, string.Empty, null);
    }

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        descriptionBattleEnded = false;
        RefreshSkills();
    }

    private void OnBattleEnded(BattleResult result)
    {
        descriptionBattleEnded = true;
        hoveredSkillButton = null;
        SetDescription(string.Empty, string.Empty, null);
    }

    private void Select(PendingActionType action)
    {
        // 다른 스킬은 기존 선택을 먼저 비웁니다. 새 요청이 거절되면 미선택으로 남습니다.
        if (inputHandler != null && inputHandler.PendingAction != action && !inputHandler.TryCancelSkillSelection())
        {
            SyncSelection();
            return;
        }
        inputHandler?.BeginPendingAction(action);
        // 요청이 IP 부족·튜토리얼 제한 등으로 거절되어도 실제 입력 상태만 표시합니다.
        SyncSelection();
    }

    /// <summary>씬의 스킬 버튼 EventTrigger에 연결합니다. 잠금·미결선 버튼의 왼쪽 클릭은 기존 선택만 취소합니다.</summary>
    public void OnUnavailableSkillClicked(BaseEventData eventData)
    {
        if (!(eventData is PointerEventData pointer) || pointer.button != PointerEventData.InputButton.Left) return;
        var button = pointer.pointerClick != null ? pointer.pointerClick.GetComponent<Button>() : null;
        // 활성 버튼은 기존 ToggleButton 경로에서 선택합니다. 같은 클릭을 중복 처리하지 않습니다.
        if (button == null || button.IsInteractable()) return;
        inputHandler?.TryCancelSkillSelection();
        SyncSelection();
    }

    private void RefreshSkills()
    {
        var unit = battleFlowManager != null ? battleFlowManager.CurrentUnit : null;
        if (unit != null && unit.IsPlayer) unit.ResolveSelectedSkill();
        var connected = unit != null && unit.IsPlayer ? unit.SelectedSkillData : null;
        if (HasHeroSlots)
        {
            var catalog = DHCsvTemplateCatalog.Instance;
            var skills = new List<SkillData>();
            if (unit != null && unit.IsPlayer && unit.SourceData != null && catalog != null &&
                catalog.TryGetPlayerUnitTemplate(unit.SourceData.UnitTemplateKey, out var template))
                skills = catalog.GetSkillsByClassIndex(template.ClassIndex);

            // 강화판은 ReplaceSkillKey로 같은 기본 스킬 자리에 묶습니다.
            // 현재 데이터에서 루미나의 습득 순서는 ID 순서와 다릅니다.
            var bases = skills.FindAll(s => string.IsNullOrEmpty(ReplacementKey(catalog, s)));
            bases.Sort((a, b) => { int order = a.acquireLevel.CompareTo(b.acquireLevel); return order != 0 ? order : a.skillIndex.CompareTo(b.skillIndex); });
            for (int i = 0; i < heroSlots.Length; i++)
            {
                var slot = heroSlots[i];
                if (slot == null) continue;
                SkillData basic = i < bases.Count ? bases[i] : null;
                SkillData shown = basic;
                if (basic != null)
                {
                    foreach (var candidate in skills)
                        if (ReplacementKey(catalog, candidate) == basic.skillKey && candidate.acquireLevel <= unit.Level &&
                            candidate.acquireLevel > shown.acquireLevel) shown = candidate;
                    // 실행 연결된 기본판을 UI만으로 강화판으로 바꿔 표시하지 않습니다.
                    if (connected != null && (connected.skillIndex == basic.skillIndex || ReplacementKey(catalog, connected) == basic.skillKey))
                        shown = connected;
                }
                // 기존 카탈로그가 없는 테스트 씬도 원래 연결된 한 스킬은 그대로 표시합니다.
                if (bases.Count == 0 && i == 0) shown = connected;
                slot.skill = shown;
                bool locked = shown != null && shown.acquireLevel > unit.Level;
                bool wired = shown != null && connected != null && shown.skillIndex == connected.skillIndex;
                if (slot.button != null) slot.button.interactable = wired && !locked;
                if (slot.name != null) slot.name.text = shown != null ? shown.skillName : "-";
                if (slot.icon != null)
                {
                    // '+' 변형은 별도 스킬 ID이며 협회 강화 단계와 혼용하지 않습니다.
                    slot.icon.sprite = shown != null ? Sprites.Icon.ClassSkill(shown.skillIndex, 1) : null;
                    slot.icon.enabled = slot.icon.sprite != null;
                }
                if (slot.lockedOverlay != null) slot.lockedOverlay.SetActive(locked);
                if (slot.unconnectedLabel != null) slot.unconnectedLabel.SetActive(shown != null && !locked && !wired);
            }
        }
        SyncSelection();
    }

    private static string ReplacementKey(DHCsvTemplateCatalog catalog, SkillData skill)
    {
        return catalog != null && catalog.TryGetClassSkillTemplate(skill.skillIndex, out var template)
            ? template.ReplaceSkillKey : string.Empty;
    }

    private void SyncSelection()
    {
        var action = inputHandler != null ? inputHandler.PendingAction : PendingActionType.None;
        CurrentSkillData = action == PendingActionType.ClassSkill || action == PendingActionType.WeaponSkill
            ? battleFlowManager?.GetCurrentUnitSkill(action) : null;
        if (HasHeroSlots)
        {
            foreach (var slot in heroSlots)
                if (slot != null && slot.toggle != null)
                    slot.toggle.SetState(action == PendingActionType.ClassSkill && slot.button != null && slot.button.interactable &&
                        slot.skill != null && CurrentSkillData != null && slot.skill.skillIndex == CurrentSkillData.skillIndex, false);
        }
        else if (toggle1 != null) toggle1.SetState(action == PendingActionType.ClassSkill, false);
        if (toggle2 != null) toggle2.SetState(action == PendingActionType.WeaponSkill, false);
        RefreshDescription();
    }

    /// <summary>SkillButtonTooltip이 버튼의 호버만 전달합니다. 선택·장착·시전 상태는 바꾸지 않습니다.</summary>
    public void SetDescriptionHover(Button button, bool hovering)
    {
        if (!isActiveAndEnabled || button == null) return;
        if (hovering) hoveredSkillButton = button;
        else if (hoveredSkillButton == button) hoveredSkillButton = null;
        RefreshDescription();
    }

    private void RefreshDescription()
    {
        if (descriptionTitle == null) return;
        var unit = battleFlowManager != null ? battleFlowManager.CurrentUnit : null;
        if (unit == null || descriptionBattleEnded)
        {
            SetDescription(string.Empty, string.Empty, null);
            return;
        }
        if (!unit.IsPlayer)
        {
            SetDescription("적 행동 중", string.Empty, null);
            return;
        }

        var action = inputHandler != null ? inputHandler.PendingAction : PendingActionType.None;
        SkillData skill = null;
        // 버튼 자체를 기억하고 현재 턴의 데이터를 읽어, 호버한 채 턴이 바뀌어도 이전 유닛 정보가 남지 않습니다.
        if (hoveredSkillButton != null && hoveredSkillButton.isActiveAndEnabled && hoveredSkillButton.IsInteractable())
        {
            if (toggle2 != null && hoveredSkillButton.gameObject == toggle2.gameObject)
            {
                skill = unit.EquippedWeaponData?.ToSkillData();
                if (skill != null) action = PendingActionType.WeaponSkill;
            }
            else if (HasHeroSlots)
            {
                foreach (var slot in heroSlots)
                    if (slot != null && slot.button == hoveredSkillButton && slot.skill != null)
                    {
                        skill = slot.skill;
                        action = PendingActionType.ClassSkill;
                        break;
                    }
            }
        }
        if (skill == null) skill = CurrentSkillData;
        if (skill == null)
        {
            SetDescription("스킬 선택", "사용할 스킬 버튼 또는\n코어 스킬을 클릭하세요.", null);
            return;
        }

        // 기존 공용 툴팁과 같은 설명/강화 조회를 사용합니다. 강화 단계와 데이터 원본의 불일치는 별도 확인 대상입니다.
        var gm = GameManager.Instance;
        int unitIndex = unit.SourceData != null ? unit.SourceData.UnitIndex : 0;
        string body;
        if (action == PendingActionType.WeaponSkill && unit.EquippedWeaponData != null)
        {
            int weaponIndex = unit.EquippedWeaponIndex > 0 ? unit.EquippedWeaponIndex : (unit.SourceData?.CurrentWeaponIndex ?? 0);
            int level = gm != null && gm.Workshop != null && unitIndex > 0 ? Mathf.Max(1, gm.Workshop.GetWeaponLevel(weaponIndex)) : 1;
            body = WeaponTooltipText.BuildWeaponSkillDesc(unit.EquippedWeaponData, weaponIndex, level);
        }
        else
        {
            int level = gm != null && gm.Lab != null && unitIndex > 0 ? gm.Lab.GetSkillLevel(unitIndex, skill.skillIndex) : (unit.SourceData?.SkillLevel ?? 1);
            body = ClassSkillTooltipText.BuildDesc(skill, level);
        }
        SetDescription(skill.skillName, body, skill);
    }

    private void SetDescription(string title, string body, SkillData skill)
    {
        if (descriptionTitle != null) descriptionTitle.text = title;
        if (descriptionBody != null) descriptionBody.text = body;
        if (descriptionTags == null) return;
        for (int i = 0; i < descriptionTags.Length; i++)
        {
            var label = descriptionTags[i];
            if (label == null) continue;
            string text = string.Empty;
            if (skill != null)
            {
                if (i == 0) text = skill.classSkillEffect switch { 0 => "공격", 1 => "회복", 2 => "부활", 3 => "버프", 4 => "디버프", _ => string.Empty };
                if (i == 1) text = skill.classSkillRange switch { 0 => "근거리", 1 => "원거리", _ => string.Empty };
                if (i == 2) text = skill.classSkillTarget switch { 0 => "단수", 1 => "복수", 2 => "전체", _ => string.Empty };
            }
            label.text = text;
            label.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }
}
