using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using ASB.Work.BattleGrid;

namespace JC.BattleTesting
{
    // 원본 대응: ASB/Scripts/Battle/Core/BattleSceneManager.Start/PostBattleSequence,
    // ASB/Scripts/Unit/BossController 페이즈 효과, EnemySpawner의 생성·시체 부활.
    // JC 전용 진입점입니다. CombatContext/Repository/보상/저장에는 쓰지 않으며 원본 씬은 변경하지 않습니다.
    [DefaultExecutionOrder(-500)]
    public sealed class JcBattleTestSession : MonoBehaviour
    {
        [Tooltip("편집 중인 초기 설정입니다. 적용 및 재시작 버튼으로 전투에 반영하며, 아군 불사만 즉시 적용합니다.")]
        public JcBattleTestSettings settings = new JcBattleTestSettings();
        [Tooltip("명시적 저장/불러오기 대상 프로필입니다. 진행 HP·턴·소환 상태는 저장하지 않습니다.")]
        public JcBattleTestProfile profile;
        [Tooltip("씬에서 사용할 기존 공용 히어로/무기/스킬 데이터 카탈로그입니다. 영속 카탈로그가 다르면 시작을 차단합니다.")]
        public DHCsvTemplateCatalog catalog;
        [Tooltip("최신 율리아·증폭기·장막·굴렁쇠의 원본 테이블 SO입니다. 원본 데이터는 변경하지 않습니다.")]
        public EnemyUnit1SectorDataTable enemyTable;
        [Tooltip("기존 히어로 프리팹 카탈로그입니다. 등록부를 새로 만들지 않고 읽기만 합니다.")]
        public BattleUnitPrefabCatalog unitPrefabs;
        [Tooltip("율리아 본체의 기존 전투 프리팹입니다. 테스트 인스턴스의 제어 컴포넌트만 JC 구현으로 연결합니다.")]
        public GameObject yuliaPrefab;
        [Tooltip("증폭기 40002/40003과 굴렁쇠 40005 순서의 기존 전투 프리팹입니다.")]
        public GameObject[] auxiliaryPrefabs = new GameObject[3];
        [Tooltip("장막의 임시 전투 프리팹입니다. 정식 외형 도착 시 이 참조를 교체합니다. 독립 HP·스킬은 테이블에서 적용됩니다.")]
        public GameObject veilPrefab;
        [Tooltip("아군의 기존 그리드 루트입니다. 재시작해도 카메라·환경·그리드 설정은 유지합니다.")]
        public Transform playerPlace;
        [Tooltip("적의 기존 그리드 루트입니다. 논리 슬롯 1~6을 기존 슬롯맵으로 해석합니다.")]
        public Transform enemyPlace;
        [Tooltip("JC 전용 턴 흐름입니다. 페이즈·승패 판정을 행동 완료 경계에 연결합니다.")]
        public JcBattleFlowManager flow;
        [Tooltip("JC 전용 전투 실행기입니다. 기존 스킬 및 연출 카탈로그를 사용합니다.")]
        public JcBattleManager execution;
        [Tooltip("JC 전용 상태 공유 객체입니다. 예약/라운드 규칙은 현재 코드 사양을 따릅니다.")]
        public JcEncounterBlackboard blackboard;
        [Tooltip("소환·시체 부활을 테스트 세션으로 연결하는 JC 스포너입니다.")]
        public JcEnemySpawner spawner;
        [Tooltip("율리아·증폭기·장막·굴렁쇠의 JC 전용 VFX입니다. 원본 프리팹은 수정하지 않습니다.")]
        public Vfx.JcYuliaVfxDirector bossVfx;
        [Tooltip("씬 종료 버튼으로 이동할 씬입니다. 비우면 테스트 전투를 정지합니다. Editor에서는 Play Mode를 종료합니다.")]
        public string exitScene;

