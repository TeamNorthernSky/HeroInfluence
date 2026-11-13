using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;

/// <summary>JC | 2026-11-12 | 적 필드·턴 배지와 영웅 턴 배지가 하나의 정보창을 공유합니다.</summary>
public sealed class BattleEnemyInfoTooltip : MonoBehaviour
{
    [Serializable]
    private class SkillRow
    {
        [Tooltip("씬에서 위치·크기를 편집하는 스킬 행입니다. 없는 스킬의 행은 숨깁니다.")] public RectTransform root;
        [Tooltip("현재 스킬 이름을 표시합니다.")] public TMP_Text title;
        [Tooltip("현재 스킬 설명을 표시합니다. 긴 설명은 행 안에서 줄바꿈합니다.")] public TMP_Text body;
        [Tooltip("임시 공통 아이콘입니다. 정식 리소스 수령 시 씬에서 교체합니다.")] public Image icon;
    }

    [Tooltip("툴팁을 소유하는 전투씬 Canvas입니다. 공용 DDOL Canvas를 사용하지 않습니다.")]
    [SerializeField] private Canvas battleCanvas;
    [Tooltip("전투 종료·현재 행동 유닛을 조회하는 기존 전투 흐름입니다.")]
    [SerializeField] private BattleFlowManager flow;
    [Tooltip("현재 선택 상태와 자동전투 여부를 조회하는 기존 입력입니다.")]
    [SerializeField] private InputHandler input;
    [Tooltip("빌런 모델·점유 셀에 마우스 광선을 쏘는 카메라입니다. 비어 있으면 MainCamera를 조회합니다.")]
    [SerializeField] private Camera battlefieldCamera;
    [Tooltip("빌런 모델 또는 전투 셀을 찾을 레이어입니다. 대상 선택 입력과 동일한 값을 사용합니다.")]
    [SerializeField] private LayerMask battlefieldMask = ~0;
    [Tooltip("필드 호버 광선의 최대 거리입니다. 단위는 월드 미터입니다.")]
    [SerializeField, Min(1f)] private float rayDistance = 1000f;
    [Tooltip("씬에 배치한 정보창입니다. 위치와 배경 높이만 실행 중 조절하며 자식 배치는 유지합니다.")]
    [SerializeField] private RectTransform panel;
    [Tooltip("적 또는 영웅의 이름입니다. 등급은 별도 공란으로 둡니다.")]
    [SerializeField] private TMP_Text unitName;
    [Tooltip("빌런 등급 공간입니다. 현재 기획 미확정으로 비워둡니다.")]
    [SerializeField] private TMP_Text rank;
    [Tooltip("턴 슬롯과 동일한 기존 초상화 라이브러리에서 적·영웅 초상화를 읽습니다.")]
    [SerializeField] private Image portrait;
    [Tooltip("현재 HP / 최대 HP 숫자입니다.")]
    [SerializeField] private TMP_Text hp;
    [Tooltip("전투 유닛의 FinalStats.Atk를 표시합니다.")]
    [SerializeField] private TMP_Text attack;
    [Tooltip("전투 유닛의 FinalStats.DEF를 표시합니다.")]
    [SerializeField] private TMP_Text defense;
    [Tooltip("스킬 1~5 표시 행입니다. 위치는 고정하며 표시 개수에 따라 배경 높이만 바뀝니다.")]
    [SerializeField] private SkillRow[] skills = new SkillRow[5];
    [Tooltip("스킬이 없는 빌런의 안내입니다.")]
    [SerializeField] private TMP_Text noSkills;
    // JC | 2026-11-12 | 영웅은 스킬 섹션 전체를 숨겨 이름·초상화·세 능력치만 표시합니다.
    [Tooltip("적 정보에만 표시하는 스킬 제목 영역입니다. 영웅 턴 배지에서는 제목과 목록을 모두 숨깁니다.")]
    [SerializeField] private RectTransform skillHeader;
    [Tooltip("영웅 툴팁의 배경 높이입니다. Canvas 단위이며 스킬 영역의 빈 공간을 남기지 않습니다.")]
    [SerializeField, Min(1f)] private float heroHeight = 224f;
    [Tooltip("적 메시 호버 시 마우스에서 떨어질 기본 거리입니다. Canvas 단위이며 턴 프레임과 겹치면 자동 이동합니다. 배지 호버는 슬롯 기준으로 배치합니다.")]
    [SerializeField] private Vector2 pointerOffset = new Vector2(22f, -18f);
    [Tooltip("화면 경계와 확보할 간격입니다. Canvas 단위이며 0이면 여백이 없습니다.")]
    [SerializeField, Min(0f)] private float edgePadding = 12f;
    [Tooltip("마지막 표시 스킬 행 아래의 여백입니다. Canvas 단위입니다.")]
    [SerializeField, Min(0f)] private float bottomPadding = 20f;
    [Tooltip("스킬이 없을 때의 최소 배경 높이입니다. Canvas 단위입니다.")]
    [SerializeField, Min(1f)] private float minimumHeight = 252f;

