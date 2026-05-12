using UnityEngine;
using UnityEngine.SceneManagement;

// [JC 신설 260512]
// 옵션 B 진입 트리거. CombatEncounterManager.BeginCombat이 더 이상 LoadScene을 호출하지 않으므로,
// CombatStarted 이벤트를 구독해 전투씬을 로드한다. ASB 측 정식 구현 도입 시 폐기 가능.
[DisallowMultipleComponent]
public class CombatSceneEntryController : MonoBehaviour
{
    [SerializeField] private CombatEncounterManager combatEncounterManager;
    [SerializeField] private string battleSceneName = "TmpBattleScene";

    private void Awake()
    {
        if (combatEncounterManager == null)
            combatEncounterManager = FindFirstObjectByType<CombatEncounterManager>();
    }

    private void OnEnable()
    {
        if (combatEncounterManager != null)
            combatEncounterManager.CombatStarted += HandleCombatStarted;
    }

    private void OnDisable()
    {
        if (combatEncounterManager != null)
            combatEncounterManager.CombatStarted -= HandleCombatStarted;
    }

    private void HandleCombatStarted(PartyGridMover party, EnemyGridMover enemy)
    {
        if (string.IsNullOrWhiteSpace(battleSceneName))
            return;

        SceneManager.LoadScene(battleSceneName);
    }
}