        public string Status { get; private set; } = "준비 중";
        public IJcBossActionProvider ManualProvider { get; set; }
        public JcYuliaEncounterRules Rules { get; private set; }
        private readonly List<BattleCharactor> runtimeUnits = new List<BattleCharactor>();
        private readonly Dictionary<BattleCharactor, IDisposable> immortal = new Dictionary<BattleCharactor, IDisposable>();
        private readonly Dictionary<BattleCharactor, IDisposable> chargeModifiers = new Dictionary<BattleCharactor, IDisposable>();
        private readonly Dictionary<BattleCharactor, int> unitSlots = new Dictionary<BattleCharactor, int>();
        private BattleLogicalSlotMap allySlots, enemySlots;
        private JcBattleTestSettings applied;
        private JcEnemyPreset appliedPreset;
        private BattleCharactor boss;
        private BattleResult finalizedResult;
        private string modalMessage;
        private bool modalConfirmed, resultModal, restarting;
        private float savedTimeScale = 1;
        private IDisposable modalLock;

        private void Start() => ApplyAndRestart();
        private void Update()
        {
            if (applied == null) return;
            foreach (var actor in runtimeUnits.Where(x => x != null && x.IsPlayer))
            {
                if (settings.alliesCannotDie && !actor.IsDead && !immortal.ContainsKey(actor))
                    immortal[actor] = actor.AddMinimumHpConstraint(1, this);
                else if (!settings.alliesCannotDie && immortal.TryGetValue(actor, out var handle))
                { handle.Dispose(); immortal.Remove(actor); }
            }
        }

