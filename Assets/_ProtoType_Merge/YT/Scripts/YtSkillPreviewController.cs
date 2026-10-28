using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Cell = ASB.Work.BattleGrid.GridCell;
using Grid = ASB.Work.BattleGrid.BattleGridManager;

namespace YT.Preview
{
    // HeroSkillPreviewRig의 대상 선택/실제 시전 경로를 전투 연출 씬 부트스트랩에 연결합니다.
    [DefaultExecutionOrder(1000)]
    public sealed class YtSkillPreviewController : MonoBehaviour
    {
        public BattlePresentationPreview presentation;
        public BattleManager battle;
        public BattleFlowManager flow;
        public GameObject battleHud;
        [Range(.1f, 1f)] public float allyHpRatio = .5f;
        [Min(.1f)] public float repeatInterval = 1f;
        public bool showOverlay = true;
        public bool repeat;
        public int initialHero = 10001;

        private static readonly KeyCode[] BasicKeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };
        private static readonly KeyCode[] AlterKeys = { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F };
        private static readonly string[] HeroNames = { "저스티스", "루미나", "블랙불릿", "네코밍" };
        private static readonly string[] EnemyNames = { "빌런 드론", "빌런 원거리병", "빌런 방패병" };
        private Vector2 panelScroll;
        private sealed class State
        {
            public BattleCharactor unit;
            public Cell cell;
            public Vector3 position;
            public Quaternion rotation;
            public IDisposable rates, minimumHp;
        }
        private readonly List<State> states = new List<State>();
        private IDisposable flowLock;
        private SkillData pending, lastSkill;
        private BattleCharactor lastTarget;
        private bool ready;
        private float repeatAt, savedSpeed;
        private string message = "프리뷰 준비 중…";
        private GUIStyle labelStyle, titleStyle, buttonStyle, selectedHeroStyle, selectedButtonStyle;
        private RawImage runtimeCursorImage;
        private readonly Vector3[] cursorCorners = new Vector3[4];
        public BattleCharactor Actor { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool LastCastSucceeded { get; private set; }
        public int CompletedCastCount { get; private set; }
        public int SelectedSkillIndex => pending != null ? pending.skillIndex : 0;

        private IEnumerator Start()
        {
            savedSpeed = battle.CurrentBattleSpeed;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (flow.Participants.Count < 4 && Time.realtimeSinceStartup < deadline) yield return null;
            if (flow.Participants.Count < 4)
            {
                message = "유닛 생성 실패: Console을 확인하세요.";
                yield break;
            }
            // 실제 전투의 스폰/스탯 설정은 그대로 사용하고 턴 진행만 멈춥니다.
            flow.StopBattleLoop();
            // 프리뷰는 첫 턴 진입까지 진행하지 않으므로 진입 UI 숨김도 함께 해제합니다.
            var entryGate = JC.BattleTesting.JcBattleHudEntryGate.Find(gameObject.scene);
            if (entryGate != null) entryGate.gameObject.SetActive(false);
            flowLock = flow.AcquireFlowLock(this, "YT skill preview");
            yield return null;
            foreach (var unit in flow.Participants.Where(u => u != null))
            {
                var state = new State { unit = unit, cell = unit.OccupiedCell,
                    position = unit.transform.position, rotation = unit.transform.rotation };
                state.rates = unit.AddRuntimeStatModifier(new RuntimeStatModifier(
                    RuntimeStatMask.CounterRate | RuntimeStatMask.CriticalRate | RuntimeStatMask.AvoidRate,
                    RuntimeStatOperation.Override, default), this);
                state.minimumHp = unit.AddMinimumHpConstraint(1f, this);
                states.Add(state);
                // 일부 원본 무기 클립에 남은 빈 기본 이벤트는 이 프리뷰에서만 수신합니다.
                var animator = unit.Anim != null ? unit.Anim.Animator : null;
                if (animator != null && animator.runtimeAnimatorController != null &&
                    animator.runtimeAnimatorController.animationClips.Any(c => c.events.Any(e => e.functionName == "NewEvent")) &&
                    animator.GetComponent<YtPreviewAnimationEvents>() == null)
                    animator.gameObject.AddComponent<YtPreviewAnimationEvents>();
                if (unit.IsPlayer)
                {
                    int family = HeroNumber(unit) * 1000;
                    for (int slot = 1; slot <= 4; slot++)
                    for (int variant = 0; variant <= 1; variant++)
                    {
                        var source = DHCsvTemplateCatalog.Instance.GetSkillTemplate(family + slot * 10 + variant);
                        if (source == null || unit.availableSkills.Any(s => s.skillIndex == source.skillIndex)) continue;
                        var skill = Copy(source);
                        skill.acquireLevel = 1;
                        unit.availableSkills.Add(skill);
                    }
                }
            }
            ready = true;
            RestoreUnits();
            SelectHero(initialHero);
            battle.ChangeBattleSpeed(1f);
        }

        private static int HeroNumber(BattleCharactor unit)
            => int.TryParse(unit.PresentationIndex, out int id) ? id - 10000 : 0;

        private static SkillData Copy(SkillData source)
        {
            var skill = JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(source));
            skill.IPCost = 0;
            return skill;
        }

