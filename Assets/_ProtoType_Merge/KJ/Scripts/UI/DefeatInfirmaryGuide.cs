using JC.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>일반 전투 패배 후 협회에 돌아오면 의무실을 튜토리얼 효과로 안내한다.</summary>
[DisallowMultipleComponent]
public sealed class DefeatInfirmaryGuide : MonoBehaviour
{
    private static bool pending;
    private FacilityModule facility;
    private Button boundButton;
    private JcTutorialGuideView view;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        pending = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void Request(CombatContext context)
    {
        if (context != null && !context.IsTutorial && !context.IsSimulation && !DHGameEndState.IsEnding)
            pending = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var scenes = GameSceneManager.Instance;
        if (DHGameEndState.IsEnding || scene.name == (scenes != null ? scenes.TitleScene : "TitleScene")
            || scene.name == (scenes != null ? scenes.GameLoadScene : "GameLoadScene"))
        {
            pending = false;
            return;
        }
        if (!pending || scene.name != (scenes != null ? scenes.LobbyScene : "HQLobbyScene")) return;
        // UIComposer creates and registers the facilities during scene Awake.
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<DefeatInfirmaryGuide>() != null) return;
        var host = new GameObject("[KJ_DefeatInfirmaryGuide]");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<DefeatInfirmaryGuide>();
    }

    private void Update()
    {
        if (!pending || DHGameEndState.IsEnding)
        {
            Destroy(gameObject);
            return;
        }
        if (facility == null)
        {
            foreach (var candidate in LobbyUIRegistry.Facilities)
                if (candidate != null && candidate.Department == HQDepartment.Infirmary)
                {
                    BindFacility(candidate);
                    break;
                }
        }
        if (facility == null || view == null) return;
        if (facility.FacilityModal != null && facility.FacilityModal.activeInHierarchy)
        {
            Complete();
            return;
        }
        bool building = LobbyUIRegistry.BuildMode != null && LobbyUIRegistry.BuildMode.IsBuildMode;
        view.SetVisible(!building, boundButton != null ? boundButton.transform as RectTransform : null,
            JcTutorialGuideView.FocusPresentation.PointerAbove);
    }

    private void BindFacility(FacilityModule target)
    {
        var button = target.UnlockedButton;
        if (button == null || button.transform.parent is not RectTransform) return;
        facility = target;
        boundButton = button;
        boundButton.onClick.AddListener(OnFacilityClicked);

        var overlay = new GameObject("DefeatInfirmaryHighlight", typeof(RectTransform), typeof(CanvasGroup));
        overlay.layer = button.gameObject.layer;
        var rect = (RectTransform)overlay.transform;
        rect.SetParent(button.transform.parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var group = overlay.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        view = overlay.AddComponent<JcTutorialGuideView>();
        view.focusFrame = CreateGraphic("FocusFrame", JcTutorialGraphic.Shape.Frame, 6, 18);
        view.pointer = CreateGraphic("Pointer", JcTutorialGraphic.Shape.Pointer, 0, 0);
        view.pointer.rectTransform.sizeDelta = new Vector2(28, 20);
        view.convergingOutlines = new[]
        {
            CreateGraphic("ConvergingOutline1", JcTutorialGraphic.Shape.Frame, 3, 14),
            CreateGraphic("ConvergingOutline2", JcTutorialGraphic.Shape.Frame, 3, 14)
        };
        view.SetVisible(true, button.transform as RectTransform);
    }

    private JcTutorialGraphic CreateGraphic(string name, JcTutorialGraphic.Shape shape, float width, float glow)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.layer = view.gameObject.layer;
        child.transform.SetParent(view.transform, false);
        var graphic = child.AddComponent<JcTutorialGraphic>();
        graphic.Configure(shape, view.accent, 16, width);
        graphic.SetGlowWidth(glow);
        return graphic;
    }

    private void OnFacilityClicked()
    {
        // Build mode may already have exited in the facility's earlier click listener.
        // Dismiss only when treatment actually opened, not when upgrade information opened.
        if (facility != null && facility.FacilityModal != null && facility.FacilityModal.activeInHierarchy)
            Complete();
    }

    private void Complete()
    {
        pending = false;
        if (view != null) view.SetVisible(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (boundButton != null) boundButton.onClick.RemoveListener(OnFacilityClicked);
        if (view != null) Destroy(view.gameObject);
    }

    private void OnDisable()
    {
        if (view != null) view.SetVisible(false);
    }
}