        // 원본 대응: SimulationBattleSceneController 입력 검증 + BattleSceneManager 진입 순서.
        // 설정 검증이 성공하기 전까지 진행 중인 전투를 초기화하지 않습니다.
        public bool Validate(out string error)
        {
            try { Prepare(settings); error = string.Empty; return true; }
            catch (Exception ex) { error = ex.Message; return false; }
        }
        private List<JcTestDataAssembler.PreparedAlly> Prepare(JcBattleTestSettings candidate)
        {
            if (catalog == null || Application.isPlaying && catalog != DHCsvTemplateCatalog.Instance) throw new InvalidOperationException("씬 카탈로그와 현재 공용 카탈로그가 다릅니다. 테스트 씬을 직접 실행해 주세요.");
            // 원본 DHCsvTemplateCatalog.Awake 대응: Edit Mode의 도메인 리로드 뒤에는 Awake가 실행되지 않습니다.
            // 원본 SO를 수정하지 않고 이 씬 인스턴스의 조회 캐시만 다시 구성합니다.
            if (!Application.isPlaying) catalog.ReloadTemplates();
            if (enemyTable == null || unitPrefabs == null || yuliaPrefab == null || veilPrefab == null || auxiliaryPrefabs.Length != 3 || auxiliaryPrefabs.Any(x => x == null))
                throw new InvalidOperationException("테이블/전투 프리팹 연결이 누락되었습니다.");
            if (flow == null || execution == null || blackboard == null || spawner == null) throw new InvalidOperationException("JC 전투 제어 연결이 누락되었습니다.");
            if (!BattleLogicalSlotMap.TryCreate(playerPlace, out allySlots, out var allyError)) throw new InvalidOperationException(allyError);
            if (!BattleLogicalSlotMap.TryCreate(enemyPlace, out enemySlots, out var enemyError)) throw new InvalidOperationException(enemyError);
            if (candidate.allies == null || candidate.allies.Length != 4 || candidate.allies.Any(x => x == null)) throw new InvalidOperationException("히어로 4명의 설정이 필요합니다.");
            if (candidate.allies.Select(x => x.unitKey).Distinct().Count() != 4 || candidate.allies.Select(x => x.slot).Distinct().Count() != 4)
                throw new InvalidOperationException("히어로 종류/배치 슬롯은 중복할 수 없습니다.");
            var prepared = new List<JcTestDataAssembler.PreparedAlly>();
            foreach (var setup in candidate.allies)
            {
                if (setup.level < 1 || setup.level > 8 || setup.slot < 1 || setup.slot > 6 || setup.weaponLevel < 1 || setup.weaponLevel > 5 || setup.attackTraining < 0 || setup.attackTraining > 3 || setup.healthTraining < 0 || setup.healthTraining > 3 || setup.hpRatio <= 0 || setup.hpRatio > 1 || setup.initialIP < 0 || float.IsNaN(setup.initialIP) || float.IsNaN(setup.hpRatio) || setup.skillLevels == null || setup.skillLevels.Length != 4 || setup.skillLevels.Any(x => x < 1 || x > 5))
                    throw new InvalidOperationException("아군 설정 범위를 확인해 주세요: " + setup.unitKey);
                if (!unitPrefabs.TryGetPlayerPrefab(setup.unitKey, out var prefab) || prefab == null) throw new InvalidOperationException("히어로 프리팹이 없습니다: " + setup.unitKey);
                prepared.Add(JcTestDataAssembler.PrepareAlly(setup, catalog));
            }
            var preset = candidate.enemyPreset;
            if (preset == null || preset.enemyLevel < 1 || preset.startPhase < 1 || preset.startPhase > 3 || preset.bossSlot < 1 || preset.bossSlot > 6) throw new InvalidOperationException("적 프리셋 설정이 올바르지 않습니다.");
            if (preset.fullEncounter && (preset.startPhase != 1 || preset.bossSlot == 4 || preset.bossSlot == 6)) throw new InvalidOperationException("전체 보스전은 1페이즈로 시작하며 증폭기 슬롯 4/6과 보스 슬롯이 중복될 수 없습니다.");
            int id = preset.startPhase == 3 ? 40004 : 40001;
            var bossData = JcTestDataAssembler.BuildEnemy(enemyTable, id, preset.enemyLevel, preset.bossSlot);
            if (preset.actionMode == JcBossActionMode.RepeatSkill && !bossData.ExplicitSkills.Any(x => x.skillIndex == preset.repeatSkillId)) throw new InvalidOperationException("시작 페이즈에 없는 반복 스킬입니다: " + preset.repeatSkillId);
            foreach (int auxiliary in new[] { 40002, 40003, 40005 }) JcTestDataAssembler.BuildEnemy(enemyTable, auxiliary, preset.enemyLevel, 1);
            return prepared;
        }
        public void ApplyAndRestart()
        {
            if (!Application.isPlaying || restarting) return;
            if (flow != null && flow.IsActionInProgress && !resultModal) { Status = "행동이 끝난 뒤 재시작해 주세요."; return; }
            List<JcTestDataAssembler.PreparedAlly> prepared;
            try { prepared = Prepare(settings); }
            catch (Exception ex) { Status = ex.Message; Debug.LogError("[JC 전투 테스트] " + Status, this); return; }
            StartCoroutine(RestartRoutine(prepared));
        }
        private IEnumerator RestartRoutine(List<JcTestDataAssembler.PreparedAlly> prepared)
        {
            restarting = true;
            CloseModal();
            ManualProvider?.CancelPending();
            flow.StopBattleLoop();
            if (bossVfx != null) { bossVfx.ClearEffects(); bossVfx.Begin(this); }
            foreach (var handle in immortal.Values) handle.Dispose();
            foreach (var handle in chargeModifiers.Values) handle.Dispose();
            immortal.Clear(); chargeModifiers.Clear();
            foreach (var actor in runtimeUnits) if (actor != null) { actor.ClearOccupiedCell(); actor.gameObject.SetActive(false); Destroy(actor.gameObject); }
            runtimeUnits.Clear(); unitSlots.Clear();
            // 초기 선배치 객체도 JC 씬의 전투 유닛만 정리합니다. 환경/카메라 오브젝트는 유지합니다.
            foreach (var place in new[] { playerPlace, enemyPlace })
                foreach (var actor in place.GetComponentsInChildren<BattleCharactor>(true))
                    if (actor != null) { actor.ClearOccupiedCell(); actor.gameObject.SetActive(false); Destroy(actor.gameObject); }
            yield return null;
            if (appliedPreset != null) Destroy(appliedPreset);
            applied = JsonUtility.FromJson<JcBattleTestSettings>(JsonUtility.ToJson(settings));
            appliedPreset = Instantiate(settings.enemyPreset); applied.enemyPreset = appliedPreset;
            JcBattleRandom.Reset(applied.useFixedSeed, applied.seed);
            Rules = new JcYuliaEncounterRules(appliedPreset.startPhase, !appliedPreset.fullEncounter);
            finalizedResult = BattleResult.None;
            blackboard.ResetAll(); blackboard.ExternalPhasePolicy = true;
            blackboard.SetTestPhase(Rules.Phase, false);
            spawner.TestSpawn = SpawnSummon;
            spawner.TestRevive = ReviveCorpse;
            foreach (var data in prepared)
            {
                unitPrefabs.TryGetPlayerPrefab(data.Setup.unitKey, out var prefab);
                allySlots.TryResolve(data.Setup.slot, out var slot);
                var go = Instantiate(prefab, slot.WorldPosition, slot.WorldRotation, slot.Cell.transform);
                var actor = go.GetComponent<BattleCharactor>();
                JcTestDataAssembler.ApplyAlly(actor, data);
                actor.AssignToCell(slot.Cell);
                runtimeUnits.Add(actor); unitSlots[actor] = data.Setup.slot;
                if (settings.alliesCannotDie) immortal[actor] = actor.AddMinimumHpConstraint(1, this);
            }
            boss = SpawnEnemy(Rules.Phase == 3 ? 40004 : 40001, appliedPreset.bossSlot);
            if (appliedPreset.fullEncounter) { SpawnEnemy(40002, 4); SpawnEnemy(40003, 6); }
            execution.DamageTakenPolicy = target => target == boss && Rules.Phase == 3 ? Rules.VeilDamageMultiplier : 1;
            flow.OnBattleEnded -= BattleEnded; flow.OnBattleEnded += BattleEnded;
            // 원본 BattleSceneManager 새 씬 진입 대응: 씬을 다시 로드하지 않으므로 종료된 UI 상태만 복원합니다.
            foreach (var order in FindObjectsByType<JcTurnOrderUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) order.gameObject.SetActive(true);
            foreach (var tooltip in FindObjectsByType<JcBattleEnemyInfoTooltip>(FindObjectsInactive.Include, FindObjectsSortMode.None)) tooltip.ResetForTestBattle();
            flow.Initialize(runtimeUnits.ToList(), false);
            // 원본 BattleSceneManager.Initialize 순서 대응: 참가자 초기화 완료 후 이번 전투의 정책을 연결합니다.
            flow.ResultPolicy = () => finalizedResult;
            flow.AfterAction = ResolveActionBoundary;
            flow.SkippedBossAction = ReportSkippedBoss;
            foreach (var unit in runtimeUnits) execution.ApplyBattleSpeedToUnit(unit);
            Status = "전투 진행 중 / 페이즈 " + Rules.Phase;
            restarting = false;
            flow.StartBattleLoop();
        }

