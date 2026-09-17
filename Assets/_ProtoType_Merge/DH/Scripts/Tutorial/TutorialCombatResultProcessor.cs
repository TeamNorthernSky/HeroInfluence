using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialCombatResultProcessor : MonoBehaviour
{
    private Coroutine pendingProcess;

    private void OnEnable()
    {
        pendingProcess = StartCoroutine(ProcessAfterSceneReady());
    }

    private void OnDisable()
    {
        if (pendingProcess == null)
            return;

        StopCoroutine(pendingProcess);
        pendingProcess = null;
    }

    private IEnumerator ProcessAfterSceneReady()
    {
        yield return null;
        yield return null;
        pendingProcess = null;

        CombatContext context = CombatContext.Instance;
        if (context == null || !context.IsTutorial || context.Result == CombatResult.None)
            yield break;

        TutorialProgressRepository repository = TutorialProgressRepository.EnsureInstance();
        repository.SetLastCombatResult(context.Result);
        repository.ApplyPendingCombatResult(context.Result);
        context.ClearTutorial();
    }
}
