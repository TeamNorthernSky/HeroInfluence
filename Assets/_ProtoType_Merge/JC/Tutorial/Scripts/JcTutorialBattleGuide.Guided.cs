using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace JC.Tutorial
{
    public sealed partial class JcTutorialBattleGuide
    {
        // 기존 전투 안내 컴포넌트의 진행 확장입니다. 별도 러너나 씬 컴포넌트를 등록하지 않습니다.
        private static JcTutorialBattleGuide activeGuide;
        private bool guidedActive, detailed, formation, nextPending;
        private ASB.Work.BattleGrid.BattleGridManager formationGrid;
        private int guidedPhase, phaseFrame, clickDownFrame = -1;
        private BattleCharactor guidedActor, guidedEnemy;
        private BattleFlowManager guidedFlow;
        private InputHandler guidedInput;
        private SkillButtonController guidedSkills;
        private TurnSlotUI guidedSlot;
        private IDisposable actionRestriction;
        private RectTransform worldFocus, allowedPointer, turnFocus, profileFocus;
        private Button nextButton;
        private TMP_Text nextLabel, guidedBody;
        private string savedBody, characterId;
        private Vector2 savedBodySize, savedBodyPosition, savedBodyAnchorMin, savedBodyAnchorMax, savedBodyPivot;
        private Transform savedBodyParent;
        private int savedBodySibling;
        private bool savedBodyAutoSize;
        private ITextPreprocessor savedBodyPreprocessor;
        private RectTransform guidedTooltip;
        private bool savedNavigation;
        private EventSystem guidedEventSystem;
        private const int IntroSkill = 3010;
        private const int FreeGuidePhase = 5;
        private bool FreeGuide => detailed && guidedPhase == FreeGuidePhase;
        private RectTransform currentTurnFocus;
        public bool GuidedActive => guidedActive;
        public static bool BlocksBattleControls => activeGuide != null && activeGuide.guidedActive;
        public static RectTransform MessageObstacle => activeGuide != null && activeGuide.guidedActive ? activeGuide.layoutPanel : null;
        public static bool AllowsCancel => !BlocksBattleControls || activeGuide.FreeGuide;

        private T SceneComponent<T>() where T : Component => gameObject.scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

        public bool BeginCharacter(BattleCharactor actor, string id) => BeginGuided(actor, id, false);

        public bool BeginFormation(BattleCharactor actor) => BeginGuided(actor, null, true);

        private bool BeginGuided(BattleCharactor actor, string id, bool showFormation)
        {
            if (guidedActive || actor == null || view == null || view.panel == null || resultShield == null ||
                stepTexts == null || stepTexts.Length == 0 || stepTexts[0] == null) return false;
            CacheLayout();
            guidedBody = stepTexts[0].GetComponent<TMP_Text>();
            if (guidedBody == null) return false;
            guidedFlow = SceneComponent<BattleFlowManager>();
            guidedInput = SceneComponent<InputHandler>();
            guidedSkills = SceneComponent<SkillButtonController>();
            if (guidedFlow == null || guidedInput == null || guidedSkills == null) return false;
            guidedActor = actor; characterId = id; formation = showFormation; detailed = !formation && id == "10003";
            if (formation) formationGrid = SceneComponent<ASB.Work.BattleGrid.BattleGridManager>();
            savedBody = guidedBody.text;
            var bodyRect = guidedBody.rectTransform;
            savedBodySize = bodyRect.sizeDelta; savedBodyPosition = bodyRect.anchoredPosition;
            savedBodyParent = bodyRect.parent; savedBodySibling = bodyRect.GetSiblingIndex();
            savedBodyAnchorMin = bodyRect.anchorMin; savedBodyAnchorMax = bodyRect.anchorMax; savedBodyPivot = bodyRect.pivot;
            savedBodyAutoSize = guidedBody.enableAutoSizing; savedBodyPreprocessor = guidedBody.textPreprocessor;
            bodyRect.SetParent(layoutPanel, false);
            bodyRect.anchorMin = bodyRect.anchorMax = bodyRect.pivot = new Vector2(.5f, 1);
            guidedBody.enableAutoSizing = false;
            JcTutorialGuideView.BindWordWrapping(guidedBody);
            guidedActive = true; activeGuide = this;
            view.FixedBattleOutline = true;
            guidedEventSystem = EventSystem.current;
            if (guidedEventSystem != null) { savedNavigation = guidedEventSystem.sendNavigationEvents; guidedEventSystem.sendNavigationEvents = false; guidedEventSystem.SetSelectedGameObject(null); }
            var auto = SceneComponent<AutoBattleController>();
            if (auto != null) auto.IsAutoBattle = false;
            guidedInput.IsAutoBattleActive = false;
            actionRestriction = guidedFlow.AddPlayerActionConstraint(this, AllowsGuidedAction, "전투 튜토리얼 안내");
            var rects = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).ToArray();
            profileFocus = rects.FirstOrDefault(r => r.name == "BottomPanel");
            if (profileFocus == null) profileFocus = guidedSkills.transform as RectTransform;
            var turns = SceneComponent<TurnOrderUI>();
            turnFocus = turns != null ? turns.transform as RectTransform : null;
            if (worldFocus == null)
            {
                worldFocus = new GameObject("GuidedEnemyScreenBounds", typeof(RectTransform)).GetComponent<RectTransform>();
                worldFocus.SetParent(layoutPanel.parent, false);
                worldFocus.anchorMin = worldFocus.anchorMax = new Vector2(.5f, .5f);
            }
            if (currentTurnFocus == null && turnFocus != null)
            {
                currentTurnFocus = new GameObject("GuidedCurrentTurnFrame", typeof(RectTransform)).GetComponent<RectTransform>();
                currentTurnFocus.SetParent(turnFocus, false);
                currentTurnFocus.anchorMin = currentTurnFocus.anchorMax = currentTurnFocus.pivot = new Vector2(0, 1);
            }
            EnsureNextButton();
            BeginNotice(null);
            noticeOpen = false; instructionStep = -1;
            SetGuidedPhase(0);
            return true;
        }

        private bool AllowsGuidedAction(BattleCharactor actor, PendingActionType action, BattleCharactor target)
        {
            if (!guidedActive) return true;
            if (!FreeGuide || nextPending || actor != guidedActor || action != PendingActionType.ClassSkill) return false;
            return target == null || (target != null && !target.IsPlayer && !target.IsDead &&
                guidedInput.PendingSkill != null && guidedInput.PendingSkill.skillIndex == IntroSkill);
        }
        public static bool AllowsSkill(Scene scene, PendingActionType action, int skill)
        {
            var g = activeGuide;
            return g == null || !g.guidedActive || g.gameObject.scene != scene ||
                (g.FreeGuide && action == PendingActionType.ClassSkill && skill == IntroSkill && !g.nextPending);
        }
        public static void SkillSelected(Scene scene, BattleCharactor actor, SkillData skill)
        {
            var g = activeGuide;
            if (g != null && g.guidedActive && g.gameObject.scene == scene && g.FreeGuide &&
                actor == g.guidedActor && skill != null && skill.skillIndex == IntroSkill)
            {
                g.EndGuided(true);
                // 선택 성공과 같은 프레임에 숨깁니다. 다음 소개에서는 기존 페이드를 다시 사용합니다.
                if (g.view.panel != null) { g.view.panel.alpha = 0; g.view.panel.blocksRaycasts = false; }
            }
        }
        public static bool AllowsHover(Scene scene, BattleCharactor target, bool fromUI)
        {
            var g = activeGuide;
            return g == null || !g.guidedActive || g.gameObject.scene != scene ||
                (g.FreeGuide || (target == g.guidedEnemy && ((g.guidedPhase == 3 && !fromUI) || (g.guidedPhase == 4 && fromUI))));
        }
        public bool IsAllowedPointer(Vector2 position)
        {
            if (!guidedActive || nextPending) return false;
            if (FreeGuide) return true;
            if (nextButton != null && nextButton.gameObject.activeInHierarchy && ContainsPointer((RectTransform)nextButton.transform, position)) return true;
            if (allowedPointer == null) return false;
            return ContainsPointer(allowedPointer, position);
        }
        private static bool ContainsPointer(RectTransform rect, Vector2 point)
        {
            var c = rect.GetComponentInParent<Canvas>();
            return RectTransformUtility.RectangleContainsScreenPoint(rect, point, c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null);
        }
        public static void PlaceTooltip(Scene scene, RectTransform tooltip)
        {
            var g = activeGuide;
            if (g == null || !g.guidedActive || g.gameObject.scene != scene || tooltip == null) return;
            g.guidedTooltip = tooltip;
            if (g.dim != null && (g.guidedPhase == 3 || g.guidedPhase == 4)) g.dim.SetTooltip(tooltip);
            if (!g.detailed || g.formation || g.guidedPhase != 3 || g.layoutPanel == null) return;
            // Only the world-enemy hover lesson pins the tooltip above its instruction panel.
            var panelCanvas = g.layoutPanel.GetComponentInParent<Canvas>();
            var tooltipCanvas = tooltip.GetComponentInParent<Canvas>();
            var panelCamera = panelCanvas != null && panelCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? panelCanvas.worldCamera : null;
            var tooltipCamera = tooltipCanvas != null && tooltipCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? tooltipCanvas.worldCamera : null;
            var corners = new Vector3[4];
            g.layoutPanel.GetWorldCorners(corners);
            var panelLeft = RectTransformUtility.WorldToScreenPoint(panelCamera, corners[1]);
            var panelRight = RectTransformUtility.WorldToScreenPoint(panelCamera, corners[2]);
            tooltip.GetWorldCorners(corners);
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(tooltipCamera, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(tooltipCamera, corners[2]);
            float width = topRight.x - bottomLeft.x, height = topRight.y - bottomLeft.y;
            float gap = 12f * (panelCanvas != null ? panelCanvas.scaleFactor : 1f);
            float left = Mathf.Clamp(Mathf.Lerp(panelLeft.x, panelRight.x, .59f) - width * .5f, 8f, Mathf.Max(8f, Screen.width-width-8f));
            float bottom = Mathf.Min(panelLeft.y + gap, Screen.height-height-8f);
            var pivot = RectTransformUtility.WorldToScreenPoint(tooltipCamera, tooltip.position);
            var desired = pivot + new Vector2(left-bottomLeft.x, bottom-bottomLeft.y);
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(tooltip, desired, tooltipCamera, out var world))
                tooltip.position = world;
        }
        private void EnsureNextButton()
        {
            if (nextButton != null) return;
            var go = new GameObject("GuidedNext", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(layoutPanel, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(1, 0);
            r.anchoredPosition = new Vector2(-24, 14); r.sizeDelta = new Vector2(150, 42);
            go.GetComponent<Image>().color = new Color(.04f, .3f, .7f, 1);
            nextButton = go.GetComponent<Button>(); nextButton.onClick.AddListener(RequestGuidedNext);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(go.transform, false);
            var lr = (RectTransform)label.transform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            nextLabel = label.GetComponent<TMP_Text>(); nextLabel.font = guidedBody.font; nextLabel.fontSize = 22;
            nextLabel.alignment = TextAlignmentOptions.Center; nextLabel.raycastTarget = false;
        }
        private void RequestGuidedNext()
        {
            if (!guidedActive || FreeGuide || Time.frameCount <= phaseFrame || nextPending) return;
            nextPending = true;
        }
        private void SetGuidedPhase(int phase)
        {
            view.SetActionHint(JcTutorialGuideView.ActionHint.None);
            guidedPhase = phase; phaseFrame = Time.frameCount; clickDownFrame = -1;
            nextPending = false; allowedPointer = null;
            string title, text;
            if (formation)
            {
                string frontColor = ColorUtility.ToHtmlStringRGB(ASB.Work.BattleGrid.BattleGridManager.TutorialFrontColor);
                string rearColor = ColorUtility.ToHtmlStringRGB(ASB.Work.BattleGrid.BattleGridManager.TutorialRearColor);
                string[] titles = { JcTutorialText.Get("battleguide.guided.001", "전열과 후열"), JcTutorialText.Get("battleguide.guided.002", "반격과 피해경감"), JcTutorialText.Get("battleguide.guided.003", "공격 가능한 대상") };
                string[] texts = {
                    JcTutorialText.Format("battleguide.guided.004", "전장에는 적과 가까운 ‘전열’과 그 뒤에 있는 ‘후열’이 있습니다.\n\n<color=#{0}>녹색으로 표시된 열이 ‘<b>전열</b>’</color>\n<color=#{1}>적색으로 표시된 열이 ‘<b>후열</b>’</color>\n\n전열과 후열은 각각의 위치에 따른 특징을 지닙니다.", frontColor, rearColor),
                    JcTutorialText.Format("battleguide.guided.005", "<color=#{0}><b>전열</b></color>에서는 근거리 공격을 받고 살아남으면 반격 확률에 따라 반격합니다. 충전·휴식 중이거나 반격용 공격 스킬이 없으면 반격할 수 없습니다.\n<line-height=50%>\n</line-height><color=#{1}><b>후열</b></color>에서는 경감 확률에 따라 피해 계산 배율이 50%로 줄고, 해당 타격은 치명타가 되지 않습니다. 반격 피해에는 경감이 적용되지 않습니다.", frontColor, rearColor),
                    JcTutorialText.Get("battleguide.guided.006", "스킬에 따라 공격할 수 있는 열과 대상이 달라집니다.\n사용할 스킬을 선택한 뒤, 바닥에 표시되는 공격 가능한 대상을 확인해 보세요.") };
                title = titles[phase]; text = texts[phase];
            }
            else if (phase == 0)
            {
                switch (characterId)
                {
                    case "10001": title = JcTutorialText.Get("battleguide.guided.007", "저스티스의 차례입니다"); text = JcTutorialText.Get("battleguide.guided.008", "저스티스는 근거리에서 싸우며 아군을 보호하는 히어로입니다.\n‘저스티스 등장!’은 적 하나를 공격하고, 다음 자신의 차례까지 적의 단일 공격을 자신에게 유도합니다."); break;
                    case "10002": title = JcTutorialText.Get("battleguide.guided.009", "루미나의 차례입니다"); text = JcTutorialText.Get("battleguide.guided.010", "루미나는 마법을 이용한 원거리 공격에 특화된 캐릭터 입니다.\n지금 사용가능한 스킬 '플레어 봄'은 하나의 적에게 원거리 공격을 하는 스킬이며, 레벨업을 하면 광역 공격 기술을 습득할 수 있습니다."); break;
                    case "10004": title = JcTutorialText.Get("battleguide.guided.011", "네코밍의 차례입니다"); text = JcTutorialText.Get("battleguide.guided.012", "네코밍은 아군의 체력을 회복하는 지원형 히어로입니다.\n‘아픈 거 다 날아가라’로 체력이 줄어든 아군 한 명을 회복할 수 있습니다."); break;
                    default: title = JcTutorialText.Get("battleguide.guided.013", "블랙 불릿의 차례입니다"); text = JcTutorialText.Get("battleguide.guided.014", "블랙 불릿은 적 하나를 집중 공격하는 원거리 히어로입니다.\n첫 공격에 앞서, 전투 화면과 조작 방법을 알아보겠습니다."); break;
                }
            }
            else if (phase == FreeGuidePhase)
            {
                title = JcTutorialText.Get("battleguide.guided.015", "스킬 사용하기");
                text = JcTutorialText.Get("battleguide.guided.016", "왼쪽 스킬 버튼 위에 마우스 커서를 올리면 아래에서 스킬 설명을 확인할 수 있습니다.\n스킬을 클릭한 뒤 공격할 적을 선택하면 스킬을 사용합니다. 선택한 스킬은 버튼 테두리가 빛나며 표시됩니다.");
            }
            else
            {
                string[] titles = { "", JcTutorialText.Get("battleguide.guided.017", "행동할 히어로 확인하기"), JcTutorialText.Get("battleguide.guided.018", "행동 순서 확인하기"), JcTutorialText.Get("battleguide.guided.019", "전장의 적 살펴보기"), JcTutorialText.Get("battleguide.guided.020", "배지로 적 찾아보기") };
                string[] texts = { "", JcTutorialText.Get("battleguide.guided.021", "왼쪽에는 현재 행동할 히어로의 정보와 사용할 수 있는 스킬이 표시됩니다.\n위쪽에서 체력을 확인하고, 아래쪽에서 사용할 스킬을 선택할 수 있습니다."),
                    JcTutorialText.Get("battleguide.guided.022", "오른쪽의 ‘현재 턴’은 지금 행동하는 유닛을 보여줍니다.\n아래의 배지는 위에서부터 앞으로 행동할 유닛의 순서입니다."),
                    JcTutorialText.Get("battleguide.guided.023", "밝게 표시된 적 위에 마우스를 올려 보세요.\n적의 정보와 윤곽선·바닥 타일이 표시되고, 턴 목록에서도 해당 적을 확인할 수 있습니다."),
                    JcTutorialText.Get("battleguide.guided.024", "오른쪽 턴 목록의 적 배지에 마우스를 올리면, 전장에서 해당 적의 윤곽선과 바닥 타일이 강조됩니다.") };
                title = titles[phase]; text = texts[phase];
            }
            guidedBody.text = text; view.SetContent(title, "");
            view.heading.enableAutoSizing = false; view.heading.fontSize = guidedBody.fontSize;
            view.heading.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, guidedBody.fontSize * 1.5f);
            nextButton.gameObject.SetActive(!FreeGuide);
            nextButton.interactable = true;
            nextLabel.text = formation && phase == 2 ? JcTutorialText.Get("battleguide.guided.025", "전투 시작") : JcTutorialText.Get("battleguide.guided.026", "다음");
            ((RectTransform)nextButton.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 150);
            if (FreeGuide) { UnlockInput(); if (dim != null) dim.gameObject.SetActive(false); }

            foreach (var button in continueButtons) if (button != null) button.gameObject.SetActive(false);
            view.SetPanelSuppressed(false); view.SetVisible(true);
            // 모든 새 소개/조작 안내에 같은 여백을 적용합니다. 중첩 Text 부모 크기에 의존하지 않습니다.
            var canvasRect = layoutPanel.parent as RectTransform;
            float width = Mathf.Min(820, canvasRect != null ? Mathf.Max(320, canvasRect.rect.width - 64) : 820);
            float textWidth = width - 64;
            // 실제 Rect 너비·텍스트 전처리·리치 텍스트 문단 간격을 함께 반영합니다.
            guidedBody.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
            float textHeight = Mathf.Ceil(guidedBody.preferredHeight) + 8;
            float height = Mathf.Max(FreeGuide ? 260 : 300, 96 + textHeight + (FreeGuide ? 40 : 80));
            guidedBody.rectTransform.sizeDelta = new Vector2(textWidth, textHeight);
            guidedBody.rectTransform.anchoredPosition = new Vector2(0, -96);
            layoutPanel.sizeDelta = new Vector2(width, height);
            bool lower = phase == 3 || phase == 4;
            layoutPanel.anchorMin = layoutPanel.anchorMax = new Vector2(.5f, lower ? 0 : 1);
            layoutPanel.anchoredPosition = new Vector2(0, (lower ? 1 : -1) * (height * .5f + 20));

        }
        private void UpdateGuided()
        {
            if (guidedFlow != null && guidedFlow.IsBattleEnded) { EndGuided(false); return; }
            if (guidedActor == null || guidedActor.IsDead || guidedFlow == null || guidedFlow.CurrentUnit != guidedActor)
            { EndGuided(true); return; }
            if (FreeGuide)
            {
                allowedPointer = null;
                view.SetVisible(true, null, JcTutorialGuideView.FocusPresentation.BorderOnly);
                var skillButton = guidedSkills.FindSkillButton(IntroSkill);
                view.SetActionHint(JcTutorialGuideView.ActionHint.ClickSkill, skillButton != null ? (RectTransform)skillButton.transform : null);
                dim.gameObject.SetActive(false); resultShield.SetActive(false);
                return;
            }
            if (formation) { UpdateFormationFocus(); return; }
            if (detailed)
            {
                if (guidedEnemy == null || guidedEnemy.IsDead)
                {
                    guidedSlot = FindObjectsByType<TurnSlotUI>(FindObjectsSortMode.None)
                        .Where(s => s.gameObject.scene == gameObject.scene && s.DisplayedUnit != null && !s.DisplayedUnit.IsPlayer && !s.DisplayedUnit.IsDead)
                        .OrderBy(s => s.transform.GetSiblingIndex()).FirstOrDefault();
                    guidedEnemy = guidedSlot != null ? guidedSlot.DisplayedUnit : null;
                }
                if (guidedPhase >= 3 && guidedEnemy == null)
                { Debug.LogError("[Tutorial] 소개할 적 연결이 없어 상세 안내를 종료합니다.", this); EndGuided(true); return; }
            }
            UpdateWorldFocus();
            if (currentTurnFocus != null)
            {
                // TurnFrameReferenceFit의 375 x 1069 기준에서 현재 턴 배지는 상단 311까지다.
                // 비활성 레거시 조각의 경계는 다음 슬롯까지 포함하므로 사용하지 않는다.
                // 부모 Rect 크기에 비례하므로 CanvasScaler/해상도 변경에도 같은 배지를 감싼다.
                currentTurnFocus.sizeDelta = new Vector2(turnFocus.rect.width, turnFocus.rect.height * (311f / 1069f));
            }
            var badge = guidedSlot != null ? guidedSlot.transform as RectTransform : null;
            RectTransform focus = guidedPhase == 1 ? profileFocus : guidedPhase == 2 ? turnFocus :
                guidedPhase == 3 ? worldFocus : guidedPhase == 4 ? badge : currentTurnFocus;
            allowedPointer = guidedPhase == 3 ? worldFocus : guidedPhase == 4 ? badge : null;
            view.SetVisible(true, focus, JcTutorialGuideView.FocusPresentation.BorderOnly);
            view.SetActionHint(guidedPhase == 3 ? JcTutorialGuideView.ActionHint.HoverEnemy : guidedPhase == 4 ? JcTutorialGuideView.ActionHint.HoverBadge : JcTutorialGuideView.ActionHint.None, focus);
            dim.SetTargets(layoutPanel, focus);
            dim.SetExtraTargets(guidedPhase == 3 || guidedPhase == 4 ? new[] { worldFocus, badge } : Array.Empty<RectTransform>());
            dim.SetTooltip(guidedPhase == 3 || guidedPhase == 4 ? guidedTooltip : null);
            dim.SetRoundedTarget(guidedPhase == 4 ? worldFocus : null, 16f);
            resultShield.SetActive(true);
        }
        private void LateGuided()
        {
            if (guidedEventSystem != null && !FreeGuide) guidedEventSystem.SetSelectedGameObject(null);
            if (!FreeGuide && Time.frameCount > phaseFrame)
            {
                if (Input.GetMouseButtonDown(0)) clickDownFrame = Time.frameCount;
                if (Input.GetMouseButtonUp(0) && clickDownFrame > phaseFrame) RequestGuidedNext();
            }
            if (nextPending && !Input.GetMouseButton(0))
            {
                if (formation && guidedPhase < 2) SetGuidedPhase(guidedPhase + 1);
                else if (!detailed) EndGuided(true);
                else SetGuidedPhase(guidedPhase + 1);
            }
        }
        private void UpdateFormationFocus()
        {
            allowedPointer = null;
            bool rows = guidedPhase < 2;
            if (formationGrid != null) formationGrid.SetTutorialRows(this, rows);
            view.SetVisible(true, rows ? null : profileFocus, JcTutorialGuideView.FocusPresentation.BorderOnly);
            // 안내창과 입력 차단은 유지하고, 배치 설명의 디밍은 실제 바닥 셀에서만 그립니다.
            dim.SetTargets(layoutPanel, rows ? null : profileFocus);
            dim.SetExtraTargets(Array.Empty<RectTransform>());
            dim.gameObject.SetActive(!rows);
            resultShield.SetActive(true);
        }
        private void UpdateWorldFocus()
        {
            if (guidedEnemy == null || Camera.main == null) return;
            var renderers = guidedEnemy.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
            b.Encapsulate(guidedEnemy.transform.position + Vector3.down * .1f);
            Vector2 lo = Vector2.positiveInfinity, hi = Vector2.negativeInfinity;
            for (int i=0;i<8;i++)
            {
                var p = b.center + Vector3.Scale(b.extents, new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector2 screen = Camera.main.WorldToScreenPoint(p);
                var parent = worldFocus.parent as RectTransform; var canvas = parent.GetComponentInParent<Canvas>();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
                lo = Vector2.Min(lo, local); hi = Vector2.Max(hi, local);
            }
            worldFocus.localPosition = (lo + hi) * .5f; worldFocus.sizeDelta = hi - lo + Vector2.one * 44;
        }
        private void EndGuided(bool notify)
        {
            if (!guidedActive) return;
            guidedActive = false; formation = false;
            if (dim != null) dim.SetTooltip(null);
            guidedTooltip = null;
            view.FixedBattleOutline = false;
            if (formationGrid != null) formationGrid.SetTutorialRows(this, false);
            formationGrid = null;
            if (activeGuide == this) activeGuide = null;
            actionRestriction?.Dispose(); actionRestriction = null;
            if (guidedEventSystem != null) guidedEventSystem.sendNavigationEvents = savedNavigation;
            if (nextButton != null) nextButton.gameObject.SetActive(false);
            if (guidedBody != null)
            {
                guidedBody.text = savedBody; guidedBody.enableAutoSizing = savedBodyAutoSize; guidedBody.textPreprocessor = savedBodyPreprocessor;
                var rect = guidedBody.rectTransform;
                rect.SetParent(savedBodyParent, false); rect.SetSiblingIndex(savedBodySibling);
                rect.anchorMin = savedBodyAnchorMin; rect.anchorMax = savedBodyAnchorMax; rect.pivot = savedBodyPivot;
                rect.sizeDelta = savedBodySize; rect.anchoredPosition = savedBodyPosition;
            }
            if (dim != null) { dim.SetExtraTargets(Array.Empty<RectTransform>()); dim.gameObject.SetActive(false); }
            noticeOpen = releasePending = false; UnlockInput(); view.SetVisible(false);
            layoutPanel.anchorMin = originalAnchorMin; layoutPanel.anchorMax = originalAnchorMax;
            layoutPanel.sizeDelta = originalSize; layoutPanel.anchoredPosition = originalPosition;
            if (notify && continueButtons != null && continueButtons.Length > 0 && continueButtons[0] != null) continueButtons[0].onClick.Invoke();
        }
    }
}
