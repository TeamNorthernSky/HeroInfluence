using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cell = ASB.Work.BattleGrid.GridCell;
using Grid = ASB.Work.BattleGrid.BattleGridManager;

namespace JC.VFX.Seam
{
    /// <summary>캐릭터별 테스트 씬에서 실제 스킬 실행 경로를 반복 재생하는 입력 리그.</summary>
    public sealed class HeroSkillPreviewRig : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            [Tooltip("이 스킬을 선택하는 키. 선택 후 유효한 유닛·열·진영을 클릭합니다.")]
            public KeyCode key;
            [Tooltip("실제 CSV 스킬 ID. 강화판은 기본 ID + 1을 사용합니다.")]
            public int skillIndex;
            [Tooltip("게임 화면의 키 안내에 표시할 스킬 이름입니다.")]
            public string label;
        }

        [Tooltip("이 씬에서 생성한 시전자 참조를 제공하는 기존 프리뷰 컨트롤러입니다.")]
        [SerializeField] private SkillPresentationPreviewController preview;
        [Tooltip("실제 전투 스킬을 실행할 씬의 BattleManager입니다.")]
        [SerializeField] private BattleManager battle;
        [Tooltip("Q/W, E/R, A/S, D/F에 각 계열의 기본·강화판을 연결합니다.")]
        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();
        [Tooltip("대상 클릭에 사용할 카메라. 비우면 MainCamera를 사용합니다.")]
        [SerializeField] private Camera pickCamera;
        [Tooltip("시전 중단, 위치·HP·부활 사용권 복구 및 잔여 효과 제거 키입니다.")]
        [SerializeField] private KeyCode resetKey = KeyCode.Z;
        [Tooltip("첫 번째 보조 아군의 생존/사망을 전환합니다. TAO의 부활 분기를 시험할 때 사용합니다.")]
        [SerializeField] private KeyCode allyStateKey = KeyCode.G;
        [Tooltip("회복 확인을 위한 초기 아군 HP 비율입니다. 0.5는 최대 HP의 절반입니다.")]
        [SerializeField, Range(0.1f, 1f)] private float allyHpRatio = 0.5f;
        [Tooltip("게임 화면 왼쪽 위에 키와 현재 선택 안내를 표시합니다.")]
        [SerializeField] private bool showOverlay = true;

        private sealed class UnitState
        {
            public BattleCharactor unit;
            public Cell cell;
            public Vector3 position;
            public Quaternion rotation;
            public IDisposable rates;
            public IDisposable minimumHp;
        }
        private readonly List<UnitState> units = new List<UnitState>();
        private readonly HashSet<int> initialRoots = new HashSet<int>();
        private Binding pending;
        private string message = "유닛 준비 중";
        private bool ready;
        private float originalTimeScale;
        private GUIStyle textStyle;
        public BattleCharactor Actor { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool LastCastSucceeded { get; private set; }
        public int CompletedCastCount { get; private set; }
        public int SelectedSkillIndex => pending != null ? pending.skillIndex : 0;

        private IEnumerator Start()
        {
            originalTimeScale = Time.timeScale;
            // 기존 부트스트랩·전투 억제기가 스폰과 스탯 설정을 끝낸 뒤 상태를 잡습니다.
            for (int i = 0; i < 3; i++) yield return null;
            if (preview == null) preview = FindFirstObjectByType<SkillPresentationPreviewController>();
            if (battle == null) battle = FindFirstObjectByType<BattleManager>();
            Actor = preview != null && preview.PreviewActorTransform != null
                ? preview.PreviewActorTransform.GetComponent<BattleCharactor>() : null;
            if (Actor == null || battle == null)
            {
                message = "프리뷰 시전자 또는 전투 관리자 연결을 확인하세요.";
                Debug.LogError("[HeroSkillPreviewRig] " + message, this);
                yield break;
            }
            foreach (var unit in FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (unit.gameObject.scene != gameObject.scene) continue;
                var state = new UnitState { unit = unit, cell = unit.OccupiedCell,
                    position = unit.transform.position, rotation = unit.transform.rotation };
                // 진형 보너스까지 계산한 최종 확률을 고정하여 반복 시험의 우발적 분기를 막습니다.
                state.rates = unit.AddRuntimeStatModifier(new RuntimeStatModifier(
                    RuntimeStatMask.CounterRate | RuntimeStatMask.CriticalRate | RuntimeStatMask.AvoidRate,
                    RuntimeStatOperation.Override, default), this);
                if (unit.IsPlayer != Actor.IsPlayer)
                    state.minimumHp = unit.AddMinimumHpConstraint(1f, this);
                units.Add(state);
            }
            foreach (var root in gameObject.scene.GetRootGameObjects()) initialRoots.Add(root.GetInstanceID());
            ready = true;
            ResetPreview();
        }

        private void Update()
        {
            if (!ready || Input.GetMouseButton(1)) return; // 자유 카메라 Q/W/E/A/S/D와 충돌 방지
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.LeftBracket)) Time.timeScale = Mathf.Max(.1f, Time.timeScale * .5f);
                if (Input.GetKeyDown(KeyCode.RightBracket)) Time.timeScale = Mathf.Min(2f, Time.timeScale * 2f);
                if (Input.GetKeyDown(KeyCode.Backslash)) Time.timeScale = 1f;
            }
            if (Input.GetKeyDown(resetKey)) { ResetPreview(); return; }
            if (Input.GetKeyDown(allyStateKey)) { ToggleAllyState(); return; }
            if (Input.GetKeyDown(KeyCode.Escape)) { pending = null; Grid.Instance?.ClearPreviewHighlight(); return; }
            foreach (var binding in bindings)
                if (binding != null && Input.GetKeyDown(binding.key)) { SelectKey(binding.key); break; }
            if (pending != null && Input.GetMouseButtonDown(0)) PickTarget();
        }

        public bool SelectKey(KeyCode key)
        {
            if (!ready || IsPlaying) return false;
            pending = bindings.FirstOrDefault(b => b != null && b.key == key);
            Grid.Instance?.ClearPreviewHighlight();
            if (pending == null) return false;
            var skill = DHCsvTemplateCatalog.Instance?.GetSkillTemplate(pending.skillIndex);
            if (skill == null) { message = "스킬 데이터가 없습니다: " + pending.skillIndex; pending = null; return false; }
            string target = SkillActivationRules.RequiresReviveTarget(Actor, skill) ? "쓰러진 아군" :
                SkillActivationRules.Kind(Actor, skill) == SkillActivationKind.Side ? "대상 진영의 칸" :
                SkillActivationRules.Kind(Actor, skill) == SkillActivationKind.Column ? "대상 열의 칸" :
                skill.classSkillEffect == 1 ? "회복할 아군 (자신 포함)" : "대상 유닛";
            message = pending.label + " → " + target + " 클릭";
            return true;
        }

        private void PickTarget()
        {
            var camera = pickCamera != null ? pickCamera : Camera.main;
            if (camera == null) return;
            foreach (var hit in Physics.RaycastAll(camera.ScreenPointToRay(Input.mousePosition), 300f).OrderBy(h => h.distance))
            {
                var unit = hit.collider.GetComponentInParent<BattleCharactor>();
                var cell = unit != null ? unit.OccupiedCell : hit.collider.GetComponentInParent<Cell>();
                if (TryClick(unit, cell)) return;
            }
        }

        public bool TryClick(BattleCharactor unit, Cell cell)
        {
            if (!ready || IsPlaying || pending == null) return false;
            var source = DHCsvTemplateCatalog.Instance?.GetSkillTemplate(pending.skillIndex);
            if (source == null || !SkillActivationRules.TryResolveClick(Actor, source, unit, cell, out var target)) return false;
            var binding = pending;
            pending = null;
            // 미리보기 복사본만 비용을 제거합니다. 원본 CSV/저장 데이터는 바꾸지 않습니다.
            var skill = JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(source));
            skill.IPCost = 0;
            if (ASB.Work.Battle.SkillExecution.SkillAreaPreviewHelper.TryGetAreaCells(Actor, target, skill,
                out var main, out var splash)) Grid.Instance?.ShowPreviewHighlight(skill, main, splash);
            StartCoroutine(Cast(binding, skill, target));
            return true;
        }

        private IEnumerator Cast(Binding binding, SkillData skill, BattleCharactor target)
        {
            IsPlaying = true;
            LastCastSucceeded = false;
            message = binding.label + " 재생 중";
            try
            {
                yield return battle.ExecuteGridSkill(Actor, target, skill, ok => LastCastSucceeded = ok);
                CompletedCastCount++;
                message = LastCastSucceeded ? binding.label + " 완료 · Z 초기화" : binding.label + " 실행 실패";
                if (!LastCastSucceeded) Debug.LogWarning("[HeroSkillPreviewRig] 실행 실패: " + skill.skillIndex, this);
            }
            finally { IsPlaying = false; }
        }

        public void ToggleAllyState()
        {
            if (!ready || IsPlaying) return;
            var ally = units.Select(s => s.unit).FirstOrDefault(u => u != null && u != Actor && u.IsPlayer == Actor.IsPlayer);
            if (ally == null) { message = "이 씬에는 보조 아군이 없습니다."; return; }
            if (ally.IsDead) ally.Revive(allyHpRatio);
            else ally.TakeDamage(ally.MaxHp * 2f);
            Actor.HasUsedRevive = false; // 프리뷰에서 부활 분기를 반복 시험하는 조작
            message = ally.IsDead ? "아군 쓰러짐 · D/F 선택 후 해당 아군 클릭" : "아군 생존 · 부활 준비 해제";
            pending = null;
        }

        public void ResetPreview()
        {
            if (!ready) return;
            StopAllCoroutines();
            battle.StopAllCoroutines();
            IsPlaying = false;
            pending = null;
            foreach (var state in units)
            {
                var unit = state.unit;
                if (unit == null) continue;
                unit.GetComponent<PresentationRuntimeContext>()?.Clear();
                if (unit.IsDead) unit.Revive(1f);
                foreach (var effect in unit.ActiveStatusEffects.ToArray())
                    if (effect != null) unit.RemoveStatusEffect(effect.effectType);
                unit.HasUsedRevive = false;
                unit.PendingReviveTarget = null;
                // 이동 트윈은 코루틴과 별도로 동작하므로 위치 복원 전에 함께 중단합니다.
                PrimeTween.Tween.StopAll(unit.transform);
                unit.transform.SetPositionAndRotation(state.position, state.rotation);
                if (state.cell != null) unit.AssignToCell(state.cell);
                unit.InitializeCurrentState(unit.MaxHp * (unit.IsPlayer == Actor.IsPlayer ? allyHpRatio : 1f), unit.FinalStats.Influence);
                if (unit.Anim != null)
                {
                    unit.Anim.StopAllCoroutines();
                    bool enabledBefore = unit.Anim.enabled;
                    unit.Anim.enabled = false;
                    unit.Anim.enabled = enabledBefore;
                    unit.Anim.SetAnimationSpeed(battle.CurrentBattleSpeed);
                    unit.Anim.PlayIdleAnimation();
                }
            }
            // 이 독립 프리뷰 씬에서 실행 중 생성된 루트는 투사체·연출 임시 객체입니다.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (!initialRoots.Contains(root.GetInstanceID())) Destroy(root);
            Grid.Instance?.ClearPreviewHighlight();
            message = "스킬 키 선택 → 대상 클릭 · G 아군 생존/사망 전환";
        }

        private void OnDisable()
        {
            if (ready) Time.timeScale = originalTimeScale;
            foreach (var state in units)
            {
                state.rates?.Dispose();
                state.minimumHp?.Dispose();
            }
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            GUI.Box(new Rect(10, 10, 460, 238), GUIContent.none);
            float y = 18;
            foreach (var binding in bindings)
            {
                if (binding == null) continue;
                GUI.Label(new Rect(18, y, 445, 20), binding.key + "  " + binding.label, textStyle); y += 18;
            }
            GUI.Label(new Rect(18, y + 4, 445, 22), message, textStyle);
            GUI.Label(new Rect(18, y + 27, 445, 20), "Z 초기화 · G 아군 전환 · Esc 선택 취소", textStyle);
            GUI.Label(new Rect(18, y + 47, 445, 20), "[ / ] 배속 · \\ 1배속 · 현재 " + Time.timeScale.ToString("0.##") + "배", textStyle);
        }
    }
}
