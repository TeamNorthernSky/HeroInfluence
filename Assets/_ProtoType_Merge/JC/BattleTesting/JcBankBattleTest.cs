using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BattleTesting
{
    // JC | 2026-11-05 | 은행 폴리싱도 보스 테스트처럼 본게임 씬/데이터/표시 연결부를 사용합니다.
    // 이 컴포넌트는 진입 설정만 소유하며 카탈로그·배경·전투 규칙을 별도로 복제하지 않습니다.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class JcBankBattleTest : MonoBehaviour
    {
        public enum BankEncounter
        {
            [InspectorName("인질 구출 · BE002")] HostageRescue = 2,
            [InspectorName("정면 돌파 · BE003")] FrontalAssault = 3,
            [InspectorName("루미나 폭주 · BE004")] LuminaRampage = 4
        }

        [InspectorName("은행 전투"), Tooltip("본게임 1구역 은행 점령전입니다. Play를 시작하면 TmpBattleScene으로 바로 진입합니다.")]
        public BankEncounter encounter = BankEncounter.FrontalAssault;
        [InspectorName("테스트 아군"), Tooltip("임시 파티의 영웅 키·레벨·장비·시작 HP입니다. 실제 저장 데이터와 보상에는 반영하지 않습니다.")]
        public SimulationAllyInput[] allies = {
            new SimulationAllyInput { Enabled = true, UnitTemplateKey = "10001", Level = 8 },
            new SimulationAllyInput { Enabled = true, UnitTemplateKey = "10002", Level = 8 },
            new SimulationAllyInput { Enabled = true, UnitTemplateKey = "10003", Level = 8 },
            new SimulationAllyInput { Enabled = true, UnitTemplateKey = "10004", Level = 8 }
        };

        public string BattleKey => "BE" + ((int)encounter).ToString("000");

        // JC | 2026-11-05 | 카탈로그는 씬에 연결된 기존 컴포넌트/원본 SO를 그대로 읽습니다.
        // 데이터 누락 시 다른 전투나 임시 배경으로 대체하지 않고 진입 전에 원인을 표시합니다.
        public bool TryBuildRequest(out List<SimulationAllyRuntimeData> party, out int enemyLevel, out string error)
        {
            party = new List<SimulationAllyRuntimeData>(); enemyLevel = 0; error = string.Empty;
            var bank = JcBankBattleBackground.Load();
            if (!bank || !bank.visualPrefab || !bank.Matches(false, null, bank.zoneId, BattleKey))
            { error = "본게임 은행 배경 또는 전투 키 연결을 확인하세요: " + BattleKey; return false; }
            if (!DHCsvTemplateCatalog.Instance || !EventScriptCatalog.Instance)
            { error = "씬의 기존 데이터·이벤트 카탈로그가 준비되지 않았습니다."; return false; }
            DHCsvTemplateCatalog.Instance.ReloadTemplates(); EventScriptCatalog.Instance.Reload();
            var enabled = (allies ?? Array.Empty<SimulationAllyInput>()).Where(a => a != null && a.Enabled).ToArray();
            if (enabled.Length < 1 || enabled.Length > 6)
            { error = "테스트 아군을 1~6명 지정하세요."; return false; }
            foreach (var input in enabled)
            {
                if (!SimulationUnitFactory.TryCreateRuntimeData(input, party.Count, out var unit, out error)) return false;
                party.Add(unit);
            }
            // JC | 2026-11-05 | 적 편성과 특수 시나리오·레벨 제한은 기존 본게임 모의전 경로로 해석합니다.
            enemyLevel = YT.Simulation.YtOutpostSimulation.ResolveEnemyLevel(bank.zoneId, BattleKey, party.Average(a => a.UnitData.Level));
            return SimulationUnitFactory.ValidateEnemy(BattleKey, bank.zoneId, enemyLevel, out error);
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!TryBuildRequest(out var party, out int enemyLevel, out string error))
            { Debug.LogError("[JC 은행 테스트] " + error, this); return; }
            var context = CombatContext.EnsureInstance();
            if (!context.BeginSimulation("JC_BANK_PREVIEW", party, BattleKey, enemyLevel, "TitleScene", JcBankBattleBackground.Load().zoneId))
            { Debug.LogError("[JC 은행 테스트] 테스트 컨텍스트 생성 실패", this); return; }
            try
            {
                // JC | 2026-11-05 | 본게임 분기가 은행 프리팹·공통 카메라·은행 AO를 적용합니다.
                // 테스트용 HUD/배경 사본을 유지하지 않으므로 이후 본게임 폴리싱도 그대로 반영됩니다.
                JcArnoldBattlePresentation.PrepareCameraOnly(SimulationBattleLauncher.BattleSceneName);
                SceneManager.LoadScene(SimulationBattleLauncher.BattleSceneName);
            }
            catch (Exception exception)
            {
                JcArnoldBattlePresentation.CancelPending(); context.ClearSimulation();
                Debug.LogException(exception, this);
            }
#endif
        }
    }
}
