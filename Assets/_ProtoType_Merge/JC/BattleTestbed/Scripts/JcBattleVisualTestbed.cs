using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ASB.Work.BattleGrid;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BattleTestbed
{
    /// <summary>실전 전투를 복제한 독립 테스트 환경. 영속 저장소에 테스트 유닛을 등록하지 않습니다.</summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class JcBattleVisualTestbed : MonoBehaviour
    {
        [Tooltip("Z 초기화 때 UI·전투 흐름·그리드를 새로 구성하는 프리팹입니다. 배경과 카메라는 포함하지 않습니다.")]
        [SerializeField] private GameObject battleRuntimePrefab;
        [Tooltip("씬에 미리 배치한 첫 전투 런타임입니다. 이후 Z로 만든 인스턴스로 교체됩니다.")]
        [SerializeField] private GameObject battleRuntime;
        [Tooltip("모델과 애니메이터를 검증한 테스트 전용 아군·적 프리팹 등록표입니다. 두 스포너에도 같은 등록표를 연결합니다.")]
        [SerializeField] private BattleUnitPrefabCatalog prefabCatalog;
        [Tooltip("기본 배치할 히어로 네 명의 데이터 키입니다. 첫 번째 히어로가 G 사망 대상입니다.")]
        [SerializeField] private string[] heroKeys = { "10001", "10002", "10003", "10004" };
        [Tooltip("아군 진영 안의 1~6번 논리 셀입니다. 탐사 초기 진형과 같은 5·1·3·2이며, 히어로 키 순서로 대응합니다.")]
        [SerializeField] private int[] heroCells = { 5, 1, 3, 2 };
        [Tooltip("적 배치를 섞는 시드입니다. 같은 시드에서는 Z 초기화 후에도 배치가 같습니다.")]
        [SerializeField] private int enemyShuffleSeed = 922;
        [Tooltip("피해로 줄어든 HP를 보여 준 뒤 전부 회복하는 시간(실제 초)입니다. 치명타도 HP 1에서 멈춥니다.")]
        [Min(0f)] [SerializeField] private float hpRecoveryDelay = 0.25f;
        [Tooltip("화면 왼쪽 위에 테스트 상태와 Z/G 단축키 안내를 표시합니다.")]
        [SerializeField] private bool showHelp = true;

        private readonly List<BattleCharactor> allies = new List<BattleCharactor>();
        private readonly HashSet<int> environmentRoots = new HashSet<int>();
        private BattleFlowManager flow;
        private string[] enemyKeys;
        private bool resetting;
        private bool deathRequested;
        private bool previousAuto;
        private float previousSpeed;
        private float previousTimeScale;
        private string status = "전투 준비 중";

        private void Awake()
        {
            previousAuto = BattleRuntimeSettings.IsAutoBattle;
            previousSpeed = BattleRuntimeSettings.BattleSpeed;
            previousTimeScale = Time.timeScale;
            BattleRuntimeSettings.Reset();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root != battleRuntime) environmentRoots.Add(root.GetInstanceID());
        }

        private IEnumerator Start()
        {
            // 데이터 카탈로그, UI 및 전역 부트스트랩의 Awake/Start 이후에 참가자를 연결합니다.
            yield return null;
            TryStartBattle();
        }

        private void Update()
        {
            if (resetting) return;
            if (Input.GetKeyDown(KeyCode.Z))
            {
                StartCoroutine(ResetBattle());
                return;
            }
            if (Input.GetKeyDown(KeyCode.G) && flow != null) deathRequested = true;
            if (deathRequested && flow != null && !flow.IsActionInProgress && !flow.IsTurnPresentationPending)
            {
                deathRequested = false;
                if (allies.Count > 0 && allies[0] != null && !allies[0].IsDead)
                {
                    battleRuntime.GetComponentInChildren<InputHandler>(true)?.ClearSelectionState();
                    allies[0].GetComponent<JcBattleTestbedVitals>().KillForRevivalTest();
                    status = $"G: {allies[0].DisplayName} 사망 · 부활 스킬 또는 Z로 복원";
                }
            }
        }

        private void TryStartBattle()
        {
            try { BuildBattle(); }
            catch (Exception ex)
            {
                flow?.StopBattleLoop();
                status = "테스트 전투 준비 실패 — Console 확인 후 Z";
                Debug.LogException(ex, this);
            }
        }

        private void BuildBattle()
        {
            var catalog = DHCsvTemplateCatalog.Instance;
            if (catalog == null || prefabCatalog == null || battleRuntime == null || battleRuntimePrefab == null)
                throw new InvalidOperationException("테스트씬의 데이터/런타임 연결이 없습니다.");
            if (heroKeys.Length != 4 || heroCells.Length != 4 || heroCells.Distinct().Count() != 4)
                throw new InvalidOperationException("서로 다른 셀에 히어로 네 명을 지정해야 합니다.");
            if (!catalog.IsLoaded) catalog.ReloadTemplates();
            foreach (var canvas in battleRuntime.GetComponentsInChildren<Canvas>(true))
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) canvas.worldCamera = Camera.main;
            var playerSpawner = battleRuntime.GetComponentInChildren<PlayerSpawner>(true);
            var enemySpawner = battleRuntime.GetComponentInChildren<EnemySpawner>(true);
            flow = battleRuntime.GetComponentInChildren<BattleFlowManager>(true);
            if (playerSpawner == null || enemySpawner == null || flow == null)
                throw new InvalidOperationException("전투 런타임에 스포너 또는 턴 관리자가 없습니다.");
            if (!BattleLogicalSlotMap.TryCreate(playerSpawner.transform, out var playerSlots, out string playerSlotError))
                throw new InvalidOperationException(playerSlotError);
            if (!BattleLogicalSlotMap.TryCreate(enemySpawner.transform, out var enemySlots, out string enemySlotError))
                throw new InvalidOperationException(enemySlotError);
            if (enemySlots.Count != 6 || heroCells.Any(cell => !playerSlots.TryResolve(cell, out _)))
                throw new InvalidOperationException("테스트 유닛을 배치할 4개 아군 셀/6개 적 셀이 필요합니다.");

            // 실패 시 부분 배치가 남지 않도록 네 명의 임시 데이터를 먼저 검증합니다.
            var inputs = new SimulationAllyRuntimeData[4];
            var definitions = new UnitData[4];
            for (int i = 0; i < heroKeys.Length; i++)
            {
                if (!catalog.TryGetPlayerUnitTemplate(heroKeys[i], out var template) ||
                    !catalog.TryGetPlayerTemplate(heroKeys[i], out definitions[i]) ||
                    prefabCatalog.GetPlayerPrefab(heroKeys[i]) == null)
                    throw new InvalidOperationException($"히어로 데이터/프리팹 누락: {heroKeys[i]}");
                int level = GetBasicSkillTestLevel(catalog, template.ClassIndex);
                var input = new SimulationAllyInput { Enabled = true, UnitTemplateKey = heroKeys[i], Level = level, HpRatio = 1f };
                if (!SimulationUnitFactory.TryCreateRuntimeData(input, i, out inputs[i], out string error))
                    throw new InvalidOperationException(error);
            }
            if (enemyKeys == null) enemyKeys = CreateEnemyLineup(catalog, prefabCatalog, enemyShuffleSeed);
            // Z 때도 실제 생성에 사용할 프리팹을 검사합니다. 빈 모델/중복 전투체를 생성하지 않습니다.
            foreach (string key in enemyKeys.Distinct())
                if (!ValidateEnemyPrefab(prefabCatalog.GetEnemyPrefab(key), out string prefabError))
                    throw new InvalidOperationException($"적 프리팹 검증 실패: {key} — {prefabError}");
            allies.Clear();
            var participants = new List<BattleCharactor>();
            for (int i = 0; i < heroKeys.Length; i++)
            {
                playerSlots.TryResolve(heroCells[i], out var slot);
                var go = playerSpawner.SpawnUnit(heroKeys[i], slot.GridNumber);
                if (go == null) throw new InvalidOperationException($"히어로 생성 실패: {heroKeys[i]}");
                go.GetComponent<CharactorScript>().Initialize(inputs[i].UnitData, definitions[i]);
                var unit = PrepareUnit(go, true);
                allies.Add(unit);
                participants.Add(unit);
            }
            for (int i = 0; i < enemyKeys.Length; i++)
            {
                enemySlots.TryResolve(i + 1, out var slot);
                var go = enemySpawner.SpawnUnit(enemyKeys[i], slot.GridNumber);
                if (go == null) throw new InvalidOperationException($"적 생성 실패: {enemyKeys[i]}");
                participants.Add(PrepareUnit(go, false));
            }
            BattleGridManager.Instance.RebuildCache();
            flow.Initialize(participants, false);
            flow.StartBattleLoop();
            status = "히어로 4명 · 적 6명 · 기본 스킬 4종 · HP 자동 회복 / IP 유지";
        }

        private BattleCharactor PrepareUnit(GameObject go, bool player)
        {
            var unit = go.GetComponent<BattleCharactor>();
            unit.Initialize();
            unit.IsPlayer = player;
            unit.EnsureAnimationController();
            go.AddComponent<JcBattleTestbedVitals>().Initialize(unit, hpRecoveryDelay);
            return unit;
        }

        public static int GetBasicSkillTestLevel(DHCsvTemplateCatalog catalog, int classIndex)
        {
            var skills = catalog.GetSkillsByClassIndex(classIndex);
            var basics = skills.Where(s => s != null && s.skillIndex % 10 == 0).ToArray();
            if (basics.Length != 4) throw new InvalidOperationException($"기본 스킬이 4종이 아닙니다: class={classIndex}");
            int level = Mathf.Max(1, basics.Max(s => s.acquireLevel));
            if (skills.Any(s => s != null && s.skillIndex % 10 != 0 && s.acquireLevel <= level))
                throw new InvalidOperationException($"기본 스킬 해금 레벨에 강화판이 겹칩니다: class={classIndex}");
            return level;
        }

        public static string[] CreateEnemyLineup(DHCsvTemplateCatalog catalog, BattleUnitPrefabCatalog prefabs, int seed)
        {
            var pool = prefabs.EnemyPrefabs.Where(e => e != null && e.Prefab != null &&
                catalog.TryGetEnemyTemplate(e.UnitKey, out var data) && data != null &&
                ValidateEnemyPrefab(e.Prefab, out _))
                .Select(e => e.UnitKey).Distinct().OrderBy(k => k).ToArray();
            if (pool.Length == 0) throw new InvalidOperationException("데이터·메시·Animator·단일 전투체 검사를 통과한 적 프리팹이 없습니다.");
            var random = new System.Random(seed);
            var result = new string[6];
            for (int slot = 0; slot < result.Length;)
            {
                for (int i = pool.Length - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (pool[i], pool[j]) = (pool[j], pool[i]);
                }
                for (int i = 0; i < pool.Length && slot < result.Length; i++) result[slot++] = pool[i];
            }
            return result;
        }

        public static bool ValidateEnemyPrefab(GameObject prefab, out string error)
        {
            error = string.Empty;
            if (prefab == null || !prefab.activeSelf)
                error = "프리팹이 없거나 루트가 비활성입니다.";
            else if (prefab.GetComponentsInChildren<BattleCharactor>(true).Length != 1 ||
                     prefab.GetComponent<BattleCharactor>() == null ||
                     prefab.GetComponentsInChildren<EnemyScript>(true).Length != 1 ||
                     prefab.GetComponent<EnemyScript>() == null)
                error = "루트에 전투 캐릭터와 적 스크립트가 하나씩 있어야 합니다. 중첩 전투체는 허용하지 않습니다.";
            else if (!prefab.GetComponentsInChildren<Renderer>(true).Any(HasVisibleMesh))
                error = "활성 메시와 재질이 없습니다. 중첩 모델 참조를 확인하세요.";
            else if (!prefab.GetComponentsInChildren<Animator>(true).Any(a => a.enabled &&
                     IsHierarchyActive(a.transform) && a.runtimeAnimatorController != null))
                error = "활성 Animator 또는 애니메이션 컨트롤러가 없습니다.";
            return error.Length == 0;
        }

        private static bool HasVisibleMesh(Renderer renderer)
        {
            if (!renderer.enabled || renderer.forceRenderingOff || !IsHierarchyActive(renderer.transform)) return false;
            var skin = renderer as SkinnedMeshRenderer;
            var filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = skin != null ? skin.sharedMesh : (filter != null ? filter.sharedMesh : null);
            return mesh != null && mesh.vertexCount > 0 && renderer.sharedMaterials.Any(m => m != null && m.shader != null);
        }

        private static bool IsHierarchyActive(Transform target)
        {
            // 프리팹 자산도 검사하므로 activeInHierarchy 대신 부모의 activeSelf를 확인합니다.
            for (var t = target; t != null; t = t.parent)
                if (!t.gameObject.activeSelf) return false;
            return true;
        }

        private IEnumerator ResetBattle()
        {
            resetting = true;
            deathRequested = false;
            status = "전투 초기화 중";
            flow?.StopBattleLoop();
            if (battleRuntime != null)
            {
                foreach (var context in battleRuntime.GetComponentsInChildren<PresentationRuntimeContext>(true)) context.Clear();
                foreach (var behaviour in battleRuntime.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null) behaviour.StopAllCoroutines();
                foreach (var t in battleRuntime.GetComponentsInChildren<Transform>(true)) PrimeTween.Tween.StopAll(t);
                battleRuntime.SetActive(false);
                Destroy(battleRuntime);
            }
            // 전투가 생성한 독립 VFX만 정리합니다. 시작 때 있던 배경/조명/카메라는 보존합니다.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root != battleRuntime && !environmentRoots.Contains(root.GetInstanceID())) Destroy(root);
            allies.Clear();
            flow = null;
            yield return null; // 이전 싱글턴 및 이벤트 구독의 OnDestroy 완료 후 교체
            BattleRuntimeSettings.Reset();
            battleRuntime = Instantiate(battleRuntimePrefab);
            SceneManager.MoveGameObjectToScene(battleRuntime, gameObject.scene);
            battleRuntime.name = battleRuntimePrefab.name;
            yield return null;
            TryStartBattle();
            resetting = false;
        }

        private void OnGUI()
        {
            if (!showHelp) return;
            GUI.Box(new Rect(12, 12, 570, 58), $"BATTLE VISUAL TESTBED   |   Z 초기화   |   G 첫 히어로 사망\n{status}");
        }

        private void OnDestroy()
        {
            BattleRuntimeSettings.SetAutoBattle(previousAuto);
            BattleRuntimeSettings.SetBattleSpeed(previousSpeed);
            Time.timeScale = previousTimeScale;
        }
    }
}
