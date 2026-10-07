using System.Collections;
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
        private const string PublicityCompletedKey = "KJ.Tutorial.Publicity.AfterFirstVictory";
        [Header("홍보 이후 점령전·협회 안내")]
        public TutorialOutpostObject captureOutpost;
        [SerializeField, Tooltip("레벨 재생성 후 점령 대상을 다시 찾을 기존 건물 식별자입니다.")]
        private string captureOutpostKey = "tutorial_outpost:38_8";
        public Vector2Int captureEntryCell = new Vector2Int(38, 7);
        private bool showingAssociationEntry;
        [Header("홍보 이후 화면 이동 안내")]
        [TextArea, Tooltip("홍보를 마친 뒤 디밍된 설명 창에서 한 번 안내하는 카메라 이동 방법입니다.")]
        public string cameraScrollExplanation = "화면 밖에도 이동할 수 있는 목표가 있습니다.\nW A S D 키로 지도를 위·왼쪽·아래·오른쪽으로 움직일 수 있습니다.\n확인 후 D키를 눌러 오른쪽의 목표를 찾아보세요.";
        [TextArea, Tooltip("설명 확인 후 목표가 화면 안에 들어올 때까지 표시하는 실습 안내입니다. 이동력은 사용하지 않습니다.")]
        public string cameraScrollPractice = "D키를 누르고 있으면 지도가 오른쪽으로 이동합니다.\n빛나는 목표가 화면 안에 들어올 때까지 이동하세요.\n필요하면 W A S D 키로 화면 위치를 조절할 수 있습니다.";
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
        [Header("튜토리얼 카메라 구도")]
        [Tooltip("파티에 즉시 고정한 뒤 카메라를 이동할 화면 비율입니다. X 양수는 오른쪽, Y 음수는 아래쪽이며 배경과 목표는 반대 방향으로 보입니다. 0이면 이동하지 않습니다. 시작 안내가 닫힐 때와 크리스탈 수집 목표가 처음 표시될 때 각각 한 번 적용하며 일반 탐사에는 적용하지 않습니다.")]
        public Vector2 initialCameraViewOffset = new Vector2(.04f, -.06f);
        [Min(0), Tooltip("시작 안내 종료 및 크리스탈 수집 안내의 구도 조정에 걸리는 실제 시간(초)입니다. 0이면 즉시 적용합니다. 파티 추적·줌 설정은 유지합니다.")]
        public float initialCameraMoveDuration = .6f;
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
        private Coroutine initialCameraMove;
        private bool initialCameraMovePending;
        private bool externalPresentation;
        public int PresentationOpenedFrame { get; private set; } = -1;
        public string CurrentGuideStep { get; private set; }
        private bool awaitingTutorialTurn;
        private int guidedTurn;
        // 설명 차단막은 월드/스크롤 차단을 유지하고 턴 종료 버튼의 클릭만 통과시킨다.
        public RectTransform AllowedInputTarget => awaitingTutorialTurn && explanationKey == null &&
            !releaseShieldPending && !WorldInputGate.IsTurnResolving && turnManager != null &&
            !turnManager.IsTurnAdvancing && turnManager.CurrentTurn == guidedTurn ? nextTurn : null;
        public static bool BlocksSystemMenuEscape => ModalManager.Top != null &&
            ModalManager.Top.GetComponent<JcTutorialDismissSurface>() is JcTutorialDismissSurface surface &&
            surface.guide != null && surface.guide.isActiveAndEnabled && !surface.guide.externalPresentation;

        private void OnEnable() { LevelLoader.RuntimeLevelLoaded += OnLevelLoaded; }
        private void Start()
        {
            JcTutorialGuideView.BindWordWrapping(quest); JcTutorialGuideView.BindWordWrapping(introTitle);
            ApplyQuestShadow();
            InstallScrollGuards();
            BindRepository(); CaptureItems(); ResolveCaptureOutpost();
            nextTurnButton = nextTurn != null ? nextTurn.GetComponent<Button>() : null;
            if (turnManager != null) originalTurnControl = turnManager.TurnControlEnabled;
            if (nextTurnButton != null) originalTurnButton = nextTurnButton.interactable;
            turnStateCaptured = true;
            initialCameraMovePending = Application.isPlaying && repository != null && repository.CurrentTurn == 1 && !Seen("intro");
        }
        private IEnumerator AdjustCameraView(bool preserveCurrentView = false, string seenKey = null)
        {
            try
            {
                // 안내 박스가 사라지고 카메라 추적이 갱신된 뒤 현재 화면을 기준으로 보정한다.
                yield return null;
                yield return null;
                if (exiting || !isActiveAndEnabled || parties == null || parties.PlayerParty == null) yield break;
                var camera = Camera.main;
                var follower = camera != null ? camera.GetComponent<QuarterViewCameraFollower>() : null;
                if (follower == null || camera.gameObject.scene != gameObject.scene) yield break;
                var party = parties.PlayerParty;
                var plane = new Plane(Vector3.up, party.transform.position);
                var centerRay = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
                var shiftedRay = camera.ViewportPointToRay(new Vector3(.5f + initialCameraViewOffset.x, .5f + initialCameraViewOffset.y, 0));
                if (!plane.Raycast(centerRay, out float centerDistance) || !plane.Raycast(shiftedRay, out float shiftedDistance)) yield break;
                Vector3 offset = shiftedRay.GetPoint(shiftedDistance) - centerRay.GetPoint(centerDistance);
                offset.y = 0;
                Vector3 partyStart = party.transform.position;
                // 크리스탈 단계는 기존 팬을 유지한다. 파티 중심으로 되돌린 뒤 이동하지 않는다.
                Vector3 origin = preserveCurrentView ? follower.GetFocusWorldPosition() : partyStart;
                float elapsed = 0;
                while (!exiting && isActiveAndEnabled && party != null)
                {
                    float progress = initialCameraMoveDuration <= 0 ? 1 : Mathf.Clamp01(elapsed / initialCameraMoveDuration);
                    follower.FocusWorldPosition(origin + (party.transform.position - partyStart) + offset * Mathf.SmoothStep(0, 1, progress));
                    if (progress >= 1)
                    {
                        if (seenKey != null) Mark(seenKey);
                        break;
                    }
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            finally { initialCameraMove = null; }
        }
        private readonly List<(TMP_Text text, Material original, Material shadow)> questMaterials = new List<(TMP_Text, Material, Material)>();
        private void ApplyQuestShadow()
        {
            if (quest == null || questMaterials.Count > 0) return;
            // TMP 전용 Underlay를 인스턴스 머티리얼에만 적용한다. 공유 폰트/다른 씬은 변경하지 않는다.
            foreach (var text in quest.transform.parent.parent.GetComponentsInChildren<TMP_Text>(true))
            {
                var original = text.fontSharedMaterial;
                if (original == null || !original.HasProperty("_UnderlayColor")) continue;
                var shadow = new Material(original) { name = original.name + " (Tutorial Shadow)", hideFlags = HideFlags.HideAndDontSave };
                shadow.EnableKeyword("UNDERLAY_ON");
                shadow.SetColor("_UnderlayColor", new Color(.015f, .025f, .04f, .92f));
                shadow.SetFloat("_UnderlayOffsetX", 1); shadow.SetFloat("_UnderlayOffsetY", -1);
                shadow.SetFloat("_UnderlayDilate", .12f); shadow.SetFloat("_UnderlaySoftness", .16f);
                text.fontSharedMaterial = shadow; text.UpdateMeshPadding();
                questMaterials.Add((text, original, shadow));
            }
            foreach (var image in quest.transform.parent.parent.GetComponentsInChildren<Image>(true))
            {
                if (image.GetComponent<Shadow>() != null) continue;
                var shadow = image.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(.015f, .025f, .04f, .92f);
                shadow.effectDistance = new Vector2(2, -2); shadow.useGraphicAlpha = true;
                questImageShadows.Add(shadow);
            }
        }
        private readonly List<Shadow> questImageShadows = new List<Shadow>();
        private void InstallScrollGuards()
        {
            if (nextTurn != null) JcTutorialScrollGuard.Install(nextTurn, true);
            foreach (var image in FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (image.gameObject.scene == gameObject.scene && image.name == "MiniMap")
                    JcTutorialScrollGuard.Install(image.rectTransform);
        }
        public void PrepareForSceneExit()
        {
            exiting = true; Hide();
            // 진행 초기화로 시작 안내가 되살아나지 않도록 갱신을 중단하고 즉시 숨긴다.
            if (view != null) { view.SetPanelSuppressed(true); view.RenderPresentation(0, 1); }
            enabled = false;
        }
        private bool exiting;
        private void OnDestroy()
        {
            JcBuildingSilhouetteFeature.ClearTutorialHighlight(this);
            foreach (var entry in questMaterials)
            {
                if (entry.text != null && entry.text.fontSharedMaterial == entry.shadow) entry.text.fontSharedMaterial = entry.original;
                if (entry.shadow != null) { if (Application.isPlaying) Destroy(entry.shadow); else DestroyImmediate(entry.shadow); }
            }
            questMaterials.Clear();
            foreach (var shadow in questImageShadows) if (shadow != null) { if (Application.isPlaying) Destroy(shadow); else DestroyImmediate(shadow); }
            questImageShadows.Clear();
        }
        private void OnLevelLoaded(LevelLoader loader)
        {
            if (loader != levelLoader) return;
            // 레벨 로더가 기존 오브젝트를 폐기하므로 다음 갱신에서 새 인스턴스를 찾는다.
            captureOutpost = null;
            showingAssociationEntry = false;
            CaptureItems();
        }
        private void ResolveCaptureOutpost()
        {
            if (captureOutpost != null && captureOutpost.gameObject.scene == gameObject.scene)
            {
                captureOutpostKey = captureOutpost.ObjectKey;
                return;
            }
            captureOutpost = null;
            if (string.IsNullOrEmpty(captureOutpostKey)) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<TutorialOutpostObject>(true))
            {
                if (candidate.gameObject.scene != gameObject.scene || candidate.ObjectKey != captureOutpostKey) continue;
                captureOutpost = candidate;
                showingAssociationEntry = false;
                return;
            }
        }
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
            if (externalPresentation) return;
            if (exiting) { Hide(); return; }
            BindRepository();
            float delta = lastTime < 0 ? 0 : Mathf.Clamp(time - lastTime, 0, .1f); lastTime = time;
            if (view == null || goal == null || repository == null || grid == null || movement == null || parties == null) { Hide(); return; }
            ObserveCombat();
            if (repository.TutorialCompleted || repository.PendingCombatSourceType != TutorialCombatSourceType.None || !movement.isActiveAndEnabled || !movement.ConstraintEnabled) { Hide(); return; }
            var party = parties.PlayerParty;
            if (party == null) { Hide(); return; }
            movement.SetGuidanceTarget(this, null);
            JcBuildingSilhouetteFeature.ClearTutorialHighlight(this);
            bool otherModal = ModalManager.HasAny && ModalManager.Top != explanationShield;
            if (awaitingTutorialTurn && !otherModal)
            {
                if (WorldInputGate.IsTurnResolving || (turnManager != null && turnManager.IsTurnAdvancing)) return;
                if (repository.CurrentTurn > guidedTurn && party.RemainingMovePoints > 0)
                {
                    awaitingTutorialTurn = false; SetShield(false); view.SetVisible(false);
                }
            }
            if (otherModal || WorldInputGate.IsTurnResolving)
            {
                if (movementGaugePulse != null) movementGaugePulse.Suspend(true);
                view.SetVisible(false); SetContinue(false); HideMarker();
                SetIntroVisible(false);
                return;
            }
            if (movementGaugePulse != null) movementGaugePulse.Suspend(false);
            if (releaseShieldPending) return;
            if (Seen("first-combat")) { RefreshPostBattleGuidance(party, time); return; }
            bool learningMovement = repository.CurrentTurn == 1 && !Seen("exhausted") && !Seen("restored");
            SetTurnAvailable(!learningMovement);
            if (explanationKey != null)
            {
                CurrentGuideStep = explanationKey;
                SetShield(true); SetContinue(true); view.SetVisible(true, explanationFocus, ExplanationStyle(explanationKey)); HideMarker(); return;
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
            if (movement.CurrentOrder == 2 && !Seen("move-gauge-pulse") && movementGauge != null)
            {
                if (movementGaugePulse != null) movementGaugePulse.Stop();
                Explain("move-gauge-pulse", "이동력 확인", "이동하면 이동력이 소모됩니다.\n남은 이동력은 화면 아래 게이지에서 확인하세요.\n아무 곳이나 클릭하면 계속합니다.", movementGauge, time);
                return;
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
                // 체험용 이전 선택은 새 턴에 사용하지 않는다. 노란 목표보다 먼저 경로와 목적지를 지운다.
                foreach (var selection in FindObjectsByType<ClickSelectionController>(FindObjectsSortMode.None))
                    if (selection.gameObject.scene == gameObject.scene) selection.ClearMovePreview();
                selectedSince = -1;
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
                    ResourceBlock(item.type), time); return;
            }
            if (nearest != null)
            {
                if (!Seen("resources"))
                {
                    Explain("resources", "필드 자원", (Seen("restored") ? "새 턴이 시작되어 이동력이 회복되었습니다.\n" : "") + "필드의 아이템을 선택하고 한 번 더 누르면 이동합니다. 가까이 다가가면 자원을 획득할 수 있습니다.", null, time); return;
                }
                bool selected = IsSelected(nearest.cell, time);
                ShowAction("collect", "자원 수집", selected && time - selectedSince >= confirmDelay ? confirmText : $"표시된 {ResourceName(nearest.type)}을 수집하세요.");
                ShowMarker(nearest.cell, time);
                if (Application.isPlaying && nearest.type == ResourceType.Crystal && !Seen("crystal-camera-view") && initialCameraMove == null)
                    initialCameraMove = StartCoroutine(AdjustCameraView(true, "crystal-camera-view"));
                return;
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
        private void RefreshPostBattleGuidance(PartyGridMover party, float time)
        {
            ResolveCaptureOutpost();
            // 복귀 상태 복원과 홍보의 마지막 확인이 끝나기 전에는 이동을 허용하지 않는다.
            if (!repository.IsMessageSeen(PublicityCompletedKey) || captureOutpost == null)
            {
                view.SetVisible(false); HideMarker(); SetTurnAvailable(false); return;
            }
            bool claimed = repository.TryGetOutpostState(captureOutpost.ObjectKey, out var state) &&
                state == TutorialOutpostClaimState.HeroClaimed;
            if (claimed)
            {
                if (!showingAssociationEntry)
                {
                    foreach (var selection in FindObjectsByType<ClickSelectionController>(FindObjectsSortMode.None))
                        if (selection.gameObject.scene == gameObject.scene) selection.ClearMovePreview();
                    showingAssociationEntry = true;
                }
                if (!Seen("association-entry-explanation"))
                {
                    SetTurnAvailable(false);
                    movement.SetGuidanceTarget(this, null);
                    if (explanationKey != "association-entry-explanation")
                        Explain("association-entry-explanation", "협회 입장", "점령한 건물을 더블클릭하여 협회에 입장할 수 있습니다.\n이 안내를 닫은 뒤, 연녹색 외곽선으로 강조된 건물을 더블클릭하세요.", null, time);
                    return;
                }
                CurrentGuideStep = "association-entry";
                SetShield(false); SetContinue(false); view.SetVisible(false);
                JcBuildingSilhouetteFeature.SetTutorialHighlight(this, captureOutpost.transform, view.accent);
                SetQuest("점령한 건물을 더블클릭하세요");
                SetTurnAvailable(false);
                movement.SetGuidanceTarget(this, null);
                HideMarker();
                return;
            }
            showingAssociationEntry = false;
            if (!Seen("camera-scroll-explanation"))
            {
                SetTurnAvailable(false);
                SetQuest("W A S D 키로 화면을 이동하는 방법을 확인하세요");
                // 설명을 매 프레임 다시 열면 클릭을 연 프레임으로 취급하여 닫을 수 없다.
                if (explanationKey != "camera-scroll-explanation")
                    Explain("camera-scroll-explanation", "화면 이동", cameraScrollExplanation, null, time);
                return;
            }
            if (!Seen("camera-scroll-practice") && !party.IsMoving)
            {
                var camera = Camera.main;
                Vector3 target = grid.GridToWorldCenter(captureEntryCell);
                target.y = grid.GetCellSurfaceY(captureEntryCell);
                bool visible = camera != null && camera.gameObject.scene == gameObject.scene &&
                    IsCaptureTargetVisible(camera.WorldToViewportPoint(target));
                if (!visible)
                {
                    SetTurnAvailable(false);
                    SetQuest("D키로 오른쪽 화면을 이동하여 목표를 찾으세요");
                    ShowAction("camera-scroll-practice", "화면 이동 연습", cameraScrollPractice);
                    ShowMarker(captureEntryCell, time);
                    // 카메라는 기존 WASD 입력을 사용하고, 목표를 찾는 동안 월드 이동 선택만 막는다.
                    movement.SetGuidanceTarget(this, null);
                    return;
                }
                Mark("camera-scroll-practice");
            }
            SetTurnAvailable(true);
            if (party.IsMoving)
            {
                ShowAction("capture-moving", "점령전으로 이동", movingText); HideMarker(); return;
            }
            if (party.RemainingMovePoints <= 0)
            {
                SetQuest("턴을 종료하여 이동력을 회복하세요");
                ShowAction("end-turn", "다음 턴", "턴 종료 버튼을 눌러 이동력을 회복한 뒤\n목표 지점으로 계속 이동하세요.", nextTurn);
                HideMarker(); return;
            }
            SetQuest("표시된 전투 진입 지점으로 이동하세요");
            ShowAction("capture-approach", "건물 점령", "표시된 목표 지점을 선택한 뒤\n한 번 더 눌러 이동하세요.");
            ShowMarker(captureEntryCell, time);
        }
        // 우측 퀘스트·상하 HUD에서 떨어진 영역에 목표가 들어와야 클릭 안내로 전환한다.
        internal static bool IsCaptureTargetVisible(Vector3 viewport)
            => viewport.z > 0 && viewport.x >= .12f && viewport.x <= .78f &&
               viewport.y >= .24f && viewport.y <= .86f;

        private RectTransform ResourceBlock(ResourceType type)
        {
            // HUD 숫자가 아닌 아이콘·숫자를 포함한 기존 블록 Image의 경계를 사용한다.
            var label = resourceHud != null ? resourceHud.GetResourceAnchor(type) : null;
            if (label == null) return null;
            return label.parent is RectTransform block && block.GetComponent<Image>() != null ? block : label;
        }
        private static JcTutorialGuideView.FocusPresentation ExplanationStyle(string key)
        {
            if (key == "camera-scroll-explanation" || key == "association-entry-explanation") return JcTutorialGuideView.FocusPresentation.DimmedExplanation;
            if (key == "exhausted" || key == "move-gauge-pulse") return JcTutorialGuideView.FocusPresentation.Spotlight;
            return key != null && key.StartsWith("resource-") ? JcTutorialGuideView.FocusPresentation.ResourceBlock : JcTutorialGuideView.FocusPresentation.PointerAbove;
        }
        private void Explain(string key, string title, string text, RectTransform focus, float time)
        {
            explanationKey = key; explanationFocus = focus;
            PresentationOpenedFrame = Time.frameCount;
            view.blockPanelRaycasts = true;
            CurrentGuideStep = key; view.SetContent("탐사 · " + title, text);
            SetShield(true); SetContinue(true); view.SetVisible(true, focus, ExplanationStyle(key)); HideMarker();
        }
        public void ContinueExplanation()
        {
            if (externalPresentation) return;
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
            if (initialCameraMovePending && !exiting)
            {
                initialCameraMovePending = false;
                initialCameraMove = StartCoroutine(AdjustCameraView());
            }
        }
        private void LateUpdate() => ReleaseDismissedInput(Input.GetMouseButton(0));
        private void ReleaseDismissedInput(bool pointerHeld)
        {
            if (externalPresentation) return;
            // UI 클릭 처리와 월드 Update가 끝난 뒤 해제한다. 자동 종료 중 누르고 있으면 놓을 때까지 보호한다.
            if (!releaseShieldPending || pointerHeld) return;
            releaseShieldPending = false; SetShield(false);
        }
        private void ShowAction(string step, string title, string text, RectTransform focus = null)
        {
            CurrentGuideStep = step;
            bool turnGuidance = step == "end-turn";
            if (turnGuidance && !awaitingTutorialTurn) { awaitingTutorialTurn = true; guidedTurn = repository.CurrentTurn; }
            SetShield(turnGuidance); SetContinue(false);
            view.blockPanelRaycasts = false;
            view.SetContent(title, text); view.SetVisible(true, focus, turnGuidance ? JcTutorialGuideView.FocusPresentation.Spotlight : JcTutorialGuideView.FocusPresentation.PointerAbove);
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
        // 전투 복귀 후 기존 KJ 홍보 안내가 같은 메시지·차단 패널을 사용한다.
        public void SetExternalPresentation(bool active)
        {
            if (externalPresentation == active) return;
            Hide();
            externalPresentation = active;
        }
        private void SetContinue(bool visible) { if (continueButton != null && continueButton.gameObject.activeSelf != visible) continueButton.gameObject.SetActive(visible); }
        private void HideMarker() { if (goal != null) goal.Present(false, Vector3.zero, 1, 0); }
        private void ShowMarker(Vector2Int cell, float time)
        {
            movement.SetGuidanceTarget(this, cell);
            Vector3 p = grid.GridToWorldCenter(cell); p.y = grid.GetCellSurfaceY(cell);
            bool selected = IsSelected(cell, time);
            goal.Present(true, p, grid.CellSize, time, !selected, TargetTop(cell), selected ? null : GuidancePath(cell, time));
        }
        private AStarPathfinder guidePathfinder;
        private Vector2Int pathStart, pathGoal;
        private float nextPathRefresh;
        private readonly List<Vector3> guidePath = new List<Vector3>();
        private IReadOnlyList<Vector3> GuidancePath(Vector2Int target, float time)
        {
            var party = parties.PlayerParty;
            if (party == null) return null;
            var start = party.GetCurrentGrid();
            if (guidePath.Count > 0 && start == pathStart && target == pathGoal && time < nextPathRefresh) return guidePath;
            pathStart = start; pathGoal = target; nextPathRefresh = time + .25f; guidePath.Clear();
            if (guidePathfinder == null) guidePathfinder = FindFirstObjectByType<AStarPathfinder>();
            if (guidePathfinder == null) return null;
            var cells = guidePathfinder.FindPath(start, target, party.transform, maxVisitedNodes: 4096);
            if (cells == null && grid.HasInteractionTarget(target))
                cells = guidePathfinder.FindPathToAdjacent(start, target, party.transform, maxVisitedNodes: 4096);
            if (cells == null) return null;
            foreach (var cell in cells) { var point = grid.GridToWorldCenter(cell); point.y = grid.GetCellSurfaceY(cell); guidePath.Add(point); }
            // 상호작용 위치까지 장식 선을 연결한다. 실제 이동 판정은 기존 미리보기가 수행한다.
            if (cells.Count > 0 && cells[cells.Count-1] != target) { var end = grid.GridToWorldCenter(target); end.y = grid.GetCellSurfaceY(target); guidePath.Add(end); }
            return guidePath;
        }
        private float TargetTop(Vector2Int cell)
        {
            Component target = null;
            if (grid.TryGetGridItemObjectAtGrid(cell, out var item)) target = item as Component;
            if (target == null && grid.TryGetTutorialEnemyObjectAtGrid(cell, out var tutorialEnemy)) target = tutorialEnemy;
            if (target == null && grid.TryGetEnemyObjectAtGrid(cell, out var enemy)) target = enemy;
            if (target == null) return float.NegativeInfinity;
            float top = target.transform.position.y;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
                if (renderer.enabled) top = Mathf.Max(top, renderer.bounds.max.y);
            return top;
        }
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
        private void EndIntro() { if (!showingIntro) return; showingIntro = false; SetIntroVisible(false); SetShield(false); }
        private void Hide()
        {
            JcBuildingSilhouetteFeature.ClearTutorialHighlight(this);
            if (movement != null) movement.ClearGuidanceTarget(this);
            if (movementGaugePulse != null) movementGaugePulse.Stop();
            releaseShieldPending = false; showingIntro = false; awaitingTutorialTurn = false;
            if (view != null) view.SetVisible(false);
            HideMarker(); SetContinue(false); SetShield(false); SetIntroVisible(false); SetTurnAvailable(true);
        }
        private void OnDisable()
        {
            if (initialCameraMove != null) { StopCoroutine(initialCameraMove); initialCameraMove = null; }
            LevelLoader.RuntimeLevelLoaded -= OnLevelLoaded;
            // 전투 시작 실패 시 pending이 해제된다. 실제 씬 이탈 때만 재진입 방지 기록을 남긴다.
            if (Application.isPlaying && repository != null && repository.PendingCombatSourceType == TutorialCombatSourceType.Enemy) Mark("first-combat");
            if (repository != null) repository.ProgressChanged -= ObserveCombat;
            repository = null; Hide();
        }
        public static string ResourceName(ResourceType type)
        { return type == ResourceType.Chip ? "히어로 메달" : type == ResourceType.Crystal ? "크리스탈" : type == ResourceType.Supply ? "건설 자재" : "자금"; }
        private static string ResourceUse(ResourceType type)
        { return type == ResourceType.Chip ? "히어로 스킬 강화에 사용하는 자원입니다." : type == ResourceType.Crystal ? "히어로 코어 제작과 강화에 사용하는 자원입니다." : type == ResourceType.Supply ? "협회 시설의 건설과 승급에 사용하는 자원입니다." : "협회 운영과 다양한 강화에 사용하는 기본 자원입니다."; }
    }
}
