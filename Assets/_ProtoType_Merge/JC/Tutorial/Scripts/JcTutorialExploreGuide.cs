using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JC.Tutorial
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100), AddComponentMenu("JC Tutorial/탐사 안내")]
    public sealed class JcTutorialExploreGuide : MonoBehaviour
    {
        private const string Prefix = "jc.explore.guide.v2.";
        [Header("기존 씬 연결")]
        [Tooltip("현재 이동 목표를 읽습니다. 이동 순서·규칙은 변경하지 않습니다.")] public TutorialMovementConstraint movement;
        [Tooltip("목표 위치와 지면 높이를 읽는 그리드입니다.")] public GridManager grid;
        [Tooltip("현재 플레이어 파티를 조회합니다.")] public PartyRegistry parties;
        [Tooltip("클릭으로 선택한 목적지 표식입니다.")] public Transform selectedDestination;
        [Tooltip("이동력 소진 후 강조할 기존 턴 종료 버튼입니다.")] public RectTransform nextTurn;
        [Tooltip("두 번째 이동 안내에서 강조할 화면 하단 이동력 UI입니다. 머리 위 게이지보다 우선해서 사용합니다.")] public RectTransform movementGauge;
        [Tooltip("첫 이동 완료 후 하단 이동력 UI를 3회 강조하는 씬 연출입니다.")] public JcTutorialFocusPulse movementGaugePulse;
        [Tooltip("기존 퀘스트 본문입니다. 안내 목표와 수집 진행도를 표시합니다.")] public TMP_Text quest;
        [Tooltip("튜토리얼 보유량을 표시하는 기존 상단 자원 UI입니다.")] public ResourceHUDController resourceHud;
        [Tooltip("레벨 재생성 뒤 아이템 연결을 갱신할 기존 로더입니다.")] public LevelLoader levelLoader;
        [Tooltip("안내 패널과 UI 강조 표현입니다.")] public JcTutorialGuideView view;
        [Tooltip("이동·아이템·적 목표를 표시하는 장식입니다. 클릭 판정은 없습니다.")] public JcTutorialWorldMarker goal;
        [Header("설명 확인·시작 연출")]
        [Tooltip("설명 중 월드 입력을 막는 기존 Modal 방식의 배경입니다. 확인 시 해제됩니다.")] public GameObject explanationShield;
        [Tooltip("설명을 읽고 다음 안내로 넘기는 버튼입니다.")] public Button continueButton;
        [Tooltip("시작 안내 박스의 중앙 문구입니다. 우측 퀘스트와 같은 목표를 표시합니다.")] public TMP_Text introTitle;
        [Tooltip("첫 안내 박스입니다. 기존 문구를 하위에 배치하며 닫을 때 함께 숨깁니다.")] public RectTransform introBox;
        [Tooltip("첫 안내 박스를 향해 수렴하는 장식 테두리입니다. 클릭 판정은 갖지 않습니다.")] public JcTutorialGraphic[] introOutlines;
        [Min(.1f), Tooltip("첫 안내가 자동으로 닫히는 시간(초)입니다. 화면을 클릭하면 더 일찍 닫힙니다.")] public float introDuration = 2.5f;
        [Min(0), Tooltip("수렴 테두리가 박스 바깥에서 시작하는 여유(Canvas 단위)입니다. 0이면 크기 변화가 없습니다.")] public float introOutlineExpansion = 80;
        [Tooltip("이동불가 체험용 위치입니다. 실제로 도착해야 하는 이동 순서에는 추가하지 않습니다.")] public Transform blockedMoveTarget;
        [Min(0), Tooltip("이동불가 경로를 선택한 후 설명을 띄우기까지의 시간(초)입니다. 붉은 경로를 먼저 보여 줍니다.")] public float blockedPreviewDuration = .65f;
        [Tooltip("초기 이동 학습이 끝난 뒤 기존 턴 종료를 허용하는 담당자입니다.")] public TutorialTurnManager turnManager;
        [Min(.1f), Tooltip("시작 목표가 한 번 점멸하고 테두리가 수렴하는 시간(초)입니다. 표시 시간 / 이 값만큼 반복하며 기본값은 3회입니다.")] public float introPulsePeriod = 2.5f / 3;
        [Header("이동 안내")]
        [TextArea, Tooltip("목표를 선택하기 전에 표시하는 안내입니다.")] public string selectText = "빛나는 목표 위치를 눌러 이동 경로를 확인하세요.";
        [TextArea, Tooltip("목표 선택 후 표시하는 안내입니다.")] public string confirmText = "목표 위치를 한 번 더 누르면 이동합니다.";
        [TextArea, Tooltip("파티가 실제로 이동하는 동안 표시하는 안내입니다.")] public string movingText = "목표 위치로 이동 중입니다.";
        [TextArea, Tooltip("이동력이 0일 때 확인을 기다리는 설명입니다.")] public string exhaustedText = "이동력을 모두 사용하면 이번 턴에는 더 이동할 수 없습니다.";
        [Min(0), Tooltip("목표 선택 후 재클릭 안내가 나타나는 시간(초)입니다. 0이면 즉시 표시합니다.")] public float confirmDelay = 1f;

        private sealed class ItemTarget
        {
            public string key; public ResourceType type; public int amount; public Vector2Int cell;
        }
        private readonly List<ItemTarget> items = new List<ItemTarget>();
        private TutorialProgressRepository repository;
        private string explanationKey;
        private RectTransform explanationFocus;
        private float selectedSince = -1, introElapsed, lastTime = -1;
        private Vector2Int previousTarget;
        private RectTransform gauge;
        private float nextGaugeSearch;
        private bool showingIntro, releaseShieldPending, turnStateCaptured, originalTurnControl, originalTurnButton;
        private Button nextTurnButton;
        public int PresentationOpenedFrame { get; private set; } = -1;
        public string CurrentGuideStep { get; private set; }

        private void OnEnable() { LevelLoader.RuntimeLevelLoaded += OnLevelLoaded; }
        private void Start()
        {
            JcTutorialGuideView.BindWordWrapping(quest); JcTutorialGuideView.BindWordWrapping(introTitle);
            BindRepository(); CaptureItems();
            nextTurnButton = nextTurn != null ? nextTurn.GetComponent<Button>() : null;
            if (turnManager != null) originalTurnControl = turnManager.TurnControlEnabled;
            if (nextTurnButton != null) originalTurnButton = nextTurnButton.interactable;
            turnStateCaptured = true;
        }
        private void OnLevelLoaded(LevelLoader loader) { if (loader == levelLoader) CaptureItems(); }
        private void CaptureItems()
        {
            items.Clear();
            var keys = new HashSet<string>();
            foreach (var item in FindObjectsByType<TutorialItemObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item.gameObject.scene != gameObject.scene || !keys.Add(item.ItemKey)) continue;
                items.Add(new ItemTarget { key = item.ItemKey, type = item.ResourceType, amount = item.Amount, cell = item.GetCurrentGrid(grid) });
            }
        }
        private void BindRepository()
        {
            if (repository == TutorialProgressRepository.Instance && repository != null) return;
            if (repository != null) repository.ProgressChanged -= ObserveCombat;
            repository = TutorialProgressRepository.Instance;
            if (repository != null) repository.ProgressChanged += ObserveCombat;
        }
        // 전투 진입 기록만 관찰한다. 히어로톡 완료 플래그나 전투 호출을 대신하지 않는다.
        private void ObserveCombat()
        {
            if (repository.PendingCombatSourceType == TutorialCombatSourceType.Enemy) Hide();
        }
        private bool Seen(string key) => repository != null && repository.IsMessageSeen(Prefix + key);
        private void Mark(string key) { repository?.MarkMessageSeen(Prefix + key); }
        private void Update() => RefreshPresentation(Time.unscaledTime);

        public void RefreshPresentation(float time)
        {
            BindRepository();
            float delta = lastTime < 0 ? 0 : Mathf.Clamp(time - lastTime, 0, .1f); lastTime = time;
            if (view == null || goal == null || repository == null || grid == null || movement == null || parties == null) { Hide(); return; }
            ObserveCombat();
            if (repository.TutorialCompleted || Seen("first-combat") || repository.PendingCombatSourceType == TutorialCombatSourceType.Enemy || !movement.isActiveAndEnabled || !movement.ConstraintEnabled) { Hide(); return; }
            var party = parties.PlayerParty;
            if (party == null) { Hide(); return; }
            movement.SetGuidanceTarget(this, null);
            bool otherModal = ModalManager.HasAny && ModalManager.Top != explanationShield;
            if (otherModal || WorldInputGate.IsTurnResolving)
            {
                if (movementGaugePulse != null) movementGaugePulse.Suspend(true);
                view.SetVisible(false); SetContinue(false); HideMarker();
                SetIntroVisible(false);
                return;
            }
            if (movementGaugePulse != null) movementGaugePulse.Suspend(false);
            if (releaseShieldPending) return;
            bool learningMovement = repository.CurrentTurn == 1 && !Seen("exhausted") && !Seen("restored");
            SetTurnAvailable(!learningMovement);
            if (explanationKey != null)
            {
                CurrentGuideStep = explanationKey;
                SetShield(true); SetContinue(true); view.SetVisible(true, explanationFocus); HideMarker(); return;
            }
            bool hasTarget = movement.CurrentTargetCells.Count > 0;
            bool moving = party.IsMoving;
            if (!Seen("intro") && movement.CurrentOrder == 1 && repository.CurrentTurn == 1)
            {
                CurrentGuideStep = "intro";
                if (!showingIntro) PresentationOpenedFrame = Time.frameCount;
                showingIntro = true;
                SetQuest("목표 위치까지 이동하세요"); SetShield(true); SetContinue(false); view.SetVisible(false); HideMarker();
                introElapsed += delta; DrawIntro(introElapsed);
                if (introElapsed >= introDuration) DismissIntro();
                return;
            }
            EndIntro();
            if (moving) { ShowAction("moving", "탐사 · 이동", movingText); HideMarker(); return; }
            if (movement.CurrentOrder == 2 && !Seen("move-gauge-pulse") && movementGauge != null && movementGaugePulse != null)
            {
                movementGaugePulse.Begin(movementGauge);
                Mark("move-gauge-pulse");
            }
            // 두 번의 실제 이동 후 도착이 필요 없는 체험용 목표를 표시한다. 이동력·이동 순서는 변경하지 않는다.
            if (party.RemainingMovePoints <= 0)
            {
                if (!Seen("exhausted") && !hasTarget && blockedMoveTarget != null)
                {
                    SetQuest("다음 목표를 눌러 이동 가능한지 확인하세요");
                    var blockedCell = grid.WorldToGrid(blockedMoveTarget.position);
                    movement.SetGuidanceTarget(this, blockedCell);
                    bool selected = IsSelected(blockedCell, time);
                    if (!selected || time - selectedSince < blockedPreviewDuration)
                    {
                        ShowAction("blocked-preview", "탐사 · 이동 확인", selected
                            ? "붉은 경로는 이번 턴에 이동할 수 없는 경로입니다."
                            : "빛나는 목표 위치를 눌러 이동할 수 있는지 확인하세요.");
                        if (selected) HideMarker(); else ShowMarker(blockedCell, time);
                        return;
                    }
                }
                if (!Seen("exhausted"))
                {
                    SetQuest("이동불가 이유를 확인하세요");
                    Explain("exhausted", "이동력 소진", exhaustedText, FindGauge(party, time), time); return;
                }
                SetQuest("다음 턴을 시작하세요");
                ShowAction("end-turn", "다음 턴", "턴 종료 버튼을 눌러 이동력을 회복하세요.", nextTurn); HideMarker(); return;
            }
            if (Seen("exhausted") && !Seen("restored") && repository.CurrentTurn > 1)
            {
                // 회복 안내는 확인 클릭을 요구하지 않고 자원 수집 안내에 이어 표시한다.
                Mark("restored");
            }
            if (hasTarget)
            {
                SetQuest(movement.CurrentOrder == 1 ? "목표 위치까지 이동하세요" : "다음 목표 위치까지 이동하세요");
                var target = movement.CurrentTargetCells[0]; bool selected = IsSelected(target, time);
                string instruction = selected && time - selectedSince >= confirmDelay ? confirmText : selectText;
                if (movement.CurrentOrder > 1 && !selected)
                    instruction = "이동하면 이동력이 소모됩니다. 남은 이동력을 확인하고 다음 목표를 눌러 보세요.";
                ShowAction("move", "탐사 · 이동", instruction);
                ShowMarker(target, time); return;
            }
            int collected = 0; ItemTarget nearest = null; float distance = float.MaxValue;
            var cell = party.GetCurrentGrid();
            foreach (var item in items)
            {
                if (repository.IsItemCollected(item.key)) { collected++; continue; }
                float d = (item.cell - cell).sqrMagnitude;
                if (d < distance) { distance = d; nearest = item; }
            }
            SetQuest(nearest != null ? $"필드 자원을 수집하세요 ({collected}/{items.Count})" : "빌런에게 접근하세요");
            foreach (var item in items)
            {
                if (!repository.IsItemCollected(item.key) || Seen("resource-" + item.type)) continue;
                Explain("resource-" + item.type, ResourceName(item.type) + " 획득",
                    $"{ResourceName(item.type)} {item.amount}개를 획득했습니다. {ResourceUse(item.type)}\n보유량은 화면 상단에서 확인할 수 있습니다.",
                    resourceHud != null ? resourceHud.GetResourceAnchor(item.type) : null, time); return;
            }
            if (nearest != null)
            {
                if (!Seen("resources"))
                {
                    Explain("resources", "필드 자원", (Seen("restored") ? "새 턴이 시작되어 이동력이 회복되었습니다.\n" : "") + "필드의 아이템을 선택하고 한 번 더 누르면 이동합니다. 가까이 다가가면 자원을 획득할 수 있습니다.", null, time); return;
                }
                bool selected = IsSelected(nearest.cell, time);
                ShowAction("collect", "자원 수집", selected && time - selectedSince >= confirmDelay ? confirmText : $"표시된 {ResourceName(nearest.type)}을 수집하세요.");
                ShowMarker(nearest.cell, time); return;
            }
            var enemies = TutorialEnemyRegistry.Instance; TutorialEnemyObject enemy = null;
            if (enemies != null)
                foreach (var candidate in enemies.Enemies)
                    if (candidate != null && candidate.gameObject.activeInHierarchy && !repository.IsObjectInactive(candidate.ObjectKey)) { enemy = candidate; break; }
            if (enemy == null) { Hide(); return; }
            if (!Seen("enemy"))
            {
                Explain("enemy", "첫 전투", "빌런 주변의 붉은 영역에 도착하면 전투가 시작됩니다. 이동력을 확인하고 접근하세요.", null, time); return;
            }
            ShowAction("enemy-approach", "첫 전투", "표시된 빌런을 선택한 뒤 한 번 더 눌러 접근하세요.");
            ShowMarker(enemy.GetCurrentGrid(), time);
        }
        private void Explain(string key, string title, string text, RectTransform focus, float time)
        {
            explanationKey = key; explanationFocus = focus;
            PresentationOpenedFrame = Time.frameCount;
            view.blockPanelRaycasts = true;
            CurrentGuideStep = key; view.SetContent("탐사 · " + title, text);
            SetShield(true); SetContinue(true); view.SetVisible(true, focus); HideMarker();
        }
        public void ContinueExplanation()
        {
            if (ModalManager.Top != explanationShield || releaseShieldPending) return;
            if (showingIntro) { DismissIntro(); return; }
            if (explanationKey == null) return;
            Mark(explanationKey); explanationKey = null; explanationFocus = null;
            SetContinue(false); view.SetVisible(false); selectedSince = -1;
            releaseShieldPending = true;
        }
        private void DismissIntro()
        {
            Mark("intro"); showingIntro = false; SetIntroVisible(false);
            releaseShieldPending = true;
        }
        private void LateUpdate() => ReleaseDismissedInput(Input.GetMouseButton(0));
        private void ReleaseDismissedInput(bool pointerHeld)
        {
            // UI 클릭 처리와 월드 Update가 끝난 뒤 해제한다. 자동 종료 중 누르고 있으면 놓을 때까지 보호한다.
            if (!releaseShieldPending || pointerHeld) return;
            releaseShieldPending = false; SetShield(false);
        }
        private void ShowAction(string step, string title, string text, RectTransform focus = null)
        {
            CurrentGuideStep = step; SetShield(false); SetContinue(false);
            view.blockPanelRaycasts = false;
            view.SetContent(title, text); view.SetVisible(true, focus);
        }
        private void SetTurnAvailable(bool available)
        {
            if (!turnStateCaptured) return;
            if (turnManager != null) turnManager.TurnControlEnabled = available && originalTurnControl;
            if (nextTurnButton != null) nextTurnButton.interactable = available && originalTurnButton;
        }
        private bool IsSelected(Vector2Int cell, float time)
        {
            bool selected = selectedDestination != null && selectedDestination.gameObject.activeInHierarchy && grid.WorldToGrid(selectedDestination.position) == cell;
            if (!selected || previousTarget != cell) selectedSince = -1;
            if (selected && selectedSince < 0) selectedSince = time;
            previousTarget = cell; return selected;
        }
        private void SetQuest(string text) { if (quest != null && quest.text != text) quest.text = text; }
        private void SetShield(bool visible) { if (explanationShield != null && explanationShield.activeSelf != visible) explanationShield.SetActive(visible); }
        private void SetContinue(bool visible) { if (continueButton != null && continueButton.gameObject.activeSelf != visible) continueButton.gameObject.SetActive(visible); }
        private void HideMarker() { if (goal != null) goal.Present(false, Vector3.zero, 1, 0); }
        private void ShowMarker(Vector2Int cell, float time)
        { movement.SetGuidanceTarget(this, cell); Vector3 p = grid.GridToWorldCenter(cell); p.y = grid.GetCellSurfaceY(cell); goal.Present(true, p, grid.CellSize, time, !IsSelected(cell, time)); }
        private RectTransform FindGauge(PartyGridMover party, float time)
        {
            if (movementGauge != null && movementGauge.gameObject.activeInHierarchy) return movementGauge;
            if (gauge != null && gauge.gameObject.activeInHierarchy) return gauge;
            if (time < nextGaugeSearch) return null;
            nextGaugeSearch = time + 1;
            foreach (var g in FindObjectsByType<PartyMovePointWorldBillboardGauge>(FindObjectsSortMode.None)) if (g.TargetParty == party) { gauge = g.transform as RectTransform; return gauge; }
            foreach (var g in FindObjectsByType<PartyMovePointScreenGauge>(FindObjectsSortMode.None)) if (g.TargetParty == party) { gauge = g.transform as RectTransform; return gauge; }
            return null;
        }
        private void DrawIntro(float elapsed)
        {
            SetIntroVisible(true);
            float phase = elapsed / Mathf.Max(.1f, introPulsePeriod);
            float pulse = .5f - .5f * Mathf.Cos(phase * Mathf.PI * 2);
            if (introTitle != null)
            {
                introTitle.text = "목표 위치까지 이동하세요";
                introTitle.alpha = Mathf.Lerp(.65f, 1, pulse);
            }
            if (introOutlines == null) return;
            for (int i = 0; i < introOutlines.Length; i++)
            {
                var outline = introOutlines[i]; if (outline == null || introBox == null) continue;
                float progress = Mathf.Repeat(phase + i / (float)introOutlines.Length, 1);
                float remaining = 1 - Mathf.SmoothStep(0, 1, progress);
                outline.rectTransform.sizeDelta = introBox.sizeDelta + Vector2.one * (2 * introOutlineExpansion * remaining);
                var color = view.accent; color.a = Mathf.Sin(progress * Mathf.PI) * .65f; outline.color = color;
            }
        }
        private void SetIntroVisible(bool value)
        {
            if (introBox != null) introBox.gameObject.SetActive(value);
            if (introTitle != null) introTitle.gameObject.SetActive(value);
        }
        private void EndIntro() { showingIntro = false; SetIntroVisible(false); SetShield(false); }
        private void Hide()
        {
            if (movement != null) movement.ClearGuidanceTarget(this);
            if (movementGaugePulse != null) movementGaugePulse.Stop();
            releaseShieldPending = false; showingIntro = false;
            if (view != null) view.SetVisible(false);
            HideMarker(); SetContinue(false); SetShield(false); SetIntroVisible(false); SetTurnAvailable(true);
        }
        private void OnDisable()
        {
            LevelLoader.RuntimeLevelLoaded -= OnLevelLoaded;
            // 전투 시작 실패 시 pending이 해제된다. 실제 씬 이탈 때만 재진입 방지 기록을 남긴다.
            if (Application.isPlaying && repository != null && repository.PendingCombatSourceType == TutorialCombatSourceType.Enemy) Mark("first-combat");
            if (repository != null) repository.ProgressChanged -= ObserveCombat;
            repository = null; Hide();
        }
        public static string ResourceName(ResourceType type)
        { return type == ResourceType.Chip ? "히어로 메달" : type == ResourceType.Crystal ? "아티펙트 수정" : type == ResourceType.Supply ? "건설 자재" : "자금"; }
        private static string ResourceUse(ResourceType type)
        { return type == ResourceType.Chip ? "히어로 스킬 강화에 사용하는 자원입니다." : type == ResourceType.Crystal ? "히어로 코어 제작과 강화에 사용하는 자원입니다." : type == ResourceType.Supply ? "협회 시설의 건설과 승급에 사용하는 자원입니다." : "협회 운영과 다양한 강화에 사용하는 기본 자원입니다."; }
    }
}
