using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.Tutorial
{
    // 튜토리얼 종료 진입점만 사용한다. 공용 페이드 및 일반 탐사 씬에는 설정을 남기지 않는다.
    public sealed class JcTutorialExitTransition : MonoBehaviour
    {
        private static JcTutorialExitTransition active;
        public static bool IsActive => active != null;
        public static void Begin(string destination = "DHScene_3")
        {
            if (active != null) return;
            if (!Application.CanStreamedLevelBeLoaded(destination))
            { Debug.LogError("Tutorial exit destination is unavailable: " + destination); return; }
            var host = new GameObject("Tutorial Exit Transition");
            DontDestroyOnLoad(host); active = host.AddComponent<JcTutorialExitTransition>();
            active.StartCoroutine(active.Transition(destination));
        }
        public static void HideTutorialPresentation()
        {
            foreach (var guide in FindObjectsByType<JcTutorialExploreGuide>(FindObjectsInactive.Include, FindObjectsSortMode.None)) guide.PrepareForSceneExit();
            foreach (var guide in FindObjectsByType<JcTutorialBattleGuide>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                guide.enabled = false;
                if (guide.view != null) { guide.view.SetPanelSuppressed(true); guide.view.SetVisible(false); guide.view.RenderPresentation(0,1); }
            }
        }
        private IEnumerator Transition(string destination)
        {
            ModalManager.Register(gameObject);
            // 진행 상태 초기화 전에 씬 소유 작업을 중단해 완료 콜백의 재진입을 막습니다.
            var sourceScene = SceneManager.GetActiveScene();
            foreach (var root in sourceScene.GetRootGameObjects())
            {
                foreach (var ui in root.GetComponentsInChildren<TutorialExploreUIController>(true))
                { ui.StopAllCoroutines(); ui.HideTutorial(); ui.enabled = false; }
                foreach (var turns in root.GetComponentsInChildren<TutorialTurnManager>(true))
                { turns.StopAllCoroutines(); turns.enabled = false; }
                foreach (var mover in root.GetComponentsInChildren<PartyGridMover>(true))
                { mover.StopMovement(); mover.StopAllCoroutines(); mover.enabled = false; }
            }
            JC.Environment.JcDayNightController.CancelForScene(sourceScene);
            AudioManager.Instance?.StopLoopingSfx("Explore_Move");
            HideTutorialPresentation();
            var fade = SceneFadeController.Instance;
            if (fade == null)
            {
                // 직접 씬 실행에서도 동일한 공용 페이드를 영속 인스턴스로 확보한다.
                var host = new GameObject("Scene Fade Controller");
                DontDestroyOnLoad(host); fade = host.AddComponent<SceneFadeController>();
            }
            yield return SceneFadeController.PresentLoadingScreen(.35f);
            CombatContext.Instance?.ClearTutorial();
            TutorialProgressRepository.ClearProgress();
            TutorialCatalog.DestroyAllTutorialCatalogs();
            ReleaseIncompleteExplorationCatalogs();
            // 튜토리얼 전투에서 남은 빈 일반 카탈로그가 새 씬의 정상 카탈로그를
            // 중복 싱글턴으로 제거하지 않도록 Destroy 처리가 끝난 뒤 로드한다.
            yield return null;
            var load = SceneFadeController.LoadSceneAsyncWithFade(destination, LoadSceneMode.Single, .35f, .35f);
            if (load != null) yield return load;
            // 공용 컨트롤러가 Start 이후 한 번만 페이드인한다. 자체 페이드 중복을 피한다.
            if (load != null) while (fade != null && fade.IsTransitioning) yield return null;
            else if (fade != null && !fade.IsTransitioning) yield return fade.FadeIn(.35f);
            ModalManager.Unregister(gameObject);
            Destroy(gameObject);
        }
        private static void ReleaseIncompleteExplorationCatalogs()
        {
            foreach (var catalog in FindObjectsByType<DHCsvTemplateCatalog>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (catalog.GetAllPlayerUnitTemplates().Count != 0) continue;
                // 같은 오브젝트의 다른 카탈로그와 기존 정상 탐사 데이터는 보존한다.
                Destroy(catalog);
            }
        }
        private void OnDestroy() { ModalManager.Unregister(gameObject); if (active == this) active = null; }
    }
}
