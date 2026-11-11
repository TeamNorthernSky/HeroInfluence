using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BattleTesting
{
    // 실제 전투의 참가자·보상·복귀는 기존 CombatContext/BattleSceneManager가 담당합니다.
    // 이 연결부는 탐사씬이 살아 있을 때 외형을 복제하고 전투씬의 표시만 바꿉니다.
    public sealed class JcArnoldBattlePresentation : MonoBehaviour
    {
        static JcArnoldBattlePresentation pending;
        GameObject background;
        string destination;
        Camera battleCamera;
        JcBattleCameraProfile profile;
        Vector3 originalPosition, startPosition, endPosition, center;
        Quaternion originalRotation, startRotation, endRotation;
        float originalFov, startFov, endFov, elapsed, createdAt;
        bool moving, entered, cameraOnly;
        JcBankBattleBackground bankSettings;
        bool bossBackground;
        JcBossCamera bossCamera;
        bool clearTutorialArenaTrees;
        double lightingHour;
        JC.Environment.JcDayNightProfile lightingProfile;
        public string Report => background == null ? string.Empty :
            background.TryGetComponent<JcDynamicBattleOutput>(out var output) ? output.Report : bossBackground ? "보스 무대 배경" : "은행 실내 배경";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() { pending = null; fixedOrigin = null; fixedKey = null; fixedZone = 0; }

        // 기존 연결부 이름은 유지하되 모든 실제 전투의 표시를 담당합니다.
        static Transform fixedOrigin;
        static string fixedKey;
        static int fixedZone;

        public static void SetFixedOrigin(Transform origin, int zone, string key)
        {
            fixedOrigin = origin; fixedZone = zone; fixedKey = key;
        }
        public static void ClearFixedOrigin(Transform origin)
        {
            if (fixedOrigin != origin) return;
            fixedOrigin = null; fixedKey = null; fixedZone = 0;
        }

        public static bool UsesExplorationBackground(CombatContext context)
        {
            if (context == null) return false;
            if (context.HasEventBattle) return true;
            return context.CombatEnemy != null && context.CombatEnemy.SourceType == CombatEnemySourceType.Field;
        }

        public static JcDynamicBattleSettings ResolveBackgroundSettings(Scene scene, Vector3 partyPosition,
            EnemyGridMover enemy, CombatEventBattleData eventBattle)
        {
            var settings = new JcDynamicBattleSettings();
            bool movingEncounter = enemy != null && enemy.BehaviorType == EnemyBehaviorType.Mobile;
            if (movingEncounter)
            {
                settings.encounterPosition = partyPosition;
                settings.enemyPosition = enemy.transform.position;
            }
            else
            {
                Transform origin = fixedOrigin;
                string key = fixedKey;
                int zone = fixedZone;
                string mainKey = eventBattle?.SourceMainEventKey;
                if (!string.IsNullOrWhiteSpace(mainKey))
                {
                    var main = FindObjectsOfType<MainEventObject>(true).FirstOrDefault(e =>
                        e.gameObject.scene == scene && e.EventKey == mainKey && e.ZoneId == eventBattle.ZoneId);
                    if (main != null) { origin = main.transform; key = main.EventKey; zone = main.ZoneId; }
                }
                else if (!string.IsNullOrWhiteSpace(eventBattle?.SourceSubEventKey))
                {
                    // 대화 종료 시 임시 origin이 해제되어도 전투 요청에 저장된 출처로 복구합니다.
                    var sub = FindObjectsOfType<SubEventObject>(true).FirstOrDefault(e =>
                        e.gameObject.scene == scene && e.EventKey == eventBattle.SourceSubEventKey && e.ZoneId == eventBattle.ZoneId);
                    if (sub != null) { origin = sub.transform; key = sub.EventKey; zone = sub.ZoneId; }
                }
                if (origin == null && enemy != null) origin = enemy.transform;
                if (origin == null || origin.gameObject.scene != scene)
                    throw new InvalidOperationException("고정 이벤트의 기준 오브젝트를 찾지 못했습니다");
                settings.encounterPosition = origin.position;
                // 파티의 접근 위치와 무관한 일정한 선호 방향입니다.
                settings.enemyPosition = origin.position + Vector3.forward;
                var entry = JcBattlePresentationProfile.Load()?.Find(zone, key);
                if (entry != null)
                {
                    if (entry.overrideAnchor) settings.encounterPosition = entry.anchor;
                    settings.enemyPosition = settings.encounterPosition + Vector3.forward;
                    settings.axis = entry.axis;
                }
            }
            return settings;
        }

        // 전환 실패 시 다음 전투에 이전 배경/sceneLoaded 콜백이 남지 않게 정리합니다.
        public static void CancelPending()
        {
            if (pending == null) return;
            SceneManager.sceneLoaded -= pending.Loaded;
            Destroy(pending.gameObject);
            pending = null;
        }

        public static void Prepare(PartyGridMover party, EnemyGridMover enemy, string battleScene)
        {
            Prepare(party, enemy, battleScene, false);
        }

        // 튜토리얼도 공통 카메라/HUD 및 명시된 실내 배경 분기를 사용합니다.
        public static void PrepareCameraOnly(string battleScene)
        {
            Prepare(null, null, battleScene, true);
        }

        static void Prepare(PartyGridMover party, EnemyGridMover enemy, string battleScene, bool cameraOnly)
        {
            if (pending != null) Destroy(pending.gameObject);
            if ((!cameraOnly && party == null) || string.IsNullOrWhiteSpace(battleScene)) return;
            var context = CombatContext.Instance;
            var cameraProfile = Resources.Load<JcBattleCameraProfile>("JC_BattleCamera_Default");
            var owner = new GameObject("JC_BattlePresentation");
            DontDestroyOnLoad(owner);
            var bridge = owner.AddComponent<JcArnoldBattlePresentation>();
            bridge.profile = cameraProfile; bridge.destination = battleScene;
            bridge.cameraOnly = cameraOnly;
            bridge.lightingHour=JC.Environment.JcDayNightController.CurrentHours;
            bridge.lightingProfile=JC.Environment.JcDayNightController.SnapshotForBattle();
            bridge.createdAt = Time.realtimeSinceStartup; pending = bridge;
            SceneManager.sceneLoaded += bridge.Loaded;

            var boss = JcBossBattleBackground.Load();
            if (boss != null && boss.Matches(context) && boss.visualPrefab != null)
            {
                bridge.background = Instantiate(boss.visualPrefab, owner.transform, false);
                bridge.background.GetComponentInChildren<JcBossCityTime>(true)?.SetHour(bridge.lightingHour);
                bridge.bossBackground = true;
                bridge.background.SetActive(false);
                return;
            }
            // 명시된 은행 전투만 실내로 교체합니다. 진형·판정·카메라는 공통 경로를 유지합니다.
            var bank = JcBankBattleBackground.Load();
            if (bank != null && bank.Matches(context))
            {
                if (bank.visualPrefab != null)
                {
                    bridge.background = Instantiate(bank.visualPrefab, owner.transform, false);
                    bridge.bankSettings = bank;
                    bridge.background.SetActive(false);
                    return;
                }
                Debug.LogError("[JC Battle] 은행 실내 배경 프리팹 연결 누락");
            }
            // 점령/본부 전투 또는 생성 실패에도 카메라와 UI는 독립적으로 적용합니다.
            bool tutorialOutdoor = cameraOnly && context != null && context.IsTutorial && context.TutorialBattleKey == "TUT_01";
            bridge.clearTutorialArenaTrees = tutorialOutdoor;
            if (!tutorialOutdoor && (cameraOnly || !UsesExplorationBackground(context))) return;
            JcDynamicBattleSource source = null;
            GameObject output = null;
            try
            {
                if (cameraProfile == null) throw new InvalidOperationException("전투 카메라 프로필 누락");
                string sourceEnemy = context.EventBattle?.SourceEnemyPlacementKey;
                if (enemy == null && !string.IsNullOrWhiteSpace(sourceEnemy))
                    enemy = FindObjectsOfType<EnemyGridMover>().FirstOrDefault(e =>
                        e.TryGetComponent<EnemyIdentity>(out var identity) &&
                        MapProgressKey.NormalizeSegment(identity.PlacementKey) == MapProgressKey.NormalizeSegment(sourceEnemy));

                var sourceScene = tutorialOutdoor ? SceneManager.GetActiveScene() : party.gameObject.scene;
                JcDynamicBattleSettings settings;
                if (tutorialOutdoor)
                {
                    var origin = sourceScene.GetRootGameObjects()
                        .SelectMany(r => r.GetComponentsInChildren<TutorialEnemyObject>(true))
                        .FirstOrDefault(e => e.TutorialBattleKey == context.TutorialBattleKey);
                    if (origin == null) throw new InvalidOperationException("첫 튜토리얼 전투의 탐사 조우 위치 누락");
                    settings = new JcDynamicBattleSettings { encounterPosition = origin.transform.position,
                        enemyPosition = origin.transform.position + Vector3.right, axis = JcBattleRoadAxis.X, addBattleSurfaceDetails = false };
                }
                else settings = ResolveBackgroundSettings(sourceScene, party.transform.position, enemy, context.EventBattle);
                source = JcDynamicBattleGeometry.Capture(sourceScene, tutorialOutdoor);
                settings.source = source;
                cameraProfile.viewA.Resolve(Vector3.zero, out var a, out _, out _);
                cameraProfile.viewB.Resolve(Vector3.zero, out var b, out _, out _);
                output = JcDynamicBattleGeometry.Build(source, settings, null, new[] { a, b });
                output.SetActive(false);
                output.transform.SetParent(owner.transform, false);
                bridge.background = output;
            }
            catch (Exception e)
            {
                if (output != null) Destroy(output);
                Debug.LogWarning("[JC Battle] 동적 배경 생성 실패, 기존 배경과 공통 뷰 사용: " + e.Message);
            }
            finally { if (source != null) Destroy(source); }
        }

        void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != destination) { if (mode == LoadSceneMode.Single) Destroy(gameObject); return; }
            entered = true;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            if (pending == this) pending = null;
            if (background != null)
            foreach (var root in scene.GetRootGameObjects())
                if (root != gameObject && (root.name == "Ground" || root.name == "HeroUnionZ1" || root.name.StartsWith("BGOffice", StringComparison.Ordinal) ||
                    root.name.StartsWith("BGHouse", StringComparison.Ordinal) || root.name.StartsWith("BGStore", StringComparison.Ordinal) ||
                    root.name == "JC_TutorialConstructionBackdrop"))
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            // JC | 2026-11-05 | 보스 원경은 실내 보정 대신 월드 조명으로 초기화하고 실내 수광은 별도로 유지합니다.
            if(!bossBackground && !bankSettings) JC.Environment.JcDayNightController.EnsureBattle(scene,lightingHour,lightingProfile,bankSettings!=null);
            JC.EnvironmentDetail.JcBattleRoadMarkings.BindScene(scene);
            if (background != null) background.SetActive(true);
            if (clearTutorialArenaTrees) RemoveArenaTrees(scene);
            Resources.Load<JcBattleHudLayout>("JC_BattleHud_Layout")?.Apply(scene);
            battleCamera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (battleCamera == null) return;
            // JC | 2026-11-09 | 통합: 렌더러는 공통 실내 조절기가 소유하고 기존 은행 카메라 연결·진형 이동은 보존합니다.
            // JC | 2026-11-06 | 본게임·BankTestScene의 동일 로딩 경로에서 기존 공용 연출기에 은행 구도만 전달합니다.
            // 카메라를 별도 Update에서 제어하지 않고 공격 진입·유지·복귀는 기존 연출기가 담당합니다.
            var bankPresentation = BattlePresentationController.FindInScene(scene);
            if (bankPresentation) bankPresentation.BankBackground = bankSettings;
            var roots = scene.GetRootGameObjects();
            var player = roots.FirstOrDefault(r => r.name == "PlayerPlace");
            var enemies = roots.FirstOrDefault(r => r.name == "EnemyPlace");
            center = player != null && enemies != null ? (player.transform.position + enemies.transform.position) * .5f : Vector3.zero;
            // JC | 2026-11-06 | 은행에서만 진형 전체를 카운터 반대쪽(+X)으로 실제 가로 셀 간격의 절반 이동합니다.
            // 카메라 기준점은 위의 기존 중앙을 유지합니다. 스폰 전 루트를 옮겨 셀·클릭 영역·유닛 위치가 함께 맞습니다.
            if (bankSettings && background && player && enemies)
            {
                var cells = player.GetComponentsInChildren<ASB.Work.BattleGrid.GridCell>(true);
                var leftCell = cells.Single(c => c.name == "Grid_0_0");
                var middleCell = cells.Single(c => c.name == "Grid_0_1");
                var halfCell = (middleCell.transform.position - leftCell.transform.position) * .5f;
                player.transform.position += halfCell;
                enemies.transform.position += halfCell;
            }
            originalPosition = battleCamera.transform.position; originalRotation = battleCamera.transform.rotation; originalFov = battleCamera.fieldOfView;
            var bossSettings = bossBackground ? JcBossBattleBackground.Load() : null;
            // JC | 2026-11-05 | 유리·표면과 조명 조절기를 같은 환경 루트 아래에서 찾을 수 있게 합니다.
            GameObject bossEnvironmentRoot = null;
            if ((bossSettings || bankSettings) && background)
            {
                bossEnvironmentRoot = roots.FirstOrDefault(r => r.name == "JC_Environment");
                if (!bossEnvironmentRoot)
                {
                    bossEnvironmentRoot = new GameObject("JC_Environment");
                    SceneManager.MoveGameObjectToScene(bossEnvironmentRoot, scene);
                }
            }
            // JC | 2026-11-05 | 기존 실내 조절기가 기준 환경을 잡기 전에 도시 태양과 시간대를 설정합니다.
            if(bossSettings && background){
                var cityObject=new GameObject("JC_BossCityLighting");cityObject.transform.SetParent(bossEnvironmentRoot.transform,false);
                cityObject.AddComponent<JcBossCityLighting>().Initialize(background.transform,bossSettings,lightingHour);
            }
            // JC | 2026-11-09 | 실제/테스트의 공용 진입에서 동일한 실내 조절 구조를 설치합니다.
            // 원경은 위에서 먼저 초기화하고 은행/보스 저장 프로필은 각각 그대로 전달합니다.
            if ((bossSettings || bankSettings) && background)
            {
                var controls = new GameObject("JC_IndoorEnvironment");
                controls.transform.SetParent(bossEnvironmentRoot.transform,false);
                controls.AddComponent<JcIndoorEnvironment>().Initialize(background,battleCamera,bankSettings,bossSettings);
            }
            var bossCameraProfile = bossSettings ? bossSettings.cameraProfile : null;
            if (bossCameraProfile)
            {
                var controls = new GameObject("JC_BossCamera");
                SceneManager.MoveGameObjectToScene(controls, scene);
                bossCamera = controls.AddComponent<JcBossCamera>();
                bossCamera.Initialize(bossCameraProfile, battleCamera, center, background.transform);
                profile = bossCamera.Draft;
            }
            else if (profile != null) SelectView(0);
            Debug.Log("[JC Battle] 공통 뷰 적용: " + Report, this);
        }

        public void SelectView(int view)
        {
            if (battleCamera == null || (view != 2 && profile == null) || (bossCamera && !bossCamera.CanCaptureBase)) return;
            if (bossCamera) bossCamera.EndBasePreview();
            startPosition = battleCamera.transform.position; startRotation = battleCamera.transform.rotation; startFov = battleCamera.fieldOfView;
            if (view == 2) { endPosition = originalPosition; endRotation = originalRotation; endFov = originalFov; }
            else (view == 0 ? profile.viewA : profile.viewB).Resolve(center, out endPosition, out endRotation, out endFov);
            // JC | 2026-11-06 | 은행 기본 구도만 요청한 수평 이동을 적용합니다. 스킬 카메라를 강제 제어하거나 원근감으로 Y/Z를 재계산하지 않습니다.
            if (view == 0 && bankSettings) endPosition.x += bankSettings.viewAHorizontalOffset;
            elapsed = 0; moving = true;
        }
        // 배경 외곽의 가로수는 유지하고 첫 튜토리얼의 진형/중앙 통로에 들어온 나무 외형만 제외합니다.
        internal static int RemoveArenaTrees(Scene scene)
        {
            int removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                bool tree = false;
                for (var t = renderer.transform; t != null; t = t.parent)
                    if (t.name.StartsWith("Tree", StringComparison.OrdinalIgnoreCase)) { tree = true; break; }
                var mesh = renderer.GetComponent<MeshFilter>();
                tree |= mesh != null && mesh.sharedMesh != null && mesh.sharedMesh.name.StartsWith("Tree", StringComparison.OrdinalIgnoreCase);
                var bounds = renderer.bounds;
                if (!tree || !renderer.enabled || bounds.max.x < -6.5f || bounds.min.x > 6.5f || bounds.max.z < -19 || bounds.min.z > 19) continue;
                renderer.enabled = false; removed++;
            }
            return removed;
        }
        void Update()
        {
            // sceneLoaded 이후 Start에서 생성된 외형까지 첫 Update에서 정리합니다.
            if (entered && clearTutorialArenaTrees) { RemoveArenaTrees(gameObject.scene); clearTutorialArenaTrees = false; }
            if (!entered) { if (Time.realtimeSinceStartup - createdAt > 30) Destroy(gameObject); return; }
            if (Input.GetKeyDown(KeyCode.F6)) SelectView(0);
            if (Input.GetKeyDown(KeyCode.F7)) SelectView(1);
            if (Input.GetKeyDown(KeyCode.F8)) SelectView(2);
            if (!moving || battleCamera == null) return;
            elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / .65f);
            var pos = Vector3.Lerp(startPosition,endPosition,t);
            var rot = Quaternion.Lerp(startRotation,endRotation,t);
            float fov = Mathf.Lerp(startFov,endFov,t);
            if (bossCamera)
            {
                if (!bossCamera.CanCaptureBase) { moving=false; return; }
                bossCamera.ApplyPose(pos,rot,fov);
            }
            else
            {
                battleCamera.transform.SetPositionAndRotation(pos,rot);
                battleCamera.fieldOfView = fov;
            }
            moving = t < 1;
        }
        void OnGUI()
        {
            // JC | 2026-11-09 | 개발자 안내만 Noto로 표시하고 종료 시 기존 GUI 글꼴을 복원합니다.
            using var developerFontScope = new DebugPanelManager.DeveloperGuiFontScope(GUI.skin);
            if (!entered) return;
            // 빌드 씬에서는 디버그 창이 활성화된 동안에만 카메라 전환 버튼을 그린다.
            if (gameObject.scene.buildIndex >= 0 && !DebugPanelManager.IsDebugPanelActive) return;
            if (GUI.Button(new Rect(Screen.width - 306, 8, 96, 24), "A [F6]")) SelectView(0);
            if (GUI.Button(new Rect(Screen.width - 206, 8, 96, 24), "B [F7]")) SelectView(1);
            if (GUI.Button(new Rect(Screen.width - 106, 8, 96, 24), "F8")) SelectView(2);
        }
        void OnDestroy() { if(lightingProfile!=null)Destroy(lightingProfile); SceneManager.sceneLoaded -= Loaded; if (pending == this) pending = null; }
    }
}
