using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.Indicators
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("JC Indicators/탐사 오브젝트 호버 윤곽선 설정")]
    public sealed class JcInteractionHoverOutlineController : MonoBehaviour
    {
        [SerializeField, InspectorName("오브젝트 윤곽선 설정")]
        private JcInteractionHoverOutlineValues values = new JcInteractionHoverOutlineValues();
        public JcInteractionHoverOutlineValues Values => values;
        public void LoadSavedSettings()
        {
            var profile = Resources.Load<JcInteractionHoverOutlineProfile>("JC_InteractionHover/" + gameObject.scene.name);
            if (profile != null) values = profile.values.Copy();
        }
        private void OnEnable() => LoadSavedSettings();
        private void Start()
        {
            if (!Application.isPlaying) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var target in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (IsInteraction(target)) JcInteractionHoverMeshes.Prepare(target.transform);
        }

        private static bool IsInteraction(MonoBehaviour target) =>
            target is IGridItemObject || target is IGridEnemyObject || target is HeroUnionUnit ||
            target is VillainUnionBase || target is Outpost || target is TutorialOutpostObject ||
            target is TutorialBuildingObject || target is MainEventObject || target is SubEventObject ||
            target is MapEventObject || target is WorldEventObject;

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            var clicks = ClickSelectionController.Active;
            var target = clicks != null && clicks.gameObject.scene == gameObject.scene &&
                gameObject.scene == SceneManager.GetActiveScene()
                ? clicks.GetOutlineHoverTarget(JcPointerInput.ScreenPosition, values.showUnreachable) : null;
            if (target is HeroUnionUnit || target is Outpost outpost && outpost.IsPlayerClaimed ||
                target is TutorialOutpostObject tutorial && tutorial.CurrentClaimState == TutorialOutpostClaimState.HeroClaimed)
                target = null;
            if (target != null)
                JcBuildingSilhouetteFeature.SetInteractionHighlight(this, target.transform, JcInteractionHoverMeshes.Prepare(target.transform));
            else
                JcBuildingSilhouetteFeature.ClearInteractionHighlight(this);
        }

        private void OnDisable() => JcBuildingSilhouetteFeature.ClearInteractionHighlight(this);
    }
}
