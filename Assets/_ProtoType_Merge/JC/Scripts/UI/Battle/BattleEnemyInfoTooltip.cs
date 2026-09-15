using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;

/// <summary>전투씬에 미리 배치한 빌런 정보창 하나를 필드·턴 초상화 호버에 공유합니다.</summary>
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
    [Tooltip("빌런 이름입니다. 등급은 확정 전까지 별도 공란으로 둡니다.")]
    [SerializeField] private TMP_Text unitName;
    [Tooltip("빌런 등급 공간입니다. 현재 기획 미확정으로 비워둡니다.")]
    [SerializeField] private TMP_Text rank;
    [Tooltip("기존 초상화 라이브러리에서 읽는 빌런 초상화입니다.")]
    [SerializeField] private Image portrait;
    [Tooltip("현재 HP / 최대 HP 숫자입니다.")]
    [SerializeField] private TMP_Text hp;
    [Tooltip("기존 HP 게이지 자산을 재사용합니다. Filled 방식으로 현재 비율을 표시합니다.")]
    [SerializeField] private Image hpFill;
    [Tooltip("전투 유닛의 FinalStats.Atk를 표시합니다.")]
    [SerializeField] private TMP_Text attack;
    [Tooltip("전투 유닛의 FinalStats.DEF를 표시합니다.")]
    [SerializeField] private TMP_Text defense;
    [Tooltip("스킬 1~5 표시 행입니다. 위치는 고정하며 표시 개수에 따라 배경 높이만 바뀝니다.")]
    [SerializeField] private SkillRow[] skills = new SkillRow[5];
    [Tooltip("스킬이 없는 빌런의 안내입니다.")]
    [SerializeField] private TMP_Text noSkills;
    [Tooltip("마우스에서 떨어질 거리입니다. Canvas 단위이며 X는 오른쪽, Y는 위쪽이 양수입니다.")]
    [SerializeField] private Vector2 pointerOffset = new Vector2(22f, -18f);
    [Tooltip("화면 경계와 확보할 간격입니다. Canvas 단위이며 0이면 여백이 없습니다.")]
    [SerializeField, Min(0f)] private float edgePadding = 12f;
    [Tooltip("마지막 표시 스킬 행 아래의 여백입니다. Canvas 단위입니다.")]
    [SerializeField, Min(0f)] private float bottomPadding = 20f;
    [Tooltip("스킬이 없을 때의 최소 배경 높이입니다. Canvas 단위입니다.")]
    [SerializeField, Min(1f)] private float minimumHeight = 252f;

    private BattleCharactor shownUnit;
    private bool battleEnded;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private readonly Vector3[] corners = new Vector3[4];
    private PointerEventData pointerData;
    private EventSystem pointerSystem;
    private readonly HashSet<string> warnedDescriptions = new HashSet<string>();
    private readonly HashSet<string> warnedOverflow = new HashSet<string>();
    private static readonly Regex EnemyValue = new Regex(@"\{EnemySkill([1-5])(Sub)?Value\}");

    private void OnEnable()
    {
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
        if (input.PendingAction != PendingActionType.None) return true;
        // 기존 연출 상태를 읽습니다. 전투 로직·공용 툴팁의 상태는 변경하지 않습니다.
        return flow.BattleManager != null && flow.BattleManager.Presentation.IsSequenceRunning;
    }

    private void LateUpdate()
    {
        if (ShouldHide()) { Hide(); return; }
        var target = FindHoveredUnit();
        if (target == null || target.IsPlayer || target.IsDead) { Hide(); return; }
        if (target != shownUnit) Bind(target);
        RefreshStats();
        PlaceAtPointer(Input.mousePosition);
    }

    private BattleCharactor FindHoveredUnit()
    {
        var system = EventSystem.current;
        if (system != null)
        {
            if (pointerData == null || pointerSystem != system)
            {
                pointerSystem = system;
                pointerData = new PointerEventData(system);
            }
            pointerData.position = Input.mousePosition;
            uiHits.Clear();
            system.RaycastAll(pointerData, uiHits);
            if (uiHits.Count > 0)
            {
                // 메뉴·다른 UI 뒤의 필드는 호버하지 않습니다. 최상단 UI만 확인합니다.
                var slot = uiHits[0].gameObject.GetComponentInParent<TurnSlotUI>();
                return slot != null && slot.gameObject.scene == gameObject.scene ? slot.DisplayedUnit : null;
            }
        }

        var camera = battlefieldCamera != null ? battlefieldCamera : Camera.main;
        if (camera == null) return null;
        var hits = Physics.RaycastAll(camera.ScreenPointToRay(Input.mousePosition), rayDistance, battlefieldMask);
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
        shownUnit = unit;
        panel.gameObject.SetActive(true);
        if (unitName != null) unitName.text = unit.UnitName;
        if (rank != null) rank.text = string.Empty;
        if (portrait != null)
        {
            portrait.sprite = Sprites.Portrait.Enemy(unit.SourceEnemyData?.UnitTemplateKey);
            portrait.enabled = portrait.sprite != null;
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
        return EnemyValue.Replace(skill.description ?? string.Empty, match =>
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
    }

    private void RefreshStats()
    {
        if (shownUnit == null) return;
        if (hp != null) hp.text = $"{shownUnit.CurrentHp:0.##}/{shownUnit.MaxHp:0.##}";
        if (hpFill != null) hpFill.fillAmount = shownUnit.MaxHp > 0 ? Mathf.Clamp01(shownUnit.CurrentHp / shownUnit.MaxHp) : 0;
        if (attack != null) attack.text = shownUnit.FinalStats.Atk.ToString("0.##");
        if (defense != null) defense.text = shownUnit.FinalStats.DEF.ToString("0.##");
    }

    private void PlaceAtPointer(Vector2 screenPosition)
    {
        var canvasRect = (RectTransform)battleCanvas.transform;
        var camera = battleCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : battleCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, camera, out var point)) return;
        var bounds = canvasRect.rect;
        float width = panel.rect.width, height = panel.rect.height;
        float x = point.x + pointerOffset.x, y = point.y + pointerOffset.y;
        if (x + width > bounds.xMax - edgePadding) x = point.x - pointerOffset.x - width;
        if (y - height < bounds.yMin + edgePadding) y = point.y - pointerOffset.y + height;
        x = Mathf.Clamp(x, bounds.xMin + edgePadding, Mathf.Max(bounds.xMin + edgePadding, bounds.xMax - edgePadding - width));
        y = Mathf.Clamp(y, Mathf.Min(bounds.yMax - edgePadding, bounds.yMin + edgePadding + height), bounds.yMax - edgePadding);
        panel.anchoredPosition = new Vector2(x, y);
    }

    private void Hide()
    {
        shownUnit = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }
}