    [Tooltip("툴팁과 턴 프레임·강조 모델 사이의 여백입니다. Canvas 단위이며 0이면 경계에 붙습니다.")]
    [SerializeField, Min(0f)] private float outlineClearance = 12f;

    // JC | 2026-11-13 | 시각 조절은 JC_BattleUI_VFX 하위에서만 소유합니다. 툴팁은 실제 선택 결과와 설정을 읽습니다.
    [SerializeField, Tooltip("JC_BattleUI_VFX/TurnSlotHover의 공용 배지·화살표 조절기입니다.")]
    private JcBattleTurnSlotSettings turnSlotSettings;
    [SerializeField, Tooltip("JC_BattleUI_VFX/CellBox의 그리드 외형과 선택 호버 윤곽선 설정입니다.")]
    private BattleCellVisualSettings cellVisualSettings;

    private JcBattleHoverLink hoverLink;
    private readonly JcBattleTooltipPlacement tooltipPlacement = new JcBattleTooltipPlacement();
    private bool hoveredTurnUI;
    private TurnSlotUI hoveredTurnSlot;
    private BattleCharactor shownUnit;
    private bool battleEnded;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private readonly Vector3[] corners = new Vector3[4];
    private PointerEventData pointerData;
    private EventSystem pointerSystem;
    private readonly HashSet<string> warnedDescriptions = new HashSet<string>();
    private readonly HashSet<string> warnedOverflow = new HashSet<string>();
    private static readonly Regex EnemyValue = new Regex(@"\{EnemySkill([1-5])(Sub)?Value\}(?:[ \t]*[%％])?");

    private void OnEnable()
    {
        // JC | 2026-11-12 | 모든 전투씬에서 저장된 동일 스타일로 시작합니다.
        hoverLink = GetComponent<JcBattleHoverLink>();
        if (hoverLink == null) hoverLink = gameObject.AddComponent<JcBattleHoverLink>();
        hoverLink.Initialize(battleCanvas);
        tooltipPlacement.Initialize(battleCanvas);
        battleEnded = false;
        if (flow != null) flow.OnBattleEnded += OnBattleEnded;
        Hide();
    }

    private void OnDisable()
    {
        if (flow != null) flow.OnBattleEnded -= OnBattleEnded;
        Hide();
    }

    private void OnBattleEnded(BattleResult result) { battleEnded = true; Hide(); }

    private bool ShouldHide()
    {
        if (battleEnded || panel == null || battleCanvas == null || flow == null || input == null) return true;
        if (Time.timeScale <= 0f || !flow.isActiveAndEnabled || flow.CurrentUnit == null ||
            flow.IsFlowBlocked || flow.IsActionInProgress) return true;
        // JC | 2026-11-13 | 대상 선택 중 배지·모델 안내만 허용합니다. 정보창과 실행·도주 중 호버 차단은 유지합니다.
        if (input.PendingAction != PendingActionType.None &&
            input.PendingAction != PendingActionType.ClassSkill && input.PendingAction != PendingActionType.WeaponSkill) return true;
        // 기존 연출 상태를 읽습니다. 전투 로직·공용 툴팁의 상태는 변경하지 않습니다.
        return flow.BattleManager != null && flow.BattleManager.Presentation.IsSequenceRunning;
    }