        // 원본 대응: EnemySpawner.SpawnPlanEntry. 원본 프리팹의 씬 인스턴스만 JC 제어로 연결합니다.
        private BattleCharactor SpawnEnemy(int id, int logicalSlot)
        {
            var entry = JcTestDataAssembler.BuildEnemy(enemyTable, id, appliedPreset.enemyLevel, logicalSlot);
            enemySlots.TryResolve(logicalSlot, out var slot);
            GameObject prefab = id == 40001 ? yuliaPrefab : id == 40004 ? veilPrefab : auxiliaryPrefabs[id == 40002 ? 0 : id == 40003 ? 1 : 2];
            var go = Instantiate(prefab, slot.WorldPosition, slot.WorldRotation * Quaternion.Euler(0, 180, 0), slot.Cell.transform);
            go.name = "JC_" + entry.Data.Name;
            ReplaceInstanceComponent<EnemyScript, JcEnemyScript>(go);
            if (go.GetComponent<BossController>() != null) ReplaceInstanceComponent<BossController, JcBossController>(go);
            if (go.GetComponent<EncounterParticipant>() != null) ReplaceInstanceComponent<EncounterParticipant, JcEncounterParticipant>(go);
            if (go.GetComponent<MadonnaSkillReceiveTimeline>() != null) ReplaceInstanceComponent<MadonnaSkillReceiveTimeline, JcMadonnaSkillReceiveTimeline>(go);
            foreach (var legacy in go.GetComponents<CharactorScript>()) DestroyImmediate(legacy);
            var actor = go.GetComponent<BattleCharactor>();
            actor.IsPlayer = false;
            go.GetComponent<JcEnemyScript>().Initialize(entry.Data);
            // 원본 EnemySpawner의 영속 형식 주입 대응: 실제 저장소와 분리한 스냅샷으로 초상화/최대 IP 조회를 지원합니다.
            actor.BindPersistentEnemySourceData(new EnemyUnitPersistentData(800000 + logicalSlot, "FV" + id,
                appliedPreset.enemyLevel, entry.Data.baseStats, entry.Data.baseStats, entry.Data.baseStats.HP, 100));
            actor.SetTemplateIndex(id.ToString()); actor.SetDisplayName(entry.Data.Name);
            actor.availableSkills = entry.ExplicitSkills.Select(JcTestDataAssembler.Copy).ToList();
            actor.SetClassSkillIndex(actor.availableSkills[0].skillIndex); actor.ResolveSelectedSkill(false);
            actor.MarkInitializedFromDataPipeline(true);
            bossVfx?.Attach(actor);
            actor.AssignToCell(slot.Cell);
            actor.OnDied += EnemyDied;
            if (id == 40001 || id == 40004)
            {
                var controller = go.GetComponent<JcBossController>() ?? go.AddComponent<JcBossController>();
                controller.ConfigureTestSummons(id == 40004 ? "FV40004_1" : "FV40001_3", spawner, flow, blackboard);
                if (id == 40004 || appliedPreset.actionMode == JcBossActionMode.RepeatSkill || ManualProvider != null)
                    go.GetComponent<JcEnemyScript>().ActionProvider = SelectBossAction;
            }
            execution.ApplyBattleSpeedToUnit(actor);
            runtimeUnits.Add(actor); unitSlots[actor] = logicalSlot;
            return actor;
        }
        private static void ReplaceInstanceComponent<TOriginal, TReplacement>(GameObject go) where TOriginal : Component where TReplacement : Component
        {
            var original = go.GetComponent<TOriginal>();
            if (original == null) { if (go.GetComponent<TReplacement>() == null) go.AddComponent<TReplacement>(); return; }
            string json = JsonUtility.ToJson(original);
            if (original is Behaviour behaviour) behaviour.enabled = false;
            DestroyImmediate(original);
            var replacement = go.GetComponent<TReplacement>() ?? go.AddComponent<TReplacement>();
            JsonUtility.FromJsonOverwrite(json, replacement);
        }
        private GameObject SpawnSummon(string idKey, int gridNumber)
        {
            if (!enemySlots.TryGetByGridNumber(gridNumber, out var slot) || slot.Cell.OccupyingUnit != null) return null;
            if (slot.Cell.GetComponentsInChildren<BattleCharactor>(true).Any(x => !x.IsDead)) return null;
            int id = int.Parse(idKey.Replace("FV", ""));
            return SpawnEnemy(id, slot.LogicalSlot).gameObject;
        }
        private bool ReviveCorpse(BattleCharactor corpse, int gridNumber, float ratio)
        {
            if (corpse == null || !corpse.IsDead || !enemySlots.TryGetByGridNumber(gridNumber, out var slot) || (slot.Cell.OccupyingUnit != null && slot.Cell.OccupyingUnit != corpse)) return false;
            corpse.Revive(ratio); corpse.AssignToCell(slot.Cell);
            execution.ApplyBattleSpeedToUnit(corpse);
            if (!flow.Participants.Contains(corpse)) flow.RegisterRuntimeParticipant(corpse);
            return !corpse.IsDead;
        }
        private void EnemyDied(BattleCharactor actor)
        {
            if (actor.TemplateIndex == "40002" || actor.TemplateIndex == "40003")
            {
                Rules.AmplifierDestroyed(int.Parse(actor.TemplateIndex));
                flow.RemoveFromPendingQueue(actor);
            }
        }
        // 원본 대응: EnemySpawner.RemoveCorpseAfterDelay/RemoveCorpseFromFlow.
        // JC는 현재 시전·반격·후속 효과가 끝난 경계에서 소환체의 점유/참가를 해제합니다.
        // 증폭기 시체는 복귀 이벤트와 기존 행동권을 보존하므로 삭제하지 않습니다.
        private void CleanupDeadSummons()
        {
            foreach (var corpse in runtimeUnits.Where(x => x != null && x.IsDead && x.TemplateIndex == "40005").ToArray())
            {
                corpse.ClearOccupiedCell(); flow.RemoveUnit(corpse, refreshQueue: false);
                runtimeUnits.Remove(corpse); unitSlots.Remove(corpse); Destroy(corpse.gameObject);
            }
        }
        private IEnumerator ResolveActionBoundary(BattleCharactor actor, bool skipped)
        {
            if (Rules.EvaluateAfterAction(boss.CurrentHp / boss.MaxHp))
            {
                if (Rules.Phase == 3)
                {
                    var old = boss; int slot = unitSlots[old]; old.ClearOccupiedCell();
                    boss = SpawnEnemy(40004, slot);
                    flow.ReplaceParticipant(old, boss);
                    runtimeUnits.Remove(old); unitSlots.Remove(old); old.gameObject.SetActive(false); Destroy(old.gameObject);
                    foreach (var amplifier in runtimeUnits.Where(x => x != null && (x.TemplateIndex == "40002" || x.TemplateIndex == "40003")))
                    {
                        amplifier.ResetEnergyStack(); amplifier.ResetIncapacitation(); amplifier.SetPendingRest(false);
                        amplifier.GetComponent<JcEncounterParticipant>()?.ReactivateForPhaseThree();
                        if (amplifier.IsDead) { amplifier.Revive(0.5f); enemySlots.TryResolve(unitSlots[amplifier], out var ampSlot); amplifier.AssignToCell(ampSlot.Cell); }
                        execution.ApplyBattleSpeedToUnit(amplifier);
                    }
                }
                blackboard.SetTestPhase(Rules.Phase, true);
                if (bossVfx != null) yield return bossVfx.PhaseEntry(Rules.Phase, boss);
            }
            if (!skipped && actor != null && !actor.IsDead && (actor.TemplateIndex == "40002" || actor.TemplateIndex == "40003"))
                Rules.AmplifierActionCompleted(int.Parse(actor.TemplateIndex));
            foreach (var amplifier in runtimeUnits.Where(x => x != null && (x.TemplateIndex == "40002" || x.TemplateIndex == "40003")))
            {
                if (chargeModifiers.TryGetValue(amplifier, out var oldModifier)) oldModifier.Dispose();
                chargeModifiers[amplifier] = amplifier.AddRuntimeStatModifier(new RuntimeStatModifier(RuntimeStatMask.Atk, RuntimeStatOperation.Multiply,
                    new StatBlock { Atk = Rules.AmplifierAttackMultiplier(int.Parse(amplifier.TemplateIndex)) }), this);
            }
            CleanupDeadSummons();
            bool objective = boss.IsDead && (!appliedPreset.fullEncounter || Rules.Phase == 3);
            finalizedResult = JcYuliaEncounterRules.EvaluateResult(objective, runtimeUnits.Any(x => x != null && x.IsPlayer && !x.IsDead));
            // 대응: BattleSceneManager.PostBattleSequence. 종료 팝업의 TimeScale=0으로 파괴/잔광이 얼지 않게 합니다.
            // 행동권은 계속 잠긴 상태이며 승패·피해·턴 판정에는 영향을 주지 않습니다.
            if (finalizedResult != BattleResult.None && bossVfx != null && bossVfx.preset != null)
                for (float t = 0; t < bossVfx.preset.destroySeconds + .3f; t += Time.deltaTime * Mathf.Max(.01f, execution.CurrentBattleSpeed))
                    yield return null;
            Status = (finalizedResult == BattleResult.Victory ? "전투 승리" : finalizedResult == BattleResult.Defeat ? "전투 패배" : "전투 진행 중") + " / 페이즈 " + Rules.Phase + " / 과부하 " + Rules.Overload;
            yield break;
        }

