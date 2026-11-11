using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-10 | 적 추가는 시뮬레이션에만 허용하고 율리아는 목록과 확정 양쪽에서 차단합니다.
    public static class JcSimulationEnemyPlacement
    {
        public static bool IsAllowed(string key)
        {
            string normalized = (key ?? "").Trim().ToUpperInvariant();
            if (normalized.StartsWith("FV")) normalized = normalized.Substring(2);
            return int.TryParse(normalized, out int value) && value != 40001;
        }

        public static bool TryApply(JcSimulationFormation formation, IReadOnlyDictionary<int, string> additions,
            BattleFlowManager flow, EnemySpawner spawner, out string error)
        {
            error = "";
            if (!JcSimulationBattleControls.IsSimulationSession || !flow || !spawner)
            { error = "시뮬레이션 전투에서만 적을 추가할 수 있습니다."; return false; }
            var entries = new Dictionary<int, EnemySpawnEntry>();
            var slots = new Dictionary<int, int>();
            foreach (var item in additions)
            {
                if (item.Key < 0 || item.Key >= formation.Cells.Length || !IsAllowed(item.Value) ||
                    formation.Cells[item.Key].Coords.x < 2 || formation.IsFixed(item.Key) || formation.Draft[item.Key] ||
                    !spawner.TryGetGridNumber(formation.Cells[item.Key], out int slot))
                { error = "적을 배치할 수 없는 칸 또는 제외된 적입니다."; return false; }
                if (!EnemySpawnPlanBuilder.TryBuildUnitEntry(item.Value, CombatContext.Instance.EnemyLevel, 0, out var entry, out error)) return false;
                if (!IsAllowed(entry.Data.Index) || !spawner.CanResolveEntry(entry))
                { error = "이 적의 전투 프리팹을 사용할 수 없습니다."; return false; }
                entries[item.Key] = entry;
                slots[item.Key] = slot;
            }
            var original = formation.Cells.Select(c => c.OccupyingUnit).ToArray();
            if (!formation.TryApply(flow.Participants, out error)) return false;
            var created = new List<GameObject>();
            try
            {
                spawner.RefreshSimulationFormation();
                foreach (var item in entries)
                {
                    if (!spawner.TrySpawnUnitIfEmpty(item.Value, slots[item.Key], out var go) || !go)
                        throw new InvalidOperationException("빈 칸 생성에 실패했습니다.");
                    created.Add(go);
                    var unit = go.GetComponent<BattleCharactor>();
                    if (!unit || !flow.RegisterRuntimeParticipant(unit))
                        throw new InvalidOperationException("전투 참가 등록에 실패했습니다.");
                }
                return true;
            }
            catch (Exception exception)
            {
                // JC | 2026-11-10 | 일부 생성 실패 시 새 참가자와 이동도 되돌려 확인 버튼의 부분 적용을 남기지 않습니다.
                foreach (var go in created)
                {
                    if (!go) continue;
                    var unit = go.GetComponent<BattleCharactor>();
                    if (unit) { flow.RemoveUnit(unit, false); unit.ClearOccupiedCell(); }
                    go.transform.SetParent(null, true);
                    UnityEngine.Object.Destroy(go);
                }
                var rollback = new JcSimulationFormation(formation.Cells, flow.Participants);
                Array.Copy(original, rollback.Draft, original.Length);
                bool restored = rollback.TryApply(flow.Participants, out string rollbackError);
                spawner.RefreshSimulationFormation();
                error = exception.Message + (restored ? " 변경을 취소했습니다. 창을 다시 열어 주세요." : " 복구 확인 필요: " + rollbackError);
                Debug.LogError("[JC Simulation] " + error);
                return false;
            }
        }
    }
}