    private void LateUpdate()
    {
        if (ShouldHide()) { Hide(); return; }
        var target = FindHoveredUnit();
        bool selectingSkill = input.PendingAction != PendingActionType.None;
        // ASB가 이미 계산한 필드 호버를 사용하여 부활/영역 선택까지 실제 선택 가능 대상과 일치시킵니다.
        if (selectingSkill && !hoveredTurnUI) target = input.PreviewHoverTarget;
        // JC | 2026-11-13 | 부활 분류만 보고 적까지 회복 대상으로 취급하지 않고 실제 호버 대상의 진영을 구분합니다.
        bool healing = selectingSkill && target != null &&
            ASB.Work.Battle.SkillExecution.SkillAreaPreviewHelper.IsHealingPreviewSide(input.PendingSkill, flow.CurrentUnit, target.IsPlayer);
        if (target == null || (!healing && target.IsDead) || !target.gameObject.activeInHierarchy ||
            (target.IsPlayer && !hoveredTurnUI && !healing) ||
            (selectingSkill && !input.IsValidPreviewTarget(target))) { Hide(); return; }
        if (!JC.Tutorial.JcTutorialBattleGuide.AllowsHover(gameObject.scene, target, hoveredTurnUI)) { Hide(); return; }
        // JC | 2026-11-12 | 정보창과 선택 중 배지 표시를 분리합니다. ASB 입력·선택 대상에는 쓰지 않습니다.
        var settings = turnSlotSettings != null ? turnSlotSettings.Draft : null;
        var cells = cellVisualSettings != null ? cellVisualSettings.Draft : null;
        if (target.IsPlayer && !healing) hoverLink.Clear();
        else hoverLink.Show(target, hoveredTurnUI, hoveredTurnSlot,
            battlefieldCamera != null ? battlefieldCamera : Camera.main, selectingSkill, false, settings, healing,
            cells != null && cells.highlightTargetModel,
            selectingSkill && cells != null ? (Color?)cells.For(healing ? BattleCellVisualState.HealingArea : BattleCellVisualState.ConfirmedArea).mainColor : null,
            !healing || (settings != null && settings.showHealingTargets),
            selectingSkill ? input.PendingSkill : null, selectingSkill ? flow.CurrentUnit : null);
        if (selectingSkill) { HidePanel(); return; }
        if (target != shownUnit) Bind(target);
        RefreshStats();
        if (!JC.Tutorial.JcTutorialBattleGuide.TryPlaceTooltip(gameObject.scene, panel))
            PlaceAtPointer(Input.mousePosition);
    }