        // 원본 대응: EnemyScript.RunAITurn/EAI_40001. 반복은 AI 예약·휴식만 우회하며 실행 유효성은 유지합니다.
        private IEnumerator SelectBossAction(BattleCharactor actor, JcBattleManager manager, JcBattleFlowManager battleFlow)
        {
            string failure = null;
            for (;;)
            {
                JcBossActionRequest request = null;
                if (ManualProvider != null) yield return ManualProvider.Select(actor, failure, result => request = result);
                else
                {
                    if (appliedPreset.actionMode != JcBossActionMode.RepeatSkill && actor.HasPendingRest) { actor.SetPendingRest(false); yield break; }
                    request = new JcBossActionRequest(appliedPreset.actionMode == JcBossActionMode.RepeatSkill ? appliedPreset.repeatSkillId : 400041);
                }
                if (request == null || request.Cancelled) yield break;
                if (request.Skip) { request.TryCommit(); yield break; }
                var skill = actor.availableSkills.Find(x => x.skillIndex == request.SkillId);
                failure = skill == null ? "현재 상태에서 사용할 수 없는 스킬입니다." : actor.IsStunned || actor.IsIncapacitated ? "현재 행동할 수 없는 상태입니다." : null;
                var targets = skill == null ? new List<BattleCharactor>() : TargetingHelper.GetValidTargetsForSkillData(actor, skill).Where(x => x != null && !x.IsDead).ToList();
                if (skill != null && (skill.skillKey == "FV40001_3" || skill.skillKey == "FV40004_1"))
                {
                    targets = battleFlow.GetAlivePlayerUnits();
                    if (!enemySlots.OrderedSlots.Take(3).Any(x => x.Cell.OccupyingUnit == null)) failure = "소환할 전열 슬롯이 모두 점유되어 있습니다.";
                }
                var target = request.Target;
                if (failure == null && (targets.Count == 0 || target != null && !targets.Contains(target))) failure = "유효한 대상이 없습니다.";
                if (failure != null)
                {
                    request.Cancel();
                    if (ManualProvider != null) continue;
                    yield return ShowReason(failure); yield break;
                }
                if (target == null) target = targets[JcBattleRandom.Range(0, targets.Count)];
                if (!request.TryCommit()) yield break;
                battleFlow.ShowTargetHighlight(actor, target, skill);
                bool success = false;
                yield return manager.ExecuteGridSkill(actor, target, skill, ok => success = ok);
                battleFlow.ClearTargetHighlight();
                if (!success && ManualProvider == null) yield return ShowReason("스킬 실행이 완료되지 않았습니다.");
                if (success && appliedPreset.actionMode != JcBossActionMode.RepeatSkill) actor.SetPendingRest(true);
                yield break;
            }
        }
        private IEnumerator ReportSkippedBoss(BattleCharactor actor, string reason)
        {
            if (actor == boss && appliedPreset.actionMode == JcBossActionMode.RepeatSkill) yield return ShowReason(reason);
        }
        private IEnumerator ShowReason(string reason)
        {
            OpenModal("실행할 수 없습니다.\n" + reason + "\n확인하면 턴을 넘기고 전투를 재개합니다.", false);
            yield return new WaitUntil(() => modalConfirmed);
            CloseModal();
        }
        private void BattleEnded(BattleResult result) => OpenModal((result == BattleResult.Victory ? "승리했습니다." : "패배했습니다.") + "\n현재 인스펙터 설정으로 전투를 초기화하시겠습니까?", true);
        private void OpenModal(string message, bool isResult)
        {
            modalMessage = message; modalConfirmed = false; resultModal = isResult;
            modalLock = flow.AcquireFlowLock(this, message); savedTimeScale = Time.timeScale; Time.timeScale = 0;
        }
        private void CloseModal()
        {
            if (modalMessage != null) Time.timeScale = savedTimeScale;
            modalMessage = null; resultModal = false; modalLock?.Dispose(); modalLock = null;
        }
        private void OnGUI()
        {
            if (modalMessage == null) return;
            // Modal의 차단 영역은 화면 전체입니다. 뒤쪽 게임 입력은 FlowLock/TimeScale로도 차단합니다.
            GUI.ModalWindow(GetInstanceID(), new Rect((Screen.width - 560) / 2f, (Screen.height - 230) / 2f, 560, 230), _ => {
                GUILayout.Label(modalMessage); GUILayout.FlexibleSpace();
                if (resultModal)
                {
                    if (GUILayout.Button(new GUIContent("초기화", "현재 인스펙터 설정을 검증한 후 전투만 재시작합니다."))) ApplyAndRestart();
                    if (GUILayout.Button(new GUIContent("씬 종료", "테스트 전투를 종료합니다."))) Exit();
                }
                else if (GUILayout.Button(new GUIContent("확인", "실행 불가 턴을 소비하고 전투를 재개합니다."))) modalConfirmed = true;
            }, "JC 전투 테스트");
        }
        public void Exit()
        {
            flow.StopBattleLoop(); CloseModal(); ManualProvider?.CancelPending();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            if (!string.IsNullOrWhiteSpace(exitScene)) SceneManager.LoadScene(exitScene);
            else Status = "테스트 종료";
#endif
        }
        private void OnDisable()
        {
            ManualProvider?.CancelPending(); CloseModal();
            foreach (var handle in immortal.Values) handle.Dispose(); immortal.Clear();
            foreach (var handle in chargeModifiers.Values) handle.Dispose(); chargeModifiers.Clear();
            if (flow != null) flow.OnBattleEnded -= BattleEnded;
        }
    }
}
