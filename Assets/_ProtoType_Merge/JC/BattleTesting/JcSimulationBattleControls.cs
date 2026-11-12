using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ASB.Work.BattleGrid;

namespace JC.BattleTesting
{
    // JC | 2026-11-10 | BattleSimulationScene에서 시작한 전투에만 임시 편집 UI를 붙입니다. 본게임·튜토리얼·보스 테스트 설정은 보존합니다.
    [DisallowMultipleComponent]
    public sealed class JcSimulationBattleControls : MonoBehaviour
    {
        BattleFlowManager flow;
        Canvas canvas;
        RectTransform modal;
        Button editButton, targetButton;
        Toggle repeatToggle, draftToggle;
        TMP_Text status, message;
        Button[] cellButtons;
        JcSimulationFormation formation;
        int selected = -1;
        BattleCharactor repeatUnit, draftRepeatUnit;
        Predicate<BattleCharactor> previousFilter, ownedFilter;
        IDisposable editLock;
        TMP_FontAsset font;
        // JC | 2026-11-10 | 적 생성도 확인 전까지 초안에만 보관하며 이동 선택과 빈 칸 추가를 구분합니다.
        readonly Dictionary<int, string> pendingEnemies = new Dictionary<int, string>();
        RectTransform enemyPicker;
        EnemySpawner enemySpawner;
        List<EnemyData> enemyChoices;
        int enemyPage, enemySlot;
        readonly Color normal = new Color(.15f, .22f, .3f, 1);
        readonly Color accent = new Color(.65f, .48f, .08f, 1);

        public static bool IsSimulationSession => CombatContext.Instance && CombatContext.Instance.IsSimulation &&
            (CombatContext.Instance.ReturnSceneName == SimulationBattleLauncher.ReturnSceneName ||
             JcStandaloneBattleTest.IsTestScene(SceneManager.GetActiveScene()));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToScene(scene);
        static void Attach()
        {
            if (!IsSimulationSession) return;
            // JC | 2026-11-12 | 기존 시뮬레이션과 명시된 JC 전용 테스트씬에서만 공통 편집기를 설치합니다.
            // JC | 2026-11-12 | 페이드의 추가 로딩 중 아직 활성 씬이 바뀌지 않은 기존 시뮬레이션도 지원합니다.
            for (int i = 0; i < SceneManager.sceneCount; i++) AttachToScene(SceneManager.GetSceneAt(i));
        }
        public static void AttachToScene(Scene scene)
        {
            if (!IsSimulationSession || (scene.name != SimulationBattleLauncher.BattleSceneName && !JcStandaloneBattleTest.IsTestScene(scene))) return;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var manager in root.GetComponentsInChildren<BattleFlowManager>())
                if (!manager.GetComponent<JcSimulationBattleControls>()) manager.gameObject.AddComponent<JcSimulationBattleControls>();
        }

