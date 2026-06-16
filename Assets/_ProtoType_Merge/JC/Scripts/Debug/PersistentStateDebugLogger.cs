using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [Phase A 검증용] 씬 전환 + 명시 시점에 PersistentUnitRepository / PersistentEnemyRepository 상태를 덤프.
/// 자동 등록: RuntimeInitializeOnLoadMethod 로 BootScene 진입 시 자체적으로 GameObject 생성 + DontDestroyOnLoad.
/// 명시 호출: PersistentStateDebugLogger.Dump("label") 로 임의 시점 캡처 가능.
/// 검증 완료 후 본 파일 + 호출처 5건 제거 예정.
/// </summary>
public class PersistentStateDebugLogger : MonoBehaviour
{
    private const string GameObjectName = "[PersistentStateDebugLogger]";

    private static PersistentStateDebugLogger instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (instance != null) return;

        GameObject host = new GameObject(GameObjectName);
        DontDestroyOnLoad(host);
        instance = host.AddComponent<PersistentStateDebugLogger>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 한 프레임 대기 후 dump — PartyUnitBootstrap.Start 등의 Bootstrap 로직이 끝난 상태를 캡처
        StartCoroutine(DumpAfterFrame($"SceneLoaded: {scene.name}"));
    }

    private System.Collections.IEnumerator DumpAfterFrame(string label)
    {
        yield return null;
        Dump(label);
    }

    public static void Dump(string label)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"=== PersistentState Dump @ {label} ===");

        AppendUnitRepositoryState(sb);
        AppendEnemyRepositoryState(sb);

        Debug.Log(sb.ToString());
    }

    // [JC 수정 260512] 머지 사이클: 파티/적부대/전투컨텍스트 책임이 분리됨
    //   - 파티 → PartyPersistentRepository
    //   - 적 부대 → EnemyGroupPersistentRepository
    //   - 전투 컨텍스트(CombatParty/CombatEnemy) → CombatContext
    private static void AppendUnitRepositoryState(StringBuilder sb)
    {
        PersistentUnitRepository repo = PersistentUnitRepository.Instance;
        PartyPersistentRepository partyRepo = PartyPersistentRepository.Instance;
        CombatContext combatContext = CombatContext.Instance;

        if (repo == null)
        {
            sb.AppendLine("[Unit] Repository is null");
            return;
        }

        int partyCount = partyRepo != null ? partyRepo.Parties.Count : 0;
        sb.AppendLine($"[Unit] units={repo.Units.Count}, parties={partyCount}");

        for (int i = 0; i < repo.Units.Count; i++)
        {
            UnitPersistentData u = repo.Units[i];
            if (u == null) continue;
            StatBlock s = u.BaseStats;
            sb.AppendLine(
                $"  unit[{u.UnitIndex}] tpl='{u.UnitTemplateKey}' Lv{u.Level} IP{u.CurrentInfluence:0.#} " +
                $"HP{s.HP} Atk{s.Atk} DEF{s.DEF} Spd{s.Speed} " +
                $"skill={u.CurrentSkillIndex} weapon={u.CurrentWeaponIndex}");
        }

        if (partyRepo != null)
        {
            for (int i = 0; i < partyRepo.Parties.Count; i++)
            {
                PartyPersistentData p = partyRepo.Parties[i];
                if (p == null) continue;
                sb.AppendLine($"  party[{p.PartyId}] units=[{FormatIndices(p.UnitIndices)}]");
            }
        }
        else
        {
            sb.AppendLine("  (PartyPersistentRepository is null)");
        }

        CombatPartyPersistentData cp = combatContext != null ? combatContext.CombatParty : null;
        if (cp != null)
            sb.AppendLine($"  combatParty=[{cp.PartyId}] units=[{FormatIndices(cp.UnitIndices)}]");
        else
            sb.AppendLine("  combatParty=(null)");
    }

    private static void AppendEnemyRepositoryState(StringBuilder sb)
    {
        PersistentEnemyRepository repo = PersistentEnemyRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepo = EnemyGroupPersistentRepository.Instance;
        CombatContext combatContext = CombatContext.Instance;

        if (repo == null)
        {
            sb.AppendLine("[Enemy] Repository is null");
            return;
        }

        int enemyCount = enemyGroupRepo != null ? enemyGroupRepo.Enemies.Count : 0;
        sb.AppendLine($"[Enemy] units={repo.Units.Count}, enemies={enemyCount}");

        for (int i = 0; i < repo.Units.Count; i++)
        {
            EnemyUnitPersistentData u = repo.Units[i];
            if (u == null) continue;
            StatBlock s = u.BaseStats;
            sb.AppendLine(
                $"  unit[{u.UnitIndex}] tpl='{u.UnitTemplateKey}' Lv{u.Level} " +
                $"HP{s.HP} Atk{s.Atk} DEF{s.DEF} Spd{s.Speed}");
        }

        if (enemyGroupRepo != null)
        {
            for (int i = 0; i < enemyGroupRepo.Enemies.Count; i++)
            {
                EnemyPersistentData e = enemyGroupRepo.Enemies[i];
                if (e == null) continue;
                sb.AppendLine($"  enemy[{e.EnemyId}] units=[{FormatIndices(e.UnitIndices)}]");
            }
        }
        else
        {
            sb.AppendLine("  (EnemyGroupPersistentRepository is null)");
        }

        CombatEnemyPersistentData ce = combatContext != null ? combatContext.CombatEnemy : null;
        if (ce != null)
            sb.AppendLine($"  combatEnemy=[{ce.EnemyId}] units=[{FormatIndices(ce.UnitIndices)}]");
        else
            sb.AppendLine("  combatEnemy=(null)");
    }

    private static string FormatIndices(System.Collections.Generic.IReadOnlyList<int> indices)
    {
        if (indices == null || indices.Count == 0) return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < indices.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(indices[i]);
        }
        return sb.ToString();
    }
}