    private BattleCharactor FindHoveredUnit()
    {
        hoveredTurnUI = false;
        hoveredTurnSlot = null;
        // Game View 밖에서는 에디터가 무한대 포인터 좌표를 반환할 수 있습니다.
        var pointer = Input.mousePosition;
        if (float.IsNaN(pointer.x) || float.IsNaN(pointer.y) ||
            float.IsInfinity(pointer.x) || float.IsInfinity(pointer.y)) return null;
        var system = EventSystem.current;
        if (system != null)
        {
            if (pointerData == null || pointerSystem != system)
            {
                pointerSystem = system;
                pointerData = new PointerEventData(system);
            }
            pointerData.position = pointer;
            uiHits.Clear();
            system.RaycastAll(pointerData, uiHits);
            if (uiHits.Count > 0)
            {
                // 메뉴·다른 UI 뒤의 필드는 호버하지 않습니다. 최상단 UI만 확인합니다.
                hoveredTurnUI = true;
                var slot = uiHits[0].gameObject.GetComponentInParent<TurnSlotUI>();
                var eventCamera = uiHits[0].module != null ? uiHits[0].module.eventCamera : null;
                if (slot != null && slot.gameObject.scene == gameObject.scene &&
                    hoverLink.IsBadgeHit(slot, pointer, eventCamera)) hoveredTurnSlot = slot;
                // 슬롯의 사각 모서리는 배지 바깥입니다. 필드용 위치로 정보창이 열리지 않게 합니다.
                return hoveredTurnSlot != null ? hoveredTurnSlot.DisplayedUnit : null;
            }
        }

        var camera = battlefieldCamera != null ? battlefieldCamera : Camera.main;
        if (camera == null) return null;
        var hits = Physics.RaycastAll(camera.ScreenPointToRay(pointer), rayDistance, battlefieldMask);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            var unit = hit.collider.GetComponentInParent<BattleCharactor>();
            if (unit == null) unit = hit.collider.GetComponentInParent<ASBGridCell>()?.OccupyingUnit;
            if (unit != null) return unit.gameObject.scene == gameObject.scene ? unit : null;
        }
        return null;
    }

    private void Bind(BattleCharactor unit)
    {
        // JC | 2026-11-04 | 적 정보에 처음 진입할 때 호버음을 내고 동일 대상에 머무는 동안 중복 재생을 피합니다.
        PresentationSfxPlayer.Play("Button_Hover");
        shownUnit = unit;
        panel.gameObject.SetActive(true);
        // JC | 2026-11-12 | 영웅은 스킬 매칭용 클래스명이 아니라 실제 영웅 표시명을 사용합니다.
        if (unitName != null) unitName.text = unit.IsPlayer ? unit.DisplayName : unit.UnitName;
        if (rank != null) rank.text = string.Empty;
        if (portrait != null)
        {
            // JC | 2026-11-12 | ASB 턴 슬롯과 같은 영웅 템플릿 키를 사용하여 테스트 임시 파티도 동일하게 표시합니다.
            portrait.sprite = unit.IsPlayer
                ? (unit.SourceData != null ? Sprites.Portrait.Hero(unit.SourceData.UnitTemplateKey) : null)
                : Sprites.Portrait.EnemyByActor(unit);
            portrait.enabled = portrait.sprite != null;
        }
        if (skillHeader != null) skillHeader.gameObject.SetActive(!unit.IsPlayer);
        if (unit.IsPlayer)
        {
            foreach (var row in skills) if (row?.root != null) row.root.gameObject.SetActive(false);
            if (noSkills != null) noSkills.gameObject.SetActive(false);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, heroHeight);
            return;
        }
        string key = unit.SourceEnemyData?.UnitTemplateKey;
        var catalog = DHCsvTemplateCatalog.Instance;
        int count = 0;
        float height = minimumHeight;
        for (int slot = 1; slot <= 5; slot++)
        {
            var skill = ResolveSkill(unit, catalog, key, slot);
            if (skill == null || string.IsNullOrWhiteSpace(skill.skillName)) continue;
            if (count >= skills.Length) break;
            var row = skills[count++];
            row.root.gameObject.SetActive(true);
            if (row.title != null) row.title.text = skill.skillName;
            if (row.body != null) row.body.text = BuildDescription(skill, slot);
            row.root.GetWorldCorners(corners);
            height = Mathf.Max(height, -panel.InverseTransformPoint(corners[0]).y + bottomPadding);
        }
        for (int i = count; i < skills.Length; i++) if (skills[i]?.root != null) skills[i].root.gameObject.SetActive(false);
        if (noSkills != null) noSkills.gameObject.SetActive(count == 0);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        if (!string.IsNullOrEmpty(key) && ResolveSkill(unit, catalog, key, 6) != null && warnedOverflow.Add(key))
            Debug.LogWarning("[BattleEnemyInfoTooltip] 5개를 초과하는 스킬이 있습니다. 표시 상한을 재확인해 주세요: " + key, this);
    }

    private static SkillData ResolveSkill(BattleCharactor unit, DHCsvTemplateCatalog catalog, string key, int slot)
    {
        if (catalog != null && !string.IsNullOrEmpty(key))
        {
            var data = catalog.GetSkillTemplate(EnemySkillKeyRules.Compose(key, slot));
            if (data != null) return data;
        }
        return unit.availableSkills?.Find(s => s != null && s.slot == slot);
    }

    private string BuildDescription(SkillData skill, int slot)
    {
        string description = EnemyValue.Replace(skill.description ?? string.Empty, match =>
        {
            // 다른 슬롯을 가리키는 원본은 임의 보정하지 않고 재확인 대상으로 남깁니다.
            if (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) != slot)
            {
                if (warnedDescriptions.Add(skill.skillKey ?? skill.skillName))
                    Debug.LogWarning("[BattleEnemyInfoTooltip] 스킬 설명의 교차 슬롯 계수 확인 필요: " + skill.skillName, this);
                return "수치 확인 중";
            }
            float value = match.Groups[2].Success ? skill.skillSubValue : skill.skillValue;
            return (value * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
        });
        return ClassSkillTooltipText.FormatBattleDescription(description);
    }

    private void RefreshStats()
    {
        if (shownUnit == null) return;
        if (hp != null) hp.text = $"{shownUnit.CurrentHp:0.##}/{shownUnit.MaxHp:0.##}";
        if (attack != null) attack.text = shownUnit.FinalStats.Atk.ToString("0.##");
        if (defense != null) defense.text = shownUnit.FinalStats.DEF.ToString("0.##");
    }

    private void PlaceAtPointer(Vector2 screenPosition)
    {
        bool visible = tooltipPlacement.Place(panel, shownUnit, hoveredTurnSlot,
            battlefieldCamera != null ? battlefieldCamera : Camera.main,
            screenPosition, pointerOffset, edgePadding, outlineClearance,
            hoverLink.ModelClearancePixels, hoverLink.BadgeClearance);
        panel.gameObject.SetActive(visible);
    }

    private void Hide()
    {
        if (hoverLink != null) hoverLink.Clear();
        HidePanel();
    }

    // JC | 2026-11-12 | 선택 중 턴 배지 표시를 지우지 않고 정보 패널만 닫습니다.
    private void HidePanel()
    {
        shownUnit = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }
}