        void Start()
        {
            flow = GetComponent<BattleFlowManager>();
            if (!flow || !IsSimulationSession) { enabled = false; return; }
            // JC | 2026-11-10 | 오브젝트 검색 순서에 따라 영문 숫자용 폰트를 고르지 않도록 전투 UI의 한글 폰트를 명시합니다.
            font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == "netmarbleB SDF");
            if (!font) { Debug.LogError("[JC Simulation] 전투 UI의 netmarbleB SDF 폰트를 찾을 수 없습니다.", this); enabled = false; return; }
            BuildUi();
        }

        void Update()
        {
            if (!canvas || !flow) return;
            bool active = IsSimulationSession && !flow.IsBattleEnded && !flow.IsEndingBattle;
            canvas.gameObject.SetActive(active);
            if (!active) { Close(); SetRepeat(null); return; }
            if (ownedFilter != null && (!CanRepeat(repeatUnit) || flow.SimulationTurnFilter != ownedFilter)) SetRepeat(null);
            editButton.interactable = CanOpen();
            repeatToggle.interactable = modal == null && (ownedFilter != null || CanRepeat(repeatUnit) || CanRepeat(flow.CurrentUnit));
            repeatToggle.SetIsOnWithoutNotify(ownedFilter != null);
            status.text = ownedFilter != null ? $"반복 대상: {repeatUnit.UnitName} · 다음 턴부터" : "시뮬레이션 · 진형 편집 / 반복턴";
            if (modal && (flow.IsActionInProgress || flow.IsTurnPresentationPending)) Close();
        }

        bool CanOpen() => modal == null && flow.CurrentUnit && !flow.IsActionInProgress && !flow.IsTurnPresentationPending &&
            !flow.IsFlowBlocked && !flow.IsEndingBattle && !flow.IsBattleEnded;
        bool CanRepeat(BattleCharactor unit) => unit && !unit.IsDead && flow.Participants.Contains(unit);

        void BuildUi()
        {
            var root = new GameObject("JC_SimulationControls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var toolbar = Panel("시뮬레이션 도구", root.transform, new Vector2(470, 104), new Color(.04f, .08f, .12f, .96f));
            toolbar.anchorMin = toolbar.anchorMax = toolbar.pivot = Vector2.one;
            toolbar.anchoredPosition = new Vector2(-225, -18);
            editButton = MakeButton(toolbar, "진형 변경", new Vector2(-130, 15), new Vector2(180, 44), Open);
            repeatToggle = MakeToggle(toolbar, "한 명 반복", new Vector2(105, 15), on => SetRepeat(on ? (CanRepeat(repeatUnit) ? repeatUnit : flow.CurrentUnit) : null));
            status = Label(toolbar, "", new Vector2(0, -30), new Vector2(450, 32), 19);
        }

        void Open()
        {
            if (!CanOpen()) return;
            var grid = BattleGridManager.Instance;
            if (!grid) return;
            formation = new JcSimulationFormation(grid.AllCells, flow.Participants);
            pendingEnemies.Clear();
            enemySpawner = FindFirstObjectByType<EnemySpawner>();
            editLock = flow.AcquireFlowLock(this, "시뮬레이션 진형 편집");
            foreach (var input in FindObjectsByType<InputHandler>(FindObjectsSortMode.None)) input.ClearSelectionState();
            modal = Panel("진형 변경", canvas.transform, Vector2.zero, new Color(0, 0, 0, .72f));
            modal.anchorMin = Vector2.zero;
            modal.anchorMax = Vector2.one;
            modal.offsetMin = modal.offsetMax = Vector2.zero;
            var body = Panel("편집창", modal, new Vector2(1060, 740), new Color(.055f, .08f, .12f, 1));
            Label(body, "진형 변경", new Vector2(0, 320), new Vector2(960, 56), 32);
            Label(body, "캐릭터 → 목적지: 이동·교환  /  빈 적 칸: 적 추가", new Vector2(0, 262), new Vector2(1000, 44), 23);
            // JC | 2026-11-10 | 각 열의 제목을 실제 칸 중심에 맞춰 전열·후열을 명확하게 구분합니다.
            string[] headings = { "아군 · 후열", "아군 · 전열", "적군 · 전열", "적군 · 후열" };
            for (int column = 0; column < 4; column++)
                Label(body, headings[column], new Vector2(-366 + column * 238 + (column >= 2 ? 18 : 0), 206), new Vector2(220, 42), 24);
            selected = -1;
            cellButtons = new Button[formation.Cells.Length];
            for (int i = 0; i < cellButtons.Length; i++)
            {
                int index = i;
                var coords = formation.Cells[i].Coords;
                float x = -366 + coords.x * 238 + (coords.x >= 2 ? 18 : 0);
                cellButtons[i] = MakeButton(body, "", new Vector2(x, 132 - coords.y * 90), new Vector2(220, 76), () => SelectCell(index));
            }
            draftRepeatUnit = CanRepeat(repeatUnit) ? repeatUnit : flow.CurrentUnit;
            draftToggle = MakeToggle(body, "한 명 반복", new Vector2(-280, -175), _ => { });
            draftToggle.SetIsOnWithoutNotify(ownedFilter != null);
            targetButton = MakeButton(body, "", new Vector2(175, -175), new Vector2(540, 52), CycleTarget);
            message = Label(body, "확인 시 즉시 재배치 · 반복턴은 현재 턴 종료 후 적용", new Vector2(0, -242), new Vector2(1000, 65), 22);
            MakeButton(body, "취소", new Vector2(-140, -310), new Vector2(220, 55), Close);
            MakeButton(body, "확인 · 적용", new Vector2(140, -310), new Vector2(220, 55), Confirm);
            RefreshDraft();
        }

        void SelectCell(int index)
        {
            if (formation.IsFixed(index)) return;
            if (selected < 0)
            {
                if (formation.Draft[index]) selected = index;
                else if (formation.Cells[index].Coords.x >= 2) OpenEnemyPicker(index);
            }
            else if (selected == index) selected = -1;
            else if (pendingEnemies.ContainsKey(index)) message.text = "추가 예정인 적을 먼저 취소해 주세요.";
            else if (formation.TrySwap(selected, index)) selected = -1;
            else { message.text = "같은 진영의 칸을 선택해 주세요."; }
            RefreshDraft();
        }
        void CycleTarget()
        {
            var candidates = flow.Participants.Where(CanRepeat).ToArray();
            if (candidates.Length == 0) return;
            draftRepeatUnit = candidates[(Array.IndexOf(candidates, draftRepeatUnit) + 1) % candidates.Length];
            RefreshDraft();
        }
        void RefreshDraft()
        {
            for (int i = 0; i < cellButtons.Length; i++)
            {
                var unit = formation.Draft[i];
                string label = unit ? unit.UnitName : "빈 칸";
                if (pendingEnemies.TryGetValue(i, out string key) && DHCsvTemplateCatalog.Instance.TryGetEnemyTemplate(key, out var template))
                    label = template.Name + "\n추가 예정";
                cellButtons[i].GetComponentInChildren<TMP_Text>().text = label + (formation.IsFixed(i) ? "\n변경 불가" : "");
                cellButtons[i].interactable = !formation.IsFixed(i);
                cellButtons[i].image.color = i == selected ? accent : normal;
            }
            targetButton.GetComponentInChildren<TMP_Text>().text = "반복 대상: " + (draftRepeatUnit ? draftRepeatUnit.UnitName : "없음") + "  ›";
        }
        void Confirm()
        {
            if (flow.IsActionInProgress || flow.IsEndingBattle || flow.IsBattleEnded || flow.IsTurnPresentationPending) { Close(); return; }
            if (draftToggle.isOn && !CanRepeat(draftRepeatUnit)) { message.text = "살아 있는 반복 대상을 선택해 주세요."; return; }
            // JC | 2026-11-10 | 기존 이동과 신규 적 생성을 한 확인 동작으로 검증합니다. 반복 필터와 현재 턴은 유지합니다.
            string error;
            bool applied = pendingEnemies.Count == 0 ? formation.TryApply(flow.Participants, out error) :
                JcSimulationEnemyPlacement.TryApply(formation, pendingEnemies, flow, enemySpawner, out error);
            if (!applied) { message.text = error; return; }
            foreach (var spawner in FindObjectsByType<PlayerSpawner>(FindObjectsSortMode.None)) spawner.RefreshSimulationFormation();
            foreach (var spawner in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) spawner.RefreshSimulationFormation();
            SetRepeat(draftToggle.isOn ? draftRepeatUnit : null);
            if (CanRepeat(draftRepeatUnit)) repeatUnit = draftRepeatUnit;
            Close();
            RefreshTurnPreview();
        }

        void SetRepeat(BattleCharactor unit)
        {
            if (!flow) return;
            if (ownedFilter != null && flow.SimulationTurnFilter == ownedFilter) flow.SimulationTurnFilter = previousFilter;
            ownedFilter = null;
            previousFilter = null;
            if (CanRepeat(unit))
            {
                repeatUnit = unit;
                previousFilter = flow.SimulationTurnFilter;
                // JC | 2026-11-10 | 사망 프레임에도 빈 턴 큐로 종료되지 않게 원래 필터로 즉시 돌아갑니다.
                ownedFilter = candidate => CanRepeat(repeatUnit) ? candidate == repeatUnit : previousFilter == null || previousFilter(candidate);
                flow.SimulationTurnFilter = ownedFilter;
            }
            RefreshTurnPreview();
        }
        void RefreshTurnPreview()
        {
            foreach (var ui in FindObjectsByType<TurnOrderUI>(FindObjectsSortMode.None)) ui.RefreshSimulationPreview();
        }
        void Close()
        {
            CloseEnemyPicker();
            pendingEnemies.Clear();
            if (modal) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); }
            modal = null;
            formation = null;
            editLock?.Dispose();
            editLock = null;
        }

        // JC | 2026-11-10 | 기존 카탈로그의 적을 페이지별로 표시합니다. 시체/인질은 빈 칸으로 취급하지 않습니다.
        void OpenEnemyPicker(int index)
        {
            if (!enemySpawner || !DHCsvTemplateCatalog.Instance) { message.text = "적 데이터를 준비하지 못했습니다."; return; }
            enemySlot = index;
            enemyPage = 0;
            enemyChoices = DHCsvTemplateCatalog.Instance.GetAllEnemyTemplates().Where(t =>
                JcSimulationEnemyPlacement.IsAllowed(t.Index) &&
                EnemySpawnPlanBuilder.TryBuildUnitEntry(t.Index, CombatContext.Instance.EnemyLevel, 0, out var entry, out _) &&
                enemySpawner.CanResolveEntry(entry)).ToList();
            DrawEnemyPicker();
        }
        void CloseEnemyPicker()
        {
            if (enemyPicker) { enemyPicker.gameObject.SetActive(false); Destroy(enemyPicker.gameObject); }
            enemyPicker = null;
        }
        void DrawEnemyPicker()
        {
            CloseEnemyPicker();
            enemyPicker = Panel("적 선택 차단막", modal, Vector2.zero, new Color(0, 0, 0, .7f));
            enemyPicker.anchorMin = Vector2.zero; enemyPicker.anchorMax = Vector2.one;
            enemyPicker.offsetMin = enemyPicker.offsetMax = Vector2.zero;
            var body = Panel("추가할 적 선택", enemyPicker, new Vector2(1000, 650), new Color(.055f, .08f, .12f, 1));
            Label(body, "추가할 적 선택 · 율리아 제외", new Vector2(0, 272), new Vector2(950, 50), 30);
            Label(body, "레벨 " + CombatContext.Instance.EnemyLevel + " · 최종 확인 시 배치 / 다음 라운드부터 참여", new Vector2(0, 220), new Vector2(950, 44), 22);
            for (int i = enemyPage * 8; i < Mathf.Min(enemyChoices.Count, (enemyPage + 1) * 8); i++)
            {
                var choice = enemyChoices[i];
                int local = i - enemyPage * 8;
                MakeButton(body, choice.Name + "  (" + choice.Index + ")", new Vector2(local % 2 == 0 ? -240 : 240, 143 - local / 2 * 82),
                    new Vector2(460, 68), () => { pendingEnemies[enemySlot] = choice.Index; CloseEnemyPicker(); RefreshDraft(); });
            }
            int pages = Mathf.Max(1, Mathf.CeilToInt(enemyChoices.Count / 8f));
            Label(body, (enemyPage + 1) + " / " + pages, new Vector2(0, -178), new Vector2(160, 40), 22);
            MakeButton(body, "이전", new Vector2(-225, -178), new Vector2(175, 45), () => { enemyPage--; DrawEnemyPicker(); }).interactable = enemyPage > 0;
            MakeButton(body, "다음", new Vector2(225, -178), new Vector2(175, 45), () => { enemyPage++; DrawEnemyPicker(); }).interactable = enemyPage + 1 < pages;
            MakeButton(body, "추가 취소 · 빈 칸", new Vector2(-240, -260), new Vector2(350, 54), () => { pendingEnemies.Remove(enemySlot); CloseEnemyPicker(); RefreshDraft(); });
            MakeButton(body, "돌아가기", new Vector2(240, -260), new Vector2(350, 54), CloseEnemyPicker);
        }
        void OnDisable()
        {
            Close();
            SetRepeat(null);
            if (canvas) canvas.gameObject.SetActive(false);
        }
        void OnDestroy() { if (canvas) Destroy(canvas.gameObject); }

        // JC | 2026-11-10 | uGUI 입력 차단막을 사용해 편집 클릭이 전투 타깃 선택으로 전달되지 않게 합니다.
        RectTransform Panel(string name, Transform parent, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rect;
        }
        TMP_Text Label(Transform parent, string text, Vector2 pos, Vector2 size, float fontSize)
        {
            var go = new GameObject("문구", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            var label = go.GetComponent<TextMeshProUGUI>();
            if (font) label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }
        Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var rect = Panel(text, parent, size, normal);
            rect.anchoredPosition = pos;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            Label(rect, text, Vector2.zero, size - new Vector2(16, 8), 23);
            return button;
        }
        Toggle MakeToggle(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction<bool> action)
        {
            var rect = Panel(text, parent, new Vector2(245, 44), normal);
            rect.anchoredPosition = pos;
            var toggle = rect.gameObject.AddComponent<Toggle>();
            var mark = Panel("체크", rect, new Vector2(23, 23), new Color(.3f, 1, .4f));
            mark.anchoredPosition = new Vector2(-101, 0);
            toggle.targetGraphic = rect.GetComponent<Image>();
            toggle.graphic = mark.GetComponent<Image>();
            // JC | 2026-11-10 | 런타임에 체크 이미지를 연결한 직후에도 꺼진 상태가 정확히 표시되도록 초기 알파를 지정합니다.
            toggle.toggleTransition = Toggle.ToggleTransition.None;
            toggle.SetIsOnWithoutNotify(false);
            toggle.graphic.canvasRenderer.SetAlpha(0);
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(rect, text, new Vector2(16, 0), new Vector2(205, 40), 21);
            toggle.onValueChanged.AddListener(action);
            return toggle;
        }
    }
}
