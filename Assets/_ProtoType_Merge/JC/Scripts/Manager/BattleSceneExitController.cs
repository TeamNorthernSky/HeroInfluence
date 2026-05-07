using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class BattleSceneExitController : MonoBehaviour
{
    [SerializeField] private BattleFlowManager battleFlowManager;
    [SerializeField] private string returnSceneName = "DHScene";
    [SerializeField] private float exitDelaySeconds = 1.5f;

    private bool exitScheduled;

    private void Awake()
    {
        if (battleFlowManager == null)
            battleFlowManager = FindFirstObjectByType<BattleFlowManager>();
    }

    private void OnEnable()
    {
        if (battleFlowManager != null)
            battleFlowManager.OnBattleEnded += HandleBattleEnded;
    }

    private void OnDisable()
    {
        if (battleFlowManager != null)
            battleFlowManager.OnBattleEnded -= HandleBattleEnded;
    }

    private void HandleBattleEnded(BattleResult result)
    {
        if (exitScheduled) return;
        exitScheduled = true;
        Debug.Log($"[BattleSceneExitController] Battle ended ({result}). Returning to '{returnSceneName}' in {exitDelaySeconds:F1}s.");
        StartCoroutine(ExitAfterDelay());
    }

    private IEnumerator ExitAfterDelay()
    {
        yield return new WaitForSecondsRealtime(exitDelaySeconds);

        var encounter = FindFirstObjectByType<CombatEncounterManager>();
        if (encounter != null)
            encounter.ClearCombatState();

        SceneManager.LoadScene(returnSceneName);
    }
}
