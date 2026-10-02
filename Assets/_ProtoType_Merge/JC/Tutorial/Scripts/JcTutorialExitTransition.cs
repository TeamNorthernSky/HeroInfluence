using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.Tutorial
{
    // 튜토리얼 종료 진입점만 사용한다. 공용 페이드 및 일반 탐사 씬에는 설정을 남기지 않는다.
    public sealed class JcTutorialExitTransition : MonoBehaviour
    {
        private static JcTutorialExitTransition active;
        public static void Begin(string destination = "DHScene_3")
        {
            if (active != null) return;
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
            HideTutorialPresentation();
            var fade = SceneFadeController.Instance;
            if (fade == null)
            {
                // 직접 씬 실행에서도 동일한 공용 페이드를 영속 인스턴스로 확보한다.
                var host = new GameObject("Scene Fade Controller");
                DontDestroyOnLoad(host); fade = host.AddComponent<SceneFadeController>();
            }
            yield return fade.FadeOut(.35f);
            TutorialProgressRepository.ClearProgress();
            TutorialCatalog.DestroyAllTutorialCatalogs();
            ReleaseIncompleteExplorationCatalogs();
            // 튜토리얼 전투에서 남은 빈 일반 카탈로그가 새 씬의 정상 카탈로그를
            // 중복 싱글턴으로 제거하지 않도록 Destroy 처리가 끝난 뒤 로드한다.
            yield return null;
            var load = GameSceneManager.Instance != null ? GameSceneManager.Instance.LoadSceneAsync(destination) : SceneFadeController.LoadSceneAsyncWithFade(destination);
            if (load != null) yield return load;
            // 새 씬의 Start 및 첫 UI 배치를 암전 안에서 마친다.
            yield return null;
            yield return null;
            yield return fade.FadeIn(.35f);
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
