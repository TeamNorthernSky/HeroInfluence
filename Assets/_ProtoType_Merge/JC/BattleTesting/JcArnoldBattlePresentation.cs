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
        public string Report => background != null ? background.GetComponent<JcDynamicBattleOutput>().Report : string.Empty;

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

        public static void Prepare(PartyGridMover party, EnemyGridMover enemy, string battleScene)
        {
            Prepare(party, enemy, battleScene, false);
        }

        // 호출 API는 유지합니다. 튜토리얼 배경은 보존하고 공통 카메라와 HUD를 연결합니다.
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
            bridge.createdAt = Time.realtimeSinceStartup; pending = bridge;
            SceneManager.sceneLoaded += bridge.Loaded;

            // 점령/본부 전투 또는 생성 실패에도 카메라와 UI는 독립적으로 적용합니다.
            if (cameraOnly || !UsesExplorationBackground(context)) return;
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

                var settings = ResolveBackgroundSettings(party.gameObject.scene, party.transform.position, enemy, context.EventBattle);
                source = JcDynamicBattleGeometry.Capture(party.gameObject.scene);
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
                    root.name.StartsWith("BGHouse", StringComparison.Ordinal) || root.name.StartsWith("BGStore", StringComparison.Ordinal)))
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            if (background != null) background.SetActive(true);
            Resources.Load<JcBattleHudLayout>("JC_BattleHud_Layout")?.Apply(scene);
            battleCamera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (battleCamera == null) return;
            var roots = scene.GetRootGameObjects();
            var player = roots.FirstOrDefault(r => r.name == "PlayerPlace");
            var enemies = roots.FirstOrDefault(r => r.name == "EnemyPlace");
            center = player != null && enemies != null ? (player.transform.position + enemies.transform.position) * .5f : Vector3.zero;
            originalPosition = battleCamera.transform.position; originalRotation = battleCamera.transform.rotation; originalFov = battleCamera.fieldOfView;
            if (profile != null) SelectView(0);
            Debug.Log("[JC Battle] 공통 뷰 적용: " + Report, this);
        }

        public void SelectView(int view)
        {
            if (battleCamera == null || (view != 2 && profile == null)) return;
            startPosition = battleCamera.transform.position; startRotation = battleCamera.transform.rotation; startFov = battleCamera.fieldOfView;
            if (view == 2) { endPosition = originalPosition; endRotation = originalRotation; endFov = originalFov; }
            else (view == 0 ? profile.viewA : profile.viewB).Resolve(center, out endPosition, out endRotation, out endFov);
            elapsed = 0; moving = true;
        }
        void Update()
        {
            if (!entered) { if (Time.realtimeSinceStartup - createdAt > 30) Destroy(gameObject); return; }
            if (Input.GetKeyDown(KeyCode.F6)) SelectView(0);
            if (Input.GetKeyDown(KeyCode.F7)) SelectView(1);
            if (Input.GetKeyDown(KeyCode.F8)) SelectView(2);
            if (!moving || battleCamera == null) return;
            elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / .65f);
            battleCamera.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, t), Quaternion.Lerp(startRotation, endRotation, t));
            battleCamera.fieldOfView = Mathf.Lerp(startFov, endFov, t); moving = t < 1;
        }
        void OnGUI()
        {
            if (!entered) return;
            // 빌드 씬에서는 디버그 창이 활성화된 동안에만 카메라 전환 버튼을 그린다.
            if (gameObject.scene.buildIndex >= 0 && !DebugPanelManager.IsDebugPanelActive) return;
            if (GUI.Button(new Rect(Screen.width - 306, 8, 96, 24), "A [F6]")) SelectView(0);
            if (GUI.Button(new Rect(Screen.width - 206, 8, 96, 24), "B [F7]")) SelectView(1);
            if (GUI.Button(new Rect(Screen.width - 106, 8, 96, 24), "F8")) SelectView(2);
        }
        void OnDestroy() { SceneManager.sceneLoaded -= Loaded; if (pending == this) pending = null; }
    }
}
