using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BattleSimulationScene(최소 설정 씬)에 배치한다.
/// SimulationBattleConfig(SO)를 읽어 transient 아군을 만들고 CombatContext를 Simulation 모드로 설정한 뒤,
/// 기존 전투 씬(TmpBattleScene)을 로드한다. 영속 리포지토리에는 아무것도 쓰지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SimulationBattleLauncher : MonoBehaviour
{
    public const string BattleSceneName = "TmpBattleScene";
    public const string ReturnSceneName = "BattleSimulationScene";

    [SerializeField] private SimulationBattleConfig config;
    [Tooltip("ON이면 Start()에서 즉시 모의 전투를 시작한다(Play만 누르면 됨).")]
    [SerializeField] private bool autoStartOnPlay = false;

    public SimulationBattleConfig Config { get => config; set => config = value; }

    private void Start()
    {
        // 이전 실행에서 남은 컨텍스트가 있으면 정리(안전). 리포지토리는 건드리지 않는다.
        CombatContext ctx = CombatContext.Instance;
        if (ctx != null && ctx.IsSimulation)
            ctx.ClearSimulation();

        if (autoStartOnPlay)
            Launch();
    }

    /// <summary>UI 버튼 등에서 호출. 실패 시 로그만 남기고 씬은 이동하지 않는다.</summary>
    public bool Launch()
    {
        if (!TryBuildAndBegin(out string error))
        {
            Debug.LogError($"[SimulationBattleLauncher] 모의 전투 시작 실패: {error}", this);
            return false;
        }
        return true;
    }

    /// <summary>검증 → transient 아군 생성 → CombatContext.BeginSimulation → 전투 씬 로드.</summary>
    public bool TryBuildAndBegin(out string error)
    {
        error = string.Empty;

        if (config == null)
        {
            error = "SimulationBattleConfig가 할당되지 않았습니다.";
            return false;
        }

        CombatContext context = CombatContext.Instance;
        if (context == null)
        {
            error = "CombatContext를 찾을 수 없습니다.";
            return false;
        }

        if (config.allies == null || config.allies.Count == 0)
        {
            error = "아군이 비어 있습니다(최소 1명).";
            return false;
        }

        var runtimeAllies = new List<SimulationAllyRuntimeData>();
        int slot = 0;
        for (int i = 0; i < config.allies.Count; i++)
        {
            SimulationAllyInput input = config.allies[i];
            if (input == null || !input.Enabled)
                continue;

            if (!SimulationUnitFactory.TryCreateRuntimeData(input, slot, out SimulationAllyRuntimeData runtimeData, out string createError))
            {
                error = $"아군 슬롯 {i + 1}: {createError}";
                return false;
            }

            runtimeAllies.Add(runtimeData);
            slot++;
        }

        if (runtimeAllies.Count == 0)
        {
            error = "활성화된 아군이 없습니다(최소 1명).";
            return false;
        }

        if (runtimeAllies.Count > 6)
        {
            error = "아군은 최대 6명까지입니다.";
            return false;
        }

        string enemyGroupKey = string.IsNullOrWhiteSpace(config.enemyGroupKey)
            ? string.Empty
            : config.enemyGroupKey.Trim();
        if (!SimulationUnitFactory.ValidateEnemyGroup(enemyGroupKey, out string enemyError))
        {
            error = enemyError;
            return false;
        }

        int enemyLevel = Mathf.Clamp(config.enemyLevel, 1, SimulationUnitFactory.MaxSimulationLevel);
        string partyId = "SIM_" + Guid.NewGuid().ToString("N");

        if (!context.BeginSimulation(partyId, runtimeAllies, enemyGroupKey, enemyLevel, ReturnSceneName))
        {
            context.ClearSimulation();
            error = "CombatContext.BeginSimulation이 실패했습니다.";
            return false;
        }

        if (!TryLoadBattleScene(out string loadError))
        {
            // 씬 로드 실패 시 컨텍스트만 되돌린다(리포지토리 부작용 없음).
            context.ClearSimulation();
            error = loadError;
            return false;
        }

        return true;
    }

    private bool TryLoadBattleScene(out string error)
    {
        error = string.Empty;
        try
        {
            if (GameSceneManager.Instance != null)
                GameSceneManager.Instance.LoadScene(BattleSceneName);
            else
                SceneManager.LoadScene(BattleSceneName);
            return true;
        }
        catch (Exception ex)
        {
            error = $"전투 씬 로드 실패: {ex.Message}";
            Debug.LogException(ex, this);
            return false;
        }
    }
}
