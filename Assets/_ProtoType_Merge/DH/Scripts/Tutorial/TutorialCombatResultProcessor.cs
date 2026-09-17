using System.Collections;
using System.Collections.Generic;
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

        TutorialProgressRepository repository = TutorialProgressRepository.Instance;
        if (repository == null)
        {
            Debug.LogError(
                "[TutorialCombatResultProcessor] The DontDestroyOnLoad tutorial repository is missing. " +
                "The combat result will not be cleared.",
                this);
            yield break;
        }

        repository.SetLastCombatResult(context.Result);
        repository.ApplyPendingCombatResult(context.Result);
        context.ClearTutorial();
    }

    public static bool TryPersistAllies(
        IReadOnlyList<BattleCharactor> playerUnits,
        out string error)
    {
        error = string.Empty;

        TutorialProgressRepository repository = TutorialProgressRepository.Instance;
        if (repository == null)
        {
            error = "The DontDestroyOnLoad tutorial repository is missing.";
            return false;
        }

        if (playerUnits == null || playerUnits.Count == 0)
        {
            error = "No tutorial allies were supplied.";
            return false;
        }

        int savedCount = 0;
        for (int i = 0; i < playerUnits.Count; i++)
        {
            BattleCharactor player = playerUnits[i];
            UnitPersistentData source = player != null ? player.SourceData : null;
            if (player == null || source == null || string.IsNullOrWhiteSpace(source.UnitTemplateKey))
            {
                error = $"Tutorial ally at index {i} has no persistent source identity.";
                return false;
            }

            string unitTemplateKey = source.UnitTemplateKey.Trim();
            int maxHp = Mathf.Max(1, Mathf.CeilToInt(player.MaxHp));
            int currentHp = player.IsDead
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt(player.CurrentHp), 1, maxHp);
            int maxIp = Mathf.Max(0, Mathf.RoundToInt(player.MaxInfluence));
            int currentIp = Mathf.Clamp(Mathf.RoundToInt(player.CurrentInfluence), 0, maxIp);
            int atk = Mathf.Max(0, Mathf.RoundToInt(source.IngameStats.Atk));

            repository.SetUnitJoined(unitTemplateKey, true);
            repository.SetUnitStats(unitTemplateKey, currentHp, maxHp, currentIp, maxIp, atk);
            savedCount++;
        }

        if (savedCount == 0)
        {
            error = "No tutorial ally state was saved.";
            return false;
        }

        return true;
    }
}
