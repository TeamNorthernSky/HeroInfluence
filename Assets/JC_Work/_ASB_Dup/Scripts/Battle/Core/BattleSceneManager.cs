using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

public class BattleSceneManager : MonoBehaviour
{
    [Header("Prototype Boot")]
    [Tooltip("Prototype 전용: 씬에 배치된 BattleCharactor를 그대로 초기화해 전투를 시작합니다.")]
    [SerializeField] private bool includeInactiveUnits = true;
    [SerializeField] private Transform playerPlace;
    [SerializeField] private Transform enemyPlace;
    [SerializeField] private BattleFlowManager battleFlowManager;

    [Header("Boot")]
    [SerializeField] private bool createOnStart = true;

    private BattleCharactor playerBattleCharactor;
    private readonly List<BattleCharactor> playerBattleCharactors = new List<BattleCharactor>();
    private readonly List<BattleCharactor> enemyBattleCharactors = new List<BattleCharactor>();

    public BattleCharactor PlayerBattleCharactor => playerBattleCharactor;

    /// <summary>디버그/멀티 플레이어 전투체. 첫 번째 플레이어는 <see cref="PlayerBattleCharactor"/>와 동일하게 유지.</summary>
    public IReadOnlyList<BattleCharactor> PlayerBattleCharactors => playerBattleCharactors;

    /// <summary>소환된 적 전투체 목록.</summary>
    public IReadOnlyList<BattleCharactor> EnemyBattleCharactors => enemyBattleCharactors;

    private void Start()
    {
        if (!createOnStart)
        {
            return;
        }

        if (battleFlowManager == null)
        {
            Debug.LogError("[BattleSceneManager] battleFlowManager가 할당되지 않았습니다.");
            return;
        }

        playerBattleCharactor = null;
        playerBattleCharactors.Clear();
        enemyBattleCharactors.Clear();

        var inactiveMode = includeInactiveUnits ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        var sceneUnits = FindObjectsByType<BattleCharactor>(inactiveMode, FindObjectsSortMode.None).ToList();
        if (sceneUnits.Count == 0)
        {
            Debug.LogWarning("[BattleSceneManager] 씬에서 BattleCharactor를 찾지 못했습니다.");
            return;
        }

        // 그리드 ↔ 유닛 점유 동기화 후, 각 BattleCharactor.Initialize()로 스탯·스킬·무기 확정
        SyncGridOccupancy(sceneUnits);
        CollectParticipantsAfterInitialize(sceneUnits);

        var allUnits = new List<BattleCharactor>(playerBattleCharactors.Count + enemyBattleCharactors.Count);
        allUnits.AddRange(playerBattleCharactors);
        allUnits.AddRange(enemyBattleCharactors);

        battleFlowManager.Initialize(allUnits);
    }

    /// <summary>
    /// 프로토타입 부트: 씬에 배치된 유닛에 대해 <see cref="BattleCharactor.Initialize"/> 호출 후,
    /// 셀이 연결된 유닛만 플레이어/적 목록에 넣습니다.
    /// </summary>
    private void CollectParticipantsAfterInitialize(List<BattleCharactor> sceneUnits)
    {
        for (int i = 0; i < sceneUnits.Count; i++)
        {
            var battle = sceneUnits[i];
            if (battle == null)
            {
                continue;
            }

            battle.Initialize();
            if (battle.OccupiedCell == null)
            {
                Debug.LogWarning($"[BattleSceneManager] 셀 미연결 유닛은 참가 제외: {battle.UnitName} ({battle.name})");
                continue;
            }

            if (battle.TeamType == TeamType.Player)
            {
                playerBattleCharactors.Add(battle);
                if (playerBattleCharactor == null)
                {
                    playerBattleCharactor = battle;
                }
            }
            else
            {
                enemyBattleCharactors.Add(battle);
            }
        }
    }

    /// <summary>
    /// Prototype 전용: GridCell → BattleCharactor 하이어러키에 맞춰 OccupiedCell ↔ OccupyingUnit을 연결합니다.
    /// </summary>
    private void SyncGridOccupancy(List<BattleCharactor> sceneUnits)
    {
        // 1. 기존 점유 정보 해제
        for (int i = 0; i < sceneUnits.Count; i++)
        {
            if (sceneUnits[i] != null)
            {
                sceneUnits[i].ClearOccupiedCell();
            }
        }

        // 3. playerPlace / enemyPlace 하위 GridCell 수집
        var allCells = new List<GridCellRef>();
        CollectCells(playerPlace, allCells);
        CollectCells(enemyPlace, allCells);

        // 4. 각 셀에 대해 자식 BattleCharactor를 찾아 AssignToCell
        for (int i = 0; i < allCells.Count; i++)
        {
            var cell = allCells[i];
            if (cell == null)
            {
                continue;
            }

            BattleCharactor[] found = cell.GetComponentsInChildren<BattleCharactor>(includeInactiveUnits);
            if (found.Length > 1)
            {
                Debug.LogError(
                    $"[BattleSceneManager] GridCell 중복 점유 감지: cell={cell.name}, coords={cell.Coords}, units={found.Length}");
            }

            var unit = found.FirstOrDefault(x => x != null);
            if (unit == null)
            {
                continue;
            }

            unit.AssignToCell(cell);
        }

        for (int i = 0; i < sceneUnits.Count; i++)
        {
            var unit = sceneUnits[i];
            if (unit == null)
            {
                continue;
            }

            if (unit.OccupiedCell == null)
            {
                Debug.LogWarning(
                    $"[BattleSceneManager] GridCell과 연결되지 않은 유닛: {unit.UnitName} ({unit.name})");
            }
        }
    }

    private void CollectCells(Transform root, List<GridCellRef> buffer)
    {
        if (root == null)
        {
            return;
        }

        var cells = root.GetComponentsInChildren<GridCellRef>(includeInactiveUnits);
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            if (c == null)
            {
                continue;
            }

            if (!buffer.Contains(c))
            {
                buffer.Add(c);
            }
        }
    }
}