        private void Update()
        {
            if (!ready || Input.GetMouseButton(1)) return;
            if (Input.GetKeyDown(KeyCode.Backspace)) { ReloadPreview(); return; }
            if (Input.GetKeyDown(KeyCode.Tab)) showOverlay = !showOverlay;
            if (Input.GetKeyDown(KeyCode.T)) repeat = !repeat;
            if (Input.GetKeyDown(KeyCode.C) && !IsPlaying) presentation.useAttackZoom = !presentation.useAttackZoom;
            if (Input.GetKeyDown(KeyCode.LeftBracket)) SetSpeed(battle.CurrentBattleSpeed * .5f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) SetSpeed(battle.CurrentBattleSpeed * 2f);
            if (Input.GetKeyDown(KeyCode.Backslash)) SetSpeed(1f);
            if (Input.GetKeyDown(KeyCode.Escape)) { pending = null; repeat = false; ClearTargetDisplay(); }
            for (int i = 0; i < 4; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.F1 + i))) SelectHero(10001 + i);
                if (Input.GetKeyDown(BasicKeys[i])) SelectSkill(i, false);
                if (Input.GetKeyDown(AlterKeys[i])) SelectSkill(i, true);
            }
            if (Input.GetKeyDown(KeyCode.Z)) SelectCoreSkill();
            for (int i = 0; i < EnemyNames.Length; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.F9 + i))) SelectEnemy(20001 + i);
            if (Input.GetKeyDown(KeyCode.G)) ToggleAlly();
            if (Input.GetKeyDown(KeyCode.Space)) ReplayLast();
            if (pending != null && !IsPlaying)
            {
                UpdateMouseHover();
                if (Input.GetMouseButtonDown(0) && !PointerOverPanel()) PickTarget();
            }
            if (repeat && !IsPlaying && pending == null && lastSkill != null && Time.unscaledTime >= repeatAt)
                ReplayLast();
        }

        public bool SelectHero(int templateId)
        {
            if (!ready || IsPlaying) return false;
            var unit = states.Select(s => s.unit).FirstOrDefault(u => u != null && u.PresentationIndex == templateId.ToString());
            if (unit == null) return false;
            Actor = unit;
            pending = lastSkill = null;
            lastTarget = null;
            repeat = false;
            ClearTargetDisplay();
            message = HeroLabel(Actor) + " · 스킬 선택 후 대상 클릭";
            return true;
        }

        public bool SelectEnemy(int templateId)
        {
            if (templateId < 20001 || templateId > 20003) return false;
            return SelectHero(templateId);
        }

        private SkillData SkillForSlot(int slot, bool enhanced)
        {
            if (Actor == null || slot < 0 || slot > 3) return null;
            if (!Actor.IsPlayer)
                return enhanced ? null : Actor.availableSkills.Where(s => s != null)
                    .GroupBy(s => s.skillIndex).Select(g => g.First()).OrderBy(s => s.skillIndex).Skip(slot).FirstOrDefault();
            int id = HeroNumber(Actor) * 1000 + (slot + 1) * 10 + (enhanced ? 1 : 0);
            return DHCsvTemplateCatalog.Instance.GetSkillTemplate(id);
        }

        public bool SelectSkill(int slot, bool enhanced)
        {
            if (!ready || IsPlaying || Actor == null || slot < 0 || slot > 3) return false;
            var source = SkillForSlot(slot, enhanced);
            if (source == null) { message = "해당 스킬 데이터가 없습니다."; return false; }
            pending = Copy(source);
            message = SkillLabel(pending) + " → 대상 클릭";
            ShowTargets();
            return true;
        }

        public bool SelectCoreSkill()
        {
            if (!ready || IsPlaying || Actor == null || !Actor.IsPlayer) return false;
            var skill = Actor.EquippedWeaponData?.ToSkillData();
            if (skill == null) { message = "장착한 코어 스킬이 없습니다."; return false; }
            pending = Copy(skill);
            message = "코어 · " + SkillLabel(pending) + " → 대상 클릭";
            ShowTargets();
            return true;
        }

        private void ShowTargets()
        {
            ClearTargetDisplay();
            var targets = IsGuardPreview ? states.Select(s => s.unit).Where(u => u != null && u != Actor &&
                !u.IsDead && !u.IsIncapacitated && u.IsPlayer == Actor.IsPlayer).ToList()
                : TargetingHelper.GetValidTargetsForSkillData(Actor, pending);
            Grid.Instance?.SetSelectableUnits(targets, Actor, pending);
        }

        private bool IsGuardPreview => Actor != null && !Actor.IsPlayer && pending != null && pending.skillIndex == 200032;

        private bool ResolvePreviewTarget(BattleCharactor unit, Cell cell, out BattleCharactor target)
        {
            if (!IsGuardPreview) return SkillActivationRules.TryResolveClick(Actor, pending, unit, cell, out target);
            target = unit != null ? unit : cell != null ? cell.OccupyingUnit : null;
            return target != null && target != Actor && !target.IsDead && !target.IsIncapacitated && target.IsPlayer == Actor.IsPlayer;
        }

        private void ClearTargetDisplay()
        {
            Grid.Instance?.ClearPreviewHighlight();
            Grid.Instance?.ClearSelectableCells();
            Grid.Instance?.ClearAllHighlight();
        }

        private void UpdateMouseHover()
        {
            if (PointerOverPanel()) { Grid.Instance?.ClearPreviewHighlight(); return; }
            var camera = presentation.previewCamera;
            foreach (var hit in Physics.RaycastAll(camera.ScreenPointToRay(Input.mousePosition), 300f).OrderBy(h => h.distance))
            {
                var unit = hit.collider.GetComponentInParent<BattleCharactor>();
                var cell = unit != null ? unit.OccupiedCell : hit.collider.GetComponentInParent<Cell>();
                if (TryHover(unit, cell)) return;
            }
            Grid.Instance?.ClearPreviewHighlight();
        }

        public bool TryHover(BattleCharactor unit, Cell cell)
        {
            if (!ready || IsPlaying || pending == null || Actor == null ||
                !ResolvePreviewTarget(unit, cell, out var target))
            {
                Grid.Instance?.ClearPreviewHighlight();
                return false;
            }
            return ShowArea(pending, target);
        }

        private bool ShowArea(SkillData skill, BattleCharactor target)
        {
            if (!ASB.Work.Battle.SkillExecution.SkillAreaPreviewHelper.TryGetAreaCells(Actor, target, skill,
                out var main, out var splash)) return false;
            Grid.Instance?.ShowPreviewHighlight(skill, main, splash);
            return true;
        }

        private bool PointerOverPanel()
            => showOverlay && PanelRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

        private void PickTarget()
        {
            var camera = presentation.previewCamera;
            foreach (var hit in Physics.RaycastAll(camera.ScreenPointToRay(Input.mousePosition), 300f).OrderBy(h => h.distance))
            {
                var unit = hit.collider.GetComponentInParent<BattleCharactor>();
                var cell = unit != null ? unit.OccupiedCell : hit.collider.GetComponentInParent<Cell>();
                if (TryClick(unit, cell)) return;
            }
            message = "선택한 스킬에 맞는 대상·열·진영을 클릭하세요.";
        }

        public bool TryClick(BattleCharactor unit, Cell cell)
        {
            if (!ready || IsPlaying || pending == null || Actor == null) return false;
            if (!ResolvePreviewTarget(unit, cell, out var target)) return false;
            var skill = pending;
            pending = null;
            lastSkill = skill;
            lastTarget = target;
            StartCoroutine(Cast(skill, target));
            return true;
        }

        public bool ReplayLast()
        {
            if (!ready || IsPlaying || lastSkill == null || lastTarget == null) return false;
            StartCoroutine(Cast(lastSkill, lastTarget));
            return true;
        }

        private IEnumerator Cast(SkillData skill, BattleCharactor target)
        {
            if (Actor != null && !Actor.IsPlayer) RestoreUnits();
            IsPlaying = true;
            LastCastSucceeded = false;
            Grid.Instance?.ClearSelectableCells();
            ShowArea(skill, target);
            Actor.HasUsedRevive = false;
            message = SkillLabel(skill) + " 재생 중";
            try
            {
                yield return battle.ExecuteGridSkill(Actor, target, Copy(skill), ok => LastCastSucceeded = ok);
                CompletedCastCount++;
                message = SkillLabel(skill) + (LastCastSucceeded ? " 완료 · Space 다시 재생" : " 실행 실패 · 대상/조건 확인");
                if (!LastCastSucceeded) repeat = false;
            }
            finally
            {
                IsPlaying = false;
                ClearTargetDisplay();
                repeatAt = Time.unscaledTime + repeatInterval;
            }
        }

        public void ToggleAlly()
        {
            if (!ready || IsPlaying) return;
            var state = states.FirstOrDefault(s => s.unit.IsPlayer && s.unit != Actor);
            if (state == null) return;
            if (state.unit.IsDead)
            {
                state.unit.Revive(allyHpRatio);
                state.minimumHp = state.unit.AddMinimumHpConstraint(1f, this);
            }
            else
            {
                state.minimumHp?.Dispose(); state.minimumHp = null;
                state.unit.TakeDamage(state.unit.MaxHp * 2f);
            }
            Actor.HasUsedRevive = false;
            pending = null;
            ClearTargetDisplay();
            message = "보조 아군 " + (state.unit.IsDead ? "쓰러짐 · 부활 스킬 확인 가능" : "생존");
        }

        private void RestoreUnits()
        {
            foreach (var state in states)
            {
                var unit = state.unit;
                if (unit.IsDead) unit.Revive(1f);
                if (state.minimumHp == null) state.minimumHp = unit.AddMinimumHpConstraint(1f, this);
                foreach (var effect in unit.ActiveStatusEffects.ToArray())
                    if (effect != null) unit.RemoveStatusEffect(effect.effectType);
                unit.HasUsedRevive = false;
                unit.PendingReviveTarget = null;
                unit.transform.SetPositionAndRotation(state.position, state.rotation);
                unit.InitializeCurrentState(unit.MaxHp * (unit.IsPlayer ? allyHpRatio : 1f), unit.FinalStats.Influence);
                unit.Anim?.PlayIdleAnimation();
            }
        }

        public void ReloadPreview()
        {
            repeat = false;
            // 중단된 Timeline·투사체·트윈·카메라 상태까지 씬의 저장값으로 초기화합니다.
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(gameObject.scene.path,
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
#endif
        }

        private void SetSpeed(float speed) => battle.ChangeBattleSpeed(Mathf.Clamp(speed, .25f, 2f));
        private static string SkillLabel(SkillData skill) => skill == null ? "없음" : skill.skillName;
        private static string HeroLabel(BattleCharactor unit)
        {
            if (unit != null && !unit.IsPlayer && int.TryParse(unit.PresentationIndex, out int enemy))
                return enemy >= 20001 && enemy <= 20003 ? EnemyNames[enemy - 20001] : unit.PresentationIndex;
            int index = unit != null ? HeroNumber(unit) - 1 : -1;
            return index >= 0 && index < HeroNames.Length ? HeroNames[index] : "준비 중";
        }
        private Rect PanelRect => new Rect(12, 12, 355, Mathf.Min(540, Screen.height - 24));

        private void OnGUI()
        {
            if (!showOverlay) return;
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
                titleStyle = new GUIStyle(labelStyle) { fontSize = 18, fontStyle = FontStyle.Bold };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, wordWrap = true };
                selectedButtonStyle = new GUIStyle(buttonStyle) { fontStyle = FontStyle.Bold };
                selectedHeroStyle = new GUIStyle(labelStyle) { fontSize = 16, fontStyle = FontStyle.Bold };
                selectedHeroStyle.normal.textColor = new Color(1f, .85f, .25f);
            }
            Color previous = GUI.color;
            GUI.color = new Color(.035f, .05f, .075f, .92f);
            GUI.DrawTexture(PanelRect, Texture2D.whiteTexture);
            GUI.color = previous;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("스킬 연출 프리뷰", titleStyle);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            GUI.enabled = ready && !IsPlaying;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 4; i++)
            {
                bool selected = Actor != null && HeroNumber(Actor) == i + 1;
                Color background = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(.3f, .7f, 1f);
                string caption = "F" + (i + 1) + (selected ? " · 선택" : "") + "\n" + HeroNames[i];
                if (GUILayout.Button(caption, selected ? selectedButtonStyle : buttonStyle, GUILayout.Height(40)))
                    SelectHero(10001 + i);
                GUI.backgroundColor = background;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < EnemyNames.Length; i++)
            {
                bool selected = Actor != null && Actor.PresentationIndex == (20001 + i).ToString();
                Color background = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(1f, .5f, .3f);
                if (GUILayout.Button("F" + (i + 9) + (selected ? " · 선택" : "") + "\n" + EnemyNames[i],
                    selected ? selectedButtonStyle : buttonStyle, GUILayout.Height(40))) SelectEnemy(20001 + i);
                GUI.backgroundColor = background;
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Label(Actor != null ? "선택 캐릭터: " + HeroLabel(Actor) : message, selectedHeroStyle);
            bool enemySelected = Actor != null && !Actor.IsPlayer;
            GUILayout.Label(enemySelected ? "적 보유 스킬 · 선택 후 대상 클릭\n공격은 영웅, 대신 맞기는 다른 적을 선택" : "기본 스킬                        강화 스킬", labelStyle);
            GUI.enabled = ready && !IsPlaying;
            for (int i = 0; i < 4; i++)
            {
                var source = ready ? SkillForSlot(i, false) : null;
                if (enemySelected && source == null) continue;
                string name = source != null ? source.skillName : (i + 1) + "번";
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(BasicKeys[i] + "  " + name, buttonStyle, GUILayout.Height(34))) SelectSkill(i, false);
                if (!enemySelected && GUILayout.Button(AlterKeys[i] + "  " + name + "+", buttonStyle, GUILayout.Height(34))) SelectSkill(i, true);
                GUILayout.EndHorizontal();
            }
            if (!enemySelected && GUILayout.Button("Z  코어 스킬", buttonStyle, GUILayout.Height(30))) SelectCoreSkill();
            if (GUILayout.Button("Space  직전 스킬 다시 재생", buttonStyle)) ReplayLast();
            GUI.enabled = ready;
            repeat = GUILayout.Toggle(repeat, "T  같은 대상에게 반복 재생");
            GUILayout.Label(message, labelStyle);
            GUILayout.Label("[ / ] 배속 · \\ 1배속  |  현재 " + (battle != null ? battle.CurrentBattleSpeed.ToString("0.##") : "1") + "배", labelStyle);
            GUILayout.Label("G 아군 생존/사망 · Esc 선택 취소\nC 공격 줌 켜기/끄기 · F6/F7/F8 카메라\nTab 안내 숨기기 · Backspace 전체 초기화", labelStyle);
            GUI.enabled = true;
            if (GUILayout.Button("Backspace  전체 초기화", buttonStyle)) ReloadPreview();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            DrawCursorAbovePanel();
        }

        // Runtime cursor canvases render behind IMGUI; redraw the same image on top.
        private void DrawCursorAbovePanel()
        {
            if (Event.current.type != EventType.Repaint || Cursor.visible || !JcPointerInput.Inside) return;
            if (runtimeCursorImage == null || !runtimeCursorImage.isActiveAndEnabled)
                runtimeCursorImage = FindObjectsByType<RawImage>(FindObjectsSortMode.None)
                    .FirstOrDefault(image => image.isActiveAndEnabled && image.canvas != null &&
                        image.canvas.name == "JC Runtime Cursor");
            if (runtimeCursorImage == null || runtimeCursorImage.texture == null) return;
            runtimeCursorImage.rectTransform.GetWorldCorners(cursorCorners);
            Vector2 lower = RectTransformUtility.WorldToScreenPoint(null, cursorCorners[0]);
            Vector2 upper = RectTransformUtility.WorldToScreenPoint(null, cursorCorners[2]);
            Rect area = new Rect(lower.x, Screen.height - upper.y, upper.x - lower.x, upper.y - lower.y);
            Color color = GUI.color;
            int depth = GUI.depth;
            GUI.color = runtimeCursorImage.color;
            GUI.depth = -10000;
            GUI.DrawTextureWithTexCoords(area, runtimeCursorImage.texture, runtimeCursorImage.uvRect, true);
            GUI.color = color;
            GUI.depth = depth;
        }

        private void OnDisable()
        {
            repeat = false;
            flowLock?.Dispose();
            foreach (var state in states) { state.rates?.Dispose(); state.minimumHp?.Dispose(); }
            if (ready && battle != null) battle.ChangeBattleSpeed(savedSpeed);
        }
    }
}
