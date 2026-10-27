// [CHEAT_ONLY / 다른 기능 재사용 금지 — 2026-10-27 사용자 요청]
// 이 파일은 1구역 플레이테스트 진입 시간을 줄이는 치트 전용입니다.
// 런타임 진입점입니다. 정상 게임에서 이 컴포넌트를 호출하거나 씬/프리팹에 별도 부착하지 마세요.
// 의존 방향은 치트 → 기존 게임 API만 허용합니다. 일반 게임 코드는 이 폴더를 참조하지 마세요.
// 공통 기능이 필요하면 기존 정식 시스템을 사용하세요. 치트 구현을 공용 코드로 승격하거나 복사하지 마세요.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace JC.DebugTools
{
    [DefaultExecutionOrder(-200)]
    public sealed partial class JcZone1WarpCheat : MonoBehaviour
    {
        [Tooltip("Shift를 누른 채 Q를 세 번 누를 수 있는 제한 시간입니다.")]
        [SerializeField, Min(.1f)] float shortcutSeconds = 1f;
        static JcZone1WarpCheat instance;
        int taps, sceneHandle;
        int plannedZoneEnemyLevel;
        float firstTap;
        bool active, choosingCell, applying;
        PartyGridMover party;
        PartyPersistentData partyData;
        GridManager grid;
        LevelData level;
        Vector2Int anchor, originalGrid;
        RectInt zoneBounds;
        Camera viewCamera;
        QuarterViewCameraFollower follower;
        bool followerEnabled, conditionEnabled;
        DHWorldEventConditionRuntimeManager conditions;
        readonly List<JcZone1WarpRoute> selected = new List<JcZone1WarpRoute>();
        readonly List<JcZone1WarpStep> steps = new List<JcZone1WarpStep>();
        readonly Dictionary<string, bool> flags = new Dictionary<string, bool>(StringComparer.Ordinal);
        readonly Dictionary<string, bool> originalFlags = new Dictionary<string, bool>(StringComparer.Ordinal);
        readonly List<UIInputBlocker> uiLocks = new List<UIInputBlocker>();
        JcZone1WarpView ui;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        // 미사용 경로: 전용 감시 객체만 생성합니다. 여기서 게임 singleton 초기화·이벤트 구독·UI/카메라/저장 변경을 추가하지 마세요.
        static void Bootstrap()
        {
            if (instance != null) return;
            var root = new GameObject("[JC_Zone1WarpCheat]");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<JcZone1WarpCheat>();
        }

        void Awake() { instance = this; }
        void Update()
        {
            if (active)
            {
                if (SceneManager.GetActiveScene().handle != sceneHandle || party == null || ui == null || !ui.gameObject.activeInHierarchy)
                { Close(false); return; }
                if (Input.GetKeyDown(KeyCode.Escape) && !applying) { Close(false); return; }
                if (choosingCell) UpdateHover();
                return;
            }
            // 미발동 상태에서는 읽기 전용 키 조회와 치트 내부 카운터만 사용합니다. Input.ResetInputAxes 등 입력 소비/변경 금지.
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) { taps = 0; return; }
            if (!Input.GetKeyDown(KeyCode.Q)) return;
            var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (focused != null && (focused.GetComponent<TMPro.TMP_InputField>() != null || focused.GetComponent<UnityEngine.UI.InputField>() != null))
            { taps = 0; return; }
            float now = Time.unscaledTime;
            if (taps == 0 || now - firstTap > shortcutSeconds) { taps = 0; firstTap = now; }
            if (++taps < 3) return;
            taps = 0;
            try { Open(); }
            catch (Exception e) { Debug.LogWarning($"[JC 구간 테스트] {e.Message}"); Close(false); }
        }

        // Shift+Q 3회가 충족된 뒤에만 호출합니다. 일반 탐험 초기화나 다른 기능에서 호출하지 마세요.
        void Open()
        {
            if (ModalManager.HasAny || WorldInputGate.IsTurnResolving || SceneFadeController.HideCursorDuringTransition || DHGameEndState.IsEnding)
                throw new InvalidOperationException("탐험 중 다른 창을 닫고 대기 상태에서 사용해 주세요.");
            if (ChatManager.Instance != null && ChatManager.Instance.IsRunning ||
                DHWorldEventRuntimeManager.Instance != null && DHWorldEventRuntimeManager.Instance.IsRunning ||
                JC.Environment.JcDayNightController.IsTransitioning || ZoneEntryGuidanceController.IsActiveOrStoredActive)
                throw new InvalidOperationException("이벤트 또는 구역 진입 안내 중에는 사용할 수 없습니다.");
            var turns = FindFirstObjectByType<TurnManager>();
            var combat = FindFirstObjectByType<CombatEncounterManager>();
            var prompt = FindFirstObjectByType<CombatPromptService>();
            party = FindFirstObjectByType<PartyRegistry>()?.PlayerParty;
            grid = FindFirstObjectByType<GridManager>();
            if (turns == null || !turns.IsPlayerTurn || combat != null && combat.IsCombatActive || prompt != null && prompt.IsOpen ||
                party == null || grid == null || party.IsMoving || party.GetComponent<PartyRuntime>()?.IsInputLocked == true)
                throw new InvalidOperationException("플레이어 파티가 탐험 중 대기 상태일 때 사용할 수 있습니다.");
            if (DefeatedPartyReturnController.IsPartyWaiting(party)) throw new InvalidOperationException("파티 복귀 처리 중입니다.");
            if (EventScriptCatalog.Instance == null || DHEventStateRepository.Instance == null ||
                MapProgressRepository.Instance == null || PersistentUnitRepository.Instance == null || PartyPersistentRepository.Instance == null)
                throw new InvalidOperationException("진행 데이터가 준비되지 않았습니다.");
            if (DHEventStateRepository.Instance.GetFlag("Flag_MainQuestBoss1Complete"))
                throw new InvalidOperationException("이 치트는 1구역 점령전 완료 전에만 사용할 수 있습니다.");
            ResolveZone();
            if (!zoneBounds.Contains(party.GetCurrentGrid())) throw new InvalidOperationException("1구역 안에서만 사용할 수 있습니다.");
            var id = party.GetComponent<PartyIdentity>();
            if (id == null || !PartyPersistentRepository.Instance.TryGetParty(id.PartyId, out partyData) || partyData.UnitIndices.Count == 0)
                throw new InvalidOperationException("전투 파티 데이터를 찾을 수 없습니다.");
            foreach (int unit in partyData.UnitIndices)
                if (!PersistentUnitRepository.Instance.TryGetUnit(unit, out var data) || data == null || data.IsIncapacitated || data.CurrentHp <= 0)
                    throw new InvalidOperationException("파티 전원을 회복시킨 후 사용해 주세요. 생략 전투는 전원 생존 승리 기준입니다.");
            viewCamera = Camera.main;
            if (viewCamera == null) throw new InvalidOperationException("탐사 카메라를 찾을 수 없습니다.");
            follower = viewCamera.GetComponent<QuarterViewCameraFollower>();
            if (follower != null && (follower.IsExternalInputLocked || follower.IsEventZoomApplied || follower.IsEventFocusMoving))
                throw new InvalidOperationException("카메라 연출이 끝난 후 사용해 주세요.");
            selected.Clear(); steps.Clear(); originalFlags.Clear();
            foreach (var f in DHEventStateRepository.Instance.Flags) originalFlags[f.flagName] = f.value;
            originalGrid = party.GetCurrentGrid();
            sceneHandle = SceneManager.GetActiveScene().handle;
            active = true;
            conditions = DHWorldEventConditionRuntimeManager.Instance;
            if (conditions != null) { conditionEnabled = conditions.ConditionEventsEnabled; conditions.ConditionEventsEnabled = false; }
            foreach (var blocker in FindObjectsByType<UIInputBlocker>(FindObjectsSortMode.None))
            { blocker.SetAdditionalLock(this, true); uiLocks.Add(blocker); }
            ClickSelectionController.Active?.ClearMovePreview();
            CaptureCamera();
            ui = JcZone1WarpView.Create(transform, CloseFromUi, OnCellClick);
            ModalManager.Register(ui.gameObject);
            RebuildPlan();
            ShowChoices();
        }

        void ResolveZone()
        {
            var layout = FindFirstObjectByType<LevelZoneLayoutLoader>();
            if (layout == null) throw new InvalidOperationException("1구역 레이아웃을 찾을 수 없습니다.");
            foreach (var zone in layout.LoadedZones)
            {
                if (EnemySpawnPoint.ResolveZoneNumber(zone.ZoneId) != 1) continue;
                level = zone.LevelData; anchor = zone.Anchor; zoneBounds = new RectInt(zone.Anchor, zone.Size);
                if (level != null) return;
            }
            throw new InvalidOperationException("로드된 1구역 데이터가 없습니다.");
        }

        void ShowChoices()
        {
            int stage = JcZone1WarpRoute.NextStage(Flag);
            if (stage == 0) { BeginDestination(); return; }
            var options = new List<JcZone1WarpRoute>();
            foreach (var route in JcZone1WarpRoute.Routes)
                if (route.Stage == stage && !MapProgressRepository.Instance.IsMainEventCompleted(route.EventKey) && FindEvent(route.EventKey) != null)
                    options.Add(route);
            ui.ShowChoices($"1구역 구간 테스트 · 분기 {stage}", BuildSummary(), options, route =>
            {
                try { selected.Add(route); RebuildPlan(); ShowChoices(); }
                catch (Exception e) { selected.Remove(route); RebuildPlan(); ui.SetStatus(e.Message); }
            }, BeginDestination);
        }

        void BeginDestination()
        {
            try
            {
                RebuildPlan(); BuildCells();
                if (safeCells.Count == 0) throw new InvalidOperationException("안전하게 이동할 수 있는 셀이 없습니다.");
                choosingCell = true; ShowOverview();
                ui.ShowMap(BuildSummary(), () => { choosingCell = false; ClearCells(); RestoreCamera(false); ShowChoicesWithoutAutoAdvance(); });
            }
            catch (Exception e) { ui.SetStatus(e.Message); }
        }

        void ShowChoicesWithoutAutoAdvance()
        {
            selected.Clear(); RebuildPlan();
            if (JcZone1WarpRoute.NextStage(Flag) == 0) BeginDestination();
            else ShowChoices();
        }

        void OnCellClick(Vector2 position)
        {
            if (!active || !choosingCell || applying || !TryPickCell(position, out var cell)) return;
            try
            {
                ValidateUnchanged(); RebuildPlan();
                if (!IsSafe(cell)) { ui.SetStatus("이 셀에서는 전투·이벤트가 발생하거나 이동할 수 없습니다."); return; }
                applying = true;
                ApplyPlan();
                // 선택 시점부터 반대 분기 대체 적의 1셀 조우 범위를 예약하므로 적용 후에도 착지가 안전합니다.
                if (!IsSafe(cell)) throw new InvalidOperationException("맵 상태가 변경되었습니다. 진행은 적용되었고 이동은 취소했습니다.");
                party.SnapToGridPosition(cell, false);
                PartyRepositorySync.ApplyUnitsToScene(partyData.UnitIndices);
                // 기존 저장은 턴 시작 스냅샷을 사용합니다. 치트 결과를 새 테스트 재개점으로 함께 캡처합니다.
                // 치트로 만든 테스트 재개점에만 허용된 저장 기준 변경입니다. 일반 이동/전투의 저장 타이밍으로 재사용하지 마세요.
                DHTurnStartSnapshotStore.CaptureTurnStartSnapshot();
                Debug.Log($"[JC 구간 테스트] {steps.Count}개 진행 단계 적용, 파티 이동 {cell}. 레벨·스킬·IP 동기화 완료.");
                StartCoroutine(CloseAfterClick(true));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (applying) StartCoroutine(CloseAfterClick(false));
                else ui.SetStatus(e.Message);
            }
        }

        void CloseFromUi() { if (!applying) { applying = true; StartCoroutine(CloseAfterClick(false)); } }
        IEnumerator CloseAfterClick(bool moved)
        {
            // 목적지/취소 클릭이 같은 프레임의 기존 월드 입력으로 전달되지 않도록 모달 잠금을 유지합니다.
            yield return null;
            Close(moved);
        }
        void Close(bool moved)
        {
            // 치트를 쓰지 않았다면 종료/비활성화 때도 기존 게임 상태를 복원한다는 명목으로 변경하지 않습니다.
            if (!active) return;
            active = false; choosingCell = false; applying = false;
            if (ui != null) { ModalManager.Unregister(ui.gameObject); Destroy(ui.gameObject); ui = null; }
            ClearCells(); RestoreCamera(moved);
            foreach (var blocker in uiLocks) if (blocker != null) blocker.SetAdditionalLock(this, false);
            uiLocks.Clear();
            if (conditions != null) conditions.ConditionEventsEnabled = conditionEnabled;
            selected.Clear(); steps.Clear();
        }
        void OnDisable() { Close(false); }
        void OnDestroy() { Close(false); if (instance == this) instance = null; }
    }
}
